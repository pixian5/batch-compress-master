using System;

namespace BatchCompress.Avalonia.Core.Models;

/// <summary>
/// Progress information for compression/decompression operations
/// </summary>
// GPT-5, 2026-08-05：长时间批处理向 GUI 或命令行观察者报告的可变进度快照。
public class OperationProgressInfo
{
    public string CurrentFile { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int IgnoreCount { get; set; }
    public int NonExistCount { get; set; }
    public double ProcessedSizeGB { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// File entry for decompression with password
/// </summary>
// GPT-5, 2026-08-05：表示来源项目及其从 TXT 列表读取的可选单文件密码。
public class FileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string? Password { get; set; }
    public long FileSize { get; set; }
}

/// <summary>
/// Operation options for batch processing
/// </summary>
// GPT-5, 2026-08-05：聚合 GUI/命令行选择，之后由 BatchOperationService 转换为引擎选项。
public class BatchOperationOptions
{
    public string SourcePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Extension { get; set; } = "rar";
    public bool UseRandomPassword { get; set; }
    public string? CustomPassword { get; set; }
    public bool DeleteSourceAfter { get; set; }
    public bool MoveSourceAfter { get; set; }
    public bool SkipAlreadyProcessed { get; set; }
    public double MaxSizeGB { get; set; }
    public bool ShutdownAfterComplete { get; set; }
    
    // Compression options
    public Core.Interfaces.CompressionLevel CompressionLevel { get; set; }
    public bool SolidArchive { get; set; }
    public string? VolumeSize { get; set; }
    public string? VolumeSizeUnit { get; set; }
    public bool QuickOpen { get; set; }
    public bool TestArchive { get; set; }
    public string? CommentFile { get; set; }
    public string? TempDirectory { get; set; }
    public Core.Interfaces.ExistingFileMode ExistingFileMode { get; set; }
    public int RecoveryRecordPercent { get; set; }
    
    // Enclosure/contact info directories
    public string[]? EnclosureDirectories { get; set; }
    public bool AddEnclosures { get; set; }
}

/// <summary>
/// Source mode for file list
/// </summary>
public enum SourceMode
{
    FromTextFile = 0,  // Read from text file with passwords
    FromFolder = 1     // Read from folder
}
