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
    /// 从目录加载文件列表。
    /// </summary>
    public List<string> LoadFilesFromFolder(string folderPath, string extension, bool skipProcessed)
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
                var fullPath = ResolveTextEntryPath(filename, sourceFolder, extension);
                if (IsMultiVolumeArchive(fullPath, extension, out var firstVolumePath))
                {
                    fullPath = firstVolumePath;
                }

                if (SystemMetadataFileFilter.ShouldSkip(fullPath) || !File.Exists(fullPath))
                {
                    result.MissingEntries.Add(fullPath);
                    continue;
                }

                if (result.Entries.Any(entry => entry.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var fileSize = new FileInfo(fullPath).Length;
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
        if (Path.IsPathRooted(filename))
        {
            return filename;
        }

        var normalizedName = filename.Contains('.') ? filename : filename + "." + extension;
        return Path.Combine(sourceFolder, normalizedName);
    }

    private void AddArchiveDiagnostics(TextFileImportResult result, string sourceFolder, string extension)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        var matched = result.Entries.Select(entry => Path.GetFullPath(entry.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var archive in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly))
        {
            if (SystemMetadataFileFilter.ShouldSkip(archive) ||
                !Path.GetExtension(archive).TrimStart('.').Equals(extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matched.Contains(Path.GetFullPath(archive)))
            {
                continue;
            }

            if (Path.GetFileName(archive).Contains(".part", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(Path.GetFileName(archive), @"\.7z\.\d+$", RegexOptions.IgnoreCase))
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
    private bool IsMultiVolumeArchive(string path, string extension, out string firstVolumePath)
    {
        firstVolumePath = path;
        
        var filename = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        
        // 检查 .partXXX.扩展名形式的 RAR 分卷。
        // GPT-5, 2026-08-06：7-Zip 分卷使用 archive.7z.001 形式，只有编号 1 的卷可以作为任务入口。
        var sevenZipMatch = Regex.Match(filename, @"^(?<base>.+\.7z)\.(?<number>\d+)$", RegexOptions.IgnoreCase);
        if (sevenZipMatch.Success)
        {
            var digits = sevenZipMatch.Groups["number"].Value.Length;
            var firstName = sevenZipMatch.Groups["base"].Value + "." + 1.ToString().PadLeft(digits, '0');
            var candidate = Path.Combine(directory, firstName);
            if (File.Exists(candidate))
            {
                firstVolumePath = candidate;
                return true;
            }
        }

        var partMatch = Regex.Match(filename, @"\.part(\d+)\." + Regex.Escape(extension) + "$", RegexOptions.IgnoreCase);
        if (partMatch.Success)
        {
            // 兼容三种常见的首卷编号宽度。
            var baseName = filename.Substring(0, partMatch.Index);
            string[] patterns = { ".part001.", ".part01.", ".part0001." };
            
            foreach (var pattern in patterns)
            {
                var firstVolume = Path.Combine(directory, baseName + pattern + extension);
                if (File.Exists(firstVolume))
                {
                    firstVolumePath = firstVolume;
                    return true;
                }
            }
        }
        
        return false;
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
                password = PasswordUtility.GenerateCompressionPassword(outputFileName);
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
                VolumeSize = !string.IsNullOrEmpty(options.VolumeSize) ? 
                    options.VolumeSize + options.VolumeSizeUnit : null
            };
            
            // GPT-5, 2026-08-05：压缩前确保请求的附件目录存在，使 WinRAR 能够包含它们。
            if (options.AddEnclosures && Directory.Exists(sourcePath) && 
                options.EnclosureDirectories != null)
            {
                foreach (var enclosurePath in options.EnclosureDirectories)
                {
                    var enclosureName = Path.GetFileName(enclosurePath);
                    var targetPath = Path.Combine(sourcePath, enclosureName);
                    if (!Directory.Exists(targetPath))
                    {
                        Log(LogLevel.Debug, $"Creating enclosure directory: {targetPath}");
                        Directory.CreateDirectory(targetPath);
                    }
                }
            }
            
            Log(LogLevel.Information, $"Compression started: {name} -> {outputFileName}");
            progressInfo.Message = $"[开始压缩] {name}";
            progressInfo.IsError = false;
            progress.Report(progressInfo);
            
            // 调用归档引擎执行压缩。
            var result = await _archiveEngine.CompressAsync(sourcePath, outputPath, archiveOptions, cancellationToken);
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
                        Log(LogLevel.Warning, $"Failed to delete source: {ex.Message}");
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
                        
                        // 检查目标是否存在，如果存在则先删除
                        if (Directory.Exists(targetPath))
                        {
                            Directory.Delete(targetPath, true);
                        }
                        else if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }
                        
                        if (Directory.Exists(sourcePath))
                        {
                            Directory.Move(sourcePath, targetPath);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            File.Move(sourcePath, targetPath);
                        }
                    }
                    catch (Exception ex) 
                    { 
                        Log(LogLevel.Warning, $"Failed to move source: {ex.Message}");
                    }
                }
                
                // 成功消息仍写入成功日志，但不重复写入命令日志。
                progressInfo.Message = $"成功: {name}";
                progressInfo.IsError = false;
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
        
        foreach (var entry in archives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // GPT-5, 2026-08-05：TXT 列表可由用户编辑，因此执行时再次实施相同的元数据过滤。
            if (SystemMetadataFileFilter.ShouldSkip(entry.FilePath))
            {
                Log(LogLevel.Debug, $"Skipping system metadata archive: {entry.FilePath}");
                continue;
            }
            
            var archivePath = entry.FilePath;
            
            if (!File.Exists(archivePath))
            {
                Log(LogLevel.Warning, $"Archive not found: {archivePath}");
                progressInfo.NonExistCount++;
                progressInfo.Message = $"Not found: {archivePath}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
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
            
            // 分卷只允许第一卷启动解压，后处理仍覆盖全部卷。
            if (IsMultiVolumeArchive(archivePath, options.Extension, out var firstVolume))
            {
                if (!archivePath.Equals(firstVolume, StringComparison.OrdinalIgnoreCase))
                {
                    Log(LogLevel.Debug, $"Skipping non-first volume: {archiveName}");
                    // 非首卷不重复启动解压。
                    continue;
                }
            }
            
            // 优先使用 TXT 条目密码，再使用全局密码策略。
            string? password = entry.Password;
            if (string.IsNullOrEmpty(password) && options.UseRandomPassword)
            {
                password = PasswordUtility.GenerateDecompressionPassword(archiveName);
            }
            else if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(options.CustomPassword))
            {
                password = options.CustomPassword;
            }
            
            // 将批处理选项转换为归档引擎选项。
            var archiveOptions = new ArchiveOptions
            {
                // GPT-5, 2026-08-06：解压路由优先看实际文件名，同时保留批处理扩展名作为无后缀输入的后备信息。
                ArchiveFormat = Path.GetExtension(archivePath).TrimStart('.') is { Length: > 0 } actualFormat
                    ? actualFormat
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
                var sizeGB = entry.FileSize / (1024.0 * 1024.0 * 1024.0);
                processedSizeGB += sizeGB;
                progressInfo.ProcessedSizeGB = processedSizeGB;
                
                // 成功后按选项删除或移动源归档。
                if (options.DeleteSourceAfter || options.MoveSourceAfter)
                {
                    // 取得同一分卷组的全部文件，确保后处理完整。
                    var volumeFiles = GetAllVolumeFiles(archivePath, options.Extension);
                    
                    foreach (var volumeFile in volumeFiles)
                    {
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
                                
                                // 检查目标是否存在，如果存在则先删除
                                if (File.Exists(targetPath))
                                {
                                    File.Delete(targetPath);
                                }
                                
                                File.Move(volumeFile, targetPath);
                            }
                        }
                        catch (Exception ex) 
                        { 
                            Log(LogLevel.Warning, $"Failed to process archive file: {ex.Message}");
                        }
                    }
                }
                
                // 成功消息仍写入成功日志，但不重复写入命令日志。
                progressInfo.Message = $"成功: {archiveName}";
                progressInfo.IsError = false;
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
        IgnoreCount = source.IgnoreCount,
        NonExistCount = source.NonExistCount,
        ProcessedSizeGB = source.ProcessedSizeGB,
        Message = message,
        IsError = isError,
        StartTime = source.StartTime,
        Elapsed = source.Elapsed
    };
    
    /// <summary>
    /// 获取分卷归档的全部卷文件。
    /// </summary>
    private List<string> GetAllVolumeFiles(string archivePath, string extension)
    {
        var files = new List<string> { archivePath };
        
        var filename = Path.GetFileName(archivePath);
        var directory = Path.GetDirectoryName(archivePath) ?? string.Empty;
        
        // 检查 RAR 分卷和 7z 数字分卷两种命名形式。
        // GPT-5, 2026-08-06：成功后的删除或移动必须覆盖同一 7z 数字分卷组，不能只处理 .001。
        var sevenZipMatch = Regex.Match(filename, @"^(?<base>.+\.7z)\.(?<number>\d+)$", RegexOptions.IgnoreCase);
        if (sevenZipMatch.Success && Directory.Exists(directory))
        {
            var baseName = sevenZipMatch.Groups["base"].Value;
            var digitCount = sevenZipMatch.Groups["number"].Value.Length;
            files.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Regex.IsMatch(
                    Path.GetFileName(path),
                    "^" + Regex.Escape(baseName) + @"\.\d{" + digitCount + "}$",
                    RegexOptions.IgnoreCase)));
        }

        var partMatch = Regex.Match(filename, @"\.part(\d+)\." + Regex.Escape(extension) + "$", RegexOptions.IgnoreCase);
        if (partMatch.Success)
        {
            var baseName = filename.Substring(0, partMatch.Index);
            var digitCount = partMatch.Groups[1].Value.Length;
            
            // 按首卷编号宽度依次查找后续分卷。
            for (int i = 1; i <= 999; i++)
            {
                var partNumber = i.ToString().PadLeft(digitCount, '0');
                var volumePath = Path.Combine(directory, $"{baseName}.part{partNumber}.{extension}");
                
                if (File.Exists(volumePath) && !files.Contains(volumePath))
                {
                    files.Add(volumePath);
                }
                else if (i > int.Parse(partMatch.Groups[1].Value))
                {
                    break;
                }
            }
        }
        
        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
