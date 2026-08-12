using System;
using System.Collections.Generic;

namespace BatchCompress.Avalonia.Core.Models;

/// <summary>
/// 压缩或解压操作的进度信息。
/// </summary>
// GPT-5, 2026-08-05：长时间批处理向 GUI 或命令行观察者报告的可变进度快照。
public class OperationProgressInfo
{
    public string CurrentFile { get; set; } = string.Empty;
    // GUI uses the full source path to locate the active row even when filenames repeat.
    public string CurrentSourcePath { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int PostProcessFailCount { get; set; }
    public int IgnoreCount { get; set; }
    public int NonExistCount { get; set; }
    public int IncompleteVolumeCount { get; set; }
    public int AmbiguousArchiveCount { get; set; }
    public double ProcessedSizeGB { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// 带单文件密码的解压条目。
/// </summary>
// GPT-5, 2026-08-05：表示来源项目及其从 TXT 列表读取的可选单文件密码。
public class FileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string? Password { get; set; }
    public long FileSize { get; set; }
}

/// <summary>
/// 批处理操作选项。
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
    public PasswordNameMode PasswordNameMode { get; set; } = PasswordNameMode.ArchiveName;

    // 压缩选项。
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
    public bool LockArchive { get; set; }
    public string[] RarStoreOnlyExtensions { get; set; } = [.. ArchiveDefaults.StoreOnlyExtensions];

    // 附件目录和联系信息目录。
    public string[]? EnclosureDirectories { get; set; }
    public bool AddEnclosures { get; set; }
}

/// <summary>
/// 文件列表来源模式。
/// </summary>
public enum SourceMode
{
    TextFile = 0, // TXT 内容由当前压缩或解压页解释
    Folder = 1    // 目录扫描策略由当前压缩或解压页决定
}

/// <summary>
/// 随机密码参与计算的归档名称形式。
/// </summary>
public enum PasswordNameMode
{
    ArchiveName = 0,
    BaseName = 1
}

// GPT-5, 2026-08-06：文本导入结果同时保留有效条目和诊断信息，避免旧版静默丢弃无效行。
// 密码本和压缩路径清单共用该结果，界面可以据此输出匹配率、未匹配归档和疑似分卷。
public sealed class TextFileImportResult
{
    public List<FileEntry> Entries { get; } = new();
    public List<string> Paths { get; } = new();
    public List<string> MissingEntries { get; } = new();
    public List<string> UnmatchedArchives { get; } = new();
    public List<string> VolumeCandidates { get; } = new();
    public List<string> IncompleteVolumes { get; } = new();
    public List<string> AmbiguousEntries { get; } = new();
    public List<string> DuplicateVolumeEntries { get; } = new();
    public int RequestedCount { get; set; }
    public long MatchedBytes { get; set; }
}
