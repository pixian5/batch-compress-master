using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Services;

public sealed record WinRarProcessResult(int ExitCode, string StandardOutput, string StandardError);

// GPT-5, 2026-08-06：保留 WinRAR 专用类型作为稳定调用接口，实际进程生命周期由通用执行器统一管理。
public sealed class WinRarProcessRunner
{
    private readonly string _executablePath;

    public WinRarProcessRunner(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _executablePath = executablePath;
    }

    public async Task<WinRarProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await new ArchiveProcessRunner(_executablePath, "WinRAR/RAR")
            .RunAsync(arguments, cancellationToken)
            .ConfigureAwait(false);

        return new WinRarProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
