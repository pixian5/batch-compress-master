using System.Threading;
using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Interfaces;

/// <summary>
/// 压缩和解压引擎接口。
/// </summary>
// GPT-5, 2026-08-05：归档引擎接收已解析路径和取消令牌；实现不能依赖特定界面或 Shell，
// 必须通过 ArchiveResult 暴露工具相关失败。
public interface IArchiveEngine
{
    /// <summary>
    /// 压缩文件或目录。
    /// </summary>
    Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 解压归档。
    /// </summary>
    Task<ArchiveResult> ExtractAsync(string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查当前系统是否存在可用归档引擎。
    /// </summary>
    bool IsAvailable();
    
}

/// <summary>
/// 压缩和解压操作选项。
/// </summary>
public class ArchiveOptions
{
    public string ArchiveFormat { get; set; } = "rar";
    public string? Password { get; set; }
    public string? TempDirectory { get; set; }
    public string? CommentFile { get; set; }
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Normal;
    public bool SolidArchive { get; set; }
    public bool QuickOpen { get; set; }
    public bool TestArchive { get; set; }
    public string? VolumeSize { get; set; }
    public ExistingFileMode ExistingFileMode { get; set; } = ExistingFileMode.Skip;
    public int RecoveryRecordPercent { get; set; }
    public string[]? ExcludeExtensions { get; set; }
}

/// <summary>
/// 归档操作结果。
/// </summary>
public class ArchiveResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}

/// <summary>
/// 压缩级别。
/// </summary>
public enum CompressionLevel
{
    Store = 0,      // No compression
    Fastest = 1,    // Light compression
    Fast = 2,       // Fast
    Normal = 3,     // Normal
    Good = 4,       // Good
    Best = 5        // Maximum
}

/// <summary>
/// 已有文件处理模式。
/// </summary>
public enum ExistingFileMode
{
    Skip,           // Skip existing files
    Update,         // Update existing files
    Overwrite       // Overwrite existing files
}
