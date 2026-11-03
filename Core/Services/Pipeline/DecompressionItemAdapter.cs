using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Localization;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services.Pipeline;

/// <summary>
/// 解压条目：携带分卷解析结果与执行期输出目录快照（失败回滚使用）。
/// </summary>
internal sealed class PreparedDecompressionItem : PreparedBatchItem
{
    /// <summary>已解析的首卷路径（引擎实际调用的归档入口）。</summary>
    public required string ArchivePath { get; init; }

    /// <summary>分卷组解析结果，含后处理使用的整组文件集合。</summary>
    public required ArchiveVolumeResolveResult Resolved { get; init; }

    /// <summary>原始 TXT 条目（保留条目密码与列表大小回退）。</summary>
    public required FileEntry Entry { get; init; }

    /// <summary>ExecuteAsync 填充：实际输出目录。</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>ExecuteAsync 填充：输出目录在解压前是否已存在。</summary>
    public bool OutputDirectoryExisted { get; set; }

    /// <summary>ExecuteAsync 填充：解压前输出目录内容快照。</summary>
    public HashSet<string> OutputSnapshot { get; set; } = [];

    public override string BuildFailedLogMessage(string? errorMessage) =>
        $"Extraction failed: {DisplayName} - {errorMessage}";
}

/// <summary>
/// 解压循环适配器。
/// GLM-5.3, 2026-11-04：批次 2 阶段 1 从 BatchOperationService.BatchDecompressAsync
/// 迁移出的逐项准备与执行逻辑；行为与迁移前逐字等价，由解压循环表征测试锁定。
/// 每个批次创建一个实例：跨条目的重复任务去重集合保存在实例字段中。
/// </summary>
internal sealed class DecompressionItemAdapter : IBatchItemAdapter<FileEntry>
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly BatchOperationOptions _options;
    private readonly ILogger? _logger;
    private readonly HashSet<string> _processedArchives = new(StringComparer.OrdinalIgnoreCase);

    // 报告给界面与命令行的逐项状态消息跟随当前界面语言。
    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public DecompressionItemAdapter(
        IArchiveEngine archiveEngine,
        BatchOperationOptions options,
        ILogger? logger = null)
    {
        _archiveEngine = archiveEngine;
        _options = options;
        _logger = logger;
    }

    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }

    public string BuildBatchCompletedLogMessage(OperationProgressInfo progressInfo) =>
        $"Batch decompression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}";

    // 解压侧历史上在关机前输出这条诊断日志。
    public string? ShutdownRequestLogMessage => "Shutdown requested after completion";

    public Task<PreparedBatchItem?> PrepareAsync(
        FileEntry entry,
        BatchOperationOptions options,
        OperationProgressInfo progressInfo,
        IProgress<OperationProgressInfo> progress)
    {
        // GPT-5, 2026-08-05：TXT 列表可由用户编辑，因此执行时再次实施相同的元数据过滤。
        if (SystemMetadataFileFilter.ShouldSkip(entry.FilePath))
        {
            Log(LogLevel.Debug, $"Skipping system metadata archive: {entry.FilePath}");
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        ArchiveVolumeResolveResult resolved;
        try
        {
            resolved = ArchiveVolumeResolver.Resolve(entry.FilePath);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, $"无法解析归档：{entry.FilePath}：{ex.Message}");
            progressInfo.NonExistCount++;
            progressInfo.Message = string.Format(L.CannotResolveArchive, entry.FilePath, ex.Message);
            progressInfo.IsError = true;
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        if (resolved.HasCaseAmbiguity)
        {
            progressInfo.AmbiguousArchiveCount++;
            progressInfo.IgnoreCount++;
            progressInfo.Message = string.Format(L.CaseAmbiguitySkipped, entry.FilePath);
            progressInfo.IsError = true;
            Log(LogLevel.Warning, progressInfo.Message);
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        if (resolved.HasDuplicateNumbers)
        {
            progressInfo.AmbiguousArchiveCount++;
            progressInfo.IgnoreCount++;
            progressInfo.Message = string.Format(L.DuplicateVolumeSkipped, entry.FilePath);
            progressInfo.IsError = true;
            Log(LogLevel.Warning, progressInfo.Message);
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        if (resolved.IsMultiVolume && (!resolved.HasRequiredFirstVolume || !resolved.IsSequenceContiguous))
        {
            progressInfo.IncompleteVolumeCount++;
            progressInfo.IgnoreCount++;
            progressInfo.Message = BatchOperationService.BuildIncompleteVolumeMessage(resolved);
            progressInfo.IsError = true;
            Log(LogLevel.Warning, progressInfo.Message);
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        var archivePath = resolved.FirstVolumePath;
        if (archivePath == null || !File.Exists(archivePath))
        {
            Log(LogLevel.Warning, $"Archive not found: {entry.FilePath}");
            progressInfo.NonExistCount++;
            progressInfo.Message = string.Format(L.ItemNotFound, entry.FilePath);
            progressInfo.IsError = true;
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        if (!_processedArchives.Add(Path.GetFullPath(archivePath)))
        {
            Log(LogLevel.Debug, $"Skipping duplicate archive task: {archivePath}");
            progressInfo.IgnoreCount++;
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        var archiveName = Path.GetFileName(archivePath);
        progressInfo.CurrentFile = archiveName;
        progressInfo.CurrentSourcePath = archivePath;
        Log(LogLevel.Debug, $"Processing: {archiveName}");

        // 按选项跳过已经完成解压的归档。
        if (options.SkipAlreadyProcessed && archiveName.Contains("【已解压】"))
        {
            Log(LogLevel.Debug, $"Skipping already processed: {archiveName}");
            progressInfo.IgnoreCount++;
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        // 优先使用 TXT 条目密码，再使用全局密码策略。
        string? password = entry.Password;
        if (string.IsNullOrEmpty(password) && options.UseRandomPassword)
        {
            var passwordName = PasswordUtility.GetPasswordSourceName(
                resolved.LogicalArchiveName,
                options.PasswordNameMode);
            password = PasswordUtility.GenerateDecompressionPassword(passwordName);
        }
        else if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(options.CustomPassword))
        {
            password = options.CustomPassword;
        }

        // 将批处理选项转换为归档引擎选项。
        var archiveOptions = new ArchiveOptions
        {
            // GPT-5, 2026-08-07：统一解析器已经处理数字分卷，不能让 Path.GetExtension 把 .001 当成格式。
            ArchiveFormat = resolved.ActualExtension.Length > 0
                ? resolved.ActualExtension
                : options.Extension,
            Password = password,
            ExistingFileMode = options.ExistingFileMode
        };

        return Task.FromResult<PreparedBatchItem?>(new PreparedDecompressionItem
        {
            DisplayName = archiveName,
            CurrentSourcePath = archivePath,
            Options = archiveOptions,
            StartedMessage = string.Format(L.ExtractionStarted, archiveName),
            StartedLogMessage = $"Extraction started: {archiveName}",
            SucceededLogMessage = $"Extraction successful: {archiveName}",
            CommandLabel = L.ExtractionCommandLabel,
            ArchivePath = archivePath,
            Resolved = resolved,
            Entry = entry
        });
    }

    public async Task<ArchiveResult> ExecuteAsync(PreparedBatchItem item, CancellationToken cancellationToken)
    {
        var prepared = (PreparedDecompressionItem)item;

        var outputDirectoryExisted = Directory.Exists(
            string.IsNullOrWhiteSpace(_options.OutputPath)
                ? Path.GetDirectoryName(Path.GetFullPath(prepared.ArchivePath)) ?? Directory.GetCurrentDirectory()
                : Path.GetFullPath(_options.OutputPath));
        var outputDirectory = OutputPathResolver.ResolveAndCreate(_options.OutputPath, prepared.ArchivePath);
        var outputSnapshot = SnapshotOutputDirectory(outputDirectory);

        prepared.OutputDirectory = outputDirectory;
        prepared.OutputDirectoryExisted = outputDirectoryExisted;
        prepared.OutputSnapshot = outputSnapshot;

        // 调用归档引擎执行解压。
        return await _archiveEngine.ExtractAsync(
            prepared.ArchivePath, outputDirectory, prepared.Options, cancellationToken);
    }

    public long MeasuredBytes(PreparedBatchItem item, double processedSizeGBBefore)
    {
        var prepared = (PreparedDecompressionItem)item;

        // 以当前归档大小累计处理量。
        // GLM-5.3, 2026-11-03：防御解压成功后、统计前分卷被外部删除（杀毒或用户操作）
        // 的竞态；统计失败回退到列表大小。
        try
        {
            return prepared.Resolved.FilesForPostProcessing.Count > 0
                ? prepared.Resolved.FilesForPostProcessing.Sum(path => new FileInfo(path).Length)
                : prepared.Entry.FileSize;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, $"统计归档大小失败，回退到列表大小：{prepared.Entry.FilePath}：{ex.Message}");
            return prepared.Entry.FileSize;
        }
    }

    public PostProcessRequest BuildPostProcessRequest(PreparedBatchItem item, BatchOperationOptions options)
    {
        var prepared = (PreparedDecompressionItem)item;

        // 直接使用解压前已经校验过的整组文件集合（避免后处理阶段解析出不同结果）；
        // 移动前整组预检语义保留：任一目标冲突即保留整组、只计一次失败。
        return new PostProcessRequest(
            options.DeleteSourceAfter ? PostProcessMode.Delete :
            options.MoveSourceAfter ? PostProcessMode.Move : PostProcessMode.None,
            prepared.Resolved.FilesForPostProcessing,
            "【已解压】",
            L.PostProcessItemDecompressionArchive,
            PostProcessItemKind.DecompressionArchive,
            PreflightWholeGroup: true);
    }

    public void OnExecutionFailed(PreparedBatchItem item)
    {
        var prepared = (PreparedDecompressionItem)item;
        CleanupFailedExtractionOutput(
            prepared.OutputDirectory,
            prepared.OutputSnapshot,
            prepared.OutputDirectoryExisted);
    }

    private static HashSet<string> SnapshotOutputDirectory(string outputDirectory)
    {
        var snapshot = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(outputDirectory))
        {
            return snapshot;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(outputDirectory, "*", SearchOption.AllDirectories))
        {
            snapshot.Add(Path.GetRelativePath(outputDirectory, path));
        }

        return snapshot;
    }

    // 解压失败时回滚引擎已经写出的新文件，避免缺卷或密码错误留下看似成功的残片。
    private void CleanupFailedExtractionOutput(
        string outputDirectory,
        IReadOnlySet<string> outputSnapshot,
        bool outputDirectoryExisted)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        try
        {
            var currentEntries = Directory
                .EnumerateFileSystemEntries(outputDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new
                {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(outputDirectory, path)
                })
                .Where(entry => !outputSnapshot.Contains(entry.RelativePath))
                .OrderByDescending(entry => entry.RelativePath.Length)
                .ToArray();

            foreach (var entry in currentEntries)
            {
                try
                {
                    if (File.Exists(entry.FullPath))
                    {
                        File.Delete(entry.FullPath);
                    }
                    else if (Directory.Exists(entry.FullPath))
                    {
                        Directory.Delete(entry.FullPath, recursive: false);
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"清理解压失败残留失败：{entry.FullPath}：{ex.Message}");
                }
            }

            if (!outputDirectoryExisted && !Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                Directory.Delete(outputDirectory, recursive: false);
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, $"清理解压失败残留失败：{outputDirectory}：{ex.Message}");
        }
    }
}
