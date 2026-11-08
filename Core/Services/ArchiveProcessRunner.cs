using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Services;

public sealed record ArchiveProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string CommandLine);

// GPT-5, 2026-08-06：所有归档程序共用这一进程边界。参数逐项写入 ArgumentList，
// stdout/stderr 同时异步读取以防止管道阻塞，取消时终止整个子进程树。
public sealed class ArchiveProcessRunner
{
    private readonly string _executablePath;
    private readonly string _toolName;

    public ArchiveProcessRunner(string executablePath, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        _executablePath = executablePath;
        _toolName = toolName;
    }

    public async Task<ArchiveProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var commandLine = BuildCommandLine(_executablePath, arguments);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"无法启动 {_toolName}: {_executablePath}");
        }

        var standardOutputTask = ReadStreamWithCallbackAsync(process.StandardOutput, onOutputLine);
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
        return new ArchiveProcessResult(
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false),
            commandLine);
    }

    private static async Task<string> ReadStreamWithCallbackAsync(
        StreamReader reader,
        Action<string>? onOutputLine)
    {
        if (onOutputLine == null)
        {
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        var fullOutput = new StringBuilder();
        var lineBuffer = new StringBuilder();
        var buffer = new char[1024];
        int read;

        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            fullOutput.Append(buffer, 0, read);
            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];
                if (ch is '\r' or '\n')
                {
                    if (lineBuffer.Length > 0)
                    {
                        var line = lineBuffer.ToString();
                        lineBuffer.Clear();
                        try
                        {
                            onOutputLine(line);
                        }
                        catch
                        {
                            // 进度流回调异常不阻断进程输出流的完整读取
                        }
                    }
                }
                else
                {
                    lineBuffer.Append(ch);
                }
            }
        }

        if (lineBuffer.Length > 0)
        {
            var line = lineBuffer.ToString();
            try
            {
                onOutputLine(line);
            }
            catch
            {
                // 忽略回调异常
            }
        }

        return fullOutput.ToString();
    }

    // 保留可直接复制核对的完整参数；调用方明确要求日志不脱敏，因此密码也原样保留。
    private static string BuildCommandLine(string executablePath, IReadOnlyList<string> arguments)
    {
        return string.Join(" ", new[] { executablePath }
            .Concat(arguments)
            .Select(QuoteCommandArgument));
    }

    private static string QuoteCommandArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (value.All(character => !char.IsWhiteSpace(character) && character != '\"'))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static async Task WaitForExitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // GPT-5, 2026-08-06：进程可能在取消检查和终止之间自行退出，此时无需再次处理。
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // GPT-5, 2026-08-06：进程已在状态检查后退出，视为取消清理完成。
        }
    }
}
