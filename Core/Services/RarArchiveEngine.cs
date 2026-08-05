using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// RAR 压缩引擎实现
/// 跨平台支持 RAR/UnRAR
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
        var candidates = GetCandidatePaths().ToList();

        Debug.WriteLine($"[RAR] 操作系统={RuntimeInformation.OSDescription}");
        Debug.WriteLine($"[RAR] 程序目录={AppContext.BaseDirectory}");
        Debug.WriteLine($"[RAR] 候选路径({candidates.Count}): {string.Join(" | ", candidates)}");

        foreach (var candidate in candidates)
        {
            var normalized = NormalizeCandidate(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (TryValidateRarExecutable(normalized, out var validationMessage))
            {
                Debug.WriteLine($"[RAR] 已选路径: {normalized}");
                return normalized;
            }

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                Debug.WriteLine($"[RAR] 拒绝: {normalized}（{validationMessage}）");
            }
        }

        Debug.WriteLine("[RAR] 未找到可用的 RAR 程序");
        return null;
    }
    

    private IEnumerable<string> GetCandidatePaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var p in GetWindowsCandidates())
            {
                yield return p;
            }
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            foreach (var p in GetMacCandidates())
            {
                yield return p;
            }
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            foreach (var p in GetLinuxCandidates())
            {
                yield return p;
            }
            yield break;
        }
    }

    private IEnumerable<string> GetWindowsCandidates()
    {
        // 1) 程序运行目录下的 tools/WinRAR 目录（优先使用）
        var toolsWinrarDir = Path.Combine(AppContext.BaseDirectory, "tools", "WinRAR");
        yield return Path.Combine(toolsWinrarDir, "Rar.exe");
        yield return Path.Combine(toolsWinrarDir, "WinRAR.exe");

        // 2) 注册表 App Paths（HKLM/HKCU）
        foreach (var p in EnumerateAppPathsFromRegistry("rar.exe"))
        {
            yield return p;
        }
        foreach (var p in EnumerateAppPathsFromRegistry("winrar.exe"))
        {
            yield return p;
        }

        // 3) 默认安装目录
        yield return @"C:\Program Files\WinRAR\rar.exe";
        yield return @"C:\Program Files\WinRAR\winrar.exe";
        yield return @"C:\Program Files (x86)\WinRAR\rar.exe";
        yield return @"C:\Program Files (x86)\WinRAR\winrar.exe";
        
        // 4) 程序运行目录下的 winrar 目录（向后兼容，最后使用）
        var winrarDir = Path.Combine(AppContext.BaseDirectory, "winrar");
        yield return Path.Combine(winrarDir, "rar.exe");
        yield return Path.Combine(winrarDir, "winrar.exe");
    }

    private IEnumerable<string> EnumerateAppPathsFromRegistry(string exeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        string subKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}";
        var hives = new[]
        {
            Microsoft.Win32.RegistryHive.LocalMachine,
            Microsoft.Win32.RegistryHive.CurrentUser
        };

        foreach (var hive in hives)
        {
            string? defaultValue = null;
            string? pathValue = null;

            try
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey);
                if (key == null)
                {
                    continue;
                }

                // 默认值可能是完整路径
                defaultValue = key.GetValue("")?.ToString();

                // Path 值可能是目录
                pathValue = key.GetValue("Path")?.ToString();
            }
            catch
            {
                // ignore
            }

            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                yield return defaultValue;
            }

            if (!string.IsNullOrWhiteSpace(pathValue))
            {
                yield return Path.Combine(pathValue, exeName);
            }
        }
    }

    private IEnumerable<string> GetMacCandidates()
    {
        // 1) 程序运行目录下的 tools/rarmacOS 目录（优先使用）
        var toolsRarDir = Path.Combine(AppContext.BaseDirectory, "tools", "rarmacOS", "rar");
        yield return toolsRarDir;

        // 2) 固定路径（避免 Finder 启动 PATH 不完整）
        yield return "/opt/homebrew/bin/rar";
        yield return "/usr/local/bin/rar";
        yield return "/usr/bin/rar";

        // 3) PATH/which
        var which = ExecuteCommandCaptureAll("which", ["rar"], timeoutMs: 2000);
        if (!string.IsNullOrWhiteSpace(which.Output))
        {
            var firstLine = which.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstLine))
            {
                yield return firstLine.Trim();
            }
        }

        // 4) 用户目录兜底
        yield return ExpandHome("~/rar/rar");
        yield return ExpandHome("~/.local/bin/rar");
        
        // 5) 程序运行目录（向后兼容，最后使用）
        yield return Path.Combine(AppContext.BaseDirectory, "rar");
    }

    private IEnumerable<string> GetLinuxCandidates()
    {
        // 1) 程序运行目录下的 tools/rarLinux 目录（优先使用）
        var toolsRarDir = Path.Combine(AppContext.BaseDirectory, "tools", "rarLinux", "rar");
        yield return toolsRarDir;

        // 2) 固定路径
        yield return "/usr/bin/rar";
        yield return "/usr/local/bin/rar";
        yield return "/bin/rar";

        // 3) PATH/which
        var which = ExecuteCommandCaptureAll("which", ["rar"], timeoutMs: 2000);
        if (!string.IsNullOrWhiteSpace(which.Output))
        {
            var firstLine = which.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstLine))
            {
                yield return firstLine.Trim();
            }
        }

        // 4) 用户目录兜底
        yield return ExpandHome("~/rar/rar");
        yield return ExpandHome("~/.local/bin/rar");
        
        // 5) 程序运行目录：rarlinux（向后兼容，最后使用）
        yield return Path.Combine(AppContext.BaseDirectory, "rarlinux");
    }

    private static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (!path.StartsWith("~/", StringComparison.Ordinal))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return path;
        }

        return Path.Combine(home, path.Substring(2));
    }

    private static string? NormalizeCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var trimmed = candidate.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }
        return trimmed;
    }

    private bool TryValidateRarExecutable(string path, out string message)
    {
        message = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                message = "文件不存在";
                return false;
            }

            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                message = "不是文件";
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                var ext = Path.GetExtension(path);
                if (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    message = "非 exe 文件";
                    return false;
                }
            }
            else
            {
                // macOS/Linux：检查执行位；若是程序目录内的文件，允许尝试自动修复权限
                if (!IsUnixExecutable(path))
                {
                    if (IsUnderBaseDirectory(path) && TryFixUnixExecutablePermission(path))
                    {
                        // retry
                    }
                    else
                    {
                        message = "不可执行（建议 chmod +x）";
                        return false;
                    }
                }
            }

            // 运行校验：rar -?，2 秒
            var result = ExecuteCommandCaptureAll(path, ["-?"], timeoutMs: 2000);
            if (!result.Started)
            {
                message = "无法启动进程";
                return false;
            }

            if (result.TimedOut)
            {
                message = "运行校验超时";
                return false;
            }

            if (result.ExitCode != 0 && result.ExitCode != 1)
            {
                message = $"运行校验返回码异常：{result.ExitCode}";
                return false;
            }

            var output = (result.Output + "\n" + result.Error).Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                message = "运行校验无输出";
                return false;
            }

            var lower = output.ToLowerInvariant();
            bool ok = lower.Contains("rar") || lower.Contains("winrar") || lower.Contains("copyright") || lower.Contains("usage");
            if (!ok)
            {
                message = "运行校验输出不匹配";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            message = $"校验异常：{ex.Message}";
            return false;
        }
    }

    private static bool IsUnderBaseDirectory(string path)
    {
        try
        {
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            var full = Path.GetFullPath(path);
            return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnixExecutable(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFixUnixExecutablePermission(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return false;
            }

            var mode = File.GetUnixFileMode(path);
            var newMode = mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if (newMode == mode)
            {
                return true;
            }

            File.SetUnixFileMode(path, newMode);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CommandResult
    {
        public bool Started { get; init; }
        public bool TimedOut { get; init; }
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }

    private static CommandResult ExecuteCommandCaptureAll(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs)
    {
        try
        {
            using var timeout = new CancellationTokenSource(timeoutMs);
            var result = new WinRarProcessRunner(fileName)
                .RunAsync(arguments, timeout.Token)
                .GetAwaiter()
                .GetResult();

            return new CommandResult
            {
                Started = true,
                ExitCode = result.ExitCode,
                Output = result.StandardOutput,
                Error = result.StandardError
            };
        }
        catch (OperationCanceledException)
        {
            return new CommandResult { Started = true, TimedOut = true };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Started = false,
                ExitCode = -1,
                Error = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Ensure RAR executable is available
    /// </summary>
    private bool EnsureRarExecutableAvailable()
    {
        if (string.IsNullOrEmpty(_rarExecutablePath))
        {
            return IsAvailable();
        }
        return true;
    }
    
    public async Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        if (!EnsureRarExecutableAvailable())
        {
            return new ArchiveResult
            {
                Success = false,
                ExitCode = -2,
                ErrorMessage = "RAR executable not found. Please install WinRAR or RAR."
            };
        }

        try
        {
            var arguments = WinRarCommandBuilder.BuildCompressionArguments(input, output, options);
            return await ExecuteRarCommand(arguments, options.Password, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        if (!EnsureRarExecutableAvailable())
        {
            return new ArchiveResult
            {
                Success = false,
                ExitCode = -2,
                ErrorMessage = "RAR executable not found. Please install WinRAR or RAR."
            };
        }

        try
        {
            var arguments = WinRarCommandBuilder.BuildExtractionArguments(archivePath, outputDir, options);
            return await ExecuteRarCommand(arguments, options.Password, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
    
    private async Task<ArchiveResult> ExecuteRarCommand(
        IReadOnlyList<string> arguments,
        string? password,
        CancellationToken cancellationToken)
    {
        var result = await new WinRarProcessRunner(_rarExecutablePath!)
            .RunAsync(arguments, cancellationToken)
            .ConfigureAwait(false);

        var success = WinRarExitCodes.IsSuccess(result.ExitCode);
        var standardOutput = SanitizeOutput(result.StandardOutput, password);
        var standardError = SanitizeOutput(result.StandardError, password);
        return new ArchiveResult
        {
            Success = success,
            ExitCode = result.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            ErrorMessage = success ? null : BuildFailureMessage(result.ExitCode, standardOutput, standardError)
        };
    }

    private string BuildFailureMessage(int exitCode, string standardOutput, string standardError)
    {
        var processMessage = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return string.IsNullOrWhiteSpace(processMessage)
            ? GetRarErrorMessage(exitCode)
            : $"{GetRarErrorMessage(exitCode)}: {processMessage.Trim()}";
    }

    private static string SanitizeOutput(string output, string? password)
    {
        return string.IsNullOrEmpty(password)
            ? output
            : output.Replace(password, "***", StringComparison.Ordinal);
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
