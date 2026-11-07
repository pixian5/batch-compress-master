using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 安全诊断日志脱敏导出服务。
/// Antigravity, 2026-11-08：批次 16（P3-13）——聚合系统诊断数据与历史日志，
/// 严格脱敏所有命令行密码凭据后打包为 zip，便于故障排查与工单协作。
/// </summary>
public sealed class DiagnosticsExportService
{
    private readonly string _logsDirectory;
    private readonly IArchiveEngine? _rarEngine;
    private readonly IArchiveEngine? _sevenZipEngine;

    public DiagnosticsExportService(
        string? logsDirectory = null,
        IArchiveEngine? rarEngine = null,
        IArchiveEngine? sevenZipEngine = null)
    {
        _logsDirectory = logsDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs");
        _rarEngine = rarEngine;
        _sevenZipEngine = sevenZipEngine;
    }

    /// <summary>
    /// 对日志文本中的密码凭据进行脱敏替换（-p、-hp、--password 后续值替换为 ***）。
    /// </summary>
    public static string RedactSensitiveText(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // 1. 脱敏 --password "..." 与 --password ...
        var result = Regex.Replace(input, @"(--password\s+)""[^""]*""", "$1\"***\"");
        result = Regex.Replace(result, @"(--password\s+)(?!""\*\*\*""|\s)\S+", "$1***");

        // 2. 脱敏 -p"..." 与 -p...（必须位于行首或空格之后，避免误伤 --password 中的 -p）
        result = Regex.Replace(result, @"(^|\s)(-p)""[^""]*""", "$1$2\"***\"");
        result = Regex.Replace(result, @"(^|\s)(-p)(?!""\*\*\*""|\s)\S+", "$1$2***");

        // 3. 脱敏 -hp"..." 与 -hp...（WinRAR 加密文件头参数）
        result = Regex.Replace(result, @"(^|\s)(-hp)""[^""]*""", "$1$2\"***\"");
        result = Regex.Replace(result, @"(^|\s)(-hp)(?!""\*\*\*""|\s)\S+", "$1$2***");

        return result;
    }

    /// <summary>
    /// 导出脱敏诊断包至指定 zip 路径。
    /// </summary>
    public async Task<string> ExportAsync(string targetZipPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetZipPath);

        var destinationDir = Path.GetDirectoryName(Path.GetFullPath(targetZipPath));
        if (!string.IsNullOrEmpty(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        if (File.Exists(targetZipPath))
        {
            File.Delete(targetZipPath);
        }

        using (var zipStream = new FileStream(targetZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            // 1. 写入系统环境与诊断摘要
            var summary = CreateDiagnosticSummary();
            var summaryEntry = archive.CreateEntry("diagnostics_summary.json", System.IO.Compression.CompressionLevel.Optimal);
            using (var summaryStream = summaryEntry.Open())
            using (var writer = new Utf8JsonWriter(summaryStream, new JsonWriterOptions { Indented = true }))
            {
                JsonSerializer.Serialize(writer, summary);
            }

            // 2. 收集并脱敏最近日志（最多 10 份）
            if (Directory.Exists(_logsDirectory))
            {
                var logFiles = Directory.EnumerateFiles(_logsDirectory, "batchcompress_*.log*")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"\.log(\.\d+)?(\.gz)?$", RegexOptions.CultureInvariant))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Take(10)
                    .ToList();

                foreach (var logFile in logFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(logFile);
                    var entryName = fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                        ? fileName[..^3] // 解包还原为纯文本存入 zip
                        : fileName;

                    string rawText;
                    if (logFile.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fileStream = File.OpenRead(logFile);
                        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
                        using var reader = new StreamReader(gzip, Encoding.UTF8);
                        rawText = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        rawText = await File.ReadAllTextAsync(logFile, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    }

                    var redactedText = RedactSensitiveText(rawText);
                    var logEntry = archive.CreateEntry($"logs/{entryName}", System.IO.Compression.CompressionLevel.Optimal);
                    using var entryStream = logEntry.Open();
                    using var textWriter = new StreamWriter(entryStream, Encoding.UTF8);
                    await textWriter.WriteAsync(redactedText).ConfigureAwait(false);
                }
            }
        }

        return targetZipPath;
    }

    private object CreateDiagnosticSummary()
    {
        string appVersion = "unknown";
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var cur = new DirectoryInfo(baseDir);
            while (cur != null)
            {
                var candidate = Path.Combine(cur.FullName, "VERSION");
                if (File.Exists(candidate))
                {
                    appVersion = File.ReadAllText(candidate).Trim();
                    break;
                }
                cur = cur.Parent;
            }
        }
        catch { }

        return new
        {
            ExportedAtUtc = DateTime.UtcNow.ToString("o"),
            AppVersion = appVersion,
            DotNetRuntime = Environment.Version.ToString(),
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RarEngineAvailable = _rarEngine?.IsAvailable() ?? false,
            SevenZipEngineAvailable = _sevenZipEngine?.IsAvailable() ?? false
        };
    }
}
