using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using BatchCompress.Avalonia.Localization;
using BatchCompress.Avalonia.ViewModels;
using BatchCompress.Avalonia.Views;

namespace BatchCompress.Avalonia;

// GPT-5, 2026-08-05：负责桌面生命周期、原生托盘集成与主窗口可见性。
// 普通关闭窗口会退出应用；隐藏是由 ViewModel 明确发起的用户命令。
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            // GPT-5, 2026-09-30：先创建 ViewModel 完成语言检测，再生成托盘/状态栏菜单，
            // 这样菜单文案才能跟随界面语言而非默认语言。
            _viewModel = new MainWindowViewModel();
            _mainWindow = new MainWindow { DataContext = _viewModel };
            desktop.MainWindow = _mainWindow;

            // GPT-5, 2026-08-06：Avalonia 12 不再允许应用层直接修改内部 BindingPlugins；
            // 保留默认验证链，CommunityToolkit 的可观察属性验证仍由 ViewModel 自身控制。
            var strings = LocalizationService.Instance.Strings;
            if (OperatingSystem.IsMacOS())
            {
                StartMacStatusBarHelper(strings);
            }
            else
            {
                CreateTrayIcon(strings);
            }
            // GPT-5, 2026-09-30：已知限制——语言运行中切换时，非 macOS 托盘菜单与 macOS
            // 状态栏帮助进程的菜单文案不会即时刷新，重启后生效。
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(LanguageStrings strings)
    {
        // GPT-5, 2026-08-06：在桌面生命周期建立后创建托盘，确保 macOS 已注册原生 TrayIcon 工厂并能生成 NSStatusItem。
        using var iconStream = AssetLoader.Open(
            new Uri("avares://BatchCompress.Avalonia/Assets/%E5%8E%8B%E7%BC%A9.ico"));

        var menu = new NativeMenu();
        var showHideItem = new NativeMenuItem(strings.TrayShowHide);
        showHideItem.Click += TrayShowHide_Clicked;
        menu.Items.Add(showHideItem);

        var exitItem = new NativeMenuItem(strings.TrayExit);
        exitItem.Click += TrayExit_Clicked;
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            IsVisible = true,
            ToolTipText = strings.TrayTooltip,
            Menu = menu
        };
        _trayIcon.Clicked += TrayIcon_Clicked;
        MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private static void StartMacStatusBarHelper(LanguageStrings strings)
    {
        // GPT-5, 2026-08-06：Avalonia 托盘在当前 macOS 上未创建状态栏项目，改由随应用打包的原生帮助进程提供可见托盘和菜单。
        var helperPath = Path.Combine(AppContext.BaseDirectory, "BatchCompress.StatusBarHelper");
        if (!File.Exists(helperPath))
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            // GPT-5, 2026-09-30：依次传入 tooltip、显示/隐藏、退出菜单文案；
            // Swift 侧在参数缺失时回退到中文默认值。
            startInfo.ArgumentList.Add(strings.TrayTooltip);
            startInfo.ArgumentList.Add(strings.TrayShowHide);
            startInfo.ArgumentList.Add(strings.TrayExit);
            _ = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动 macOS 原生托盘帮助进程失败: {ex.Message}");
        }
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e) => ToggleMainWindow();

    private void TrayShowHide_Clicked(object? sender, EventArgs e) => ToggleMainWindow();

    private void TrayExit_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        // GPT-5, 2026-08-05：菜单和图标点击共用同一个幂等的显示/隐藏托盘动作。
        if (_mainWindow.IsVisible)
        {
            HideMainWindow();
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void HideMainWindow()
    {
        _mainWindow?.Hide();
    }
}
