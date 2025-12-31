using System;
using System.IO;
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
    
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        
        // Setup auto-scroll for CommandLog
        this.Loaded += MainWindow_Loaded;
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
        if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files && DataContext is MainWindowViewModel viewModel)
        {
            foreach (var file in files)
            {
                var firstPath = file.Path.LocalPath;
                if (string.IsNullOrEmpty(firstPath)) continue;
                
                // 如果拖入的是文件夹
                if (Directory.Exists(firstPath))
                {
                    viewModel.SaveFilePath = firstPath;
                    viewModel.CommandLog += L.DroppedFolder + firstPath + "\n";
                    break; // 只处理第一个
                }
                // 如果拖入的是TXT文件
                else if (File.Exists(firstPath) && firstPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.SourcePath = firstPath;
                    viewModel.CommandLog += L.DroppedTxtFile + firstPath + "\n";
                    break; // 只处理第一个
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
        
        if (viewModel.SourceMode == 0) // 从txt读取要解压的文件
        {
            // 检查保存路径是否为空
            if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
            {
                // 提示用户选择保存目录
                bool shouldContinue = await ShowOkCancelMessageBoxAsync(L.Hint, L.SelectSaveDirectory);
                
                if (shouldContinue)
                {
                    // 模拟点击选择目录按钮
                    await BrowseSaveFileAsync();
                    
                    // 如果用户取消了目录选择，直接返回
                    if (string.IsNullOrWhiteSpace(viewModel.SaveFilePath))
                        return;
                }
                else
                {
                    // 用户取消了提示，直接返回
                    return;
                }
            }
            
            // 确保保存路径以目录分隔符结尾
            string savePath = viewModel.SaveFilePath;
            if (!savePath.EndsWith(Path.DirectorySeparatorChar) && 
                !savePath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                savePath += Path.DirectorySeparatorChar;
            }
            

            
            // 让用户选择密码文件
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
                // 密码文件的文件名（不含路径）
                string passwordFileName = Path.GetFileName(files[0].Path.LocalPath);
                
                // 拼接完整路径
                string fullPath = Path.Combine(savePath, passwordFileName);
                
                // 设置到SourcePath
                viewModel.SourcePath = fullPath;
            }
        }
        else // 压缩此文件夹内所有文件
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
                // 添加调试日志
                viewModel.CommandLog += "BrowseSaveFileAsync called\n";
            }
            
            var options = new FolderPickerOpenOptions
            {
                Title = L.SelectSaveFolder,
                AllowMultiple = false
            };
            
            // 在Avalonia 11中，应该使用StorageProvider的正确实例
            var folders = await this.StorageProvider.OpenFolderPickerAsync(options);
            
            if (DataContext is MainWindowViewModel viewModel2)
            {
                // 添加调试日志
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
}