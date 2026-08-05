using Avalonia;
using System;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Services;

namespace BatchCompress.Avalonia;

// GPT-5, 2026-08-05: This is the only process entry point. It routes explicit compression/decompression
// requests to the headless pipeline; every other invocation initializes Avalonia on an STA thread.
sealed class Program
{
    // 初始化代码。不要在AppMain调用前使用Avalonia、第三方API或依赖SynchronizationContext的代码：此时尚未初始化，可能导致异常。
    [STAThread]
    public static int Main(string[] args)
    {
        // GPT-5, 2026-08-05: Help must return before Avalonia initialization so CLI users never create a GUI process.
        if (CommandLineHandler.IsHelpRequested(args))
        {
            CommandLineHandler.ShowHelp();
            return 0;
        }

        // GPT-5, 2026-08-05: Parsing normalizes mode switches and password precedence before either execution path consumes them.
        var options = CommandLineHandler.ParseArguments(args);

        // GPT-5, 2026-08-05: Headless mode deliberately blocks on the async task so Main returns the real batch exit code.
        if (options.Compress || options.Decompress)
        {
            return RunHeadlessAsync(options).GetAwaiter().GetResult();
        }

        // GPT-5, 2026-08-05: GUI mode owns the desktop lifetime and therefore returns only after the last window closes.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Run in headless mode for command-line operations
    /// </summary>
    private static async Task<int> RunHeadlessAsync(CommandLineOptions options)
    {
        using var logger = new FileLoggerService(options.LogFile);
        
        try
        {
            var runner = new HeadlessBatchRunner(options, logger);
            return await runner.RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogError("Fatal error in headless mode", ex);
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    // GPT-5, 2026-08-05: Keep this builder side-effect free because Avalonia designers also call it outside Program.Main.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
