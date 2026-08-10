using Avalonia;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

        // GPT-5, 2026-08-06：版本查询不初始化 Avalonia，也不创建日志或归档引擎。
        if (CommandLineHandler.IsVersionRequested(applicationArgs))
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?.Split('+')[0] ?? "unknown";
            Console.WriteLine($"BatchCompress.Avalonia {version}");
            return 0;
        }

        // GPT-5, 2026-08-06：参数错误使用退出码 2 返回，不创建 GUI，也不使用不完整的默认选项执行任务。
        var parseOutcome = CommandLineHandler.ParseArguments(applicationArgs);
        if (!parseOutcome.Success)
        {
            foreach (var error in parseOutcome.Errors)
            {
                Console.Error.WriteLine($"参数错误: {error}");
            }

            Console.Error.WriteLine("使用 --help 查看完整用法。");
            return 2;
        }

        var options = parseOutcome.Options;

        // GPT-5, 2026-08-05：无界面模式同步等待异步任务，确保 Main 返回真实的批处理退出码。
        if (options.Compress || options.Decompress)
        {
            return RunHeadlessAsync(options).GetAwaiter().GetResult();
        }

        // macOS 的 Avalonia Native 渲染计时器依赖 CoreVideo 的 CVDisplayLink。
        // 显示器刚唤醒、切换或应用从 Finder/登录项启动时，该 API 可能暂时返回
        // kCVReturnInvalidArgument (-6661)。先等待显示链稳定，避免 Avalonia 在
        // 窗口创建前直接抛出未处理异常；命令行模式不会经过这里。
        WaitForMacOsDisplayLink();

        // GPT-5, 2026-08-05：GUI 模式拥有桌面生命周期，仅在最后一个窗口关闭后返回。
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(applicationArgs);
        return 0;
    }

    /// <summary>
    /// 以无界面模式执行命令行批处理。
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
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private const string CoreVideoLibrary = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";

    [DllImport(CoreVideoLibrary, ExactSpelling = true)]
    private static extern int CVDisplayLinkCreateWithActiveCGDisplays(out IntPtr displayLink);

    [DllImport(CoreVideoLibrary, ExactSpelling = true)]
    private static extern void CVDisplayLinkRelease(IntPtr displayLink);

    private static void WaitForMacOsDisplayLink()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var lastNotice = DateTime.UtcNow;
        while (true)
        {
            if (HasDisplayLink())
            {
                // 连续两次成功且间隔一个短暂的运行循环窗口，覆盖显示器唤醒/切换竞态。
                Thread.Sleep(100);
                if (HasDisplayLink())
                {
                    return;
                }
            }

            if (DateTime.UtcNow - lastNotice >= TimeSpan.FromSeconds(10))
            {
                Console.Error.WriteLine("等待 macOS 显示器就绪后启动图形界面...");
                lastNotice = DateTime.UtcNow;
            }

            Thread.Sleep(500);
        }
    }

    private static bool HasDisplayLink()
    {
        var result = CVDisplayLinkCreateWithActiveCGDisplays(out var displayLink);
        if (result != 0)
        {
            return false;
        }

        if (displayLink != IntPtr.Zero)
        {
            CVDisplayLinkRelease(displayLink);
        }

        return true;
    }

    // GPT-5, 2026-08-05：保持构建器无副作用，因为 Avalonia 设计器也会在 Program.Main 之外调用它。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
