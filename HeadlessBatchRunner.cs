using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;

namespace BatchCompress.Avalonia;

/// <summary>
/// Headless runner for batch operations via command-line
/// </summary>
// GPT-5, 2026-08-05: Adapts CLI input to the same BatchOperationService used by the GUI while emitting
// stable console output and process exit codes suitable for scripts and CI.
public class HeadlessBatchRunner
{
    private readonly FileLoggerService _logger;
    private readonly IArchiveEngine _archiveEngine;
    private readonly BatchOperationService _batchOperationService;
    private readonly CommandLineOptions _options;

    public HeadlessBatchRunner(CommandLineOptions options, FileLoggerService logger)
    {
        _options = options;
        _logger = logger;
        _archiveEngine = new RarArchiveEngine();
        _batchOperationService = new BatchOperationService(_archiveEngine, new HeadlessSystemIntegration(_logger));
    }

    /// <summary>
    /// Run the batch operation based on command-line options
    /// </summary>
    public async Task<int> RunAsync()
    {
        _logger.LogOperation("START", $"Starting headless batch operation");
        _logger.LogOperation("MODE", _options.Compress ? "Compress" : "Decompress");
        _logger.LogOperation("SOURCE", _options.SourcePath ?? "Not specified");
        _logger.LogOperation("OUTPUT", _options.OutputPath ?? "Not specified");
        _logger.LogOperation("EXTENSION", _options.Extension);
        _logger.LogOperation("LOG_FILE", _logger.LogFilePath);

        if (!_archiveEngine.IsAvailable())
        {
            _logger.LogError("RAR executable not found. Please install WinRAR or RAR.");
            Console.WriteLine("Error: RAR executable not found. Please install WinRAR or RAR.");
            return 1;
        }

        if (string.IsNullOrEmpty(_options.SourcePath))
        {
            _logger.LogError("Source path is required for headless operation.");
            Console.WriteLine("Error: Source path is required. Use --source or -s option.");
            return 1;
        }

        if (string.IsNullOrEmpty(_options.OutputPath))
        {
            _logger.LogError("Output path is required for headless operation.");
            Console.WriteLine("Error: Output path is required. Use --output or -o option.");
            return 1;
        }

        if (!Directory.Exists(_options.SourcePath))
        {
            _logger.LogError($"Source path does not exist: {_options.SourcePath}");
            Console.WriteLine($"Error: Source path does not exist: {_options.SourcePath}");
            return 1;
        }

        if (!Directory.Exists(_options.OutputPath))
        {
            try
            {
                Directory.CreateDirectory(_options.OutputPath);
                _logger.LogOperation("CREATE_DIR", $"Created output directory: {_options.OutputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create output directory: {_options.OutputPath}", ex);
                Console.WriteLine($"Error: Failed to create output directory: {ex.Message}");
                return 1;
            }
        }

        var batchOptions = BuildBatchOperationOptions();
        var cts = new CancellationTokenSource();

        // GPT-5, 2026-08-05: Convert Ctrl+C into cooperative cancellation so WinRAR child processes receive cleanup handling.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _logger.LogWarning("Operation cancelled by user (Ctrl+C)");
            Console.WriteLine("\nCancelling operation...");
            cts.Cancel();
        };

        var progressInfo = new OperationProgressInfo();
        var progress = new Progress<OperationProgressInfo>(info =>
        {
            progressInfo = info;
            var status = info.IsError ? "ERROR" : "INFO";
            _logger.LogOperation(status, info.Message);
            
            if (_options.Verbose)
            {
                Console.WriteLine($"[{status}] {info.Message}");
            }
            else
            {
                // Simple progress indicator
                Console.Write($"\rProcessed: {info.SuccessCount} success, {info.FailCount} failed, {info.IgnoreCount} skipped    ");
            }
        });

        try
        {
            if (_options.Compress)
            {
                await RunCompressAsync(batchOptions, progress, cts.Token);
            }
            else if (_options.Decompress)
            {
                await RunDecompressAsync(batchOptions, progress, cts.Token);
            }

            Console.WriteLine();
            var summary = $"Completed: Success={progressInfo.SuccessCount}, Failed={progressInfo.FailCount}, " +
                         $"Skipped={progressInfo.IgnoreCount}, NotFound={progressInfo.NonExistCount}";
            _logger.LogOperation("COMPLETE", summary);
            Console.WriteLine(summary);
            Console.WriteLine($"Log file: {_logger.LogFilePath}");

            return progressInfo.FailCount > 0 ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation was cancelled");
            Console.WriteLine("\nOperation cancelled.");
            return 2;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception during operation", ex);
            Console.WriteLine($"\nError: {ex.Message}");
            return 1;
        }
    }

    private async Task RunCompressAsync(BatchOperationOptions options, IProgress<OperationProgressInfo> progress, CancellationToken cancellationToken)
    {
        var sourcePaths = _batchOperationService.LoadFilesFromFolder(
            _options.SourcePath!,
            _options.Extension,
            _options.SkipProcessed);

        _logger.LogOperation("FILES_FOUND", $"Found {sourcePaths.Count} files to compress");
        Console.WriteLine($"Found {sourcePaths.Count} files to compress");

        if (sourcePaths.Count == 0)
        {
            _logger.LogWarning("No files found to compress");
            Console.WriteLine("No files found to compress.");
            return;
        }

        await _batchOperationService.BatchCompressAsync(sourcePaths, options, progress, cancellationToken);
    }

    private async Task RunDecompressAsync(BatchOperationOptions options, IProgress<OperationProgressInfo> progress, CancellationToken cancellationToken)
    {
        List<FileEntry> entries;

        if (!string.IsNullOrEmpty(_options.TextFile) && File.Exists(_options.TextFile))
        {
            // Load from text file with passwords
            entries = _batchOperationService.LoadFilesFromTextFile(
                _options.TextFile,
                _options.SourcePath!,
                _options.Extension);
            _logger.LogOperation("FILES_LOADED", $"Loaded {entries.Count} files from text file");
        }
        else
        {
            // Load from folder
            var files = _batchOperationService.LoadFilesFromFolder(
                _options.SourcePath!,
                _options.Extension,
                _options.SkipProcessed);

            entries = files
                .Where(f => File.Exists(f))
                .Select(f => new FileEntry
                {
                    FilePath = f,
                    Password = null,
                    FileSize = new FileInfo(f).Length
                })
                .ToList();
        }

        _logger.LogOperation("FILES_FOUND", $"Found {entries.Count} files to decompress");
        Console.WriteLine($"Found {entries.Count} files to decompress");

        if (entries.Count == 0)
        {
            _logger.LogWarning("No files found to decompress");
            Console.WriteLine("No files found to decompress.");
            return;
        }

        await _batchOperationService.BatchDecompressAsync(entries, options, progress, cancellationToken);
    }

    private BatchOperationOptions BuildBatchOperationOptions()
    {
        var existingMode = _options.ExistingFileMode.ToLowerInvariant() switch
        {
            "skip" => ExistingFileMode.Skip,
            "update" => ExistingFileMode.Update,
            _ => ExistingFileMode.Overwrite
        };

        var compressionLevel = _options.CompressionLevel switch
        {
            0 => CompressionLevel.Store,
            1 => CompressionLevel.Fastest,
            2 => CompressionLevel.Fast,
            3 => CompressionLevel.Normal,
            4 => CompressionLevel.Good,
            5 => CompressionLevel.Best,
            _ => CompressionLevel.Normal
        };

        return new BatchOperationOptions
        {
            SourcePath = _options.SourcePath ?? string.Empty,
            OutputPath = _options.OutputPath ?? string.Empty,
            Extension = _options.Extension,
            UseRandomPassword = _options.UseRandomPassword,
            CustomPassword = _options.UseRandomPassword ? null : _options.Password,
            DeleteSourceAfter = _options.DeleteSource,
            MoveSourceAfter = _options.MoveSource,
            SkipAlreadyProcessed = _options.SkipProcessed,
            MaxSizeGB = _options.MaxSizeGB,
            ShutdownAfterComplete = _options.ShutdownAfter,
            CompressionLevel = compressionLevel,
            SolidArchive = _options.Solid && _options.CompressionLevel > 0,
            VolumeSize = _options.VolumeSize,
            VolumeSizeUnit = _options.VolumeUnit,
            QuickOpen = _options.QuickOpen,
            TestArchive = _options.TestArchive,
            CommentFile = !string.IsNullOrEmpty(_options.CommentFile) && File.Exists(_options.CommentFile) 
                ? _options.CommentFile 
                : null,
            TempDirectory = !string.IsNullOrEmpty(_options.TempDir) ? _options.TempDir : _options.OutputPath,
            ExistingFileMode = existingMode,
            RecoveryRecordPercent = _options.RecoveryRecord,
            AddEnclosures = _options.AddEnclosures,
            EnclosureDirectories = _options.AddEnclosures && !string.IsNullOrEmpty(_options.EnclosureList)
                ? _options.EnclosureList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                : null
        };
    }
}

/// <summary>
/// Headless system integration that logs instead of showing UI
/// </summary>
internal class HeadlessSystemIntegration : ISystemIntegration
{
    private readonly FileLoggerService _logger;

    public HeadlessSystemIntegration(FileLoggerService logger)
    {
        _logger = logger;
    }

    public Task OpenFolderAsync(string path)
    {
        _logger.LogOperation("OPEN_FOLDER", path);
        return Task.CompletedTask;
    }

    public Task<string?> ReadClipboardTextAsync()
    {
        return Task.FromResult<string?>(null);
    }

    public Task WriteClipboardTextAsync(string text)
    {
        _logger.LogOperation("CLIPBOARD", text);
        return Task.CompletedTask;
    }

    public void ShowNotification(string title, string message)
    {
        _logger.LogOperation("NOTIFICATION", $"{title}: {message}");
        Console.WriteLine($"[{title}] {message}");
    }

    public Task ShutdownAsync()
    {
        _logger.LogOperation("SHUTDOWN", "System shutdown requested");
        Console.WriteLine("System shutdown requested.");
        return Task.CompletedTask;
    }

    public Task CancelShutdownAsync()
    {
        _logger.LogOperation("CANCEL_SHUTDOWN", "Shutdown cancelled");
        return Task.CompletedTask;
    }
}
