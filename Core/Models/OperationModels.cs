using System;

namespace BatchCompress.Avalonia.Core.Models;

/// <summary>
/// Progress information for compression/decompression operations
/// </summary>
public class OperationProgressInfo
{
    /// <summary>
    /// The name of the file currently being processed
    /// </summary>
    public string CurrentFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of successfully processed files
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of files that failed to process
    /// </summary>
    public int FailCount { get; set; }
    
    /// <summary>
    /// Number of files that were skipped/ignored
    /// </summary>
    public int IgnoreCount { get; set; }
    
    /// <summary>
    /// Number of files that don't exist
    /// </summary>
    public int NonExistCount { get; set; }
    
    /// <summary>
    /// Total size of processed files in GB
    /// </summary>
    public double ProcessedSizeGB { get; set; }
    
    /// <summary>
    /// Status or progress message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if the current status represents an error
    /// </summary>
    public bool IsError { get; set; }
    
    /// <summary>
    /// The time when the operation started
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Elapsed time since operation started
    /// </summary>
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// File entry for decompression with password
/// </summary>
public class FileEntry
{
    /// <summary>
    /// Full path to the file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional password for the file (if encrypted)
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long FileSize { get; set; }
}

/// <summary>
/// Operation options for batch processing
/// </summary>
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
    /// <summary>
    /// Read file list and passwords from text file
    /// </summary>
    FromTextFile = 0,
    
    /// <summary>
    /// Read file list from folder (no password file)
    /// </summary>
    FromFolder = 1
}
