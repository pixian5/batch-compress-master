using System;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-06：路由层只决定归档后端，不改变任一后端的参数或输出。
// GPT-5, 2026-08-06：RAR 使用官方 RAR；ZIP 与 7z 统一使用官方 7zz。
public sealed class ArchiveEngineRouter : IArchiveEngine
{
    private readonly IArchiveEngine _rarEngine;
    private readonly IArchiveEngine _sevenZipEngine;

    public ArchiveEngineRouter()
        : this(new RarArchiveEngine(), new SevenZipArchiveEngine())
    {
    }

    public ArchiveEngineRouter(IArchiveEngine rarEngine, IArchiveEngine sevenZipEngine)
    {
        _rarEngine = rarEngine ?? throw new ArgumentNullException(nameof(rarEngine));
        _sevenZipEngine = sevenZipEngine ?? throw new ArgumentNullException(nameof(sevenZipEngine));
    }

    public bool IsAvailable() => _rarEngine.IsAvailable() || _sevenZipEngine.IsAvailable();

    public Task<ArchiveResult> CompressAsync(
        string input,
        string output,
        ArchiveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return NormalizeFormat(options.ArchiveFormat) switch
        {
            "rar" => _rarEngine.CompressAsync(input, output, options, cancellationToken),
            "7z" or "zip" => _sevenZipEngine.CompressAsync(input, output, options, cancellationToken),
            var format => throw new NotSupportedException($"不支持创建 {format} 格式归档。仅支持 rar、7z 和 zip。")
        };
    }

    public Task<ArchiveResult> ExtractAsync(
        string archivePath,
        string outputDir,
        ArchiveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var detectedKind = ArchiveVolumeResolver.DetectArchiveKind(archivePath);
        if (detectedKind == ArchiveKind.Rar)
        {
            return _rarEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken);
        }

        if (detectedKind != ArchiveKind.Unknown)
        {
            return _sevenZipEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken);
        }

        return NormalizeFormat(options.ArchiveFormat) == "rar"
            ? _rarEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken)
            : _sevenZipEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken);
    }

    private static string NormalizeFormat(string? format) =>
        format?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;
}
