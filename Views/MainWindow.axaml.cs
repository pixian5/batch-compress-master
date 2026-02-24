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
    private static readonly string WindowSettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "window-settings.json");
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
            // Ignore invalid settings and continue with defaults.
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
            // Ignore save failures to avoid blocking app shutdown.
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
        if (e.DataTransfer.TryGetFiles() is IEnumerable<IStorageItem> files && DataContext is MainWindowViewModel viewModel)
        {
            foreach (var file in files)
            {
                var firstPath = file.Path.LocalPath;
                if (string.IsNullOrEmpty(firstPath)) continue;
                
                // 濡傛灉鎷栧叆鐨勬槸鏂囦欢澶?
                if (Directory.Exists(firstPath))
                {
                    viewModel.SaveFilePath = firstPath;
                    viewModel.CommandLog += L.DroppedFolder + firstPath + "\n";
                    break; // 鍙鐞嗙涓€涓?
                }
                // 濡傛灉鎷栧叆鐨勬槸TXT鏂囦欢
                else if (File.Exists(firstPath) && firstPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.SourcePath = firstPath;
                    viewModel.CommandLog += L.DroppedTxtFile + firstPath + "\n";
                    break; // 鍙鐞嗙涓€涓?
                }
            }
        }
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Handle file browsing since we need UI context
            viewModel.BrowseSourceRequested = BrowseSourceAsync;
            viewModel.BrowseOutputRequested = BrowseOutputAsync;
            viewModel.BrowseTextFileRequested = BrowseTextFileAsync;
            viewModel.BrowseSaveFileRequested = BrowseSaveFileAsync;
        }
    }
    
    private async Task BrowseSourceAsync()
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        
        if (viewModel.SourceMode == 0) // 浠巘xt璇诲彇瑕佽В鍘嬬殑鏂囦欢
        {
            // 妫€鏌ヤ繚瀛樿矾寰勬槸鍚︿负绌?
            if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
            {
                // 鎻愮ず鐢ㄦ埛閫夋嫨淇濆瓨鐩綍
                bool shouldContinue = await ShowOkCancelMessageBoxAsync(L.Hint, L.SelectSaveDirectory);
                
                if (shouldContinue)
                {
                    // 妯℃嫙鐐瑰嚮閫夋嫨鐩綍鎸夐挳
                    await BrowseSaveFileAsync();
                    
                    // 濡傛灉鐢ㄦ埛鍙栨秷浜嗙洰褰曢€夋嫨锛岀洿鎺ヨ繑鍥?
                    if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
                        return;
                }
                else
                {
                    // 鐢ㄦ埛鍙栨秷浜嗘彁绀猴紝鐩存帴杩斿洖
                    return;
                }
            }
            
            // 纭繚淇濆瓨璺緞浠ョ洰褰曞垎闅旂缁撳熬
            string savePath = viewModel.SaveFilePath;
            if (!savePath.EndsWith(Path.DirectorySeparatorChar) && 
                !savePath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                savePath += Path.DirectorySeparatorChar;
            }
            

            
            // 璁╃敤鎴烽€夋嫨瀵嗙爜鏂囦欢
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
                // 瀵嗙爜鏂囦欢鐨勬枃浠跺悕锛堜笉鍚矾寰勶級
                string passwordFileName = Path.GetFileName(files[0].Path.LocalPath);
                
                // 鎷兼帴瀹屾暣璺緞
                string fullPath = Path.Combine(savePath, passwordFileName);
                
                // 璁剧疆鍒癝ourcePath
                viewModel.SourcePath = fullPath;
            }
        }
        else // 鍘嬬缉姝ゆ枃浠跺す鍐呮墍鏈夋枃浠?
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
                // 娣诲姞璋冭瘯鏃ュ織
                viewModel.CommandLog += "BrowseSaveFileAsync called\n";
            }
            
            var options = new FolderPickerOpenOptions
            {
                Title = L.SelectSaveFolder,
                AllowMultiple = false
            };
            
            // 鍦ˋvalonia 11涓紝搴旇浣跨敤StorageProvider鐨勬纭疄渚?
            var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
            
            if (DataContext is MainWindowViewModel viewModel2)
            {
                // 娣诲姞璋冭瘯鏃ュ織
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
        
        // Set up button click handler after dialog is created
        var button = (Button)((StackPanel)dialog.Content).Children[1];
        button.Click += (sender, e) => dialog.Close(true);
        
        return await dialog.ShowDialog<bool>(this);
    }
    
    private async Task<bool> ShowOkCancelMessageBoxAsync(string title, string message)
    {
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

