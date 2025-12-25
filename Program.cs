using Avalonia;
using System;

namespace BatchCompress.Avalonia;

sealed class Program
{
    // 初始化代码。不要在AppMain调用前使用Avalonia、第三方API或依赖SynchronizationContext的代码：此时尚未初始化，可能导致异常。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia配置，请勿移除；也被可视化设计器使用。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
