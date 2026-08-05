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
    // GPT-5, 2026-08-05: Store user window state outside the app bundle so it survives updates,
    // does not invalidate the macOS signature, and uses the correct data directory on every platform.
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

        // GPT-5, 2026-08-05: Some platform backends briefly report maximized geometry while restoring.
        // Re-apply the previous normal bounds only after the UI has finished its state transition.
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
            // GPT-5, 2026-08-05: Invalid or stale user settings must never prevent the main window from opening.
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
            // GPT-5, 2026-08-05: Window-state persistence is best-effort and must not block application shutdown.
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
        // GPT-5, 2026-08-05: ApplicationData resolves to AppData on Windows, Application Support on macOS,
        // and the user's data directory on Linux. LocalApplicationData is a fallback for unusual runtimes.
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
        // Find the CommandLogScrollViewer
        _commandLogScrollViewer = this.FindControl<ScrollViewer>("CommandLogScrollViewer");
        
        // Subscribe to CommandLog property changes
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
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
        // GPT-5, 2026-08-05: Accept only one actionable drop. A folder becomes the scan root;
        // a TXT file becomes the password/list source, so extra dropped items are intentionally ignored.
        if (e.DataTransfer.TryGetFiles() is IEnumerable<IStorageItem> files && DataContext is MainWindowViewModel viewModel)
        {
            foreach (var file in files)
            {
                var firstPath = file.Path.LocalPath;
                if (string.IsNullOrEmpty(firstPath)) continue;
                
                // GPT-5, 2026-08-05: A dropped directory is the source directory for folder-scan mode.
                if (Directory.Exists(firstPath))
                {
                    viewModel.SaveFilePath = firstPath;
                    viewModel.CommandLog += L.DroppedFolder + firstPath + "\n";
                    break;
                }
                // GPT-5, 2026-08-05: A dropped TXT file supplies the file list and optional passwords.
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
            // GPT-5, 2026-08-05: StorageProvider and Window.Hide require a live Avalonia window.
            // Injecting these callbacks keeps the view model independent of platform controls.
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

        // GPT-5, 2026-08-05: Convert storage items to paths, then merge them with the existing newline list.
        // Case-insensitive de-duplication matches common Windows/macOS filesystem behavior while preserving order.
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
        
        // GPT-5, 2026-08-05: TXT mode chooses the legacy list/password file; folder mode chooses the scan root.
        if (viewModel.SourceMode == 0)
        {
            // GPT-5, 2026-08-05: The legacy TXT workflow resolves filenames relative to SaveFilePath,
            // so require that directory before allowing the user to select the list file.
            if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
            {
                bool shouldContinue = await ShowOkCancelMessageBoxAsync(L.Hint, L.SelectSaveDirectory);
                
                if (shouldContinue)
                {
                    // GPT-5, 2026-08-05: Reuse the standard picker so every platform follows the same path rules.
                    await BrowseSaveFileAsync();
                    
                    if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
                        return;
                }
                else
                {
                    return;
                }
            }
            
            // GPT-5, 2026-08-05: Normalize only the trailing separator before combining the selected filename.
            string savePath = viewModel.SaveFilePath;
            if (!savePath.EndsWith(Path.DirectorySeparatorChar) && 
                !savePath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                savePath += Path.DirectorySeparatorChar;
            }
            

            
            // GPT-5, 2026-08-05: Prefer text files while retaining an all-files escape hatch for legacy lists.
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
                // GPT-5, 2026-08-05: Keep only the selected filename and anchor it to SaveFilePath for compatibility.
                string passwordFileName = Path.GetFileName(files[0].Path.LocalPath);
                
                string fullPath = Path.Combine(savePath, passwordFileName);
                
                viewModel.SourcePath = fullPath;
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
        // GPT-5, 2026-08-05: Output selection is always a folder and does not alter the source-mode state.
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
                // GPT-5, 2026-08-05: Record picker entry for diagnosing platform-specific storage-provider failures.
                viewModel.CommandLog += "BrowseSaveFileAsync called\n";
            }
            
            var options = new FolderPickerOpenOptions
            {
                Title = L.SelectSaveFolder,
                AllowMultiple = false
            };
            
            // GPT-5, 2026-08-05: Use Avalonia StorageProvider rather than a platform-native API to keep selection behavior portable.
            var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
            
            if (DataContext is MainWindowViewModel viewModel2)
            {
                // GPT-5, 2026-08-05: Preserve the returned count because cancellation is represented by an empty collection.
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
        // GPT-5, 2026-08-05: This picker stores the actual TXT path, unlike the legacy SourceMode path builder above.
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
        // GPT-5, 2026-08-05: Keep this dialog self-contained so callers receive a completed Task only after the user acknowledges it.
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
        
        // GPT-5, 2026-08-05: Close with true so the modal Task has an explicit successful acknowledgement result.
        var button = (Button)((StackPanel)dialog.Content).Children[1];
        button.Click += (sender, e) => dialog.Close(true);
        
        return await dialog.ShowDialog<bool>(this);
    }
    
    private async Task<bool> ShowOkCancelMessageBoxAsync(string title, string message)
    {
        // GPT-5, 2026-08-05: Use an explicit boolean result to distinguish user cancellation from a failed picker operation.
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
