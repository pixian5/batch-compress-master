using Avalonia;
using System;
using System.Linq;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Services;

namespace BatchCompress.Avalonia;

// GPT-5, 2026-08-05：这是唯一的进程入口。显式压缩/解压请求进入无界面流程，其余请求在 STA 线程初始化 Avalonia。
sealed class Program
{
    // 初始化代码。不要在AppMain调用前使用Avalonia、第三方API或依赖SynchronizationContext的代码：此时尚未初始化，可能导致异常。
    [STAThread]
    public static int Main(string[] args)
    {
        // GPT-5, 2026-08-05：Finder 和 LaunchServices 打开 macOS .app 时会附加 -psn_* 进程序列参数。
        // 它不是应用选项，必须在 System.CommandLine 解析前移除。
        var applicationArgs = args
            .Where(argument => !argument.StartsWith("-psn_", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // GPT-5, 2026-08-05：帮助信息必须在初始化 Avalonia 前返回，避免命令行用户创建 GUI 进程。
        if (CommandLineHandler.IsHelpRequested(applicationArgs))
        {
            CommandLineHandler.ShowHelp();
            return 0;
        }

        // GPT-5, 2026-08-05：解析阶段统一模式开关与密码优先级，两个执行路径共用同一份结果。
        var options = CommandLineHandler.ParseArguments(applicationArgs);

        // GPT-5, 2026-08-05：无界面模式同步等待异步任务，确保 Main 返回真实的批处理退出码。
        if (options.Compress || options.Decompress)
        {
            return RunHeadlessAsync(options).GetAwaiter().GetResult();
        }

        // GPT-5, 2026-08-05：GUI 模式拥有桌面生命周期，仅在最后一个窗口关闭后返回。
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(applicationArgs);
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

    // GPT-5, 2026-08-05：保持构建器无副作用，因为 Avalonia 设计器也会在 Program.Main 之外调用它。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
