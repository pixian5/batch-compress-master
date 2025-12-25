using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// RAR archive engine implementation
/// Cross-platform support for RAR/UnRAR
/// </summary>
public class RarArchiveEngine : IArchiveEngine
{
    private string? _rarExecutablePath;
    
    public bool IsAvailable()
    {
        _rarExecutablePath = FindRarExecutable();
        return !string.IsNullOrEmpty(_rarExecutablePath);
    }
    
    private string? FindRarExecutable()
    {
        // Platform-specific RAR executable search
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return FindRarOnWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return FindRarOnLinux();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return FindRarOnMacOS();
        }
        
        return null;
    }
    
    private string? FindRarOnWindows()
    {
        // Try registry first (original method)
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\winrar.exe");
                
                if (key != null)
                {
                    var path = key.GetValue("Path")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        var rarPath = Path.Combine(path, "winrar.exe");
                        if (File.Exists(rarPath))
                        {
                            return rarPath;
                        }
                    }
                }
            }
        }
        catch { }
        
        // Try common installation paths
        string[] commonPaths = {
            @"C:\Program Files\WinRAR\winrar.exe",
            @"C:\Program Files (x86)\WinRAR\winrar.exe"
        };
        
        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        return null;
    }
    
    private string? FindRarOnLinux()
    {
        // Try to find rar/unrar in PATH
        string[] commands = { "rar", "unrar" };
        
        foreach (var cmd in commands)
        {
            var result = ExecuteCommand("which", cmd);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result.Trim();
            }
        }
        
        return null;
    }
    
    private string? FindRarOnMacOS()
    {
        // Similar to Linux, check PATH
        string[] commands = { "rar", "unrar" };
        
        foreach (var cmd in commands)
        {
            var result = ExecuteCommand("which", cmd);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result.Trim();
            }
        }
        
        // Check Homebrew installation path
        string[] homebrewPaths = {
            "/usr/local/bin/rar",
            "/usr/local/bin/unrar",
            "/opt/homebrew/bin/rar",
            "/opt/homebrew/bin/unrar"
        };
        
        foreach (var path in homebrewPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        return null;
    }
    
    private string ExecuteCommand(string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process != null)
            {
                return process.StandardOutput.ReadToEnd();
            }
        }
        catch { }
        
        return string.Empty;
    }
    
    public async Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_rarExecutablePath))
        {
            if (!IsAvailable())
            {
                return new ArchiveResult
                {
                    Success = false,
                    ExitCode = -2,
                    ErrorMessage = "RAR executable not found. Please install WinRAR or RAR."
                };
            }
        }
        
        var arguments = BuildCompressionArguments(input, output, options);
        
        try
        {
            return await Task.Run(() => ExecuteRarCommand(arguments, cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            return new ArchiveResult
            {
                Success = false,
                ExitCode = -3,
                ErrorMessage = $"Exception during compression: {ex.Message}"
            };
        }
    }
    
    public async Task<ArchiveResult> ExtractAsync(string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_rarExecutablePath))
        {
            if (!IsAvailable())
            {
                return new ArchiveResult
                {
                    Success = false,
                    ExitCode = -2,
                    ErrorMessage = "RAR executable not found. Please install WinRAR or RAR."
                };
            }
        }
        
        var arguments = BuildExtractionArguments(archivePath, outputDir, options);
        
        try
        {
            return await Task.Run(() => ExecuteRarCommand(arguments, cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            return new ArchiveResult
            {
                Success = false,
                ExitCode = -3,
                ErrorMessage = $"Exception during extraction: {ex.Message}"
            };
        }
    }
    
    private string BuildCompressionArguments(string input, string output, ArchiveOptions options)
    {
        var args = new System.Text.StringBuilder();
        
        // Add command
        args.Append("a ");
        
        // Basic options
        args.Append("-ep1 ");  // Exclude base folder from paths
        args.Append("-IBCK "); // Run in background
        args.Append("-SCf ");  // Use UTF-8 for filenames
        
        // Existing file mode
        switch (options.ExistingFileMode)
        {
            case ExistingFileMode.Skip:
                args.Append("-o- ");
                break;
            case ExistingFileMode.Update:
                args.Append("-u ");
                break;
            case ExistingFileMode.Overwrite:
                args.Append("-o+ ");
                break;
        }
        
        // Password
        if (!string.IsNullOrEmpty(options.Password))
        {
            args.Append($"-p\"{options.Password}\" ");
        }
        
        // Compression level
        args.Append($"-m{(int)options.CompressionLevel} ");
        
        // Solid archive
        if (options.SolidArchive)
        {
            args.Append("-s -md32 -k ");
        }
        
        // Volume size
        if (!string.IsNullOrEmpty(options.VolumeSize))
        {
            args.Append($"-v{options.VolumeSize} ");
        }
        
        // Recovery record
        if (options.RecoveryRecordPercent > 0)
        {
            args.Append($"-rr{options.RecoveryRecordPercent} ");
        }
        
        // Quick open
        if (options.QuickOpen)
        {
            args.Append("-qo+ ");
        }
        
        // Test archive
        if (options.TestArchive)
        {
            args.Append("-t ");
        }
        
        // Comment file
        if (!string.IsNullOrEmpty(options.CommentFile) && File.Exists(options.CommentFile))
        {
            args.Append($"-z\"{options.CommentFile}\" ");
        }
        
        // Temp directory
        if (!string.IsNullOrEmpty(options.TempDirectory))
        {
            if (!Directory.Exists(options.TempDirectory))
            {
                Directory.CreateDirectory(options.TempDirectory);
            }
            args.Append($"-w\"{options.TempDirectory}\" ");
        }
        
        // Exclude extensions
        if (options.ExcludeExtensions != null && options.ExcludeExtensions.Length > 0)
        {
            args.Append($"-ms{string.Join(";", options.ExcludeExtensions)} ");
        }
        
        // Reference large files
        args.Append("-oi:50000000 ");
        
        // Output and input
        args.Append($"\"{output}\" \"{input}\"");
        
        return args.ToString();
    }
    
    private string BuildExtractionArguments(string archivePath, string outputDir, ArchiveOptions options)
    {
        var args = new System.Text.StringBuilder();
        
        // Extract command with paths
        args.Append("x ");
        
        // Background mode
        args.Append("-IBCK ");
        
        // Existing file mode
        if (options.ExistingFileMode == ExistingFileMode.Overwrite)
        {
            args.Append("-o+ ");
        }
        else
        {
            args.Append("-o- ");
        }
        
        // Password
        if (!string.IsNullOrEmpty(options.Password))
        {
            args.Append($"-p\"{options.Password}\" ");
        }
        
        // Archive and output directory
        args.Append($"\"{archivePath}\" \"{outputDir}\"");
        
        return args.ToString();
    }
    
    private ArchiveResult ExecuteRarCommand(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _rarExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            process.Start();
            process.WaitForExit();
            
            // Check cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                return new ArchiveResult
                {
                    Success = false,
                    ExitCode = 255,
                    ErrorMessage = "Operation cancelled by user"
                };
            }
            
            int exitCode = process.ExitCode;
            bool success = exitCode == 0 || exitCode == 1; // 0 = success, 1 = warning
            
            return new ArchiveResult
            {
                Success = success,
                ExitCode = exitCode,
                ErrorMessage = success ? null : GetRarErrorMessage(exitCode)
            };
        }
        catch (Exception ex)
        {
            return new ArchiveResult
            {
                Success = false,
                ExitCode = -3,
                ErrorMessage = $"Exception: {ex.Message}"
            };
        }
    }
    
    private string GetRarErrorMessage(int exitCode)
    {
        return exitCode switch
        {
            0 => "Success",
            1 => "Warning (non-fatal error)",
            2 => "Fatal error occurred",
            3 => "Data corruption detected",
            4 => "File is locked and cannot be modified",
            5 => "Cannot write to output",
            6 => "Cannot open file",
            7 => "Command line error",
            8 => "Not enough memory",
            9 => "Cannot create file",
            10 => "No files matching the specified mask found",
            11 => "Wrong password",
            255 => "User interrupted the operation",
            _ => "Unknown error"
        };
    }
}
