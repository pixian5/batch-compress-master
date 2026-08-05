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
/// Service for batch compression and decompression operations
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
    /// Load file list from folder
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
            // Get all files and directories in the root of the folder
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
                
                // Skip already processed files if option is enabled
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
    /// Load file entries from text file (with passwords)
    /// </summary>
    public List<FileEntry> LoadFilesFromTextFile(string txtFilePath, string sourceFolder, string extension)
    {
        var entries = new List<FileEntry>();
        
        if (!File.Exists(txtFilePath))
        {
            return entries;
        }
        
        try
        {
            var lines = File.ReadAllLines(txtFilePath);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                
                // Odd lines are filenames, even lines are passwords
                string filename = line;
                string? password = null;
                
                // Get password from next line
                if (i + 1 < lines.Length)
                {
                    password = lines[i + 1].Trim();
                    i++; // Skip the password line in next iteration
                }
                
                // Construct full path
                string fullPath;
                if (Path.IsPathRooted(filename))
                {
                    fullPath = filename;
                }
                else
                {
                    // Add extension if not present
                    if (!filename.Contains('.'))
                    {
                        filename += "." + extension;
                    }
                    fullPath = Path.Combine(sourceFolder, filename);
                }
                
                // Check for multi-volume archives
                if (IsMultiVolumeArchive(fullPath, extension, out var firstVolumePath))
                {
                    fullPath = firstVolumePath;
                }
                
                if (!SystemMetadataFileFilter.ShouldSkip(fullPath) && File.Exists(fullPath))
                {
                    entries.Add(new FileEntry
                    {
                        FilePath = fullPath,
                        Password = password,
                        FileSize = new FileInfo(fullPath).Length
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Error loading files from text file: {ex.Message}");
            Console.WriteLine($"Error loading files from text file: {ex.Message}");
        }
        
        return entries;
    }
    
    /// <summary>
    /// Check if file is a multi-volume archive and return first volume
    /// </summary>
    private bool IsMultiVolumeArchive(string path, string extension, out string firstVolumePath)
    {
        firstVolumePath = path;
        
        var filename = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        
        // Check for .partXXX.extension pattern
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
            // Try to find part001 or part01 or part0001
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
    /// Calculate total size of archives in a directory
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
    /// Batch compress files
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
            
            // Check if file/directory exists
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
            
            // Skip if already processed
            if (options.SkipAlreadyProcessed && name.Contains("【已压缩】"))
            {
                Log(LogLevel.Debug, $"Skipping already processed: {name}");
                progressInfo.IgnoreCount++;
                continue;
            }
            
            // Build output filename
            var outputFileName = name + "." + options.Extension;
            var outputDirectory = OutputPathResolver.ResolveAndCreate(options.OutputPath, sourcePath);
            var outputPath = Path.Combine(outputDirectory, outputFileName);
            
            // Check if output exists
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
            
            // Generate password
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
            
            // Compress
            var result = await _archiveEngine.CompressAsync(sourcePath, outputPath, archiveOptions, cancellationToken);
            ReportArchiveOutput("压缩命令", result, progressInfo, progress);
            
            if (result.Success)
            {
                Log(LogLevel.Information, $"Compression successful: {name} -> {outputFileName}");
                progressInfo.SuccessCount++;
                
                // Calculate size
                if (File.Exists(outputPath))
                {
                    var sizeGB = new FileInfo(outputPath).Length / (1024.0 * 1024.0 * 1024.0);
                    processedSizeGB += sizeGB;
                    progressInfo.ProcessedSizeGB = processedSizeGB;
                    Log(LogLevel.Debug, $"Output size: {sizeGB:F3} GB, Total processed: {processedSizeGB:F3} GB");
                }
                
                // Post-processing
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
                
                // Success message still goes to SuccessLog, but not to CommandLog
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
            
            // Check size limit
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }
        
        Log(LogLevel.Information, $"Batch compression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}");
        
        // Shutdown if requested
        if (options.ShutdownAfterComplete)
        {
            await _systemIntegration.ShutdownAsync();
        }
    }
    
    /// <summary>
    /// Batch decompress files
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
            
            // Skip if already processed
            if (options.SkipAlreadyProcessed && archiveName.Contains("【已解压】"))
            {
                Log(LogLevel.Debug, $"Skipping already processed: {archiveName}");
                progressInfo.IgnoreCount++;
                continue;
            }
            
            // Check if it's a multi-volume archive but not the first volume
            if (IsMultiVolumeArchive(archivePath, options.Extension, out var firstVolume))
            {
                if (!archivePath.Equals(firstVolume, StringComparison.OrdinalIgnoreCase))
                {
                    Log(LogLevel.Debug, $"Skipping non-first volume: {archiveName}");
                    // Skip non-first volumes
                    continue;
                }
            }
            
            // Determine password
            string? password = entry.Password;
            if (string.IsNullOrEmpty(password) && options.UseRandomPassword)
            {
                password = PasswordUtility.GenerateDecompressionPassword(archiveName);
            }
            else if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(options.CustomPassword))
            {
                password = options.CustomPassword;
            }
            
            // Build archive options
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
            
            // Extract
            var result = await _archiveEngine.ExtractAsync(archivePath, outputDirectory, archiveOptions, cancellationToken);
            ReportArchiveOutput("解压命令", result, progressInfo, progress);
            
            if (result.Success)
            {
                Log(LogLevel.Information, $"Extraction successful: {archiveName}");
                progressInfo.SuccessCount++;
                
                // Calculate size
                var sizeGB = entry.FileSize / (1024.0 * 1024.0 * 1024.0);
                processedSizeGB += sizeGB;
                progressInfo.ProcessedSizeGB = processedSizeGB;
                
                // Post-processing
                if (options.DeleteSourceAfter || options.MoveSourceAfter)
                {
                    // Get all volume files
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
                
                // Success message still goes to SuccessLog, but not to CommandLog
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
            
            // Check size limit
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                Log(LogLevel.Information, $"Size limit reached: {processedSizeGB:F3} GB >= {options.MaxSizeGB} GB");
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }
        
        Log(LogLevel.Information, $"Batch decompression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}");
        
        // Shutdown if requested
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
    /// Get all volume files for a multi-volume archive
    /// </summary>
    private List<string> GetAllVolumeFiles(string archivePath, string extension)
    {
        var files = new List<string> { archivePath };
        
        var filename = Path.GetFileName(archivePath);
        var directory = Path.GetDirectoryName(archivePath) ?? string.Empty;
        
        // Check for .partXXX.extension pattern
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
            
            // Find all parts
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
