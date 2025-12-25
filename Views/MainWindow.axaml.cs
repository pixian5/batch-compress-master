using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        }
    }
    
    private async Task BrowseSourceAsync()
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "选择来源文件夹",
            AllowMultiple = false
        };
        
        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SourcePath = folders[0].Path.LocalPath;
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
}