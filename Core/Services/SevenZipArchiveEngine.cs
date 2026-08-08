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

// GPT-5, 2026-08-06：7z 创建与解压统一调用 7-Zip 官方独立命令行程序 7zz。
// macOS/Linux 优先使用项目随包分发的二进制，系统安装路径仅作为开发与兼容后备。
public sealed class SevenZipArchiveEngine : IArchiveEngine
{
    private readonly string? _configuredExecutablePath;
    private string? _executablePath;

    public SevenZipArchiveEngine(string? executablePath = null)
    {
        _configuredExecutablePath = executablePath;
    }

    public bool IsAvailable()
    {
        _executablePath = FindExecutable();
        return !string.IsNullOrEmpty(_executablePath);
    }

    public async Task<ArchiveResult> CompressAsync(
        string input,
        string output,
        ArchiveOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureExecutableAvailable())
        {
            return MissingExecutableResult();
        }

        try
        {
            var arguments = SevenZipCommandBuilder.BuildCompressionArguments(input, output, options);
            var result = await ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
            if (!result.Success || !options.TestArchive)
            {
                return result;
            }

            // GPT-5, 2026-08-06：7zz 没有 WinRAR 的“创建后自动测试”开关，因此创建成功后显式执行 t。
            var testArguments = SevenZipCommandBuilder.BuildTestArguments(output, options.Password);
            var testResult = await ExecuteAsync(testArguments, cancellationToken).ConfigureAwait(false);
            // GPT-5, 2026-08-06：创建和校验是两次进程，返回时合并两者原始输出，避免丢失第一阶段诊断信息。
            testResult.CommandLine = string.Join(
                Environment.NewLine,
                new[] { result.CommandLine, testResult.CommandLine }
                    .Where(line => !string.IsNullOrWhiteSpace(line)));
            testResult.StandardOutput = JoinOutput(result.StandardOutput, testResult.StandardOutput);
            testResult.StandardError = JoinOutput(result.StandardError, testResult.StandardError);
            return testResult;
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
                ErrorMessage = $"7z 压缩异常: {ex.Message}"
            };
        }
    }

    public async Task<ArchiveResult> ExtractAsync(
        string archivePath,
        string outputDir,
        ArchiveOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureExecutableAvailable())
        {
            return MissingExecutableResult();
        }

        try
        {
            var arguments = SevenZipCommandBuilder.BuildExtractionArguments(archivePath, outputDir, options);
            return await ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
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
                ErrorMessage = $"7z 解压异常: {ex.Message}"
            };
        }
    }

    private bool EnsureExecutableAvailable()
    {
        return !string.IsNullOrEmpty(_executablePath) || IsAvailable();
    }

    private string? FindExecutable()
    {
        foreach (var candidate in GetCandidatePaths())
        {
            if (TryValidateExecutable(candidate, out var message))
            {
                Debug.WriteLine($"[7-Zip] 已选路径: {candidate}");
                return candidate;
            }

            Debug.WriteLine($"[7-Zip] 拒绝: {candidate}（{message}）");
        }

        return null;
    }

    private IEnumerable<string> GetCandidatePaths()
    {
        if (!string.IsNullOrWhiteSpace(_configuredExecutablePath))
        {
            yield return Path.GetFullPath(_configuredExecutablePath);
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "macos", "7zz");
            yield return Path.Combine(Directory.GetCurrentDirectory(), "tools", "7zip", "macos", "7zz");
            yield return "/opt/homebrew/bin/7zz";
            yield return "/usr/local/bin/7zz";
        }
        else if (OperatingSystem.IsLinux())
        {
            var platformDirectory = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
            yield return Path.Combine(AppContext.BaseDirectory, "tools", "7zip", platformDirectory, "7zz");
            yield return Path.Combine(Directory.GetCurrentDirectory(), "tools", "7zip", platformDirectory, "7zz");
            yield return "/usr/local/bin/7zz";
            yield return "/usr/bin/7zz";
        }
        else if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "windows-x64", "7z.exe");
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, "7-Zip", "7z.exe");
            }
        }
    }

    private static bool TryValidateExecutable(string path, out string message)
    {
        message = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                message = "文件不存在";
                return false;
            }

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(path);
                if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                {
                    File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var result = new ArchiveProcessRunner(path, "7-Zip")
                .RunAsync(["i"], timeout.Token)
                .GetAwaiter()
                .GetResult();
            var output = result.StandardOutput + result.StandardError;
            if (!SevenZipExitCodes.IsSuccess(result.ExitCode) ||
                !output.Contains("7-Zip", StringComparison.OrdinalIgnoreCase))
            {
                message = $"身份校验失败，返回码 {result.ExitCode}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private async Task<ArchiveResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await new ArchiveProcessRunner(_executablePath!, "7-Zip")
            .RunAsync(arguments, cancellationToken)
            .ConfigureAwait(false);
        var success = SevenZipExitCodes.IsSuccess(result.ExitCode);

        // GPT-5, 2026-08-06：stdout/stderr 是用户要求保留的原始进程输出，其中的密码明确不脱敏、不替换为 ***。
        return new ArchiveResult
        {
            Success = success,
            ExitCode = result.ExitCode,
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError,
            CommandLine = result.CommandLine,
            ErrorMessage = success ? null : BuildFailureMessage(result)
        };
    }

    private static string BuildFailureMessage(ArchiveProcessResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        var summary = result.ExitCode switch
        {
            2 => "7-Zip 严重错误",
            7 => "7-Zip 命令行参数错误",
            8 => "7-Zip 内存不足",
            255 => "7-Zip 操作被用户中止",
            _ => "7-Zip 未知错误"
        };
        return string.IsNullOrWhiteSpace(detail) ? summary : $"{summary}: {detail}";
    }

    private static ArchiveResult MissingExecutableResult() => new()
    {
        Success = false,
        ExitCode = -2,
        ErrorMessage = "未找到 7-Zip 官方命令行程序 7zz。"
    };

    private static string JoinOutput(string first, string second)
    {
        if (string.IsNullOrEmpty(first))
        {
            return second;
        }

        if (string.IsNullOrEmpty(second))
        {
            return first;
        }

        return first.TrimEnd() + Environment.NewLine + second;
    }
}
