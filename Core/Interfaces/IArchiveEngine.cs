using System.Threading;
using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Interfaces;

/// <summary>
/// Interface for compression/decompression engine
/// </summary>
public interface IArchiveEngine
{
    /// <summary>
    /// Build compression command for preview without executing
    /// </summary>
    string BuildCompressionCommand(string input, string output, ArchiveOptions options);
    
    /// <summary>
    /// Build extraction command for preview without executing
    /// </summary>
    string BuildExtractionCommand(string archivePath, string outputDir, ArchiveOptions options);
    
    /// <summary>
    /// Compress files or directories
    /// </summary>
    Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract archive
    /// </summary>
    Task<ArchiveResult> ExtractAsync(string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if the archive engine is available on the system
    /// </summary>
    bool IsAvailable();
    
    /// <summary>
    /// Current command being executed
    /// </summary>
    string? CurrentCommand { get; }
}

/// <summary>
/// Options for compression/decompression operations
/// </summary>
public class ArchiveOptions
{
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
/// Result of archive operation
/// </summary>
public class ArchiveResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Compression level
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
/// Mode for handling existing files
/// </summary>
public enum ExistingFileMode
{
    Skip,           // Skip existing files
    Update,         // Update existing files
    Overwrite       // Overwrite existing files
}
