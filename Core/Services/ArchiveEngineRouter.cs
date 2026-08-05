using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-06：路由层只决定归档后端，不改变任一后端的参数或输出。
// 创建 7z 使用官方 7zz；RAR/ZIP 继续使用 WinRAR/RAR，解压则根据实际文件名识别 7z 与分卷首卷。
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
        return IsSevenZipFormat(options.ArchiveFormat)
            ? _sevenZipEngine.CompressAsync(input, output, options, cancellationToken)
            : _rarEngine.CompressAsync(input, output, options, cancellationToken);
    }

    public Task<ArchiveResult> ExtractAsync(
        string archivePath,
        string outputDir,
        ArchiveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsSevenZipArchive(archivePath) || IsSevenZipFormat(options.ArchiveFormat)
            ? _sevenZipEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken)
            : _rarEngine.ExtractAsync(archivePath, outputDir, options, cancellationToken);
    }

    private static bool IsSevenZipFormat(string? format)
    {
        return string.Equals(format?.Trim().TrimStart('.'), "7z", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSevenZipArchive(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        return fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(fileName, @"\.7z\.\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
