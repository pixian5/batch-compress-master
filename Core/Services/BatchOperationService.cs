using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// Service for batch compression and decompression operations
/// </summary>
public class BatchOperationService
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly ISystemIntegration _systemIntegration;
    
    public BatchOperationService(IArchiveEngine archiveEngine, ISystemIntegration systemIntegration)
    {
        _archiveEngine = archiveEngine;
        _systemIntegration = systemIntegration;
    }
    
    /// <summary>
    /// Load file list from folder
    /// </summary>
    public List<string> LoadFilesFromFolder(string folderPath, string extension, bool skipProcessed)
    {
        var items = new List<string>();
        
        if (!Directory.Exists(folderPath))
        {
            return items;
        }
        
        try
        {
            // Get all files and directories in the root of the folder
            var allItems = Directory.GetFileSystemEntries(folderPath);
            
            foreach (var itemPath in allItems)
            {
                var name = Path.GetFileName(itemPath);
                
                // Skip system files
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Skip already processed files if option is enabled
                if (skipProcessed)
                {
                    if (name.Contains("【已压缩】") || name.Contains("【已解压】"))
                    {
                        continue;
                    }
                }
                
                items.Add(itemPath);
            }
        }
        catch (Exception ex)
        {
            // Log error if needed
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
                
                if (File.Exists(fullPath))
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
        catch { }
        
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
        var partMatch = Regex.Match(filename, @"\.part(\d+)\." + extension + "$", RegexOptions.IgnoreCase);
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
        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };
        
        double processedSizeGB = 0;
        
        foreach (var sourcePath in sourcePaths)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            // Check if file/directory exists
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                progressInfo.NonExistCount++;
                progressInfo.Message = $"Not found: {sourcePath}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
                continue;
            }
            
            var name = Path.GetFileName(sourcePath);
            progressInfo.CurrentFile = name;
            
            // Skip if already processed
            if (options.SkipAlreadyProcessed && name.Contains("【已压缩】"))
            {
                progressInfo.IgnoreCount++;
                continue;
            }
            
            // Build output filename
            var outputFileName = name + "." + options.Extension;
            var outputPath = Path.Combine(options.OutputPath, outputFileName);
            
            // Check if output exists
            if (File.Exists(outputPath))
            {
                if (options.ExistingFileMode == ExistingFileMode.Skip)
                {
                    progressInfo.IgnoreCount++;
                    continue;
                }
                else if (options.ExistingFileMode == ExistingFileMode.Overwrite)
                {
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
            
            // Build archive options
            var archiveOptions = new ArchiveOptions
            {
                Password = password,
                CompressionLevel = options.CompressionLevel,
                SolidArchive = options.SolidArchive,
                QuickOpen = options.QuickOpen,
                TestArchive = options.TestArchive,
                CommentFile = options.CommentFile,
                TempDirectory = options.TempDirectory ?? options.OutputPath,
                ExistingFileMode = options.ExistingFileMode,
                RecoveryRecordPercent = options.RecoveryRecordPercent,
                VolumeSize = !string.IsNullOrEmpty(options.VolumeSize) ? 
                    options.VolumeSize + options.VolumeSizeUnit : null
            };
            
            // Create enclosure directories if needed
            if (options.AddEnclosures && Directory.Exists(sourcePath) && 
                options.EnclosureDirectories != null)
            {
                foreach (var enclosurePath in options.EnclosureDirectories)
                {
                    var enclosureName = Path.GetFileName(enclosurePath);
                    var targetPath = Path.Combine(sourcePath, enclosureName);
                    if (!Directory.Exists(targetPath))
                    {
                        Directory.CreateDirectory(targetPath);
                    }
                }
            }
            
            // Compress
            var result = await _archiveEngine.CompressAsync(sourcePath, outputPath, archiveOptions, cancellationToken);
            
            // Log the command that was executed
            progressInfo.Message = $"Command: {_archiveEngine.CurrentCommand}";
            progressInfo.IsError = false;
            progress.Report(progressInfo);
            
            if (result.Success)
            {
                progressInfo.SuccessCount++;
                
                // Calculate size
                if (File.Exists(outputPath))
                {
                    var sizeGB = new FileInfo(outputPath).Length / (1024.0 * 1024.0 * 1024.0);
                    processedSizeGB += sizeGB;
                    progressInfo.ProcessedSizeGB = processedSizeGB;
                }
                
                // Post-processing
                if (options.DeleteSourceAfter)
                {
                    try
                    {
                        if (Directory.Exists(sourcePath))
                        {
                            Directory.Delete(sourcePath, true);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            File.Delete(sourcePath);
                        }
                    }
                    catch { }
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
                        if (Directory.Exists(sourcePath))
                        {
                            Directory.Move(sourcePath, targetPath);
                        }
                        else if (File.Exists(sourcePath))
                        {
                            File.Move(sourcePath, targetPath);
                        }
                    }
                    catch { }
                }
                
                progressInfo.Message = $"Success: {name}";
                progressInfo.IsError = false;
            }
            else
            {
                progressInfo.FailCount++;
                progressInfo.Message = $"Failed: {name} - {result.ErrorMessage}";
                progressInfo.IsError = true;
            }
            
            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
            progress.Report(progressInfo);
            
            // Check size limit
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }
        
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
        var progressInfo = new OperationProgressInfo
        {
            StartTime = DateTime.Now
        };
        
        double processedSizeGB = 0;
        
        foreach (var entry in archives)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            var archivePath = entry.FilePath;
            
            if (!File.Exists(archivePath))
            {
                progressInfo.NonExistCount++;
                progressInfo.Message = $"Not found: {archivePath}";
                progressInfo.IsError = true;
                progress.Report(progressInfo);
                continue;
            }
            
            var archiveName = Path.GetFileName(archivePath);
            progressInfo.CurrentFile = archiveName;
            
            // Skip if already processed
            if (options.SkipAlreadyProcessed && archiveName.Contains("【已解压】"))
            {
                progressInfo.IgnoreCount++;
                continue;
            }
            
            // Check if it's a multi-volume archive but not the first volume
            if (IsMultiVolumeArchive(archivePath, options.Extension, out var firstVolume))
            {
                if (!archivePath.Equals(firstVolume, StringComparison.OrdinalIgnoreCase))
                {
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
                Password = password,
                ExistingFileMode = options.ExistingFileMode
            };
            
            // Extract
            var result = await _archiveEngine.ExtractAsync(archivePath, options.OutputPath, archiveOptions, cancellationToken);
            
            // Log the command that was executed
            progressInfo.Message = $"Command: {_archiveEngine.CurrentCommand}";
            progressInfo.IsError = false;
            progress.Report(progressInfo);
            
            if (result.Success)
            {
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
                                File.Move(volumeFile, targetPath);
                            }
                        }
                        catch { }
                    }
                }
                
                progressInfo.Message = $"Success: {archiveName}";
                progressInfo.IsError = false;
            }
            else
            {
                progressInfo.FailCount++;
                progressInfo.Message = $"Failed: {archiveName} - {result.ErrorMessage}";
                progressInfo.IsError = true;
            }
            
            progressInfo.Elapsed = DateTime.Now - progressInfo.StartTime;
            progress.Report(progressInfo);
            
            // Check size limit
            if (options.MaxSizeGB > 0 && processedSizeGB >= options.MaxSizeGB)
            {
                progressInfo.Message = "Size limit reached";
                progress.Report(progressInfo);
                break;
            }
        }
        
        // Shutdown if requested
        if (options.ShutdownAfterComplete)
        {
            await _systemIntegration.ShutdownAsync();
        }
    }
    
    /// <summary>
    /// Get all volume files for a multi-volume archive
    /// </summary>
    private List<string> GetAllVolumeFiles(string archivePath, string extension)
    {
        var files = new List<string> { archivePath };
        
        var filename = Path.GetFileName(archivePath);
        var directory = Path.GetDirectoryName(archivePath) ?? string.Empty;
        
        // Check for .partXXX.extension pattern
        var partMatch = Regex.Match(filename, @"\.part(\d+)\." + extension + "$", RegexOptions.IgnoreCase);
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
        
        return files;
    }
}
