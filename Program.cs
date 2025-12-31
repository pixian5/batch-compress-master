using Avalonia;
using System;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Services;

namespace BatchCompress.Avalonia;

sealed class Program
{
    // 初始化代码。不要在AppMain调用前使用Avalonia、第三方API或依赖SynchronizationContext的代码：此时尚未初始化，可能导致异常。
    [STAThread]
    public static int Main(string[] args)
    {
        // Check for help request
        if (CommandLineHandler.IsHelpRequested(args))
        {
            CommandLineHandler.ShowHelp();
            return 0;
        }

        // Parse command-line arguments
        var options = CommandLineHandler.ParseArguments(args);

        // If compress or decompress mode is specified, run in headless mode
        if (options.Compress || options.Decompress)
        {
            return RunHeadlessAsync(options).GetAwaiter().GetResult();
        }

        // Otherwise, start the GUI application
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

    // Avalonia配置，请勿移除；也被可视化设计器使用。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
