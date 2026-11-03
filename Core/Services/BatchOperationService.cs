using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services.Pipeline;
using BatchCompress.Avalonia.Localization;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 批量压缩和解压的业务服务。
/// </summary>
// GPT-5, 2026-08-05：协调枚举、逐项归档执行、进度报告和后处理。
// 该服务不依赖界面，并通过 IProgress 报告所有用户可见事件。
public class BatchOperationService
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly ISystemIntegration _systemIntegration;
    private readonly PostProcessService _postProcessService;
    private readonly ILogger? _logger;

    // GPT-5, 2026-09-29：报告给界面与命令行的逐项状态消息跟随当前界面语言；
    // 仅供 ILogger 诊断的内部消息不在此列（本地化资源不是界面层，服务可安全引用）。
    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public BatchOperationService(IArchiveEngine archiveEngine, ISystemIntegration systemIntegration, ILogger? logger = null)
    {
        _archiveEngine = archiveEngine;
        _systemIntegration = systemIntegration;
        _postProcessService = new PostProcessService(logger);
        _logger = logger;
    }

    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }

    /// <summary>
    /// 从目录加载待压缩文件或目录。只扫描直接子项，不按归档扩展名过滤。
    /// </summary>
    public List<string> LoadCompressionSourcesFromFolder(string folderPath, bool skipProcessed)
    {
        Log(LogLevel.Information, $"Loading files from folder: {folderPath}");
        var items = new List<string>();

        if (!Directory.Exists(folderPath))
        {
            Log(LogLevel.Warning, $"Folder does not exist: {folderPath}");
            return items;
        }

        try
        {
            // 只扫描来源目录的直接子项，保持与旧 WinForms 的批处理范围一致。
            var allItems = Directory.GetFileSystemEntries(folderPath);

            foreach (var itemPath in allItems)
            {
                var name = Path.GetFileName(itemPath);

                // GPT-5, 2026-08-05：在扩展名和已处理判断前使用统一过滤器，确保 Windows、macOS、Linux 结果一致。
                if (SystemMetadataFileFilter.ShouldSkip(itemPath) ||
                    name.StartsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    Log(LogLevel.Debug, $"Skipping system file: {name}");
                    continue;
                }

                // 启用选项时跳过名称中带有已处理标记的项目。
                if (skipProcessed)
                {
                    if (name.Contains("【已压缩】") || name.Contains("【已解压】"))
                    {
                        Log(LogLevel.Debug, $"Skipping already processed: {name}");
                        continue;
                    }
                }

                items.Add(itemPath);
            }
            Log(LogLevel.Information, $"Found {items.Count} items to process");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Error loading items from folder: {ex.Message}");
            Console.WriteLine($"Error loading items from folder: {ex.Message}");
        }

        return items;
    }

    /// <summary>
    /// 从目录加载指定格式的待解压归档，并把分卷组统一折叠为一个任务入口。
    /// </summary>
    public List<string> LoadArchivesFromFolder(string folderPath, string extension, bool skipProcessed)
    {
        Log(LogLevel.Information, $"Loading archives from folder: {folderPath}");
        if (!Directory.Exists(folderPath))
        {
            Log(LogLevel.Warning, $"Folder does not exist: {folderPath}");
            return [];
        }

        var archives = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(path);
                if (SystemMetadataFileFilter.ShouldSkip(path) ||
                    name.StartsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                    skipProcessed && name.Contains("【已解压】", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ArchiveVolumeResolver.MatchesFormat(path, extension))
                {
                    continue;
                }

                var resolved = ArchiveVolumeResolver.Resolve(path);
                var taskPath = resolved.FirstVolumePath ?? resolved.Volumes.FirstOrDefault()?.Path ?? path;
                if (seen.Add(Path.GetFullPath(taskPath)))
                {
                    archives.Add(taskPath);
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Error loading archives from folder: {ex.Message}");
        }

        Log(LogLevel.Information, $"Found {archives.Count} archive tasks to process");
        return archives;
    }

    /// <summary>
    /// 兼容旧调用；新代码应明确调用压缩来源或解压归档扫描方法。
    /// </summary>
    public List<string> LoadFilesFromFolder(string folderPath, string extension, bool skipProcessed) =>
        LoadCompressionSourcesFromFolder(folderPath, skipProcessed);

    /// <summary>
    /// 从密码本文本文件加载带密码的归档条目。
    /// </summary>
    public List<FileEntry> LoadFilesFromTextFile(string txtFilePath, string sourceFolder, string extension)
    {
        return LoadFilesFromTextFileWithDiagnostics(txtFilePath, sourceFolder, extension).Entries;
    }

    // GPT-5, 2026-08-06：读取旧版“文件名/密码交替”密码本，并返回未匹配归档诊断。
    public TextFileImportResult LoadFilesFromTextFileWithDiagnostics(string txtFilePath, string sourceFolder, string extension)
    {
        var result = new TextFileImportResult();
        if (!File.Exists(txtFilePath))
        {
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(txtFilePath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();

            for (var i = 0; i < lines.Length; i += 2)
            {
                var filename = lines[i];
                var password = i + 1 < lines.Length ? lines[i + 1] : null;
                result.RequestedCount++;
                var requestedPath = ResolveTextEntryPath(filename, sourceFolder, extension);
                var resolved = ArchiveVolumeResolver.Resolve(requestedPath);
                if (resolved.HasCaseAmbiguity)
                {
                    result.AmbiguousEntries.Add(requestedPath);
                    Log(LogLevel.Warning, $"归档文件名存在大小写歧义，已跳过：{requestedPath}");
                    continue;
                }

                if (resolved.HasDuplicateNumbers)
                {
                    result.DuplicateVolumeEntries.Add(requestedPath);
                    Log(LogLevel.Warning, $"分卷编号重复，已跳过：{requestedPath}");
                    continue;
                }

                if (resolved.IsMultiVolume && (!resolved.HasRequiredFirstVolume || !resolved.IsSequenceContiguous))
                {
                    result.IncompleteVolumes.Add(requestedPath);
                    Log(LogLevel.Warning, BuildIncompleteVolumeMessage(resolved));
                    continue;
                }

                var fullPath = resolved.FirstVolumePath;
                if (fullPath == null || SystemMetadataFileFilter.ShouldSkip(fullPath) || !File.Exists(fullPath))
                {
                    result.MissingEntries.Add(requestedPath);
                    continue;
                }

                if (result.Entries.Any(entry => entry.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var fileSize = resolved.FilesForPostProcessing.Sum(path => new FileInfo(path).Length);
                result.Entries.Add(new FileEntry { FilePath = fullPath, Password = password, FileSize = fileSize });
                result.MatchedBytes += fileSize;
            }

            AddArchiveDiagnostics(result, sourceFolder, extension);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"读取文本文件失败: {ex.Message}");
        }

        return result;
    }

    // GPT-5, 2026-08-06：压缩清单使用“每行一个文件或目录”，不再把下一行误当成密码。
    public TextFileImportResult LoadCompressionPathsFromTextFile(string txtFilePath)
    {
        var result = new TextFileImportResult();
        if (!File.Exists(txtFilePath))
        {
            return result;
        }

        try
        {
            foreach (var rawLine in File.ReadAllLines(txtFilePath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || SystemMetadataFileFilter.ShouldSkip(line))
                {
                    continue;
                }

                result.RequestedCount++;
                var path = Path.IsPathRooted(line)
                    ? line
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(txtFilePath) ?? Directory.GetCurrentDirectory(), line));
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    result.MissingEntries.Add(path);
                    continue;
                }

                if (!result.Paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    result.Paths.Add(path);
                    result.MatchedBytes += File.Exists(path) ? new FileInfo(path).Length : 0;
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"读取压缩路径清单失败: {ex.Message}");
        }

        return result;
    }

    private static string ResolveTextEntryPath(string filename, string sourceFolder, string extension)
    {
        var normalizedName = ArchiveVolumeResolver.HasArchiveExtension(filename)
            ? filename
            : filename + "." + extension.Trim().TrimStart('.');
        return Path.IsPathRooted(normalizedName)
            ? Path.GetFullPath(normalizedName)
            : Path.GetFullPath(Path.Combine(sourceFolder, normalizedName));
    }

    private void AddArchiveDiagnostics(TextFileImportResult result, string sourceFolder, string extension)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        var matched = result.Entries
            .SelectMany(entry => ArchiveVolumeResolver.Resolve(entry.FilePath).FilesForPostProcessing)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var archive in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly))
        {
            if (SystemMetadataFileFilter.ShouldSkip(archive) ||
                !ArchiveVolumeResolver.MatchesFormat(archive, extension))
            {
                continue;
            }

            if (matched.Contains(Path.GetFullPath(archive)))
            {
                continue;
            }

            if (ArchiveVolumeResolver.Resolve(archive).IsMultiVolume)
            {
                result.VolumeCandidates.Add(archive);
            }
            else
            {
                result.UnmatchedArchives.Add(archive);
            }
        }
    }

    /// <summary>
    /// 计算目录中指定格式归档的总大小。
    /// </summary>
    public double CalculateTotalSizeGB(string directory, string extension)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            // GLM-5.3, 2026-11-03：改用 ArchiveVolumeResolver.MatchesFormat 统计，修复两个漏计：
            // 1) Unix 上 GetFiles 通配符按大小写敏感匹配，*.rar 漏掉大写 *.RAR；
            // 2) 分卷文件名（base.7z.001、base.r00 等）不匹配 *.7z / *.rar 通配符。
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => ArchiveVolumeResolver.MatchesFormat(path, extension))
                .ToArray();
            long totalBytes = files.Sum(f => new FileInfo(f).Length);
            return totalBytes / (1024.0 * 1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 批量压缩文件或目录。
    /// GLM-5.3, 2026-11-04：批次 2 阶段 1——逐项循环已下沉到 BatchExecutionPipeline，
    /// 此处保留批次级格式校验与适配器装配。
    /// </summary>
    public async Task BatchCompressAsync(
        List<string> sourcePaths,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        Log(LogLevel.Information, $"Starting batch compression of {sourcePaths.Count} items");
        Log(LogLevel.Information, $"Output path: {options.OutputPath}");
        Log(LogLevel.Information, $"Extension: {options.Extension}, Compression level: {options.CompressionLevel}");

        // 不可创建格式直接计一条失败并返回：不进循环、不触发关机。
        var normalizedFormat = ArchiveFormatCatalog.Normalize(options.Extension);
        if (!ArchiveFormatCatalog.TryGet(normalizedFormat, out var formatDefinition) ||
            !formatDefinition.CanCreate)
        {
            var progressInfo = new OperationProgressInfo
            {
                StartTime = DateTime.Now
            };
            var message = string.Format(L.FormatCannotCreate, options.Extension, ArchiveFormatCatalog.CreateFormatsText);
            Log(LogLevel.Error, message);
            progressInfo.FailCount++;
            progressInfo.Message = message;
            progressInfo.IsError = true;
            progress.Report(progressInfo);
            return;
        }

        var pipeline = new BatchExecutionPipeline<string>(
            new CompressionItemAdapter(_archiveEngine, normalizedFormat, formatDefinition, _logger),
            _postProcessService,
            _systemIntegration,
            _logger);
        await pipeline.RunAsync(sourcePaths, options, progress, cancellationToken);
    }

    /// <summary>
    /// 批量解压归档文件。
    /// GLM-5.3, 2026-11-04：批次 2 阶段 1——逐项循环已下沉到 BatchExecutionPipeline，
    /// 此处只保留适配器装配。
    /// </summary>
    public async Task BatchDecompressAsync(
        List<FileEntry> archives,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        Log(LogLevel.Information, $"Starting batch decompression of {archives.Count} archives");
        Log(LogLevel.Information, $"Output path: {options.OutputPath}");

        var pipeline = new BatchExecutionPipeline<FileEntry>(
            new DecompressionItemAdapter(_archiveEngine, options, _logger),
            _postProcessService,
            _systemIntegration,
            _logger);
        await pipeline.RunAsync(archives, options, progress, cancellationToken);
    }

    /// <summary>
    /// 构建缺卷跳过消息（解压适配器与 TXT 密码本导入共用）。
    /// </summary>
    internal static string BuildIncompleteVolumeMessage(ArchiveVolumeResolveResult resolved)
    {
        // GLM-5.3, 2026-11-03：缺卷编号换算为实际文件名后缀（r01 / z02 / part3.rar），
        // 旧式分卷的组内编号与文件名后缀有偏移，直接显示数字会误导排查。
        var reason = !resolved.HasRequiredFirstVolume
            ? L.MissingFirstVolume
            : string.Format(
                L.MissingVolumeNumbers,
                string.Join(", ", resolved.MissingNumbers.Select(
                    number => ArchiveVolumeResolver.DescribeVolumeSuffix(resolved, number))));
        return string.Format(L.IncompleteVolumesSkipped, resolved.RequestedPath, reason);
    }
}
