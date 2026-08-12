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
/// 通过命令行执行批处理的无界面运行器。
/// </summary>
// GPT-5, 2026-08-05：将命令行输入适配到 GUI 共用的 BatchOperationService，同时输出稳定的控制台信息和退出码，便于脚本与 CI 使用。
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
        // GPT-5, 2026-08-06：无界面模式与 GUI 共用同一格式路由，避免两种入口能力不一致。
        _archiveEngine = new ArchiveEngineRouter();
        _batchOperationService = new BatchOperationService(_archiveEngine, new HeadlessSystemIntegration(_logger));
    }

    /// <summary>
    /// 根据命令行选项执行批处理。
    /// </summary>
    public async Task<int> RunAsync()
    {
        _logger.LogOperation("START", $"Starting headless batch operation");
        _logger.LogOperation("MODE", _options.Compress ? "Compress" : "Decompress");
        _logger.LogOperation("SOURCE", _options.SourcePath ?? "Not specified");
        _logger.LogOperation("OUTPUT", _options.OutputPath ?? "Not specified");
        _logger.LogOperation("EXTENSION", _options.Extension);
        _logger.LogOperation("LOG_FILE", _logger.LogFilePath);

        if (!TryResolvePassword(out var passwordError))
        {
            _logger.LogError(passwordError);
            Console.Error.WriteLine($"Error: {passwordError}");
            return 2;
        }

        if (!ValidateSourcePaths(out var sourceError))
        {
            _logger.LogError(sourceError);
            Console.Error.WriteLine($"Error: {sourceError}");
            return 2;
        }

        if (_options.DryRun)
        {
            return RunDryRun();
        }

        if (!_archiveEngine.IsAvailable())
        {
            _logger.LogError("No supported archive engine was found.");
            Console.Error.WriteLine("Error: no supported archive engine was found (WinRAR/RAR or 7-Zip).");
            return 1;
        }

        if (string.IsNullOrEmpty(_options.OutputPath))
        {
            _logger.LogError("Output path is required for headless operation.");
            Console.Error.WriteLine("Error: Output path is required. Use --output or -o option.");
            return 1;
        }

        // 解压目录由 BatchOperationService 按任务创建；这样完整性预检拦截的缺卷任务不会留下空目录。
        // 压缩仍在运行器层提前创建，便于归档引擎和附件暂存使用统一输出根目录。
        if (_options.Compress && !Directory.Exists(_options.OutputPath))
        {
            try
            {
                Directory.CreateDirectory(_options.OutputPath);
                _logger.LogOperation("CREATE_DIR", $"Created output directory: {_options.OutputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create output directory: {_options.OutputPath}", ex);
                Console.Error.WriteLine($"Error: Failed to create output directory: {ex.Message}");
                return 1;
            }
        }

        var batchOptions = BuildBatchOperationOptions();
        var cts = new CancellationTokenSource();

        // GPT-5, 2026-08-05：将 Ctrl+C 转为协作式取消，使 WinRAR 子进程能够执行清理流程。
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _logger.LogWarning("Operation cancelled by user (Ctrl+C)");
            WriteLine("\nCancelling operation...");
            cts.Cancel();
        };

        var progressInfo = new OperationProgressInfo();
        // GPT-5, 2026-08-06：CLI 没有 UI 同步上下文，内联报告可保证输出顺序和最终计数稳定。
        var progress = new InlineProgress<OperationProgressInfo>(info =>
        {
            progressInfo = info;
            var status = info.IsError ? "ERROR" : "INFO";
            _logger.LogOperation(status, info.Message);

            if (_options.Verbose)
            {
                WriteLine($"[{status}] {info.Message}");
            }
            else if (!_options.Quiet)
            {
                // 非详细模式只显示一行可刷新的简要进度。
                Console.Write($"\rProcessed: {info.SuccessCount} success, {info.FailCount} failed, {info.PostProcessFailCount} post-process failed, {info.IgnoreCount} skipped    ");
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

            WriteLine(string.Empty);
            var summary = $"Completed: Success={progressInfo.SuccessCount}, Failed={progressInfo.FailCount}, " +
                         $"PostProcessFailed={progressInfo.PostProcessFailCount}, Skipped={progressInfo.IgnoreCount}, " +
                         $"NotFound={progressInfo.NonExistCount}, IncompleteVolumes={progressInfo.IncompleteVolumeCount}, " +
                         $"Ambiguous={progressInfo.AmbiguousArchiveCount}";
            _logger.LogOperation("COMPLETE", summary);
            WriteLine(summary);
            WriteLine($"Log file: {_logger.LogFilePath}");

            return progressInfo.FailCount > 0 ||
                   progressInfo.PostProcessFailCount > 0 ||
                   progressInfo.IncompleteVolumeCount > 0 ||
                   progressInfo.AmbiguousArchiveCount > 0
                ? 1
                : progressInfo.SuccessCount > 0 || progressInfo.IgnoreCount > 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation was cancelled");
            WriteLine("\nOperation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception during operation", ex);
            Console.Error.WriteLine($"\nError: {ex.Message}");
            return 1;
        }
    }

    private async Task RunCompressAsync(BatchOperationOptions options, IProgress<OperationProgressInfo> progress, CancellationToken cancellationToken)
    {
        var sourcePaths = CollectCompressionSources();

        _logger.LogOperation("FILES_FOUND", $"Found {sourcePaths.Count} files to compress");
        WriteLine($"Found {sourcePaths.Count} items to compress");

        if (sourcePaths.Count == 0)
        {
            _logger.LogWarning("No files found to compress");
            WriteLine("No files found to compress.");
            return;
        }

        await _batchOperationService.BatchCompressAsync(sourcePaths, options, progress, cancellationToken);
    }

    private async Task RunDecompressAsync(BatchOperationOptions options, IProgress<OperationProgressInfo> progress, CancellationToken cancellationToken)
    {
        var entries = CollectDecompressionEntries();

        _logger.LogOperation("FILES_FOUND", $"Found {entries.Count} files to decompress");
        WriteLine($"Found {entries.Count} files to decompress");

        if (entries.Count == 0)
        {
            _logger.LogWarning("No files found to decompress");
            WriteLine("No files found to decompress.");
            return;
        }

        await _batchOperationService.BatchDecompressAsync(entries, options, progress, cancellationToken);
    }

    // GPT-5, 2026-08-06：直接密码、密码文件与标准输入在解析层已保证互斥。
    // 此处只解析选中的秘密来源；归档命令和原始进程输出由统一批处理日志链路按用户要求原样记录。
    private bool TryResolvePassword(out string error)
    {
        error = string.Empty;
        try
        {
            if (!string.IsNullOrWhiteSpace(_options.PasswordFile))
            {
                _options.Password = File.ReadLines(_options.PasswordFile).FirstOrDefault() ?? string.Empty;
                _options.UseRandomPassword = false;
            }
            else if (_options.ReadPasswordFromStandardInput)
            {
                _options.Password = Console.In.ReadLine();
                _options.UseRandomPassword = false;
            }

            if (_options.ReadPasswordFromStandardInput && _options.Password == null)
            {
                error = "无法从指定来源读取密码。";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"读取密码失败: {ex.Message}";
            return false;
        }
    }

    private bool ValidateSourcePaths(out string error)
    {
        error = string.Empty;
        var paths = _options.InputPaths.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_options.SourcePath))
        {
            paths = paths.Append(_options.SourcePath);
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                error = $"来源不存在: {path}";
                return false;
            }
        }

        return true;
    }

    private int RunDryRun()
    {
        var paths = _options.Compress
            ? CollectCompressionSources()
            : CollectDecompressionEntries().Select(entry => entry.FilePath).ToList();

        WriteLine($"DRY-RUN mode={(_options.Compress ? "compress" : "extract")} format={_options.Extension}");
        WriteLine($"Output: {_options.OutputPath}");
        foreach (var path in paths)
        {
            WriteLine(path);
        }

        WriteLine($"Total: {paths.Count}. No files or directories were changed.");
        return paths.Count > 0 ? 0 : 1;
    }

    private List<string> CollectCompressionSources()
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.SourcePath))
        {
            if (Directory.Exists(_options.SourcePath))
            {
                paths.AddRange(_batchOperationService.LoadCompressionSourcesFromFolder(
                    _options.SourcePath,
                    _options.SkipProcessed));
            }
            else if (File.Exists(_options.SourcePath))
            {
                paths.Add(_options.SourcePath);
            }
        }

        // GPT-5, 2026-08-06：--input 表示精确项目，所以指定目录本身作为一个归档来源，不展开其直接子项。
        paths.AddRange(_options.InputPaths.Where(path => File.Exists(path) || Directory.Exists(path)));
        return paths
            .Where(path => !SystemMetadataFileFilter.ShouldSkip(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<FileEntry> CollectDecompressionEntries()
    {
        var entries = new List<FileEntry>();
        if (!string.IsNullOrWhiteSpace(_options.TextFile))
        {
            var sourceDirectory = Directory.Exists(_options.SourcePath)
                ? _options.SourcePath
                : Path.GetDirectoryName(Path.GetFullPath(_options.TextFile)) ?? Directory.GetCurrentDirectory();
            var textEntries = _batchOperationService.LoadFilesFromTextFile(
                _options.TextFile,
                sourceDirectory,
                _options.Extension);
            entries.AddRange(textEntries);
            _logger.LogOperation("FILES_LOADED", $"Loaded {textEntries.Count} files from text file");
        }

        // GPT-5, 2026-08-06：TXT 模式中的 --source 只作为相对文件名的基准目录，不能再把整个目录重复加入任务。
        if (string.IsNullOrWhiteSpace(_options.TextFile) && !string.IsNullOrWhiteSpace(_options.SourcePath))
        {
            AddDecompressionPath(entries, _options.SourcePath);
        }

        foreach (var input in _options.InputPaths)
        {
            AddDecompressionPath(entries, input);
        }

        return entries
            .Where(entry => !SystemMetadataFileFilter.ShouldSkip(entry.FilePath))
            .GroupBy(entry => Path.GetFullPath(entry.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void AddDecompressionPath(List<FileEntry> entries, string path)
    {
        if (File.Exists(path))
        {
            if (ArchiveVolumeResolver.MatchesFormat(path, _options.Extension))
            {
                AddResolvedArchive(entries, path);
            }
            return;
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in _batchOperationService.LoadArchivesFromFolder(
                     path,
                     _options.Extension,
                     _options.SkipProcessed))
        {
            AddResolvedArchive(entries, file);
        }
    }

    private static void AddResolvedArchive(List<FileEntry> entries, string path)
    {
        var resolved = ArchiveVolumeResolver.Resolve(path);
        var taskPath = resolved.FirstVolumePath ?? resolved.Volumes.FirstOrDefault()?.Path ?? path;
        if (File.Exists(taskPath))
        {
            entries.Add(CreateFileEntry(taskPath));
        }
    }

    private static FileEntry CreateFileEntry(string path) => new()
    {
        FilePath = path,
        FileSize = new FileInfo(path).Length
    };

    private void WriteLine(string message)
    {
        if (!_options.Quiet)
        {
            Console.WriteLine(message);
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
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
            PasswordNameMode = _options.PasswordName == "base"
                ? PasswordNameMode.BaseName
                : PasswordNameMode.ArchiveName,
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
            LockArchive = _options.LockArchive,
            AddEnclosures = _options.AddEnclosures,
            EnclosureDirectories = _options.AddEnclosures
                ? _options.EnclosurePaths
                    .Concat((_options.EnclosureList ?? string.Empty)
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : null
        };
    }
}

/// <summary>
/// 无界面模式的系统集成实现：记录日志，不显示桌面界面。
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
