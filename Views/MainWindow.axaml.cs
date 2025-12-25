using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Metadata;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using BatchCompress.Avalonia.ViewModels;

namespace BatchCompress.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
                bool shouldContinue = await ShowOkCancelMessageBoxAsync("提示", "请选择待解压文件保存目录");
                
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
            
            // 提示用户选择密码本txt文件
            await ShowMessageBoxAsync("提示", "请选择密码本txt文件");
            
            // 让用户选择密码文件
            var fileOptions = new FilePickerOpenOptions
            {
                Title = "选择密码本TXT文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
                }
            };
            
            var files = await StorageProvider.OpenFilePickerAsync(fileOptions);
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
                Title = "选择来源文件夹",
                AllowMultiple = false
            };
            
            var folders = await StorageProvider.OpenFolderPickerAsync(options);
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
            Title = "选择输出文件夹",
            AllowMultiple = false
        };
        
        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OutputPath = folders[0].Path.LocalPath;
        }
    }
    
    private async Task BrowseSaveFileAsync()
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "选择待压缩/解压文件保存目录",
            AllowMultiple = false
        };
        
        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveFilePath = folders[0].Path.LocalPath;
        }
    }
    
    private async Task BrowseTextFileAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "选择文本文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
            }
        };
        
        var files = await StorageProvider.OpenFilePickerAsync(options);
        if (files.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TextFilePath = files[0].Path.LocalPath;
        }
    }
    
    private async Task<bool> ShowMessageBoxAsync(string title, string message)
    {
        Window dialog = null;
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
                        Content = "确定",
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
        Window dialog = null;
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
                            new Button { Content = "确定", Width = 100 },
                            new Button { Content = "取消", Width = 100 }
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