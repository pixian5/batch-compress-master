using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services.Pipeline;

/// <summary>
/// 适配器预检完成、等待执行的条目。
/// GLM-5.3, 2026-11-04：批次 2 阶段 1 引入。基类只承载管线骨架所需的共性字段
/// （显示名、进度定位路径、引擎选项、启动/成功消息与命令前缀），
/// 压缩/解压各自的执行细节（源路径、分卷解析结果、输出快照等）由子类补充。
/// </summary>
public abstract class PreparedBatchItem
{
    /// <summary>条目显示名：刷新 CurrentFile 并参与成功/失败消息。</summary>
    public required string DisplayName { get; init; }

    /// <summary>GUI 行定位用的完整来源路径。</summary>
    public required string CurrentSourcePath { get; init; }

    /// <summary>引擎选项（格式、密码、分卷大小、附件输入等）。</summary>
    public required ArchiveOptions Options { get; init; }

    /// <summary>本地化启动消息（L.CompressionStarted / L.ExtractionStarted 的格式化结果）。</summary>
    public required string StartedMessage { get; init; }

    /// <summary>启动时的 ILogger 诊断消息。</summary>
    public required string StartedLogMessage { get; init; }

    /// <summary>成功时的 ILogger 诊断消息。</summary>
    public required string SucceededLogMessage { get; init; }

    /// <summary>命令输出转发前缀（L.CompressionCommandLabel / L.ExtractionCommandLabel）。</summary>
    public required string CommandLabel { get; init; }

    /// <summary>失败时的 ILogger 诊断消息（压缩与解压的措辞不同）。</summary>
    public abstract string BuildFailedLogMessage(string? errorMessage);
}
