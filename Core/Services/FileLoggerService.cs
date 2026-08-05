using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// File-based logger service for logging all operations to a file
/// </summary>
// GPT-5, 2026-08-05: Thread-safe file logger for headless execution. One lock protects writer lifecycle
// and multi-threaded progress messages so each physical log entry remains intact.
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

        // GPT-5, 2026-08-05: The same lock guards disposal and writes to prevent a race that writes to a disposed stream.
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
/// Provider for FileLoggerService
/// </summary>
// GPT-5, 2026-08-05: Reuses one logger instance so all categories write to the same configured batch log file.
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
