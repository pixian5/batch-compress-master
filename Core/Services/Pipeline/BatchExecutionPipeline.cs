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
/// GLM-5.3, 2026-11-04：批次 5 新增受控并发——MaxConcurrency&gt;1 时引擎调用并行执行：
/// 信号量限流；进度统计与上报全部经同一把锁保护并输出克隆快照（防撕裂读）；
/// 大小上限经独立闸门令牌停止“未启动”任务（不杀死在途引擎）；
/// 外部取消经联动令牌即时传播到全部并发引擎；关机仍只在非取消正常结束时触发恰好一次。
/// MaxConcurrency&lt;=1 走原串行路径，行为与批次 2 表征完全一致。
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
    /// MaxConcurrency&gt;1 且多于一个条目时进入并发路径，否则保持串行。
    /// </summary>
    public async Task RunAsync(
        IReadOnlyList<TInput> inputs,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        if (options.MaxConcurrency > 1 && inputs.Count > 1)
        {
            await RunConcurrentAsync(inputs, options, progress, cancellationToken);
            return;
        }

        await RunSequentialAsync(inputs, options, progress, cancellationToken);
    }

    // 串行路径：批次 2 阶段 1 的原始实现，逐字节保持（表征测试锁定）。
    private async Task RunSequentialAsync(
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

    // GLM-5.3, 2026-11-04：批次 5——受控并发执行路径。
    // 线程安全硬约束的实现方式：
    // 1) progressInfo 与 processedSizeGB 的全部读写都在同一把锁（gate）内完成，
    //    进度上报统一输出 CloneProgress 克隆快照，杜绝并发下的丢失计数与撕裂读；
    // 2) 适配器 PrepareAsync 契约为同步实现（Task.FromResult），其内部直接回写共享进度对象，
    //    因此在锁内以 GetAwaiter().GetResult() 调用（已完成任务不阻塞）；
    // 3) 引擎调用与后处理文件操作在锁外并发（真正的耗时段）；
    // 4) gateCts（闸门令牌）：大小达限或外部取消时取消，使仍在信号量上排队的任务立即放弃；
    //    engineCts（引擎令牌）：仅与外部取消联动——达限不杀死在途引擎，让其自然完成；
    // 5) 排队任务退出走 return（正常完成），因此 Task.WhenAll 只在外部取消（引擎抛
    //    OperationCanceledException）或真实异常时向上传播；关机判断点唯一，恰好触发一次。
    private async Task RunConcurrentAsync(
        IReadOnlyList<TInput> inputs,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };
        var gate = new object();
        double processedSizeGB = 0;
        var limitReached = false;

        // GLM-5.3, 2026-11-04：批次 7 审查结论：engineCts/gateCts/semaphore 均以 using
        // 声明，无论 WhenAll 正常结束、外部取消（OperationCanceledException）还是适配器
        // 真实异常向上传播，离开方法时都确定性释放（CreateLinkedTokenSource 对外部令牌的
        // 注册随之撤销，SemaphoreSlim 句柄归还），不存在泄漏路径；Task.Run 任务全部被
        // WhenAll 观测，不产生未观察的任务异常。
        using var engineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var gateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

        var tasks = new List<Task>(inputs.Count);
        foreach (var input in inputs)
        {
            // 注意不用 Task.Run 的重载令牌：排队中止统一经 WaitAsync(gateCts) 的取消异常
            // 在任务体内捕获并正常返回，避免产生“已取消任务”混合语义。
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync(gateCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 达限或外部取消：尚未启动的任务直接放弃（与串行 break 后剩余项不计数一致）。
                    return;
                }

                try
                {
                    if (limitReached || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    // —— 准备段（锁内）：适配器回写共享进度对象必须互斥。 ——
                    PreparedBatchItem? item;
                    lock (gate)
                    {
                        item = _adapter.PrepareAsync(input, options, progressInfo, progress)
                            .GetAwaiter().GetResult();
                    }

                    if (item == null)
                    {
                        return;
                    }

                    // GLM-5.3, 2026-11-04：批次 7——启动日志在锁外先写：
                    // 日志文件 I/O（FileLoggerService 自带线程安全）不占用 gate 锁，
                    // 缩短高并发下的临界区；消息内容不依赖共享进度对象。
                    Log(LogLevel.Information, item.StartedLogMessage);
                    lock (gate)
                    {
                        progressInfo.CurrentFile = item.DisplayName;
                        progressInfo.CurrentSourcePath = item.CurrentSourcePath;
                        progressInfo.Message = item.StartedMessage;
                        progressInfo.IsError = false;
                        progress.Report(CloneProgress(progressInfo));
                    }

                    // —— 执行段（锁外）：引擎并发是并行的意义所在。 ——
                    var result = await _adapter.ExecuteAsync(item, engineCts.Token);
                    lock (gate)
                    {
                        ReportArchiveOutput(item.CommandLabel, result, progressInfo, progress);
                    }

                    // —— 完成段：测量与后处理文件操作在锁外，统计与消息回写回锁内。 ——
                    if (result.Success)
                    {
                        Log(LogLevel.Information, item.SucceededLogMessage);
                        double sizeBefore;
                        lock (gate)
                        {
                            sizeBefore = processedSizeGB;
                        }

                        var measuredBytes = _adapter.MeasuredBytes(item, sizeBefore);
                        var postProcessFailures = _postProcessService.Apply(
                            _adapter.BuildPostProcessRequest(item, options));

                        // GLM-5.3, 2026-11-04：批次 7——完成消息在锁外预构建：
                        // 字符串格式化不依赖共享状态，锁内只做统计回写与快照上报；
                        // 后处理失败的警告日志仍由锁内 RecordPostProcessFailure 统一记录
                        //（与串行路径同一实现，避免双写）。
                        var postProcessFailed = postProcessFailures.Count > 0;
                        var postProcessReason = postProcessFailures.LastOrDefault() ?? string.Empty;
                        var completedMessage = postProcessFailed
                            ? string.Format(L.ItemSucceededWithPostProcessFailure, item.DisplayName, postProcessReason)
                            : string.Format(L.ItemSucceeded, item.DisplayName);

                        var sizeLimitJustReached = false;
                        lock (gate)
                        {
                            progressInfo.SuccessCount++;
                            processedSizeGB += measuredBytes / BytesPerGB;
                            progressInfo.ProcessedSizeGB = processedSizeGB;

                            foreach (var failure in postProcessFailures)
                            {
                                RecordPostProcessFailure(progressInfo, failure);
                            }

                            // 归档成功和后处理成功分别统计，避免“归档成功但移动失败”被显示为完全成功。
                            progressInfo.Message = completedMessage;
                            progressInfo.IsError = postProcessFailed;
                            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
                            progress.Report(CloneProgress(progressInfo));

                            // 达到总大小上限：只阻止未启动任务，在途任务自然完成；
                            // SizeLimitReached 只上报一次。
                            if (!limitReached &&
                                options.MaxSizeGB > 0 &&
                                processedSizeGB >= options.MaxSizeGB)
                            {
                                limitReached = true;
                                sizeLimitJustReached = true;
                                progressInfo.Message = L.SizeLimitReached;
                                progress.Report(CloneProgress(progressInfo));
                                gateCts.Cancel();
                            }
                        }

                        if (sizeLimitJustReached)
                        {
                            // 批次 7：达限日志移出 gate 锁，临界区内只保留状态切换与取消信号。
                            Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                        }
                    }
                    else
                    {
                        _adapter.OnExecutionFailed(item);
                        Log(LogLevel.Error, item.BuildFailedLogMessage(result.ErrorMessage));
                        lock (gate)
                        {
                            progressInfo.FailCount++;
                            progressInfo.Message = string.Format(L.ItemFailed, item.DisplayName, result.ErrorMessage);
                            progressInfo.IsError = true;
                            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
                            progress.Report(CloneProgress(progressInfo));
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        // 外部取消时在途引擎抛 OperationCanceledException，WhenAll 聚合后向上传播；
        // 达限场景的排队退出均为正常完成，不产生异常。
        await Task.WhenAll(tasks);

        Log(LogLevel.Information, _adapter.BuildBatchCompletedLogMessage(progressInfo));

        // 用户要求时在所有并发任务结束后请求系统关机（取消传播时不会到达这里），恰好一次。
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

    // GLM-5.3, 2026-11-04：批次 5——并发路径的状态快照克隆：按当前消息与标记原样复制，
    // 与串行路径共享同一实现，保证两条路径的快照字段口径完全一致。
    private static OperationProgressInfo CloneProgress(OperationProgressInfo source) =>
        CloneProgress(source, source.Message, source.IsError, source.IsCommandOutput);

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
