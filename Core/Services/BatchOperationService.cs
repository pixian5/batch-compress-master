using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
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
    private readonly ILogger? _logger;

    public BatchOperationService(IArchiveEngine archiveEngine, ISystemIntegration systemIntegration, ILogger? logger = null)
    {
        _archiveEngine = archiveEngine;
        _systemIntegration = systemIntegration;
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
    /// 判断是否为分卷归档，并返回第一卷路径。
    /// </summary>
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
            var files = Directory.GetFiles(directory, $"*.{extension}", SearchOption.TopDirectoryOnly);
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

        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };

        double processedSizeGB = 0;

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // GPT-5, 2026-08-05：防御手工编辑列表绕过正常枚举的情况。
            if (SystemMetadataFileFilter.ShouldSkip(sourcePath))
            {
                Log(LogLevel.Debug, $"Skipping system metadata path: {sourcePath}");
                continue;
            }

            // 手工编辑列表后仍需再次检查文件或目录是否存在。
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                Log(LogLevel.Warning, $"Source not found: {sourcePath}");
                progressInfo.NonExistCount++;
                progressInfo.Message = $"Not found: {sourcePath}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
                continue;
            }

            var name = Path.GetFileName(sourcePath);
            progressInfo.CurrentFile = name;
            Log(LogLevel.Debug, $"Processing: {name}");

            // 按选项跳过已经完成压缩的项目。
            if (options.SkipAlreadyProcessed && name.Contains("【已压缩】"))
            {
                Log(LogLevel.Debug, $"Skipping already processed: {name}");
                progressInfo.IgnoreCount++;
                continue;
            }

            // 使用来源项目名称构造输出归档名称。
            var outputFileName = name + "." + options.Extension;
            var outputDirectory = OutputPathResolver.ResolveAndCreate(options.OutputPath, sourcePath);
            var outputPath = Path.Combine(outputDirectory, outputFileName);

            // 根据用户选择处理已有输出文件。
            if (File.Exists(outputPath))
            {
                if (options.ExistingFileMode == ExistingFileMode.Skip)
                {
                    Log(LogLevel.Debug, $"Skipping existing output: {outputPath}");
                    progressInfo.IgnoreCount++;
                    progressInfo.Message = $"[跳过] 已存在：{outputFileName}";
                    progressInfo.IsError = false;
                    progress.Report(progressInfo);
                    continue;
                }
                else if (options.ExistingFileMode == ExistingFileMode.Overwrite)
                {
                    Log(LogLevel.Debug, $"Deleting existing output: {outputPath}");
                    File.Delete(outputPath);
                }
            }

            // 按当前选项生成单个归档的密码。
            string? password = null;
            if (options.UseRandomPassword)
            {
                var passwordName = PasswordUtility.GetPasswordSourceName(outputFileName, options.PasswordNameMode);
                password = PasswordUtility.GenerateCompressionPassword(passwordName);
            }
            else if (!string.IsNullOrEmpty(options.CustomPassword))
            {
                password = options.CustomPassword;
            }

            // GPT-5, 2026-08-05：每个来源项目只转换一次批处理默认值到更窄的引擎契约。
            var archiveOptions = new ArchiveOptions
            {
                ArchiveFormat = options.Extension,
                Password = password,
                CompressionLevel = options.CompressionLevel,
                SolidArchive = options.SolidArchive,
                QuickOpen = options.QuickOpen,
                TestArchive = options.TestArchive,
                CommentFile = options.CommentFile,
                TempDirectory = string.IsNullOrWhiteSpace(options.TempDirectory)
                    ? outputDirectory
                    : options.TempDirectory,
                ExistingFileMode = options.ExistingFileMode,
                RecoveryRecordPercent = options.RecoveryRecordPercent,
                LockArchive = options.LockArchive,
                RarStoreOnlyExtensions = options.Extension.Equals("rar", StringComparison.OrdinalIgnoreCase)
                    ? options.RarStoreOnlyExtensions
                    : null,
                VolumeSize = !string.IsNullOrEmpty(options.VolumeSize) ?
                    options.VolumeSize + options.VolumeSizeUnit : null
            };

            // GPT-5, 2026-08-06：附件直接作为额外输入交给归档程序。存在的路径保留其 basename，
            // 不存在的路径只在临时暂存目录创建空目录，避免修改用户源目录。
            var stagingDirectory = CreateAttachmentInputs(options, out var attachmentInputs);
            archiveOptions.AdditionalInputs = attachmentInputs;

            Log(LogLevel.Information, $"Compression started: {name} -> {outputFileName}");
            progressInfo.Message = $"[开始压缩] {name}";
            progressInfo.IsError = false;
            progress.Report(progressInfo);

            // 调用归档引擎执行压缩。
            ArchiveResult result;
            try
            {
                result = await _archiveEngine.CompressAsync(sourcePath, outputPath, archiveOptions, cancellationToken);
            }
            finally
            {
                if (stagingDirectory != null)
                {
                    try { Directory.Delete(stagingDirectory, recursive: true); }
                    catch (Exception ex) { Log(LogLevel.Warning, $"清理附件暂存目录失败：{ex.Message}"); }
                }
            }
            ReportArchiveOutput("压缩命令", result, progressInfo, progress);

            if (result.Success)
            {
                Log(LogLevel.Information, $"Compression successful: {name} -> {outputFileName}");
                progressInfo.SuccessCount++;

                // 以输出归档大小累计处理量。
                if (File.Exists(outputPath))
                {
                    var sizeGB = new FileInfo(outputPath).Length / (1024.0 * 1024.0 * 1024.0);
                    processedSizeGB += sizeGB;
                    progressInfo.ProcessedSizeGB = processedSizeGB;
                    Log(LogLevel.Debug, $"Output size: {sizeGB:F3} GB, Total processed: {processedSizeGB:F3} GB");
                }

                var postProcessFailed = false;

                // 成功后执行删除或移动等后处理。
                if (options.DeleteSourceAfter)
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Deleting source: {sourcePath}");
                        if (Directory.Exists(sourcePath))
                        {
                            Directory.Delete(sourcePath, true);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            File.Delete(sourcePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        RecordPostProcessFailure(progressInfo, $"删除源失败：{sourcePath}：{ex.Message}");
                        postProcessFailed = true;
                    }
                }
                else if (options.MoveSourceAfter)
                {
                    try
                    {
                        var processedDir = Path.Combine(Path.GetDirectoryName(sourcePath) ?? "", "【已压缩】");
                        if (!Directory.Exists(processedDir))
                        {
                            Directory.CreateDirectory(processedDir);
                        }

                        var targetPath = Path.Combine(processedDir, name);
                        Log(LogLevel.Debug, $"Moving source to: {targetPath}");

                        postProcessFailed = !TryMoveWithoutOverwrite(sourcePath, targetPath, progressInfo, "压缩源");
                    }
                    catch (Exception ex)
                    {
                        RecordPostProcessFailure(progressInfo, $"移动压缩源失败：{sourcePath}：{ex.Message}");
                        postProcessFailed = true;
                    }
                }

                // 归档成功和后处理成功分别统计，避免“归档成功但移动失败”被显示为完全成功。
                progressInfo.Message = postProcessFailed
                    ? $"成功但后处理失败: {name}"
                    : $"成功: {name}";
                progressInfo.IsError = postProcessFailed;
            }
            else
            {
                Log(LogLevel.Error, $"Compression failed: {name} - {result.ErrorMessage}");
                progressInfo.FailCount++;
                progressInfo.Message = $"失败: {name} - {result.ErrorMessage}";
                progressInfo.IsError = true;
            }

            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
            progress.Report(progressInfo);

            // 达到总大小上限后停止后续任务。
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }

        Log(LogLevel.Information, $"Batch compression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}");

        // 用户要求时在全部任务结束后请求系统关机。
        if (options.ShutdownAfterComplete)
        {
            await _systemIntegration.ShutdownAsync();
        }
    }

    private static string? CreateAttachmentInputs(BatchOperationOptions options, out string[] inputs)
    {
        inputs = [];
        if (!options.AddEnclosures || options.EnclosureDirectories is not { Length: > 0 })
        {
            return null;
        }

        string? stagingDirectory = null;
        var result = new List<string>();
        foreach (var rawPath in options.EnclosureDirectories)
        {
            var path = rawPath.Trim();
            if (path.Length == 0 || SystemMetadataFileFilter.ShouldSkip(path))
            {
                continue;
            }

            if (File.Exists(path) || Directory.Exists(path))
            {
                result.Add(Path.GetFullPath(path));
                continue;
            }

            stagingDirectory ??= Path.Combine(Path.GetTempPath(), $"batch-compress-attachments-{Guid.NewGuid():N}");
            var safeName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..")
            {
                safeName = "附件";
            }

            var emptyDirectory = Path.Combine(stagingDirectory, safeName);
            Directory.CreateDirectory(emptyDirectory);
            result.Add(emptyDirectory);
        }

        inputs = result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return stagingDirectory;
    }

    /// <summary>
    /// 批量解压归档文件。
    /// </summary>
    public async Task BatchDecompressAsync(
        List<FileEntry> archives,
        BatchOperationOptions options,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        Log(LogLevel.Information, $"Starting batch decompression of {archives.Count} archives");
        Log(LogLevel.Information, $"Output path: {options.OutputPath}");

        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };

        double processedSizeGB = 0;
        var processedArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // GPT-5, 2026-08-05：TXT 列表可由用户编辑，因此执行时再次实施相同的元数据过滤。
            if (SystemMetadataFileFilter.ShouldSkip(entry.FilePath))
            {
                Log(LogLevel.Debug, $"Skipping system metadata archive: {entry.FilePath}");
                continue;
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
                progressInfo.Message = $"无法解析: {entry.FilePath} - {ex.Message}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
                continue;
            }

            if (resolved.HasCaseAmbiguity)
            {
                progressInfo.AmbiguousArchiveCount++;
                progressInfo.IgnoreCount++;
                progressInfo.Message = $"文件名大小写存在歧义，已跳过: {entry.FilePath}";
                progressInfo.IsError = true;
                Log(LogLevel.Warning, progressInfo.Message);
                progress.Report(progressInfo);
                continue;
            }

            if (resolved.HasDuplicateNumbers)
            {
                progressInfo.AmbiguousArchiveCount++;
                progressInfo.IgnoreCount++;
                progressInfo.Message = $"分卷编号重复，已跳过: {entry.FilePath}";
                progressInfo.IsError = true;
                Log(LogLevel.Warning, progressInfo.Message);
                progress.Report(progressInfo);
                continue;
            }

            if (resolved.IsMultiVolume && (!resolved.HasRequiredFirstVolume || !resolved.IsSequenceContiguous))
            {
                progressInfo.IncompleteVolumeCount++;
                progressInfo.IgnoreCount++;
                progressInfo.Message = BuildIncompleteVolumeMessage(resolved);
                progressInfo.IsError = true;
                Log(LogLevel.Warning, progressInfo.Message);
                progress.Report(progressInfo);
                continue;
            }

            var archivePath = resolved.FirstVolumePath;
            if (archivePath == null || !File.Exists(archivePath))
            {
                Log(LogLevel.Warning, $"Archive not found: {entry.FilePath}");
                progressInfo.NonExistCount++;
                progressInfo.Message = $"Not found: {entry.FilePath}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
                continue;
            }

            if (!processedArchives.Add(Path.GetFullPath(archivePath)))
            {
                Log(LogLevel.Debug, $"Skipping duplicate archive task: {archivePath}");
                progressInfo.IgnoreCount++;
                continue;
            }

            var archiveName = Path.GetFileName(archivePath);
            progressInfo.CurrentFile = archiveName;
            Log(LogLevel.Debug, $"Processing: {archiveName}");

            // 按选项跳过已经完成解压的归档。
            if (options.SkipAlreadyProcessed && archiveName.Contains("【已解压】"))
            {
                Log(LogLevel.Debug, $"Skipping already processed: {archiveName}");
                progressInfo.IgnoreCount++;
                continue;
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

            Log(LogLevel.Information, $"Extraction started: {archiveName}");
            progressInfo.Message = $"[开始解压] {archiveName}";
            progressInfo.IsError = false;
            progress.Report(progressInfo);

            var outputDirectory = OutputPathResolver.ResolveAndCreate(options.OutputPath, archivePath);

            // 调用归档引擎执行解压。
            var result = await _archiveEngine.ExtractAsync(archivePath, outputDirectory, archiveOptions, cancellationToken);
            ReportArchiveOutput("解压命令", result, progressInfo, progress);

            if (result.Success)
            {
                Log(LogLevel.Information, $"Extraction successful: {archiveName}");
                progressInfo.SuccessCount++;

                // 以当前归档大小累计处理量。
                var processedBytes = resolved.FilesForPostProcessing.Count > 0
                    ? resolved.FilesForPostProcessing.Sum(path => new FileInfo(path).Length)
                    : entry.FileSize;
                var sizeGB = processedBytes / (1024.0 * 1024.0 * 1024.0);
                processedSizeGB += sizeGB;
                progressInfo.ProcessedSizeGB = processedSizeGB;

                var postProcessFailed = false;

                // 成功后按选项删除或移动源归档。
                if (options.DeleteSourceAfter || options.MoveSourceAfter)
                {
                    // 直接使用解压前已经校验过的文件集合，避免后处理阶段再次解析出不同结果。
                    var volumeFiles = resolved.FilesForPostProcessing;
                    var moveBlockedByConflict = false;

                    // GPT-5, 2026-08-07：分卷移动先整体检查目标，避免前几卷已移动、后几卷冲突而形成半组状态。
                    if (options.MoveSourceAfter)
                    {
                        var conflictingTarget = volumeFiles
                            .Select(volumeFile => Path.Combine(
                                Path.GetDirectoryName(volumeFile) ?? string.Empty,
                                "【已解压】",
                                Path.GetFileName(volumeFile)))
                            .FirstOrDefault(target => File.Exists(target) || Directory.Exists(target));
                        if (conflictingTarget != null)
                        {
                            RecordPostProcessFailure(
                                progressInfo,
                                $"解压归档目标已存在，已保留整组源卷：{conflictingTarget}");
                            moveBlockedByConflict = true;
                            postProcessFailed = true;
                        }
                    }

                    foreach (var volumeFile in volumeFiles)
                    {
                        if (moveBlockedByConflict)
                        {
                            break;
                        }

                        try
                        {
                            if (options.DeleteSourceAfter)
                            {
                                Log(LogLevel.Debug, $"Deleting archive: {volumeFile}");
                                File.Delete(volumeFile);
                            }
                            else if (options.MoveSourceAfter)
                            {
                                var processedDir = Path.Combine(
                                    Path.GetDirectoryName(volumeFile) ?? "", "【已解压】");
                                if (!Directory.Exists(processedDir))
                                {
                                    Directory.CreateDirectory(processedDir);
                                }

                                var targetPath = Path.Combine(processedDir, Path.GetFileName(volumeFile));
                                Log(LogLevel.Debug, $"Moving archive to: {targetPath}");

                                if (!TryMoveWithoutOverwrite(volumeFile, targetPath, progressInfo, "解压归档"))
                                {
                                    postProcessFailed = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            RecordPostProcessFailure(progressInfo, $"处理解压归档失败：{volumeFile}：{ex.Message}");
                            postProcessFailed = true;
                        }
                    }
                }

                // 归档成功和后处理成功分别统计，避免后处理失败被隐藏。
                progressInfo.Message = postProcessFailed
                    ? $"成功但后处理失败: {archiveName}"
                    : $"成功: {archiveName}";
                progressInfo.IsError = postProcessFailed;
            }
            else
            {
                Log(LogLevel.Error, $"Extraction failed: {archiveName} - {result.ErrorMessage}");
                progressInfo.FailCount++;
                progressInfo.Message = $"失败: {archiveName} - {result.ErrorMessage}";
                progressInfo.IsError = true;
            }

            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
            progress.Report(progressInfo);

            // 达到总大小上限后停止后续解压任务。
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }

        Log(LogLevel.Information, $"Batch decompression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}");

        // 用户要求时在全部解压任务结束后请求系统关机。
        if (options.ShutdownAfterComplete)
        {
            Log(LogLevel.Information, "Shutdown requested after completion");
            await _systemIntegration.ShutdownAsync();
        }
    }

    // GPT-5, 2026-08-06：归档进程输出是用户要求保留的原始诊断证据。
    // 此处明确不调用脱敏、掩码或替换逻辑，即使 stdout/stderr 中包含密码也原样转发。
    private static void ReportArchiveOutput(
        string prefix,
        ArchiveResult result,
        OperationProgressInfo progressInfo,
        IProgress<OperationProgressInfo> progress)
    {
        if (!string.IsNullOrWhiteSpace(result.CommandLine))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] command{Environment.NewLine}{result.CommandLine}",
                isError: false));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] stdout{Environment.NewLine}{result.StandardOutput.TrimEnd()}",
                isError: false));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            progress.Report(CloneProgress(
                progressInfo,
                $"[{prefix}] stderr{Environment.NewLine}{result.StandardError.TrimEnd()}",
                isError: !result.Success));
        }
    }

    // GPT-5, 2026-08-06：IProgress 可能异步投递回调，因此命令输出必须使用独立快照，
    // 防止下一条状态消息改写同一个可变对象后导致 stdout/stderr 丢失。
    private static OperationProgressInfo CloneProgress(
        OperationProgressInfo source,
        string message,
        bool isError) => new()
        {
            CurrentFile = source.CurrentFile,
            SuccessCount = source.SuccessCount,
            FailCount = source.FailCount,
            PostProcessFailCount = source.PostProcessFailCount,
            IgnoreCount = source.IgnoreCount,
            NonExistCount = source.NonExistCount,
            IncompleteVolumeCount = source.IncompleteVolumeCount,
            AmbiguousArchiveCount = source.AmbiguousArchiveCount,
            ProcessedSizeGB = source.ProcessedSizeGB,
            Message = message,
            IsError = isError,
            StartTime = source.StartTime,
            Elapsed = source.Elapsed
        };

    // GPT-5, 2026-08-07：移动属于可选后处理，目标冲突时必须保留原目标和源文件，禁止先删除目标。
    private bool TryMoveWithoutOverwrite(
        string sourcePath,
        string targetPath,
        OperationProgressInfo progressInfo,
        string itemType)
    {
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            RecordPostProcessFailure(progressInfo, $"{itemType}目标已存在，已保留双方文件：{targetPath}");
            return false;
        }

        try
        {
            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, targetPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, targetPath);
            }
            else
            {
                throw new FileNotFoundException("源文件或目录不存在", sourcePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            RecordPostProcessFailure(progressInfo, $"移动{itemType}失败：{sourcePath} -> {targetPath}：{ex.Message}");
            return false;
        }
    }

    private void RecordPostProcessFailure(OperationProgressInfo progressInfo, string message)
    {
        progressInfo.PostProcessFailCount++;
        Log(LogLevel.Warning, message);
    }

    private static string BuildIncompleteVolumeMessage(ArchiveVolumeResolveResult resolved)
    {
        var reason = !resolved.HasRequiredFirstVolume
            ? "缺少编号 1 的首卷"
            : $"缺少分卷编号 {string.Join(", ", resolved.MissingNumbers)}";
        return $"分卷不完整，已跳过：{resolved.RequestedPath}（{reason}）";
    }
}
