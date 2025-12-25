using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BatchCompress.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly ISystemIntegration _systemIntegration;
    private readonly BatchOperationService _batchOperationService;
    private CancellationTokenSource? _cancellationTokenSource;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrowseSourceButtonText))]
    private int _sourceMode; // 0 = from text file, 1 = from folder
    
    public string BrowseSourceButtonText => SourceMode == 0 ? "选txt" : "选择来源";
    
    public bool IsFromTxtMode => SourceMode == 0;
    
    [ObservableProperty]
    private string _sourcePath = string.Empty;
    
    [ObservableProperty]
    private string _saveFilePath = string.Empty;
    
    [ObservableProperty]
    private string _outputPath = string.Empty;
    
    [ObservableProperty]
    private string _textFilePath = string.Empty;
    
    [ObservableProperty]
    private string _extension = "rar";
    
    [ObservableProperty]
    private bool _useRandomPassword = true;
    
    [ObservableProperty]
    private string _customPassword = string.Empty;
    
    [ObservableProperty]
    private int _compressionLevel = 1; // 0-5
    
    [ObservableProperty]
    private bool _solidArchive = true;
    
    [ObservableProperty]
    private bool _enableVolume = true;
    
    [ObservableProperty]
    private string _volumeSize = "20";
    
    [ObservableProperty]
    private int _volumeUnit = 0; // 0=G, 1=M, 2=K
    
    [ObservableProperty]
    private int _existingFileMode = 2; // 0=Skip, 1=Update, 2=Overwrite
    
    [ObservableProperty]
    private bool _quickOpen = false;
    
    [ObservableProperty]
    private bool _testArchive = false;
    
    [ObservableProperty]
    private bool _enableComment = true;
    
    [ObservableProperty]
    private string _commentFilePath = ".\\注释.txt";
    
    [ObservableProperty]
    private string _tempDirectory = string.Empty;
    
    [ObservableProperty]
    private bool _skipAlreadyProcessed = true;
    
    [ObservableProperty]
    private bool _deleteSourceAfter = false;
    
    [ObservableProperty]
    private bool _moveSourceAfter = false;
    
    [ObservableProperty]
    private bool _addEnclosures = true;
    
    [ObservableProperty]
    private string _enclosureList = string.Empty;
    
    [ObservableProperty]
    private double _maxSizeGB = 666;
    
    [ObservableProperty]
    private bool _shutdownAfterComplete = false;
    
    [ObservableProperty]
    private string _currentFile = "Ready";
    
    [ObservableProperty]
    private int _successCount = 0;
    
    [ObservableProperty]
    private int _failCount = 0;
    
    [ObservableProperty]
    private int _ignoreCount = 0;
    
    [ObservableProperty]
    private int _nonExistCount = 0;
    
    [ObservableProperty]
    private double _processedSizeGB = 0;
    
    [ObservableProperty]
    private string _outputSizeText = "0.0GB";
    
    [ObservableProperty]
    private bool _isOperating = false;
    
    [ObservableProperty]
    private string _sourceFileList = string.Empty;
    
    [ObservableProperty]
    private string _successLog = string.Empty;
    
    [ObservableProperty]
    private string _failLog = string.Empty;
    
    [ObservableProperty]
    private string _commandLog = string.Empty;
    
    [ObservableProperty]
    private string _passwordQueryFileName = string.Empty;
    
    [ObservableProperty]
    private string _passwordQueryResult = string.Empty;
    
    [ObservableProperty]
    private bool _advancedFeaturesUnlocked = false;
    
    public MainWindowViewModel()
    {
        _archiveEngine = new RarArchiveEngine();
        _systemIntegration = new SystemIntegrationService();
        _batchOperationService = new BatchOperationService(_archiveEngine, _systemIntegration);
        
        // Initialize with default enclosure list
        EnclosureList = "c:\\【解压密码】发邮件给 qgkc520@Gmail.com\n" +
                       "c:\\【解压密码】微信号：i17269637581\n" +
                       "c:\\【解压密码】QQ号：2027123419\n" +
                       "c:\\【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";
        
        // Try to read clipboard on startup
        Task.Run(async () =>
        {
            var clipboardText = await _systemIntegration.ReadClipboardTextAsync();
            if (!string.IsNullOrEmpty(clipboardText) && Directory.Exists(clipboardText))
            {
                SourcePath = clipboardText;
            }
        });
    }
    
    // Callbacks for file browsing (set by the View)
    public Func<Task>? BrowseSourceRequested { get; set; }
    public Func<Task>? BrowseOutputRequested { get; set; }
    public Func<Task>? BrowseTextFileRequested { get; set; }
    public Func<Task>? BrowseSaveFileRequested { get; set; }
    
    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        if (BrowseSourceRequested != null)
        {
            await BrowseSourceRequested();
        }
    }
    
    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        if (BrowseOutputRequested != null)
        {
            await BrowseOutputRequested();
        }
    }
    
    [RelayCommand]
    private async Task BrowseTextFileAsync()
    {
        if (BrowseTextFileRequested != null)
        {
            await BrowseTextFileRequested();
        }
    }
    
    [RelayCommand]
    private async Task BrowseSaveFileAsync()
    {
        if (BrowseSaveFileRequested != null)
        {
            await BrowseSaveFileRequested();
        }
    }
    
    [RelayCommand]
    private async Task RefreshFileListAsync()
    {
        if (SourceMode == 0)
        {
            // Load from text file
            await LoadFromTextFileAsync();
        }
        else
        {
            // Load from folder
            await LoadFromFolderAsync();
        }
        
        // Update output size
        UpdateOutputSize();
    }
    
    private async Task LoadFromTextFileAsync()
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(TextFilePath) || !File.Exists(TextFilePath))
            {
                return;
            }
            
            var entries = _batchOperationService.LoadFilesFromTextFile(
                TextFilePath, SourcePath, Extension);
            
            var lines = new List<string>();
            foreach (var entry in entries)
            {
                lines.Add(entry.FilePath);
                lines.Add(entry.Password ?? string.Empty);
            }
            
            SourceFileList = string.Join(Environment.NewLine, lines);
        });
    }
    
    private async Task LoadFromFolderAsync()
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(SourcePath) || !Directory.Exists(SourcePath))
            {
                return;
            }
            
            var files = _batchOperationService.LoadFilesFromFolder(
                SourcePath, Extension, SkipAlreadyProcessed);
            
            SourceFileList = string.Join(Environment.NewLine, files);
        });
    }
    
    private void UpdateOutputSize()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            var size = _batchOperationService.CalculateTotalSizeGB(OutputPath, Extension);
            OutputSizeText = $"{size:F1}GB";
        }
    }
    
    [RelayCommand]
    private async Task CompressAsync()
    {
        if (IsOperating) return;
        
        try
        {
            IsOperating = true;
            ResetCounters();
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Parse source file list
            var sourcePaths = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            if (sourcePaths.Count == 0)
            {
                CommandLog += "No files to compress\n";
                return;
            }
            
            var options = BuildBatchOperationOptions();
            
            var progress = new Progress<OperationProgressInfo>(info =>
            {
                CurrentFile = info.CurrentFile;
                SuccessCount = info.SuccessCount;
                FailCount = info.FailCount;
                IgnoreCount = info.IgnoreCount;
                NonExistCount = info.NonExistCount;
                ProcessedSizeGB = info.ProcessedSizeGB;
                
                if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }
                
                CommandLog += info.Message + "\n";
            });
            
            await _batchOperationService.BatchCompressAsync(
                sourcePaths, options, progress, _cancellationTokenSource.Token);
            
            CommandLog += $"\nCompleted: Success={SuccessCount}, Fail={FailCount}, " +
                         $"Ignore={IgnoreCount}, NotFound={NonExistCount}\n";
        }
        catch (Exception ex)
        {
            CommandLog += $"Error: {ex.Message}\n";
        }
        finally
        {
            IsOperating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
    
    [RelayCommand]
    private async Task DecompressAsync()
    {
        if (IsOperating) return;
        
        try
        {
            IsOperating = true;
            ResetCounters();
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Parse source file list (with passwords if from text mode)
            List<FileEntry> entries;
            
            if (SourceMode == 0)
            {
                // From text file - alternating file/password lines
                entries = new List<FileEntry>();
                var lines = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                for (int i = 0; i < lines.Length; i += 2)
                {
                    var filePath = lines[i].Trim();
                    var password = i + 1 < lines.Length ? lines[i + 1].Trim() : null;
                    
                    if (File.Exists(filePath))
                    {
                        entries.Add(new FileEntry
                        {
                            FilePath = filePath,
                            Password = password,
                            FileSize = new FileInfo(filePath).Length
                        });
                    }
                }
            }
            else
            {
                // From folder - just file paths
                var files = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && File.Exists(s));
                
                entries = files.Select(f => new FileEntry
                {
                    FilePath = f,
                    Password = null,
                    FileSize = new FileInfo(f).Length
                }).ToList();
            }
            
            if (entries.Count == 0)
            {
                CommandLog += "No files to decompress\n";
                return;
            }
            
            var options = BuildBatchOperationOptions();
            
            var progress = new Progress<OperationProgressInfo>(info =>
            {
                CurrentFile = info.CurrentFile;
                SuccessCount = info.SuccessCount;
                FailCount = info.FailCount;
                IgnoreCount = info.IgnoreCount;
                NonExistCount = info.NonExistCount;
                ProcessedSizeGB = info.ProcessedSizeGB;
                
                if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }
                
                CommandLog += info.Message + "\n";
            });
            
            await _batchOperationService.BatchDecompressAsync(
                entries, options, progress, _cancellationTokenSource.Token);
            
            CommandLog += $"\nCompleted: Success={SuccessCount}, Fail={FailCount}, " +
                         $"Ignore={IgnoreCount}, NotFound={NonExistCount}\n";
        }
        catch (Exception ex)
        {
            CommandLog += $"Error: {ex.Message}\n";
        }
        finally
        {
            IsOperating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
    
    [RelayCommand]
    private void CancelOperation()
    {
        _cancellationTokenSource?.Cancel();
        CommandLog += "Cancelling operation...\n";
    }
    
    [RelayCommand]
    private void ClearLogs()
    {
        SourceFileList = string.Empty;
        SuccessLog = string.Empty;
        FailLog = string.Empty;
        CommandLog = string.Empty;
    }
    
    [RelayCommand]
    private async Task OpenOutputFolderAsync()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            await _systemIntegration.OpenFolderAsync(OutputPath);
        }
    }
    
    [RelayCommand]
    private async Task OpenSourceFolderAsync()
    {
        if (!string.IsNullOrEmpty(SourcePath) && Directory.Exists(SourcePath))
        {
            await _systemIntegration.OpenFolderAsync(SourcePath);
        }
    }
    
    [RelayCommand]
    private async Task QueryPasswordAsync()
    {
        await Task.Run(async () =>
        {
            var filename = PasswordQueryFileName + "." + Extension;
            
            // Generate multiple password variants
            var results = new List<string>();
            results.Add($"文件名: {filename}");
            results.Add($"压缩密码: {PasswordUtility.GenerateCompressionPassword(filename)}");
            results.Add($"解压密码: {PasswordUtility.GenerateDecompressionPassword(filename)}");
            results.Add($"UTF8-8位: {PasswordUtility.MD5UTF878(filename)}");
            results.Add($"UTF8-4位: {PasswordUtility.MD5UTF874(filename)}");
            results.Add($"GB2312-4位: {PasswordUtility.MD5GB2312(filename)}");
            
            var finalPassword = PasswordUtility.GenerateCompressionPassword(filename);
            PasswordQueryResult = finalPassword;
            
            CommandLog += string.Join("\n", results) + "\n";
            
            // Copy to clipboard
            await _systemIntegration.WriteClipboardTextAsync(finalPassword);
        });
    }
    
    [RelayCommand]
    private void UnlockAdvancedFeatures()
    {
        var unlockPassword = PasswordUtility.GenerateUnlockPassword();
        
        if (CustomPassword == unlockPassword)
        {
            AdvancedFeaturesUnlocked = true;
            EnclosureList = string.Empty;
            CommandLog += "Advanced features unlocked!\n";
        }
        else
        {
            CommandLog += "Password set successfully\n";
        }
    }
    
    [RelayCommand]
    private void SetOutputSameAsSource()
    {
        if (!string.IsNullOrEmpty(SourcePath))
        {
            OutputPath = SourcePath;
            TempDirectory = SourcePath;
        }
    }
    
    private void ResetCounters()
    {
        SuccessCount = 0;
        FailCount = 0;
        IgnoreCount = 0;
        NonExistCount = 0;
        ProcessedSizeGB = 0;
    }
    
    private BatchOperationOptions BuildBatchOperationOptions()
    {
        string[] volumeUnits = { "g", "m", "k" };
        
        return new BatchOperationOptions
        {
            SourcePath = SourcePath,
            OutputPath = OutputPath,
            Extension = Extension,
            UseRandomPassword = UseRandomPassword,
            CustomPassword = UseRandomPassword ? null : CustomPassword,
            DeleteSourceAfter = DeleteSourceAfter,
            MoveSourceAfter = MoveSourceAfter,
            SkipAlreadyProcessed = SkipAlreadyProcessed,
            MaxSizeGB = MaxSizeGB,
            ShutdownAfterComplete = ShutdownAfterComplete,
            CompressionLevel = (Core.Interfaces.CompressionLevel)CompressionLevel,
            SolidArchive = SolidArchive && CompressionLevel > 0,
            VolumeSize = EnableVolume ? VolumeSize : null,
            VolumeSizeUnit = EnableVolume ? volumeUnits[VolumeUnit] : null,
            QuickOpen = QuickOpen,
            TestArchive = TestArchive,
            CommentFile = EnableComment && File.Exists(CommentFilePath) ? CommentFilePath : null,
            TempDirectory = !string.IsNullOrEmpty(TempDirectory) ? TempDirectory : OutputPath,
            ExistingFileMode = (Core.Interfaces.ExistingFileMode)ExistingFileMode,
            RecoveryRecordPercent = 3,
            AddEnclosures = AddEnclosures,
            EnclosureDirectories = AddEnclosures ? 
                EnclosureList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) : null
        };
    }
    
    partial void OnSourceModeChanged(int value)
    {
        // Refresh file list when source mode changes
        Task.Run(async () => await RefreshFileListAsync());
    }
    
    partial void OnSourcePathChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            // Auto-refresh when path changes
            Task.Run(async () =>
            {
                await Task.Delay(500); // Debounce
                await RefreshFileListAsync();
            });
        }
    }
    
    partial void OnOutputPathChanged(string value)
    {
        UpdateOutputSize();
        
        if (!string.IsNullOrEmpty(value))
        {
            TempDirectory = value;
        }
    }
    
    partial void OnExtensionChanged(string value)
    {
        // Warn if trying to compress to 7z
        if (value.Equals("7z", StringComparison.OrdinalIgnoreCase))
        {
            CommandLog += "Warning: WinRAR cannot compress to 7z format, only extract. Consider using rar or zip.\n";
        }
    }
    
    partial void OnCompressionLevelChanged(int value)
    {
        // If "Store" (no compression) is selected, disable solid archive
        if (value == 0 && SolidArchive)
        {
            SolidArchive = false;
            CommandLog += "Solid archive disabled for Store mode\n";
        }
    }
    
    partial void OnDeleteSourceAfterChanged(bool value)
    {
        if (value && MoveSourceAfter)
        {
            MoveSourceAfter = false;
        }
    }
    
    partial void OnMoveSourceAfterChanged(bool value)
    {
        if (value && DeleteSourceAfter)
        {
            DeleteSourceAfter = false;
        }
    }
}
