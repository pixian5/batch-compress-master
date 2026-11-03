using System;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services.Pipeline;

/// <summary>
/// 批量执行管线的条目适配器契约。
/// GLM-5.3, 2026-11-04：批次 2 阶段 1 引入。压缩与解压双循环的全部条目级差异
/// （预检、引擎调用、大小统计、后处理目标、失败清理）收敛到适配器实现，
/// 管线本身只保留共性骨架（遍历、取消、进度、后处理调用、大小超限、关机）。
/// </summary>
public interface IBatchItemAdapter<TInput>
{
    /// <summary>
    /// 批次完成的 ILogger 诊断消息，例如
    /// "Batch compression complete. Success: 1, Failed: 0"。
    /// </summary>
    string BuildBatchCompletedLogMessage(OperationProgressInfo progressInfo);

    /// <summary>
    /// 请求系统关机前写入的 ILogger 诊断消息；null 表示不写
    /// （压缩侧历史上没有这条日志，仅解压侧输出）。
    /// </summary>
    string? ShutdownRequestLogMessage { get; }

    /// <summary>
    /// 预检与构建：元数据过滤、存在性检查、跳过已处理、分卷/输出解析、
    /// 密码生成与 ArchiveOptions 构建。跳过或预检失败时自行更新计数、
    /// 上报进度并返回 null；进入执行前还应刷新 progressInfo.CurrentFile。
    /// </summary>
    Task<PreparedBatchItem?> PrepareAsync(
        TInput input,
        BatchOperationOptions options,
        OperationProgressInfo progressInfo,
        IProgress<OperationProgressInfo> progress);

    /// <summary>
    /// 调用底层引擎执行压缩/解压，包裹异常路径上的清理
    /// （如压缩附件暂存目录的 finally 删除）。
    /// </summary>
    Task<ArchiveResult> ExecuteAsync(PreparedBatchItem item, CancellationToken cancellationToken);

    /// <summary>
    /// 条目成功后的大小统计（分卷求和、统计失败的回退与告警由适配器内部处理）。
    /// processedSizeGBBefore 供诊断日志输出累计值使用。
    /// </summary>
    long MeasuredBytes(PreparedBatchItem item, double processedSizeGBBefore);

    /// <summary>
    /// 构建删除/移动后处理请求：目标集合、词条与整组预检语义由适配器决定。
    /// </summary>
    PostProcessRequest BuildPostProcessRequest(PreparedBatchItem item, BatchOperationOptions options);

    /// <summary>
    /// 引擎执行失败（返回失败结果，非异常）后的残留清理；
    /// 无清理需求的适配器（压缩侧）提供空实现。
    /// </summary>
    void OnExecutionFailed(PreparedBatchItem item);
}
