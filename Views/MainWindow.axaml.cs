using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Metadata;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using BatchCompress.Avalonia.ViewModels;
using BatchCompress.Avalonia.Localization;
using Avalonia.Input;
using System.ComponentModel;

namespace BatchCompress.Avalonia.Views;

public partial class MainWindow : Window
{
    private ScrollViewer? _commandLogScrollViewer;
    private double _lastNormalWidth;
    private double _lastNormalHeight;
    private int _lastNormalX;
    private int _lastNormalY;
    private bool _hasLastNormalPosition;
    private WindowState _lastWindowState = WindowState.Normal;
    private bool _isApplyingNormalBounds;
    private static readonly JsonSerializerOptions WindowSettingsSerializerOptions = new();
    // GPT-5, 2026-08-05：将用户窗口状态存放在应用包外，确保更新后仍保留、不破坏 macOS 签名，并使用各平台正确的数据目录。
    private static readonly string WindowSettingsFilePath = GetWindowSettingsFilePath();
    public MainWindow()
    {
        InitializeComponent();
        _lastNormalWidth = Width;
        _lastNormalHeight = Height;
        _lastNormalX = Position.X;
        _lastNormalY = Position.Y;
        _hasLastNormalPosition = true;

        RestoreWindowSize();
        _lastWindowState = WindowState;
        AddHandler(DragDrop.DropEvent, Drop);

        // Setup auto-scroll for CommandLog
        this.Resized += MainWindow_Resized;
        this.PositionChanged += MainWindow_PositionChanged;
        this.Closing += MainWindow_Closing;
        this.Loaded += MainWindow_Loaded;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty || change.NewValue is not WindowState newState)
        {
            return;
        }

        // GPT-5, 2026-08-05：部分平台后端在恢复时会短暂报告最大化几何信息。
        // 仅在 UI 完成状态切换后重新应用此前的普通窗口边界。
        if (_lastWindowState == WindowState.Maximized && newState == WindowState.Normal)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (WindowState != WindowState.Normal || _isApplyingNormalBounds)
                {
                    return;
                }

                if (!IsLikelyMaximizedGeometry(ClientSize, Position))
                {
                    return;
                }

                ApplyLastNormalBounds();
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        _lastWindowState = newState;
        UpdateZoomButtonText();
        SaveWindowSize();
    }

    private void MainWindow_Resized(object? sender, WindowResizedEventArgs e)
    {
        if (WindowState == WindowState.Normal && !_isApplyingNormalBounds &&
            !IsLikelyMaximizedGeometry(e.ClientSize, Position))
        {
            UpdateLastNormalSize(e.ClientSize.Width, e.ClientSize.Height);
        }

        SaveWindowSize();
    }

    private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal && !_isApplyingNormalBounds &&
            !IsLikelyMaximizedGeometry(ClientSize, e.Point))
        {
            UpdateLastNormalPosition(e.Point);
        }

        SaveWindowSize();
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowSize();
    }

    private void RestoreWindowSize()
    {
        try
        {
            if (!File.Exists(WindowSettingsFilePath))
            {
                return;
            }

            var json = File.ReadAllText(WindowSettingsFilePath);
            var settings = JsonSerializer.Deserialize<WindowSizeSettings>(json, WindowSettingsSerializerOptions);
            if (settings is null)
            {
                return;
            }

            if (IsValidWindowDimension(settings.Width) && IsValidWindowDimension(settings.Height))
            {
                Width = settings.Width;
                Height = settings.Height;
                UpdateLastNormalSize(settings.Width, settings.Height);
            }

            if (settings.X.HasValue && settings.Y.HasValue)
            {
                UpdateLastNormalPosition(settings.X.Value, settings.Y.Value);
                Position = new PixelPoint(settings.X.Value, settings.Y.Value);
            }

            if (settings.WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch
        {
            // GPT-5, 2026-08-05：无效或过期的用户设置绝不能阻止主窗口打开。
        }
    }

    private void SaveWindowSize()
    {
        try
        {
            var stateToSave = WindowState == WindowState.Minimized
                ? WindowState.Normal
                : WindowState;

            if (stateToSave == WindowState.Normal)
            {
                var width = ClientSize.Width > 0 ? ClientSize.Width : Width;
                var height = ClientSize.Height > 0 ? ClientSize.Height : Height;
                if (!IsLikelyMaximizedGeometry(new Size(width, height), Position))
                {
                    UpdateLastNormalSize(width, height);
                    UpdateLastNormalPosition(Position);
                }
            }

            if (!IsValidWindowDimension(_lastNormalWidth) || !IsValidWindowDimension(_lastNormalHeight))
            {
                return;
            }

            var settings = new WindowSizeSettings
            {
                Width = _lastNormalWidth,
                Height = _lastNormalHeight,
                X = _hasLastNormalPosition ? _lastNormalX : null,
                Y = _hasLastNormalPosition ? _lastNormalY : null,
                WindowState = stateToSave == WindowState.Maximized
                    ? WindowState.Maximized
                    : WindowState.Normal
            };

            var directory = Path.GetDirectoryName(WindowSettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, WindowSettingsSerializerOptions);
            File.WriteAllText(WindowSettingsFilePath, json);
        }
        catch
        {
            // GPT-5, 2026-08-05：窗口状态持久化是尽力而为的操作，绝不能阻塞应用退出。
        }
    }

    private void UpdateLastNormalSize(double width, double height)
    {
        if (!IsValidWindowDimension(width) || !IsValidWindowDimension(height))
        {
            return;
        }

        _lastNormalWidth = width;
        _lastNormalHeight = height;
    }

    private static string GetWindowSettingsFilePath()
    {
        // GPT-5, 2026-08-05：ApplicationData 在 Windows 对应 AppData、在 macOS 对应 Application Support、
        // 在 Linux 对应用户数据目录。LocalApplicationData 用于异常运行时的回退。
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(
            string.IsNullOrWhiteSpace(applicationData) ? AppContext.BaseDirectory : applicationData,
            "BatchCompress.Avalonia",
            "window-settings.json");
    }

    private void UpdateLastNormalPosition(PixelPoint point)
    {
        UpdateLastNormalPosition(point.X, point.Y);
    }

    private void UpdateLastNormalPosition(int x, int y)
    {
        _lastNormalX = x;
        _lastNormalY = y;
        _hasLastNormalPosition = true;
    }

    private static bool IsValidWindowDimension(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }

    private void ApplyLastNormalBounds()
    {
        if (!IsValidWindowDimension(_lastNormalWidth) || !IsValidWindowDimension(_lastNormalHeight))
        {
            return;
        }

        _isApplyingNormalBounds = true;
        try
        {
            ClientSize = new Size(_lastNormalWidth, _lastNormalHeight);
            Width = _lastNormalWidth;
            Height = _lastNormalHeight;

            if (_hasLastNormalPosition)
            {
                Position = new PixelPoint(_lastNormalX, _lastNormalY);
            }
        }
        finally
        {
            _isApplyingNormalBounds = false;
        }

        SaveWindowSize();
    }

    private bool IsLikelyMaximizedGeometry(Size clientSize, PixelPoint position)
    {
        try
        {
            var screens = Screens;
            var screen = screens.ScreenFromWindow(this) ?? screens.ScreenFromPoint(position) ?? screens.Primary;
            if (screen is null)
            {
                return false;
            }

            var workingArea = screen.WorkingArea;
            var widthClose = Math.Abs(clientSize.Width - workingArea.Width) <= 2;
            var heightClose = Math.Abs(clientSize.Height - workingArea.Height) <= 2;

            var xClose = Math.Abs(position.X - workingArea.X) <= 12 || position.X <= -6;
            var yClose = Math.Abs(position.Y - workingArea.Y) <= 12 || position.Y <= -6;

            return widthClose && heightClose && xClose && yClose;
        }
        catch
        {
            return false;
        }
    }
    
    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        UpdateZoomButtonText();
        // Find the CommandLogScrollViewer
        _commandLogScrollViewer = this.FindControl<ScrollViewer>("CommandLogScrollViewer");
        
        // Subscribe to CommandLog property changes
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    // GPT-5, 2026-08-06：保留旧 WinForms 的放大/缩小入口，使用 Avalonia 原生窗口状态实现。
    private void ZoomButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateZoomButtonText();
    }

    private void UpdateZoomButtonText()
    {
        if (ZoomButton != null)
        {
            ZoomButton.Content = WindowState == WindowState.Maximized ? L.ZoomOut : L.ZoomIn;
        }
    }
    
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CommandLog))
        {
            // Auto-scroll to bottom when CommandLog changes
            if (_commandLogScrollViewer != null)
            {
                // Use Dispatcher to ensure UI is updated before scrolling
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _commandLogScrollViewer.ScrollToEnd();
                }, global::Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }
    
    /// <summary>
    /// Gets the current localized strings.
    /// </summary>
    private LanguageStrings L => LocalizationService.Instance.Strings;

    private void Drop(object? sender, DragEventArgs e)
    {
        // GPT-5, 2026-08-05：仅接受一个可执行的拖放项目。目录成为扫描根目录，TXT 成为密码/列表来源，故有意忽略额外项目。
        if (e.DataTransfer.TryGetFiles() is IEnumerable<IStorageItem> files && DataContext is MainWindowViewModel viewModel)
        {
            foreach (var file in files)
            {
                var firstPath = file.Path.LocalPath;
                if (string.IsNullOrEmpty(firstPath)) continue;
                
                // GPT-5, 2026-08-05：拖入目录即为文件夹扫描模式的来源目录。
                if (Directory.Exists(firstPath))
                {
                    viewModel.SaveFilePath = firstPath;
                    viewModel.CommandLog += L.DroppedFolder + firstPath + "\n";
                    break;
                }
                // GPT-5, 2026-08-05：拖入 TXT 文件提供文件列表和可选密码。
                else if (File.Exists(firstPath) && firstPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.SourcePath = firstPath;
                    viewModel.CommandLog += L.DroppedTxtFile + firstPath + "\n";
                    break;
                }
            }
        }
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            // GPT-5, 2026-08-05：StorageProvider 和 Window.Hide 需要存活的 Avalonia 窗口。
            // 注入这些回调可使 ViewModel 不依赖平台控件。
            viewModel.BrowseSourceRequested = BrowseSourceAsync;
            viewModel.BrowseOutputRequested = BrowseOutputAsync;
            viewModel.BrowseTextFileRequested = BrowseTextFileAsync;
            viewModel.BrowseSaveFileRequested = BrowseSaveFileAsync;
            viewModel.BrowseAttachmentRequested = BrowseAttachmentAsync;
            viewModel.ShowHelpRequested = ShowHelpAsync;
            viewModel.HideWindowRequested = HideWindowFromViewModel;
        }
    }

    private void HideWindowFromViewModel()
    {
        base.Hide();
    }

    private async Task BrowseAttachmentAsync()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择附件目录",
            AllowMultiple = true
        });

        // GPT-5, 2026-08-05：将存储项目转换为路径，再与现有换行列表合并。
        // 不区分大小写的去重符合常见 Windows/macOS 文件系统行为，同时保留顺序。
        var paths = folders.Select(folder => folder.Path.LocalPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        var existing = viewModel.EnclosureList
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => path.Length > 0)
            .ToList();
        foreach (var path in paths.Where(path => !existing.Contains(path, StringComparer.OrdinalIgnoreCase)))
        {
            existing.Add(path);
        }

        viewModel.EnclosureList = string.Join(Environment.NewLine, existing);
    }

    private async Task ShowHelpAsync()
    {
        await ShowMessageBoxAsync("帮助", "快捷键：F5 刷新列表，Esc 取消操作，Ctrl+L 清空日志，Ctrl+H 隐藏到托盘。\n\nWinRAR 支持 RAR 和 ZIP；恢复记录仅对 RAR 生效。附件目录每行一个，也可以使用浏览按钮选择多个目录。\n\n系统通知、托盘和关机功能由当前操作系统提供，Linux 需要 notify-send，关机可能需要管理员权限。\n\n关闭窗口会真正退出程序；需要后台运行时请使用“隐藏到托盘”，再通过托盘菜单显示/隐藏或退出。");
    }
    
    private async Task BrowseSourceAsync()
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        
        // GPT-5, 2026-08-06：两个 TXT 模式都选择清单文件；只有旧版密码本模式需要归档所在目录。
        if (viewModel.SourceMode == 0)
        {
            // GPT-5, 2026-08-05：旧版 TXT 流程相对 SaveFilePath 解析文件名，
            // 因此允许选择列表文件前必须先确定该目录。
            if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
            {
                bool shouldContinue = await ShowOkCancelMessageBoxAsync(L.Hint, L.SelectSaveDirectory);
                
                if (shouldContinue)
                {
                    // GPT-5, 2026-08-05：复用标准选择器，使每个平台遵循相同的路径规则。
                    await BrowseSaveFileAsync();
                    
                    if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
                        return;
                }
                else
                {
                    return;
                }
            }
            
            // GPT-5, 2026-08-05：合并选择的文件名之前仅规范化末尾分隔符。
            string savePath = viewModel.SaveFilePath;
            if (!savePath.EndsWith(Path.DirectorySeparatorChar) && 
                !savePath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                savePath += Path.DirectorySeparatorChar;
            }
            

            
            // GPT-5, 2026-08-05：优先显示文本文件，同时保留所有文件入口以兼容旧列表。
            var fileOptions = new FilePickerOpenOptions
            {
                Title = L.SelectPasswordTxt,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(L.TextFile) { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType(L.AllFiles) { Patterns = new[] { "*.*" } }
                }
            };
            
            var files = await this.StorageProvider.OpenFilePickerAsync(fileOptions);
            if (files.Count > 0)
            {
                // GPT-5, 2026-08-05：仅保留选择的文件名并锚定到 SaveFilePath，以保持兼容性。
                string passwordFileName = Path.GetFileName(files[0].Path.LocalPath);
                
                string fullPath = Path.Combine(savePath, passwordFileName);
                
                viewModel.SourcePath = fullPath;
            }
        }
        else if (viewModel.SourceMode == 1)
        {
            var fileOptions = new FilePickerOpenOptions
            {
                Title = "选择压缩路径清单",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(L.TextFile) { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType(L.AllFiles) { Patterns = new[] { "*.*" } }
                }
            };
            var files = await StorageProvider.OpenFilePickerAsync(fileOptions);
            if (files.Count > 0)
            {
                viewModel.SourcePath = files[0].Path.LocalPath;
            }
        }
        else
        {
            var options = new FolderPickerOpenOptions
            {
                Title = L.SelectSourceFolder,
                AllowMultiple = false
            };
            
            var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count > 0)
            {
                viewModel.SourcePath = folders[0].Path.LocalPath;
            }
        }
    }
    
    private async Task BrowseOutputAsync()
    {
        // GPT-5, 2026-08-05：输出选择始终是目录，且不会改变来源模式状态。
        var options = new FolderPickerOpenOptions
        {
            Title = L.SelectOutputFolder,
            AllowMultiple = false
        };
        
        var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OutputPath = folders[0].Path.LocalPath;
        }
    }
    
    private async Task BrowseSaveFileAsync()
    {
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                // GPT-5, 2026-08-05：记录选择器入口，用于诊断平台特定的存储提供程序失败。
                viewModel.CommandLog += "BrowseSaveFileAsync called\n";
            }
            
            var options = new FolderPickerOpenOptions
            {
                Title = L.SelectSaveFolder,
                AllowMultiple = false
            };
            
            // GPT-5, 2026-08-05：使用 Avalonia StorageProvider 而不是平台原生 API，以保持选择行为可移植。
            var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
            
            if (DataContext is MainWindowViewModel viewModel2)
            {
                // GPT-5, 2026-08-05：保留返回数量，因为取消操作以空集合表示。
                viewModel2.CommandLog += $"Folder picker returned {folders.Count} items\n";
                
                if (folders.Count > 0)
                {
                    viewModel2.SaveFilePath = folders[0].Path.LocalPath;
                    viewModel2.CommandLog += $"Selected folder: {viewModel2.SaveFilePath}\n";
                }
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CommandLog += $"Error in BrowseSaveFileAsync: {ex.Message}\n";
            }
        }
    }
    
    private async Task BrowseTextFileAsync()
    {
        // GPT-5, 2026-08-05：此选择器存储实际 TXT 路径，不同于上方旧版 SourceMode 路径构建逻辑。
        var options = new FilePickerOpenOptions
        {
            Title = L.SelectTextFile,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(L.TextFile) { Patterns = new[] { "*.txt" } },
                new FilePickerFileType(L.AllFiles) { Patterns = new[] { "*.*" } }
            }
        };
        
        var files = await this.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TextFilePath = files[0].Path.LocalPath;
        }
    }
    
    private async Task<bool> ShowMessageBoxAsync(string title, string message)
    {
        // GPT-5, 2026-08-05：保持此对话框自包含，使调用方仅在用户确认后收到完成的 Task。
        Window? dialog = null;
        dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, FontSize = 16, Margin = new Thickness(0, 20, 0, 20) },
                    new Button
                    {
                        Content = L.Ok,
                        Width = 100,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        // GPT-5, 2026-08-05：以 true 关闭，使模态 Task 获得明确的成功确认结果。
        var button = (Button)((StackPanel)dialog.Content).Children[1];
        button.Click += (sender, e) => dialog.Close(true);
        
        return await dialog.ShowDialog<bool>(this);
    }
    
    private async Task<bool> ShowOkCancelMessageBoxAsync(string title, string message)
    {
        // GPT-5, 2026-08-05：使用明确布尔结果，区分用户取消与选择器失败。
        Window? dialog = null;
        dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, FontSize = 16, Margin = new Thickness(0, 20, 0, 20) },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 20,
                        Children =
                        {
                            new Button { Content = L.Ok, Width = 100 },
                            new Button { Content = L.CancelDialog, Width = 100 }
                        }
                    }
                }
            },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        // Set up button click handlers after dialog is created
        var buttonPanel = (StackPanel)((StackPanel)dialog.Content).Children[1];
        var okButton = (Button)buttonPanel.Children[0];
        var cancelButton = (Button)buttonPanel.Children[1];
        
        okButton.Click += (sender, e) => dialog.Close(true);
        cancelButton.Click += (sender, e) => dialog.Close(false);
        
        return await dialog.ShowDialog<bool>(this);
    }

    private sealed class WindowSizeSettings
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public int? X { get; set; }

        public int? Y { get; set; }

        public WindowState WindowState { get; set; } = WindowState.Normal;
    }
}
