using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Localization;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services.Pipeline;

/// <summary>
/// 通用批量执行管线。
/// GLM-5.3, 2026-11-04：批次 2 阶段 1 从压缩/解压双循环中收敛出的共性骨架：
/// 循环遍历、取消检查、进度上报、归档命令输出转发、PostProcessService 调用、
/// 大小超限退出与完成关机。条目级差异全部由 <see cref="IBatchItemAdapter{TInput}"/> 提供，
/// 迁移期间 17 项表征测试锁定消息文本、七类计数、关机时机与文件系统副作用零漂移。
/// </summary>
public sealed class BatchExecutionPipeline<TInput>
{
    private const double BytesPerGB = 1024.0 * 1024.0 * 1024.0;

    private readonly IBatchItemAdapter<TInput> _adapter;
    private readonly PostProcessService _postProcessService;
    private readonly ISystemIntegration _systemIntegration;
    private readonly ILogger? _logger;

    // 后处理失败消息跟随当前界面语言，与原双循环的报告口径一致。
    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public BatchExecutionPipeline(
        IBatchItemAdapter<TInput> adapter,
        PostProcessService postProcessService,
        ISystemIntegration systemIntegration,
        ILogger? logger = null)
    {
        _adapter = adapter;
        _postProcessService = postProcessService;
        _systemIntegration = systemIntegration;
        _logger = logger;
    }

    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }

    /// <summary>
    /// 逐项执行整个批次。取消令牌触发时抛出 OperationCanceledException，
    /// 绝不触发关机；只有非取消正常结束时才按选项请求系统关机。
    /// </summary>
    public async Task RunAsync(
        IReadOnlyList<TInput> inputs,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };

        double processedSizeGB = 0;

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 预检：跳过或失败时适配器已自行更新计数并上报进度。
            var item = await _adapter.PrepareAsync(input, options, progressInfo, progress);
            if (item == null)
            {
                continue;
            }

            Log(LogLevel.Information, item.StartedLogMessage);
            progressInfo.Message = item.StartedMessage;
            progressInfo.IsError = false;
            progress.Report(progressInfo);

            // 调用归档引擎执行；异常（含取消）由适配器的 finally 语义负责清理暂存资源。
            var result = await _adapter.ExecuteAsync(item, cancellationToken);
            ReportArchiveOutput(item.CommandLabel, result, progressInfo, progress);

            if (result.Success)
            {
                Log(LogLevel.Information, item.SucceededLogMessage);
                progressInfo.SuccessCount++;

                // 以实际输出/输入字节数累计处理量；分卷求和与统计失败的回退由适配器负责。
                var measuredBytes = _adapter.MeasuredBytes(item, processedSizeGB);
                processedSizeGB += measuredBytes / BytesPerGB;
                progressInfo.ProcessedSizeGB = processedSizeGB;

                // 删除/移动后处理统一下沉到 PostProcessService；失败消息逐条计数并写
                // 警告日志，最后一条并入成功记录，与原双循环内联实现行为一致。
                var postProcessFailures = _postProcessService.Apply(
                    _adapter.BuildPostProcessRequest(item, options));
                var postProcessFailed = postProcessFailures.Count > 0;
                var postProcessReason = postProcessFailures.LastOrDefault() ?? string.Empty;
                foreach (var failure in postProcessFailures)
                {
                    RecordPostProcessFailure(progressInfo, failure);
                }

                // 归档成功和后处理成功分别统计，避免“归档成功但移动失败”被显示为完全成功。
                progressInfo.Message = postProcessFailed
                    ? string.Format(L.ItemSucceededWithPostProcessFailure, item.DisplayName, postProcessReason)
                    : string.Format(L.ItemSucceeded, item.DisplayName);
                progressInfo.IsError = postProcessFailed;
            }
            else
            {
                // 引擎失败后的残留清理（如解压失败回滚新写出的文件）。
                _adapter.OnExecutionFailed(item);
                Log(LogLevel.Error, item.BuildFailedLogMessage(result.ErrorMessage));
                progressInfo.FailCount++;
                progressInfo.Message = string.Format(L.ItemFailed, item.DisplayName, result.ErrorMessage);
                progressInfo.IsError = true;
            }

            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
            progress.Report(progressInfo);

            // 达到总大小上限后停止后续任务。
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                progressInfo.Message = L.SizeLimitReached;
                progress.Report(progressInfo);
                break;
            }
        }

        Log(LogLevel.Information, _adapter.BuildBatchCompletedLogMessage(progressInfo));

        // 用户要求时在全部任务结束后请求系统关机（取消异常向上传播时不会到达这里）。
        if (options.ShutdownAfterComplete)
        {
            if (_adapter.ShutdownRequestLogMessage is { Length: > 0 } shutdownLog)
            {
                Log(LogLevel.Information, shutdownLog);
            }

            await _systemIntegration.ShutdownAsync();
        }
    }

    // GPT-5, 2026-08-06：归档进程输出是用户要求保留的原始诊断证据。
    // 此处明确不调用脱敏、掩码或替换逻辑，即使 stdout/stderr 中包含密码也原样转发。
    private static void ReportArchiveOutput(
        string prefix,
        ArchiveResult result,
        OperationProgressInfo progressInfo,
        IProgress<OperationProgressInfo> progress)
    {
        if (!string.IsNullOrWhiteSpace(result.CommandLine))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] command{Environment.NewLine}{result.CommandLine}",
                isError: false,
                isCommandOutput: true));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] stdout{Environment.NewLine}{result.StandardOutput.TrimEnd()}",
                isError: false,
                isCommandOutput: true));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] stderr{Environment.NewLine}{result.StandardError.TrimEnd()}",
                isError: !result.Success,
                isCommandOutput: true));
        }
    }

    // GPT-5, 2026-08-06：IProgress 可能异步投递回调，因此命令输出必须使用独立快照，
    // 防止下一条状态消息改写同一个可变对象后导致 stdout/stderr 丢失。
    private static OperationProgressInfo CloneProgress(
        OperationProgressInfo source,
        string message,
        bool isError,
        bool isCommandOutput = false) => new()
    {
        CurrentFile = source.CurrentFile,
        CurrentSourcePath = source.CurrentSourcePath,
        SuccessCount = source.SuccessCount,
        FailCount = source.FailCount,
        PostProcessFailCount = source.PostProcessFailCount,
        IgnoreCount = source.IgnoreCount,
        NonExistCount = source.NonExistCount,
        IncompleteVolumeCount = source.IncompleteVolumeCount,
        AmbiguousArchiveCount = source.AmbiguousArchiveCount,
        ProcessedSizeGB = source.ProcessedSizeGB,
        Message = message,
        IsError = isError,
        IsCommandOutput = isCommandOutput,
        StartTime = source.StartTime,
        Elapsed = source.Elapsed
    };

    // GPT-5, 2026-09-30：后处理失败既计入统计、写入诊断日志，也把原因回传给调用方并入失败记录消息。
    // GLM-5.3, 2026-11-04：后处理执行已迁入 PostProcessService，此处保留计数与日志职责。
    private string RecordPostProcessFailure(OperationProgressInfo progressInfo, string message)
    {
        progressInfo.PostProcessFailCount++;
        Log(LogLevel.Warning, message);
        return message;
    }
}
