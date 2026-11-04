using System;
using System.IO;
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
// 超过 maxRotatedGenerations（默认 5 代，含 .1.. .5）自动淘汰最旧一代，不再覆写 .1；
// 3) 每次会话启动时清理 7 天前且匹配 batchcompress_*.log 模式的过期日志与全部轮转代副本。
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

    // GLM-5.3, 2026-11-04：批次 7——多代轮转：当前文件超过阈值时先整体后移轮转代
    // （.N-1 → .N、…、.1 → .2，最旧的 .N 先淘汰），随后当前文件改名为 .1 并新建同名日志
    // 继续写入；失败不影响后续写入（尽力而为）。File.Move 保留原文件权限，
    // 因此轮转副本继承此前收紧的 0600，新当前文件由 InitializeWriter 重新收紧。
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

    // GLM-5.3, 2026-11-04：批次 7——轮转代向上滚动：删除超限的最旧一代后，
    // 从高代到低代依次后移一格；不存在的代直接跳过（首次/中间轮转无需补齐空洞）。
    private void ShiftRotatedGenerations()
    {
        var oldestPath = $"{_logFilePath}.{_maxRotatedGenerations}";
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var generation = _maxRotatedGenerations - 1; generation >= 1; generation--)
        {
            var sourcePath = $"{_logFilePath}.{generation}";
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, $"{_logFilePath}.{generation + 1}");
            }
        }
    }

    // GLM-5.3, 2026-11-04：批次 4/7——会话启动时清理过期日志：仅删除日志目录内
    // 匹配 batchcompress_*.log（及其 .1.. .N 全部轮转代）且最后写入时间早于保留期的文件；
    // 用户显式指定的其它路径模式不受影响。
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

    // GLM-5.3, 2026-11-04：批次 7——精确匹配本工具的日志文件名：
    // batchcompress_ 前缀 + .log 结尾或 .log.<纯数字代次> 结尾（多代轮转副本）。
    private static bool IsBatchCompressLogName(string fileName) =>
        fileName.StartsWith("batchcompress_", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(fileName, @"\.log(\.\d+)?$", RegexOptions.CultureInvariant);

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
