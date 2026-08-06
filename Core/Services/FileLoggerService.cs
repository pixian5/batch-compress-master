using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 将无界面批处理操作写入文件的日志服务。
/// </summary>
// GPT-5, 2026-08-05：用于无界面执行的线程安全文件日志器。一个锁同时保护写入器生命周期和多线程进度消息，确保每条日志完整。
public class FileLoggerService : ILogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private bool _disposed;

    public FileLoggerService(string? logFilePath = null)
    {
        _logFilePath = logFilePath ?? GetDefaultLogPath();
        EnsureLogDirectoryExists();
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
            Log(LogLevel.Information, 0, "========== Log session started ==========", null, (s, _) => s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create log file '{_logFilePath}': {ex.Message}");
            Console.WriteLine("Logs will not be saved to file.");
            _writer = null;
        }
    }

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
