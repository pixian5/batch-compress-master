using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 将无界面批处理操作写入文件的日志服务。
/// </summary>
// GPT-5, 2026-08-05：用于无界面执行的线程安全文件日志器。一个锁同时保护写入器生命周期和多线程进度消息，确保每条日志完整。
// GLM-5.3, 2026-11-04：批次 4——日志可能包含命令行原文（含密码）与环境诊断：
// 1) Unix 下新建日志文件即收紧为 0600（仅当前用户读写），防止同机其他用户读取；
// 2) 单文件超过 rotationBytes（默认 10MB）触发多代轮转；
// GLM-5.3, 2026-11-04：批次 7——轮转升级为标准多代滚动：.1 → .2 → … → .N 依次后移，
// 超过 maxRotatedGenerations（默认 5 代）自动淘汰最旧一代，不再覆写 .1；
// GLM-5.3, 2026-11-04：批次 8——旧代 Gzip 压缩归档：当前日志与第 1 代 .log.1 保持纯文本
// （cat/tail/less 直接查看最新历史），第 2 代起保存为 .log.<代次>.gz 压缩格式
// （System.IO.Compression.GZipStream，BCL 内置零外部依赖，文本日志通常省 80% 以上空间）；
// 3) 每次会话启动时清理 7 天前且匹配 batchcompress_*.log 模式的过期日志与全部轮转代副本
//    （含 .gz 归档代）。
public class FileLoggerService : ILogger, IDisposable
{
    private const long DefaultRotationBytes = 10 * 1024 * 1024;

    // GLM-5.3, 2026-11-04：批次 7——默认保留的轮转代数（.1 至 .5），公开常量供测试与调用方对齐。
    public const int DefaultRotatedGenerations = 5;

    // 每 64 次写入检查一次文件大小，避免高频日志路径上每次写都触碰文件系统元数据。
    private const int RotationCheckIntervalWrites = 64;

    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(7);

    private readonly string _logFilePath;
    private readonly long _rotationBytes;

    // GLM-5.3, 2026-11-04：批次 7——轮转代数上限（>=1，非法值回退默认）。
    private readonly int _maxRotatedGenerations;

    private readonly object _lock = new();
    private StreamWriter? _writer;
    private bool _disposed;
    private int _writesSinceSizeCheck;

    public FileLoggerService(
        string? logFilePath = null,
        long rotationBytes = DefaultRotationBytes,
        int maxRotatedGenerations = DefaultRotatedGenerations)
    {
        _logFilePath = logFilePath ?? GetDefaultLogPath();
        _rotationBytes = rotationBytes > 0 ? rotationBytes : DefaultRotationBytes;
        _maxRotatedGenerations = maxRotatedGenerations > 0 ? maxRotatedGenerations : DefaultRotatedGenerations;
        EnsureLogDirectoryExists();
        PruneExpiredLogs();
        InitializeWriter();
    }

    private static string GetDefaultLogPath()
    {
        var appDir = AppContext.BaseDirectory;
        var logsDir = Path.Combine(appDir, "logs");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(logsDir, $"batchcompress_{timestamp}.log");
    }

    private void EnsureLogDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to create log directory '{directory}': {ex.Message}");
                Console.WriteLine("Logs will not be saved to file.");
            }
        }
    }

    private void InitializeWriter()
    {
        try
        {
            _writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8)
            {
                AutoFlush = true
            };
            RestrictLogFilePermissions(_logFilePath);
            Log(LogLevel.Information, 0, "========== Log session started ==========", null, (s, _) => s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create log file '{_logFilePath}': {ex.Message}");
            Console.WriteLine("Logs will not be saved to file.");
            _writer = null;
        }
    }

    // GLM-5.3, 2026-11-04：批次 4——Unix 下把日志权限收紧为 0600（仅当前用户读写）。
    // 日志按用户要求原样保留命令行（含密码参数），必须防止同机其他用户读取；
    // Windows 无对应 POSIX 模型，跳过（NTFS 继承用户配置文件目录的 ACL）。
    private static void RestrictLogFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to restrict log file permissions '{path}': {ex.Message}");
        }
    }

    // GLM-5.3, 2026-11-04：批次 7/8——多代轮转：当前文件超过阈值时先整体后移轮转代，
    // 随后当前文件改名为 .1（纯文本）并新建同名日志继续写入；失败不影响后续写入（尽力而为）。
    // 代次存储规则：.1 纯文本（直接 cat/tail 查看最新历史）；.2 起为 .log.<代次>.gz 压缩归档。
    // File.Move 保留原文件权限，纯文本轮转副本继承此前收紧的 0600；
    // 压缩副本由 CompressFileToGzip 显式收紧 0600；新当前文件由 InitializeWriter 重新收紧。
    private void RotateIfOversized()
    {
        try
        {
            _writer?.Flush();
            if (!File.Exists(_logFilePath) || new FileInfo(_logFilePath).Length <= _rotationBytes)
            {
                return;
            }

            _writer?.Dispose();
            _writer = null;
            ShiftRotatedGenerations();
            File.Move(_logFilePath, _logFilePath + ".1");
            InitializeWriter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to rotate log file '{_logFilePath}': {ex.Message}");
            if (_writer == null)
            {
                InitializeWriter();
            }
        }
    }

    // GLM-5.3, 2026-11-04：批次 8——轮转代向上滚动（高代到低代依次处理，目标名先被释放）：
    // 1) 最旧一代淘汰：.log.N.gz 与旧版纯文本 .log.N（若存在）一并删除；
    // 2) .log.g.gz（g>=2）直接重命名为 .log.{g+1}.gz（内容不变，不重压缩）；
    // 3) 纯文本 .log.g（第 1 代常态，或 0.7.1 版残留的旧代）压缩写入 .log.{g+1}.gz 后删除原文，
    //    使任何历史布局在下一轮转后收敛到 “.1 纯文本 + .2+.gz” 的标准形态。
    // Antigravity, 2026-11-04：批次 9——容灾兜底：压缩采用临时文件写入加原子替换机制；
    // 若压缩过程因磁盘满或权限受阻抛出异常，自动降级为保留纯文本移动至下一代，杜绝日志丢失。
    private void ShiftRotatedGenerations()
    {
        File.Delete($"{_logFilePath}.{_maxRotatedGenerations}");
        File.Delete($"{_logFilePath}.{_maxRotatedGenerations}.gz");

        for (var generation = _maxRotatedGenerations - 1; generation >= 1; generation--)
        {
            var gzPath = $"{_logFilePath}.{generation}.gz";
            if (File.Exists(gzPath))
            {
                File.Move(gzPath, $"{_logFilePath}.{generation + 1}.gz", overwrite: true);
                continue;
            }

            var plainPath = $"{_logFilePath}.{generation}";
            if (File.Exists(plainPath))
            {
                var targetGz = $"{_logFilePath}.{generation + 1}.gz";
                if (TryCompressFileToGzip(plainPath, targetGz))
                {
                    File.Delete(plainPath);
                }
                else
                {
                    // 降级兜底：压缩失败时保留纯文本并移动到下一代，避免日志丢失。
                    try
                    {
                        File.Move(plainPath, $"{_logFilePath}.{generation + 1}", overwrite: true);
                    }
                    catch (Exception moveEx)
                    {
                        Console.WriteLine($"Warning: Failed to move plain log fallback '{plainPath}': {moveEx.Message}");
                    }
                }
            }
        }
    }

    // GLM-5.3, 2026-11-04：批次 8——单文件 Gzip 压缩（BCL 内置，零外部依赖）。
    // Antigravity, 2026-11-04：批次 9——临时文件原子写入与全链路异常捕获。
    internal static bool TryCompressFileToGzip(string sourcePath, string targetPath)
    {
        var tempPath = targetPath + ".tmp";
        try
        {
            using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                input.CopyTo(gzip);
            }

            // 压缩生成的 .gz 归档同样收紧为 Unix 0600（内容源自含密码的日志）。
            RestrictLogFilePermissions(tempPath);
            File.Move(tempPath, targetPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to compress log file '{sourcePath}' to '{targetPath}': {ex.Message}");
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // 忽略清理临时文件的次生异常。
            }
            return false;
        }
    }

    // GLM-5.3, 2026-11-04：批次 4/7/8——会话启动时清理过期日志：仅删除日志目录内
    // 匹配 batchcompress_*.log（及其 .1.. .N 全部轮转代，含 .gz 压缩归档代）且最后写入时间
    // 早于保留期的文件；用户显式指定的其它路径模式不受影响。
    private void PruneExpiredLogs()
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - LogRetention;
            // 通配符先粗筛（Windows 三字符扩展名匹配会放宽到 *.log 前缀），再用
            // IsBatchCompressLogName 精确锁定 “batchcompress_*.log” 与 “.log.<数字>”。
            foreach (var file in Directory.EnumerateFiles(directory, "batchcompress_*.log*"))
            {
                if (IsBatchCompressLogName(Path.GetFileName(file)) &&
                    File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to prune expired logs: {ex.Message}");
        }
    }

    // GLM-5.3, 2026-11-04：批次 7/8——精确匹配本工具的日志文件名：
    // batchcompress_ 前缀 + .log / .log.<纯数字代次> / 及二者的 .gz 压缩形态结尾。
    private static bool IsBatchCompressLogName(string fileName) =>
        fileName.StartsWith("batchcompress_", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(fileName, @"\.log(\.\d+)?(\.gz)?$", RegexOptions.CultureInvariant);

    public string LogFilePath => _logFilePath;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || _disposed)
        {
            return;
        }

        // GPT-5, 2026-08-05：同一把锁保护释放和写入，避免竞态条件向已释放流写入。
        lock (_lock)
        {
            if (_writer == null || _disposed)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = logLevel.ToString().ToUpperInvariant().PadRight(11);
            var message = formatter(state, exception);

            var logEntry = $"[{timestamp}] [{level}] {message}";
            
            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            _writer.WriteLine(logEntry);

            // GLM-5.3, 2026-11-04：批次 4——按写入次数节流的大小检查触发轮转。
            _writesSinceSizeCheck++;
            if (_writesSinceSizeCheck >= RotationCheckIntervalWrites)
            {
                _writesSinceSizeCheck = 0;
                RotateIfOversized();
            }
        }
    }

    public void LogOperation(string operation, string details)
    {
        Log(LogLevel.Information, 0, $"[{operation}] {details}", null, (s, _) => s);
    }

    public void LogCommand(string command)
    {
        Log(LogLevel.Debug, 0, $"[COMMAND] {command}", null, (s, _) => s);
    }

    public void LogSuccess(string message)
    {
        Log(LogLevel.Information, 0, $"[SUCCESS] {message}", null, (s, _) => s);
    }

    public void LogError(string message, Exception? exception = null)
    {
        Log(LogLevel.Error, 0, $"[ERROR] {message}", exception, (s, _) => s);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, 0, $"[WARNING] {message}", null, (s, _) => s);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (!_disposed)
            {
                Log(LogLevel.Information, 0, "========== Log session ended ==========", null, (s, _) => s);
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// FileLoggerService 的日志提供程序。
/// </summary>
// GPT-5, 2026-08-05：复用一个日志实例，使所有分类写入同一个配置的批处理日志文件。
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string? _logFilePath;
    private FileLoggerService? _logger;

    public FileLoggerProvider(string? logFilePath = null)
    {
        _logFilePath = logFilePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        _logger ??= new FileLoggerService(_logFilePath);
        return _logger;
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }
}
