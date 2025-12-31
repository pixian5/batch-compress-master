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
using BatchCompress.Avalonia.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BatchCompress.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly ISystemIntegration _systemIntegration;
    private readonly BatchOperationService _batchOperationService;
    private CancellationTokenSource? _cancellationTokenSource;
    
    // Localization support
    public LocalizationService Localization => LocalizationService.Instance;
    public LanguageStrings L => Localization.Strings;
    
    /// <summary>
    /// List of available languages for the dropdown.
    /// </summary>
    public List<KeyValuePair<string, string>> AvailableLanguages { get; } = 
        LocalizationService.AvailableLanguages.ToList();
    
    /// <summary>
    /// Currently selected language code.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(L))]
    [NotifyPropertyChangedFor(nameof(BrowseSourceButtonText))]
    [NotifyPropertyChangedFor(nameof(SourcePathWatermark))]
    [NotifyPropertyChangedFor(nameof(SourcePathLabel))]
    [NotifyPropertyChangedFor(nameof(SourceFileListTabHeader))]
    [NotifyPropertyChangedFor(nameof(SuccessLogTabHeader))]
    [NotifyPropertyChangedFor(nameof(FailLogTabHeader))]
    [NotifyPropertyChangedFor(nameof(CommandLogTabHeader))]
    [NotifyPropertyChangedFor(nameof(ProcessingSpeedDisplay))]
    private string _selectedLanguage = "zh-CN";
    
    partial void OnSelectedLanguageChanged(string value)
    {
        Localization.CurrentLanguage = value;
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrowseSourceButtonText))]
    [NotifyPropertyChangedFor(nameof(IsFromTxtMode))]
    private int _sourceMode; // 0 = from text file, 1 = from folder
    
    public string BrowseSourceButtonText => SourceMode == 0 ? L.SelectTxt : L.SelectDirectory;    
    
    public string SourcePathWatermark => SourceMode == 0 ? L.TxtPathWatermark : L.SavePathWatermark;    
    public string SourcePathLabel => SourceMode == 0 ? L.FromTxtMode : L.CompressFolderMode;
    
    public bool IsFromTxtMode => SourceMode == 0;
    
    // Tab header with item count
    public string SourceFileListTabHeader => $"{L.FileListTab} ({(string.IsNullOrEmpty(SourceFileList) ? 0 : SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length)})";
    public string SuccessLogTabHeader => $"{L.SuccessLogTab} ({(string.IsNullOrEmpty(SuccessLog) ? 0 : SuccessLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length)})";
    public string FailLogTabHeader => $"{L.FailLogTab} ({(string.IsNullOrEmpty(FailLog) ? 0 : FailLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length)})";
    public string CommandLogTabHeader => $"{L.CommandLogTab} ({(string.IsNullOrEmpty(CommandLog) ? 0 : CommandLog.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length)})";
    
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
    private double _totalSizeGB = 0;
    
    [ObservableProperty]
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    
    [ObservableProperty]
    private TimeSpan _remainingTime = TimeSpan.Zero;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProcessingSpeedDisplay))]
    private double _processingSpeedMBPerSecond = 0;
    
    /// <summary>
    /// Gets the processing speed display string with localized unit.
    /// </summary>
    public string ProcessingSpeedDisplay => $"{ProcessingSpeedMBPerSecond:0}{L.ProcessingSpeedUnit}";
    
    [ObservableProperty]
    private DateTime _estimatedCompletionTime = DateTime.MinValue;
    
    private DateTime _operationStartTime = DateTime.MinValue;
    
    [ObservableProperty]
    private string _outputSizeText = "0.0GB";
    
    [ObservableProperty]
    private bool _isOperating = false;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceFileListTabHeader))]
    private string _sourceFileList = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuccessLogTabHeader))]
    private string _successLog = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailLogTabHeader))]
    private string _failLog = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandLogTabHeader))]
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
        
        // Skip runtime-only initialization when in design mode
        if (Design.IsDesignMode) 
        {
            // Set backing fields directly to avoid triggering tasks in design mode
            _sourceMode = 1;
            _enclosureList = "【解压密码】发邮件给 qgkc520@Gmail.com\n" +
                           "【解压密码】微信号：i17269637581\n" +
                           "【解压密码】QQ号：2027123419\n" +
                           "【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";
            return;
        }

        // Initialize with default values for runtime
        SourceMode = 1;
        EnclosureList = "【解压密码】发邮件给 qgkc520@Gmail.com\n" +
                       "【解压密码】微信号：i17269637581\n" +
                       "【解压密码】QQ号：2027123419\n" +
                       "【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";

        // Try to read clipboard on startup (Runtime only)
        Task.Run(async () =>
        {
            try 
            {
                var clipboardText = await _systemIntegration.ReadClipboardTextAsync();
                if (!string.IsNullOrEmpty(clipboardText) && Directory.Exists(clipboardText))
                {
                    SaveFilePath = clipboardText;
                }
            }
            catch 
            {
                // Ignore clipboard errors at startup
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
            if (string.IsNullOrEmpty(SaveFilePath) || !Directory.Exists(SaveFilePath))
            {
                return;
            }
            
            var files = _batchOperationService.LoadFilesFromFolder(
                SaveFilePath, Extension, SkipAlreadyProcessed);
            
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
            _operationStartTime = DateTime.Now;
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Parse source file list
            var sourcePaths = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            // If no files in list, try to load them first
            if (sourcePaths.Count == 0)
            {
                CommandLog += "No files in list, trying to load automatically...\n";
                await RefreshFileListAsync();
                
                // Try again
                sourcePaths = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                    
                if (sourcePaths.Count == 0)
                {
                    CommandLog += "Still no files to compress\n";
                    return;
                }
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
                
                // Calculate processing statistics
                ElapsedTime = DateTime.Now - _operationStartTime;
                
                // Calculate processing speed (MB per second)
                if (ElapsedTime.TotalSeconds > 0.1 && ProcessedSizeGB > 0.01)
                {
                    ProcessingSpeedMBPerSecond = (ProcessedSizeGB * 1024) / ElapsedTime.TotalSeconds;
                }
                
                // Calculate remaining time and estimated completion
                if (ProcessingSpeedMBPerSecond > 0.01 && TotalSizeGB > 0.01 && ProcessedSizeGB < TotalSizeGB)
                {
                    double remainingSizeMB = (TotalSizeGB - ProcessedSizeGB) * 1024;
                    RemainingTime = TimeSpan.FromSeconds(remainingSizeMB / ProcessingSpeedMBPerSecond);
                    EstimatedCompletionTime = DateTime.Now + RemainingTime;
                }
                else
                {
                    RemainingTime = TimeSpan.Zero;
                    EstimatedCompletionTime = DateTime.MinValue;
                }
                
                if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }
                
                // Only add command messages to CommandLog (those starting with [压缩命令] or [解压命令])
                if (info.Message.StartsWith("[压缩命令]") || info.Message.StartsWith("[解压命令]"))
                {
                    CommandLog += info.Message + "\n";
                }
            });
            
            await _batchOperationService.BatchCompressAsync(
                sourcePaths, options, progress, _cancellationTokenSource.Token);
            
            CommandLog += $"\n完成: 成功={SuccessCount}, 失败={FailCount}, " +
                         $"忽略={IgnoreCount}, 未找到={NonExistCount}\n";
                         
            _systemIntegration.ShowNotification("压缩完成", $"成功: {SuccessCount}, 失败: {FailCount}");
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
            _operationStartTime = DateTime.Now;
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Parse source file list (with passwords if from text mode)
            List<FileEntry> entries;
            
            // If source file list is empty, try to load automatically
            if (string.IsNullOrEmpty(SourceFileList.Trim()))
            {
                CommandLog += "No files in list, trying to load automatically...\n";
                await RefreshFileListAsync();
                
                // If still empty after refresh, show error
                if (string.IsNullOrEmpty(SourceFileList.Trim()))
                {
                    CommandLog += "Still no files to decompress\n";
                    return;
                }
            }
            
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
                
                // Calculate processing statistics
                ElapsedTime = DateTime.Now - _operationStartTime;
                
                // Calculate processing speed (MB per second)
                if (ElapsedTime.TotalSeconds > 0.1 && ProcessedSizeGB > 0.01)
                {
                    ProcessingSpeedMBPerSecond = (ProcessedSizeGB * 1024) / ElapsedTime.TotalSeconds;
                }
                
                // Calculate remaining time and estimated completion
                if (ProcessingSpeedMBPerSecond > 0.01 && TotalSizeGB > 0.01 && ProcessedSizeGB < TotalSizeGB)
                {
                    double remainingSizeMB = (TotalSizeGB - ProcessedSizeGB) * 1024;
                    RemainingTime = TimeSpan.FromSeconds(remainingSizeMB / ProcessingSpeedMBPerSecond);
                    EstimatedCompletionTime = DateTime.Now + RemainingTime;
                }
                else
                {
                    RemainingTime = TimeSpan.Zero;
                    EstimatedCompletionTime = DateTime.MinValue;
                }
                
                if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }
                
                // Only add command messages to CommandLog (those starting with [压缩命令] or [解压命令])
                if (info.Message.StartsWith("[压缩命令]") || info.Message.StartsWith("[解压命令]"))
                {
                    CommandLog += info.Message + "\n";
                }
            });
            
            await _batchOperationService.BatchDecompressAsync(
                entries, options, progress, _cancellationTokenSource.Token);
            
            CommandLog += $"\n完成: 成功={SuccessCount}, 失败={FailCount}, " +
                         $"忽略={IgnoreCount}, 未找到={NonExistCount}\n";
                         
            _systemIntegration.ShowNotification("解压完成", $"成功: {SuccessCount}, 失败: {FailCount}");
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
        if (!string.IsNullOrEmpty(SaveFilePath))
        {
            OutputPath = SaveFilePath;
            TempDirectory = SaveFilePath;
        }
    }
    
    private void ResetCounters()
    {
        SuccessCount = 0;
        FailCount = 0;
        IgnoreCount = 0;
        NonExistCount = 0;
        ProcessedSizeGB = 0;
        
        // Reset processing statistics
        ElapsedTime = TimeSpan.Zero;
        RemainingTime = TimeSpan.Zero;
        ProcessingSpeedMBPerSecond = 0;
        EstimatedCompletionTime = DateTime.MinValue;
        _operationStartTime = DateTime.MinValue;
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
            // Only update for TXT mode
            if (SourceMode == 0)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(500); // Debounce
                    await RefreshFileListAsync();
                    await UpdateTotalSizeAsync();
                });
            }
        }
        else
        {
            if (SourceMode == 0)
            {
                TotalSizeGB = 0;
            }
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
    
    partial void OnSaveFilePathChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Task.Run(async () =>
            {
                await Task.Delay(500); // Debounce
                await RefreshFileListAsync();
                await UpdateTotalSizeAsync();
            });
        }
        else
        {
            TotalSizeGB = 0;
        }
    }
    
    private async Task UpdateTotalSizeAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                // For both modes, use SaveFilePath as the source path
                string targetPath = SaveFilePath;
                
                if (!string.IsNullOrEmpty(targetPath) && Directory.Exists(targetPath))
                {
                    // Calculate total size of all files in the directory
                    var size = CalculateDirectorySize(targetPath);
                    TotalSizeGB = size / (1024.0 * 1024.0 * 1024.0); // Convert bytes to GB
                }
                else
                {
                    TotalSizeGB = 0;
                }
            }
            catch (Exception ex)
            {
                CommandLog += $"Error calculating total size: {ex.Message}\n";
                TotalSizeGB = 0;
            }
        });
    }
    
    private long CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;
        
        long size = 0;
        
        try
        {
            // Get all files in the directory
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            
            // Calculate total size
            foreach (string file in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    size += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    CommandLog += $"Error getting file size for {file}: {ex.Message}\n";
                }
            }
        }
        catch (Exception ex)
        {
            CommandLog += $"Error accessing directory {path}: {ex.Message}\n";
        }
        
        return size;
    }
}
