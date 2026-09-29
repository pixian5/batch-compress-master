using System;
using System.ComponentModel;
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
    // GPT-5, 2026-09-30：保存状态栏帮助进程，语言切换时需要结束旧进程再以新文案重启。
    private Process? _statusBarHelper;
    // GPT-5, 2026-09-30：记录已应用到托盘的字符串实例；语言未真正变化时避免重复重建。
    private LanguageStrings? _appliedTrayStrings;

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
            ApplyTrayLanguage(LocalizationService.Instance.Strings);
            // GPT-5, 2026-09-30：语言在运行中切换时同步刷新托盘/状态栏菜单文案。
            LocalizationService.Instance.PropertyChanged += OnLocalizationServicePropertyChanged;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 语言或字符串集合变化时，重新应用托盘文案。
    /// </summary>
    private void OnLocalizationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocalizationService.Strings) &&
            e.PropertyName != nameof(LocalizationService.CurrentLanguage))
        {
            return;
        }

        var strings = LocalizationService.Instance.Strings;
        // 切换语言会替换整个字符串实例；同一实例的重复通知不需要重建托盘。
        if (ReferenceEquals(strings, _appliedTrayStrings))
        {
            return;
        }

        ApplyTrayLanguage(strings);
    }

    /// <summary>
    /// 按当前语言创建或更新托盘入口。
    /// </summary>
    private void ApplyTrayLanguage(LanguageStrings strings)
    {
        _appliedTrayStrings = strings;

        if (OperatingSystem.IsMacOS())
        {
            RestartMacStatusBarHelper(strings);
        }
        else if (_trayIcon is null)
        {
            CreateTrayIcon(strings);
        }
        else
        {
            _trayIcon.ToolTipText = strings.TrayTooltip;
            _trayIcon.Menu = BuildTrayMenu(strings);
        }
    }

    /// <summary>
    /// 构建托盘菜单；菜单项文案需要随界面语言变化，因此单独抽取以便重建。
    /// </summary>
    private NativeMenu BuildTrayMenu(LanguageStrings strings)
    {
        var menu = new NativeMenu();

        var showHideItem = new NativeMenuItem(strings.TrayShowHide);
        showHideItem.Click += TrayShowHide_Clicked;
        menu.Items.Add(showHideItem);

        var exitItem = new NativeMenuItem(strings.TrayExit);
        exitItem.Click += TrayExit_Clicked;
        menu.Items.Add(exitItem);

        return menu;
    }

    private void CreateTrayIcon(LanguageStrings strings)
    {
        // GPT-5, 2026-08-06：在桌面生命周期建立后创建托盘，确保 macOS 已注册原生 TrayIcon 工厂并能生成 NSStatusItem。
        using var iconStream = AssetLoader.Open(
            new Uri("avares://BatchCompress.Avalonia/Assets/%E5%8E%8B%E7%BC%A9.ico"));

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(iconStream),
            IsVisible = true,
            ToolTipText = strings.TrayTooltip,
            Menu = BuildTrayMenu(strings)
        };
        _trayIcon.Clicked += TrayIcon_Clicked;
        MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    /// <summary>
    /// 以新语言重启 macOS 状态栏帮助进程。
    /// </summary>
    private void RestartMacStatusBarHelper(LanguageStrings strings)
    {
        // 帮助进程的菜单文案在启动时固定，无法在运行期改写，因此语言变化时必须重启它。
        try
        {
            if (_statusBarHelper is { HasExited: false })
            {
                _statusBarHelper.Kill();
                _statusBarHelper.WaitForExit(1000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"结束旧的状态栏帮助进程失败: {ex.Message}");
        }

        _statusBarHelper = null;
        StartMacStatusBarHelper(strings);
    }

    private void StartMacStatusBarHelper(LanguageStrings strings)
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
            _statusBarHelper = Process.Start(startInfo);
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
