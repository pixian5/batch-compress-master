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
using Avalonia.Threading;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;
using BatchCompress.Avalonia.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BatchCompress.Avalonia.ViewModels;

// GPT-5, 2026-08-05：持有主窗口全部可绑定状态，并将 UI 选择转换为 BatchOperationOptions。
// 仅界面相关的操作通过回调注入，使本类保持可测试且不依赖 Avalonia 控件。
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly ISystemIntegration _systemIntegration;
    private readonly BatchOperationService _batchOperationService;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _lastProgressNotification = DateTime.MinValue;
    private int _lastNotifiedCompletedCount;
    private double _lastNotifiedProcessedSizeGB;
    private readonly OperationTabState _compressionTabState = new();
    private readonly OperationTabState _decompressionTabState = new();
    private bool _isSwitchingOperationTab;
    private int _lastOperationTab;

    private sealed class OperationTabState
    {
        public int SourceMode { get; set; } = 1;
        public string SourcePath { get; set; } = string.Empty;
        public string SaveFilePath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string SourceFileList { get; set; } = string.Empty;
        public string Extension { get; set; } = "rar";
        public bool UseRandomPassword { get; set; } = true;
        public string CustomPassword { get; set; } = string.Empty;
        public int PasswordNameMode { get; set; }
        public int ExistingFileMode { get; set; } = 2;
        public bool SkipAlreadyProcessed { get; set; } = true;
        public bool DeleteSourceAfter { get; set; }
        public bool MoveSourceAfter { get; set; }
        public double MaxSizeGB { get; set; } = 666;
        public bool ShutdownAfterComplete { get; set; }
        public string PasswordQueryFileName { get; set; } = string.Empty;
        public string PasswordQueryResult { get; set; } = string.Empty;
        public int CompressionLevel { get; set; } = 1;
        public bool SolidArchive { get; set; } = true;
        public bool EnableVolume { get; set; } = true;
        public string VolumeSize { get; set; } = "20";
        public int VolumeUnit { get; set; }
        public int RecoveryRecordPercent { get; set; }
        public bool LockArchive { get; set; }
        public bool QuickOpen { get; set; }
        public bool TestArchive { get; set; }
        public bool EnableComment { get; set; } = true;
        public string CommentFilePath { get; set; } = "注释.txt";
        public string TempDirectory { get; set; } = string.Empty;
        public bool AddEnclosures { get; set; } = true;
        public string EnclosureList { get; set; } = string.Empty;
    }

    // 本地化支持。
    public LocalizationService Localization => LocalizationService.Instance;
    public LanguageStrings L => Localization.Strings;

    /// <summary>
    /// 下拉框可用的语言列表。
    /// </summary>
    public List<KeyValuePair<string, string>> AvailableLanguages { get; } =
        LocalizationService.AvailableLanguages.ToList();

    /// <summary>
    /// 当前选中的语言代码。
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
        // 修改本地化服务的当前语言；服务会替换全部界面字符串。
        Localization.CurrentLanguage = value;

        // 强制通知全部依赖本地化字符串的绑定，确保界面立即刷新。
        OnPropertyChanged(nameof(L));

        // 使用 ObservableCollection 重新填充下拉选项，触发控件刷新。
        RefreshAllDropdownOptions();

        // 显式通知所有依赖 L 的计算属性。
        OnPropertyChanged(nameof(BrowseSourceButtonText));
        OnPropertyChanged(nameof(SourcePathWatermark));
        OnPropertyChanged(nameof(SourcePathLabel));
        OnPropertyChanged(nameof(SourceFileListTabHeader));
        OnPropertyChanged(nameof(SuccessLogTabHeader));
        OnPropertyChanged(nameof(FailLogTabHeader));
        OnPropertyChanged(nameof(CommandLogTabHeader));
        OnPropertyChanged(nameof(ProcessingSpeedDisplay));

        // 通知本地化服务的订阅者，使外部绑定也能收到语言切换事件。
        Localization.NotifyPropertyChanged(nameof(LocalizationService.Strings));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrowseSourceButtonText))]
    [NotifyPropertyChangedFor(nameof(IsFromTxtMode))]
    private int _sourceMode = 1; // 0 = 当前页 TXT 来源，1 = 当前页目录来源

    /// <summary>
    /// 顶部一级导航：0=压缩配置，1=解压配置，2=开始，3=成功记录，4=失败记录，5=命令日志。
    /// 开始页的两个操作按钮分别使用各自配置页保存的状态。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompressionTab))]
    [NotifyPropertyChangedFor(nameof(IsDecompressionTab))]
    [NotifyPropertyChangedFor(nameof(IsStartTab))]
    [NotifyPropertyChangedFor(nameof(IsSuccessLogTab))]
    [NotifyPropertyChangedFor(nameof(IsFailLogTab))]
    [NotifyPropertyChangedFor(nameof(IsCommandLogTab))]
    [NotifyPropertyChangedFor(nameof(IsOperationTab))]
    [NotifyPropertyChangedFor(nameof(SourcePathLabel))]
    private int _activeTab;

    public bool IsCompressionTab => ActiveTab == 0;
    public bool IsDecompressionTab => ActiveTab == 1;
    public bool IsStartTab => ActiveTab == 2;
    public bool IsSuccessLogTab => ActiveTab == 3;
    public bool IsFailLogTab => ActiveTab == 4;
    public bool IsCommandLogTab => ActiveTab == 5;
    public bool IsOperationTab => IsCompressionTab || IsDecompressionTab;
    public bool IsCompressionActionVisible => IsCompressionTab || IsStartTab;
    public bool IsDecompressionActionVisible => IsDecompressionTab || IsStartTab;

    private int CurrentOperationTab => ActiveTab is 0 or 1 ? ActiveTab : _lastOperationTab;

    private bool IsCurrentOperationTab(int operationTab) =>
        ActiveTab == operationTab || (IsStartTab && _lastOperationTab == operationTab);

    public string BrowseSourceButtonText => SourceMode == 0 ? L.SelectTxt : L.SelectDirectory;

    public string SourcePathWatermark => SourceMode == 0 ? L.TxtPathWatermark : L.SavePathWatermark;
    public string SourcePathLabel => CurrentOperationTab == 0
        ? SourceMode == 0 ? L.CompressionTxtMode : L.CompressFolderMode
        : SourceMode == 0 ? L.FromTxtMode : L.DecompressFolderMode;

    /// <summary>
    /// 随语言切换动态更新的来源模式选项。
    /// </summary>
    public ObservableCollection<string> SourceModeOptions { get; } = new();

    /// <summary>
    /// 随语言切换动态更新的压缩级别选项。
    /// </summary>
    public ObservableCollection<string> CompressionLevelOptions { get; } = new();

    /// <summary>
    /// 随语言切换动态更新的已有文件处理选项。
    /// </summary>
    public ObservableCollection<string> ExistingFileModeOptions { get; } = new();

    /// <summary>
    /// 随语言切换刷新绑定的分卷单位选项。
    /// 单位文本虽然不随语言变化，但仍使用 ObservableCollection 保证控件正确刷新。
    /// </summary>
    public ObservableCollection<string> VolumeUnitOptions { get; } = new();

    public ObservableCollection<string> PasswordNameModeOptions { get; } = new();

    // GPT-5, 2026-08-07：密码依据提示跟随当前归档扩展名变化，但选项刷新不能改变用户已经选择的模式。
    private void RefreshPasswordNameModeOptions()
    {
        var currentPasswordNameMode = PasswordNameMode;
        var normalizedExtension = Extension.Trim().TrimStart('.').ToLowerInvariant();
        if (normalizedExtension.Length == 0)
        {
            normalizedExtension = "rar";
        }

        PasswordNameModeOptions.Clear();
        PasswordNameModeOptions.Add($"文件名.{normalizedExtension}");
        PasswordNameModeOptions.Add("文件名");
        PasswordNameMode = currentPasswordNameMode >= 0 && currentPasswordNameMode < PasswordNameModeOptions.Count
            ? currentPasswordNameMode
            : 0;
    }

    /// <summary>
    /// 使用当前语言重新填充全部下拉选项，确保 ComboBox 显示文本同步。
    /// </summary>
    private void RefreshAllDropdownOptions()
    {
        // 保存当前选择，重新填充后恢复有效索引。
        var currentSourceMode = SourceMode;
        var currentCompressionLevel = CompressionLevel;
        var currentExistingFileMode = ExistingFileMode;
        var currentVolumeUnit = VolumeUnit;

        // 当前操作页只展示与自身业务匹配的 TXT 和目录来源。
        RefreshSourceModeOptions(CurrentOperationTab, currentSourceMode);

        // 清空并重新填充压缩级别选项。
        CompressionLevelOptions.Clear();
        CompressionLevelOptions.Add(L.NoCompression);
        CompressionLevelOptions.Add(L.Light);
        CompressionLevelOptions.Add(L.Fast);
        CompressionLevelOptions.Add(L.Standard);
        CompressionLevelOptions.Add(L.Better);
        CompressionLevelOptions.Add(L.Best);

        // 清空并重新填充已有文件处理选项。
        ExistingFileModeOptions.Clear();
        ExistingFileModeOptions.Add(L.SkipExisting);
        ExistingFileModeOptions.Add(L.UpdateExisting);
        ExistingFileModeOptions.Add(L.OverwriteExisting);

        // 清空并重新填充分卷单位选项。
        VolumeUnitOptions.Clear();
        VolumeUnitOptions.Add("GB");
        VolumeUnitOptions.Add("MB");
        VolumeUnitOptions.Add("KB");

        RefreshPasswordNameModeOptions();

        // 集合重建后恢复有效索引，使 ComboBox 显示正确文本。
        CompressionLevel = currentCompressionLevel >= 0 && currentCompressionLevel < CompressionLevelOptions.Count ? currentCompressionLevel : 0;
        ExistingFileMode = currentExistingFileMode >= 0 && currentExistingFileMode < ExistingFileModeOptions.Count ? currentExistingFileMode : 0;
        VolumeUnit = currentVolumeUnit >= 0 && currentVolumeUnit < VolumeUnitOptions.Count ? currentVolumeUnit : 0;
    }

    private void RefreshSourceModeOptions(int tab, int requestedMode)
    {
        SourceModeOptions.Clear();
        if (tab == 1)
        {
            SourceModeOptions.Add(L.FromTxtMode);
            SourceModeOptions.Add(L.DecompressFolderMode);
        }
        else
        {
            SourceModeOptions.Add(L.CompressionTxtMode);
            SourceModeOptions.Add(L.CompressFolderMode);
        }

        SourceMode = requestedMode is 0 or 1 ? requestedMode : 1;
    }

    public bool IsFromTxtMode => SourceMode == 0;

    // 标签页标题附带当前条目数量。
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
    private int _passwordNameMode;

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
    private int _recoveryRecordPercent;

    [ObservableProperty]
    private int _existingFileMode = 2; // 0=Skip, 1=Update, 2=Overwrite

    [ObservableProperty]
    private bool _lockArchive;

    [ObservableProperty]
    private bool _quickOpen = false;

    [ObservableProperty]
    private bool _testArchive = false;

    [ObservableProperty]
    private bool _enableComment = true;

    [ObservableProperty]
    private string _commentFilePath = "注释.txt";

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
    private bool _isShutdownScheduled;

    [ObservableProperty]
    private string _currentFile = "Ready";

    [ObservableProperty]
    private string _currentSourcePath = string.Empty;

    [ObservableProperty]
    private int _successCount = 0;

    [ObservableProperty]
    private int _failCount = 0;

    [ObservableProperty]
    private int _postProcessFailCount = 0;

    [ObservableProperty]
    private int _ignoreCount = 0;

    [ObservableProperty]
    private int _nonExistCount = 0;

    [ObservableProperty]
    private int _incompleteVolumeCount = 0;

    [ObservableProperty]
    private int _ambiguousArchiveCount = 0;

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
    /// 返回带本地化单位的处理速度显示文本。
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

    public MainWindowViewModel()
    {
        // GPT-5, 2026-08-06：按归档格式选择 WinRAR/RAR 或官方 7zz，界面不直接依赖具体命令行工具。
        _archiveEngine = new ArchiveEngineRouter();
        _systemIntegration = new SystemIntegrationService();
        _batchOperationService = new BatchOperationService(_archiveEngine, _systemIntegration);

        // GPT-5, 2026-08-05：在绑定渲染前填充可观察选项集合，并在语言变化时刷新。
        RefreshAllDropdownOptions();

        // 设计器模式不执行运行时初始化。
        if (Design.IsDesignMode)
        {
            // 直接设置字段，避免设计器加载时触发异步任务。
            _sourceMode = 1;
            _enclosureList = "【解压密码】发邮件给 qgkc520@Gmail.com\n" +
                           "【解压密码】微信号：i17269637581\n" +
                           "【解压密码】QQ号：2027123419\n" +
                           "【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";
            return;
        }

        // 检测并设置系统语言。
        DetectSystemLanguage();

        // 设置运行时默认值。
        SourceMode = 1;
        EnclosureList = "【解压密码】发邮件给 qgkc520@Gmail.com\n" +
                       "【解压密码】微信号：i17269637581\n" +
                       "【解压密码】QQ号：2027123419\n" +
                       "【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";

        // GPT-5, 2026-08-05：仅在剪贴板文本是现有目录时采用，绝不把任意复制文本作为路径。
        Dispatcher.UIThread.Post(async () =>
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
                // 启动时读取剪贴板失败不应阻止应用打开。
            }
        });
    }

    /// <summary>
    /// 检测系统语言并设置对应的界面语言。
    /// </summary>
    private void DetectSystemLanguage()
    {
        try
        {
            var systemCulture = System.Globalization.CultureInfo.CurrentUICulture;
            var cultureName = systemCulture.Name;
            var twoLetterISOLanguageName = systemCulture.TwoLetterISOLanguageName;

            // 将系统区域性映射到应用支持的语言。
            string languageCode;
            if (twoLetterISOLanguageName == "zh")
            {
                // 中文根据区域代码区分简体和繁体。
                if (cultureName == "zh-TW" || cultureName == "zh-HK" || cultureName == "zh-MO" || cultureName == "zh-Hant")
                {
                    languageCode = "zh-TW"; // 繁体中文。
                }
                else
                {
                    languageCode = "zh-CN"; // 简体中文。
                }
            }
            else if (twoLetterISOLanguageName == "ja")
            {
                languageCode = "ja"; // 日语。
            }
            else if (twoLetterISOLanguageName == "de")
            {
                languageCode = "de"; // 德语。
            }
            else if (twoLetterISOLanguageName == "en")
            {
                languageCode = "en"; // 英语。
            }
            else
            {
                // 未匹配时默认使用简体中文。
                languageCode = "zh-CN";
            }

            // 只有在资源存在时才切换语言。
            if (LocalizationService.AvailableLanguages.ContainsKey(languageCode))
            {
                SelectedLanguage = languageCode;
            }
        }
        catch (System.Globalization.CultureNotFoundException ex)
        {
            // 区域性检测失败时保留简体中文默认值。
            System.Diagnostics.Debug.WriteLine($"Language detection failed: {ex.Message}");
        }
    }

    // 由视图设置的文件选择回调。
    public Func<Task>? BrowseSourceRequested { get; set; }
    public Func<Task>? BrowseOutputRequested { get; set; }
    public Func<Task>? BrowseTextFileRequested { get; set; }
    public Func<Task>? BrowseSaveFileRequested { get; set; }
    public Func<Task>? BrowseAttachmentRequested { get; set; }
    public Func<Task>? ShowHelpRequested { get; set; }
    public Action? HideWindowRequested { get; set; }
    public Func<Task<bool>>? ConfirmShutdownCancellationRequested { get; set; }

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
    private async Task BrowseAttachmentAsync()
    {
        if (BrowseAttachmentRequested != null)
        {
            await BrowseAttachmentRequested();
        }
    }

    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        if (ShowHelpRequested != null)
        {
            await ShowHelpRequested();
        }
    }

    [RelayCommand]
    private async Task RefreshFileListAsync()
    {
        var operationTab = CurrentOperationTab;
        var sourceMode = SourceMode;

        if (sourceMode == 0)
        {
            // 从 TXT 清单加载。
            await LoadFromTextFileAsync(operationTab);
        }
        else
        {
            // 从目录扫描加载。
            await LoadFromFolderAsync(operationTab);
        }

        // 刷新输出目录的已有大小统计。
        if (IsCurrentOperationTab(operationTab) && SourceMode == sourceMode)
        {
            UpdateOutputSize();
        }
    }

    private async Task LoadFromTextFileAsync(int operationTab)
    {
        var textPath = SourcePath;
        var saveFilePath = SaveFilePath;
        var extension = Extension;
        var result = await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(textPath) || !File.Exists(textPath))
            {
                return new TextFileImportResult();
            }

            if (operationTab == 0)
            {
                return _batchOperationService.LoadCompressionPathsFromTextFile(textPath);
            }

            return _batchOperationService.LoadFilesFromTextFileWithDiagnostics(
                textPath, saveFilePath, extension);
        });

        if (!IsCurrentOperationTab(operationTab) || SourceMode != 0)
        {
            return;
        }

        if (operationTab == 0)
        {
            SourceFileList = string.Join(Environment.NewLine, result.Paths);
            AppendTextImportDiagnostics(result, false);
            return;
        }

        var lines = new List<string>();
        foreach (var entry in result.Entries)
        {
            lines.Add(entry.FilePath);
            lines.Add(entry.Password ?? string.Empty);
        }

        SourceFileList = string.Join(Environment.NewLine, lines);
        AppendTextImportDiagnostics(result, true);
    }

    // GPT-5, 2026-08-06：导入诊断写入命令日志，保留旧版的匹配统计和分卷提示，但不阻塞批处理。
    private void AppendTextImportDiagnostics(TextFileImportResult result, bool passwordBook)
    {
        var type = passwordBook ? "密码本" : "压缩路径清单";
        var count = passwordBook ? result.Entries.Count : result.Paths.Count;
        var sizeGB = result.MatchedBytes / (1024.0 * 1024.0 * 1024.0);
        var estimatedSeconds = result.MatchedBytes / (1024.0 * 1024.0) / 40.0;
        CommandLog += $"[{type}] 请求={result.RequestedCount}，已匹配={count}，大小={sizeGB:F3} GB，预计约 {estimatedSeconds:F1} 秒\n";

        if (result.MissingEntries.Count > 0)
        {
            CommandLog += $"[{type}] 未找到 {result.MissingEntries.Count} 项:\n" +
                          string.Join(Environment.NewLine, result.MissingEntries) + Environment.NewLine;
        }

        if (result.IncompleteVolumes.Count > 0)
        {
            CommandLog += $"[{type}] 分卷不完整 {result.IncompleteVolumes.Count} 项:\n" +
                          string.Join(Environment.NewLine, result.IncompleteVolumes) + Environment.NewLine;
        }

        if (result.AmbiguousEntries.Count > 0 || result.DuplicateVolumeEntries.Count > 0)
        {
            var ambiguous = result.AmbiguousEntries.Concat(result.DuplicateVolumeEntries);
            CommandLog += $"[{type}] 名称或编号存在歧义 {ambiguous.Count()} 项:\n" +
                          string.Join(Environment.NewLine, ambiguous) + Environment.NewLine;
        }

        if (!passwordBook)
        {
            return;
        }

        if (result.UnmatchedArchives.Count > 0)
        {
            CommandLog += $"[密码本] 以下归档未在密码本找到，共 {result.UnmatchedArchives.Count} 个:\n" +
                          string.Join(Environment.NewLine, result.UnmatchedArchives) + Environment.NewLine;
        }

        if (result.VolumeCandidates.Count > 0)
        {
            CommandLog += $"[密码本] 以下归档疑似分卷，共 {result.VolumeCandidates.Count} 个:\n" +
                          string.Join(Environment.NewLine, result.VolumeCandidates) + Environment.NewLine;
        }
    }

    private async Task LoadFromFolderAsync(int operationTab)
    {
        var saveFilePath = SaveFilePath;
        var extension = Extension;
        var skipAlreadyProcessed = SkipAlreadyProcessed;
        var files = await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(saveFilePath) || !Directory.Exists(saveFilePath))
            {
                return new List<string>();
            }

            return operationTab == 0
                ? _batchOperationService.LoadCompressionSourcesFromFolder(
                    saveFilePath, skipAlreadyProcessed)
                : _batchOperationService.LoadArchivesFromFolder(
                    saveFilePath, extension, skipAlreadyProcessed);
        });

        if (IsCurrentOperationTab(operationTab) && SourceMode == 1)
        {
            SourceFileList = string.Join(Environment.NewLine, files);
        }
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

        if (!IsCompressionActionVisible)
        {
            CommandLog += "压缩命令只能从压缩配置页或开始页启动。\n";
            return;
        }
        ActivateOperationStateForStart(0);

        if (ExistingFileMode == 1 && LockArchive)
        {
            const string message = "更新现有文件不能与锁定归档同时使用。请取消其中一个选项。";
            CommandLog += $"[选项冲突] {message}\n";
            _systemIntegration.ShowNotification("选项冲突", message);
            return;
        }

        try
        {
            IsOperating = true;
            ResetCounters();
            _operationStartTime = DateTime.Now;

            _cancellationTokenSource = new CancellationTokenSource();

            // GPT-5, 2026-08-05：处理前统一规范按行输入；空行绝不能成为归档任务。
            var sourcePaths = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s) && !SystemMetadataFileFilter.ShouldSkip(s))
                .ToList();

            // 列表为空时先尝试自动加载。
            if (sourcePaths.Count == 0)
            {
                CommandLog += "列表中没有文件，正在自动加载...\n";
                await RefreshFileListAsync();

                // 自动加载后再次检查列表。
                sourcePaths = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && !SystemMetadataFileFilter.ShouldSkip(s))
                    .ToList();

                if (sourcePaths.Count == 0)
                {
                    CommandLog += "仍然没有要压缩的文件\n";
                    return;
                }
            }

            // GPT-5, 2026-08-05：在操作开始时快照选项，避免处理中修改影响在途任务。
            var options = BuildBatchOperationOptions();

            var progress = new Progress<OperationProgressInfo>(info =>
            {
                CurrentFile = info.CurrentFile;
                CurrentSourcePath = info.CurrentSourcePath;
                SuccessCount = info.SuccessCount;
                FailCount = info.FailCount;
                PostProcessFailCount = info.PostProcessFailCount;
                IgnoreCount = info.IgnoreCount;
                NonExistCount = info.NonExistCount;
                IncompleteVolumeCount = info.IncompleteVolumeCount;
                AmbiguousArchiveCount = info.AmbiguousArchiveCount;
                ProcessedSizeGB = info.ProcessedSizeGB;

                // 更新处理统计。
                ElapsedTime = DateTime.Now - _operationStartTime;

                // 根据已处理大小和耗时计算 MB/秒。
                if (ElapsedTime.TotalSeconds > 0.1 && ProcessedSizeGB > 0.01)
                {
                    ProcessingSpeedMBPerSecond = (ProcessedSizeGB * 1024) / ElapsedTime.TotalSeconds;
                }

                // 根据当前速度估算剩余时间和完成时刻。
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

                MaybeShowProgressNotification("压缩", info);

                // GPT-5, 2026-08-06：底层归档输出只进入命令日志，避免 stdout/stderr 被重复计入成功或失败记录。
                var isCommandOutput = info.Message.StartsWith("[压缩命令]") || info.Message.StartsWith("[解压命令]");
                if (isCommandOutput)
                {
                    CommandLog += info.Message + "\n";
                }
                else if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }

            });

            await _batchOperationService.BatchCompressAsync(
                sourcePaths, options, progress, _cancellationTokenSource.Token);

            await OfferShutdownCancellationAsync(options);

            CommandLog += $"\n完成: 成功={SuccessCount}, 归档失败={FailCount}, 后处理失败={PostProcessFailCount}, " +
                         $"忽略={IgnoreCount}, 未找到={NonExistCount}, 分卷不完整={IncompleteVolumeCount}, 歧义={AmbiguousArchiveCount}\n";

            _systemIntegration.ShowNotification("压缩完成", $"成功: {SuccessCount}, 归档失败: {FailCount}, 后处理失败: {PostProcessFailCount}");
        }
        catch (OperationCanceledException)
        {
            CommandLog += "压缩已取消\n";
            _systemIntegration.ShowNotification("压缩已取消", $"成功: {SuccessCount}, 失败: {FailCount}");
        }
        catch (Exception ex)
        {
            CommandLog += $"Error: {ex.Message}\n";
        }
        finally
        {
            IsOperating = false;
            CurrentSourcePath = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task DecompressAsync()
    {
        if (IsOperating) return;

        if (!IsDecompressionActionVisible)
        {
            CommandLog += "解压命令只能从解压配置页或开始页启动。\n";
            return;
        }
        ActivateOperationStateForStart(1);

        try
        {
            IsOperating = true;
            ResetCounters();
            _operationStartTime = DateTime.Now;

            _cancellationTokenSource = new CancellationTokenSource();

            // 解析来源列表；解压密码本模式包含交替密码行。
            List<FileEntry> entries;

            // 列表为空时先尝试自动加载。
            if (string.IsNullOrEmpty(SourceFileList.Trim()))
            {
                CommandLog += "No files in list, trying to load automatically...\n";
                await RefreshFileListAsync();

                // 自动加载后仍为空时记录错误并返回。
                if (string.IsNullOrEmpty(SourceFileList.Trim()))
                {
                    CommandLog += "Still no files to decompress\n";
                    return;
                }
            }

            if (SourceMode == 0)
            {
                // 密码本模式按文件行、密码行交替解析。
                entries = new List<FileEntry>();
                var lines = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < lines.Length; i += 2)
                {
                    var filePath = lines[i].Trim();
                    var password = i + 1 < lines.Length ? lines[i + 1].Trim() : null;

                    if (!SystemMetadataFileFilter.ShouldSkip(filePath) && File.Exists(filePath))
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
                // 其他模式只有文件路径。
                var files = SourceFileList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) &&
                                !SystemMetadataFileFilter.ShouldSkip(s) &&
                                File.Exists(s) &&
                                ArchiveVolumeResolver.MatchesFormat(s, Extension));

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
                CurrentSourcePath = info.CurrentSourcePath;
                SuccessCount = info.SuccessCount;
                FailCount = info.FailCount;
                PostProcessFailCount = info.PostProcessFailCount;
                IgnoreCount = info.IgnoreCount;
                NonExistCount = info.NonExistCount;
                IncompleteVolumeCount = info.IncompleteVolumeCount;
                AmbiguousArchiveCount = info.AmbiguousArchiveCount;
                ProcessedSizeGB = info.ProcessedSizeGB;

                // 更新处理统计。
                ElapsedTime = DateTime.Now - _operationStartTime;

                // 根据已处理大小和耗时计算 MB/秒。
                if (ElapsedTime.TotalSeconds > 0.1 && ProcessedSizeGB > 0.01)
                {
                    ProcessingSpeedMBPerSecond = (ProcessedSizeGB * 1024) / ElapsedTime.TotalSeconds;
                }

                // 根据速度估算剩余时间和完成时刻。
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

                MaybeShowProgressNotification("解压", info);

                // GPT-5, 2026-08-06：解压进程的原始输出与压缩使用同一分类规则。
                var isCommandOutput = info.Message.StartsWith("[压缩命令]") || info.Message.StartsWith("[解压命令]");
                if (isCommandOutput)
                {
                    CommandLog += info.Message + "\n";
                }
                else if (info.IsError)
                {
                    FailLog += info.Message + "\n";
                }
                else
                {
                    SuccessLog += info.Message + "\n";
                }

            });

            await _batchOperationService.BatchDecompressAsync(
                entries, options, progress, _cancellationTokenSource.Token);

            await OfferShutdownCancellationAsync(options);

            CommandLog += $"\n完成: 成功={SuccessCount}, 归档失败={FailCount}, 后处理失败={PostProcessFailCount}, " +
                         $"忽略={IgnoreCount}, 未找到={NonExistCount}, 分卷不完整={IncompleteVolumeCount}, 歧义={AmbiguousArchiveCount}\n";

            _systemIntegration.ShowNotification("解压完成", $"成功: {SuccessCount}, 归档失败: {FailCount}, 后处理失败: {PostProcessFailCount}");
        }
        catch (OperationCanceledException)
        {
            CommandLog += "解压已取消\n";
            _systemIntegration.ShowNotification("解压已取消", $"成功: {SuccessCount}, 失败: {FailCount}");
        }
        catch (Exception ex)
        {
            CommandLog += $"Error: {ex.Message}\n";
        }
        finally
        {
            IsOperating = false;
            CurrentSourcePath = string.Empty;
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

    // GPT-5, 2026-08-07：批处理服务已向系统提交一分钟后的关机请求；GUI 随即提供确认框和持续可用的取消按钮。
    // 无界面命令行不显示此交互，仍由操作系统的关机命令语义负责。
    private async Task OfferShutdownCancellationAsync(BatchOperationOptions options)
    {
        if (!options.ShutdownAfterComplete)
        {
            return;
        }

        IsShutdownScheduled = true;
        CommandLog += "已请求系统在一分钟后关机，可使用“取消关机”撤销。\n";
        _systemIntegration.ShowNotification("关机计划", "系统将在一分钟后关机。可以在应用中取消。");

        if (ConfirmShutdownCancellationRequested != null && await ConfirmShutdownCancellationRequested())
        {
            await CancelScheduledShutdownAsync();
        }
    }

    [RelayCommand]
    private async Task CancelScheduledShutdownAsync()
    {
        if (!IsShutdownScheduled)
        {
            return;
        }

        await _systemIntegration.CancelShutdownAsync();
        IsShutdownScheduled = false;
        CommandLog += "已请求取消关机。\n";
        _systemIntegration.ShowNotification("已取消关机", "已向系统发送取消关机请求。");
    }

    [RelayCommand]
    private void ClearLogs()
    {
        ClearAllLogs();
    }

    [RelayCommand]
    private void ClearSourceList()
    {
        SourceFileList = string.Empty;
        if (CurrentOperationTab == 0)
        {
            _compressionTabState.SourceFileList = string.Empty;
        }
        else
        {
            _decompressionTabState.SourceFileList = string.Empty;
        }
    }

    [RelayCommand]
    private void ClearSuccessLog() => SuccessLog = string.Empty;

    [RelayCommand]
    private void ClearFailLog() => FailLog = string.Empty;

    [RelayCommand]
    private void ClearCommandLog() => CommandLog = string.Empty;

    [RelayCommand]
    private void ClearAllLogs()
    {
        ClearAllSourceLists();
        SourceFileList = string.Empty;
        ClearSuccessLog();
        ClearFailLog();
        ClearCommandLog();
    }

    private void ClearAllSourceLists()
    {
        _compressionTabState.SourceFileList = string.Empty;
        _decompressionTabState.SourceFileList = string.Empty;
    }

    [RelayCommand]
    private void HideWindow()
    {
        HideWindowRequested?.Invoke();
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
        var sourceDirectory = !string.IsNullOrWhiteSpace(SaveFilePath) && Directory.Exists(SaveFilePath)
            ? SaveFilePath
            : File.Exists(SourcePath) ? Path.GetDirectoryName(Path.GetFullPath(SourcePath)) : SourcePath;

        if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            await _systemIntegration.OpenFolderAsync(sourceDirectory);
        }
    }

    [RelayCommand]
    private async Task QueryPasswordAsync()
    {
        var filename = PasswordQueryFileName + "." + Extension;
            var passwordName = PasswordUtility.GetPasswordSourceName(
                filename,
                PasswordNameMode == (int)Core.Models.PasswordNameMode.BaseName
                    ? Core.Models.PasswordNameMode.BaseName
                    : Core.Models.PasswordNameMode.ArchiveName);

            // 生成多种历史兼容密码候选。
            var results = new List<string>();
            results.Add($"归档名: {filename}");
            results.Add($"密码依据: {passwordName}");
            results.Add($"压缩密码: {PasswordUtility.GenerateCompressionPassword(passwordName)}");
            results.Add($"解压密码: {PasswordUtility.GenerateDecompressionPassword(passwordName)}");
            results.Add($"UTF8-8位: {PasswordUtility.MD5UTF878(filename)}");
            results.Add($"UTF8-4位: {PasswordUtility.MD5UTF874(filename)}");
            results.Add($"GB2312-4位: {PasswordUtility.MD5GB2312(filename)}");
            results.Add("旧版兼容密码:");
            results.AddRange(PasswordUtility.GetLegacyPasswordCandidates(filename));

            var finalPassword = CurrentOperationTab == 0
                ? PasswordUtility.GenerateCompressionPassword(passwordName)
                : PasswordUtility.GenerateDecompressionPassword(passwordName);
            PasswordQueryResult = finalPassword;

            CommandLog += string.Join("\n", results) + "\n";

        // 将最终候选密码复制到剪贴板。
        await _systemIntegration.WriteClipboardTextAsync(finalPassword);
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
        PostProcessFailCount = 0;
        IgnoreCount = 0;
        NonExistCount = 0;
        IncompleteVolumeCount = 0;
        AmbiguousArchiveCount = 0;
        ProcessedSizeGB = 0;

        // 重置处理统计。
        ElapsedTime = TimeSpan.Zero;
        RemainingTime = TimeSpan.Zero;
        ProcessingSpeedMBPerSecond = 0;
        EstimatedCompletionTime = DateTime.MinValue;
        _operationStartTime = DateTime.MinValue;
        _lastProgressNotification = DateTime.MinValue;
        _lastNotifiedCompletedCount = 0;
        _lastNotifiedProcessedSizeGB = 0;
    }

    // GPT-5, 2026-08-06：后台通知只在首个完成项、每 10 项、每增加 1GB 或每 5 分钟发送一次。
    // 这样隐藏窗口运行时仍能看到阶段性进度，同时避免大量归档产生通知风暴。
    private void MaybeShowProgressNotification(string operation, OperationProgressInfo info)
    {
        var completedCount = info.SuccessCount + info.FailCount + info.IgnoreCount;
        if (completedCount <= 0)
        {
            return;
        }

        var now = DateTime.Now;
        var first = _lastProgressNotification == DateTime.MinValue;
        var countMilestone = completedCount >= _lastNotifiedCompletedCount + 10;
        var sizeMilestone = info.ProcessedSizeGB >= _lastNotifiedProcessedSizeGB + 1;
        var timeMilestone = !first && now - _lastProgressNotification >= TimeSpan.FromMinutes(5);
        if (!first && !countMilestone && !sizeMilestone && !timeMilestone)
        {
            return;
        }

        _lastProgressNotification = now;
        _lastNotifiedCompletedCount = completedCount;
        _lastNotifiedProcessedSizeGB = info.ProcessedSizeGB;
        var remaining = RemainingTime > TimeSpan.Zero ? $"，剩余约 {RemainingTime:hh\\:mm\\:ss}" : string.Empty;
        _systemIntegration.ShowNotification(
            $"{operation}进行中",
            $"已处理 {completedCount} 项，成功 {info.SuccessCount}，归档失败 {info.FailCount}，后处理失败 {info.PostProcessFailCount}，当前：{info.CurrentFile}{remaining}");
    }

    private BatchOperationOptions BuildBatchOperationOptions()
    {
        // GPT-5, 2026-08-05：转换为引擎枚举或 WinRAR 单位前，钳制所有由索引驱动的控件值。
        string[] volumeUnits = { "g", "m", "k" };
        var volumeUnitIndex = VolumeUnit >= 0 && VolumeUnit < volumeUnits.Length ? VolumeUnit : 0;

        var clampedCompressionLevel = CompressionLevel >= 0 && CompressionLevel <= 5
            ? CompressionLevel : 3;
        var clampedExistingFileMode = ExistingFileMode >= 0 && ExistingFileMode <= 2
            ? ExistingFileMode : 2;

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
            PasswordNameMode = PasswordNameMode == (int)Core.Models.PasswordNameMode.BaseName
                ? Core.Models.PasswordNameMode.BaseName
                : Core.Models.PasswordNameMode.ArchiveName,
            CompressionLevel = (Core.Interfaces.CompressionLevel)clampedCompressionLevel,
            SolidArchive = SolidArchive && clampedCompressionLevel > 0,
            VolumeSize = EnableVolume ? VolumeSize : null,
            VolumeSizeUnit = EnableVolume ? volumeUnits[volumeUnitIndex] : null,
            QuickOpen = QuickOpen,
            TestArchive = TestArchive,
            CommentFile = EnableComment && File.Exists(CommentFilePath) ? CommentFilePath : null,
            TempDirectory = !string.IsNullOrEmpty(TempDirectory) ? TempDirectory : OutputPath,
            ExistingFileMode = (Core.Interfaces.ExistingFileMode)clampedExistingFileMode,
            RecoveryRecordPercent = Math.Clamp(RecoveryRecordPercent, 0, 100),
            LockArchive = LockArchive,
            AddEnclosures = AddEnclosures,
            EnclosureDirectories = AddEnclosures ?
                EnclosureList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) : null
        };
    }

    partial void OnActiveTabChanged(int oldValue, int newValue)
    {
        if (oldValue is 0 or 1)
        {
            SaveOperationTabState(oldValue == 0 ? _compressionTabState : _decompressionTabState);
            SetLastOperationTab(oldValue);
        }

        if (newValue is not (0 or 1))
        {
            RefreshSourceModeOptions(_lastOperationTab, SourceMode);
            OnPropertyChanged(nameof(SourcePathLabel));
            return;
        }

        SetLastOperationTab(newValue);
        LoadOperationTabState(newValue);

        OnPropertyChanged(nameof(SourcePathLabel));
        QueueRefresh(includeTotalSize: true);
    }

    private void ActivateOperationStateForStart(int operationTab)
    {
        if (!IsStartTab || operationTab is not (0 or 1) || _lastOperationTab == operationTab)
        {
            return;
        }

        SaveOperationTabState(_lastOperationTab == 0 ? _compressionTabState : _decompressionTabState);
        SetLastOperationTab(operationTab);
        LoadOperationTabState(operationTab);
        OnPropertyChanged(nameof(SourcePathLabel));
    }

    private void LoadOperationTabState(int operationTab)
    {
        var state = operationTab == 0 ? _compressionTabState : _decompressionTabState;
        _isSwitchingOperationTab = true;
        try
        {
            RefreshSourceModeOptions(operationTab, state.SourceMode);
            SourcePath = state.SourcePath;
            SaveFilePath = state.SaveFilePath;
            OutputPath = state.OutputPath;
            SourceFileList = state.SourceFileList;
            Extension = state.Extension;
            UseRandomPassword = state.UseRandomPassword;
            CustomPassword = state.CustomPassword;
            PasswordNameMode = state.PasswordNameMode;
            ExistingFileMode = state.ExistingFileMode;
            SkipAlreadyProcessed = state.SkipAlreadyProcessed;
            DeleteSourceAfter = state.DeleteSourceAfter;
            MoveSourceAfter = state.MoveSourceAfter;
            MaxSizeGB = state.MaxSizeGB;
            ShutdownAfterComplete = state.ShutdownAfterComplete;
            PasswordQueryFileName = state.PasswordQueryFileName;
            PasswordQueryResult = state.PasswordQueryResult;
            CompressionLevel = state.CompressionLevel;
            SolidArchive = state.SolidArchive;
            EnableVolume = state.EnableVolume;
            VolumeSize = state.VolumeSize;
            VolumeUnit = state.VolumeUnit;
            RecoveryRecordPercent = state.RecoveryRecordPercent;
            LockArchive = state.LockArchive;
            QuickOpen = state.QuickOpen;
            TestArchive = state.TestArchive;
            EnableComment = state.EnableComment;
            CommentFilePath = state.CommentFilePath;
            TempDirectory = state.TempDirectory;
            AddEnclosures = state.AddEnclosures;
            EnclosureList = state.EnclosureList;
        }
        finally
        {
            _isSwitchingOperationTab = false;
        }
    }

    private void SetLastOperationTab(int operationTab)
    {
        if (operationTab is not (0 or 1) || _lastOperationTab == operationTab)
        {
            return;
        }

        _lastOperationTab = operationTab;
        OnPropertyChanged(nameof(IsCompressionActionVisible));
        OnPropertyChanged(nameof(IsDecompressionActionVisible));
        OnPropertyChanged(nameof(SourcePathLabel));
    }

    private void SaveOperationTabState(OperationTabState state)
    {
        state.SourceMode = SourceMode;
        state.SourcePath = SourcePath;
        state.SaveFilePath = SaveFilePath;
        state.OutputPath = OutputPath;
        state.SourceFileList = SourceFileList;
        state.Extension = Extension;
        state.UseRandomPassword = UseRandomPassword;
        state.CustomPassword = CustomPassword;
        state.PasswordNameMode = PasswordNameMode;
        state.ExistingFileMode = ExistingFileMode;
        state.SkipAlreadyProcessed = SkipAlreadyProcessed;
        state.DeleteSourceAfter = DeleteSourceAfter;
        state.MoveSourceAfter = MoveSourceAfter;
        state.MaxSizeGB = MaxSizeGB;
        state.ShutdownAfterComplete = ShutdownAfterComplete;
        state.PasswordQueryFileName = PasswordQueryFileName;
        state.PasswordQueryResult = PasswordQueryResult;
        state.CompressionLevel = CompressionLevel;
        state.SolidArchive = SolidArchive;
        state.EnableVolume = EnableVolume;
        state.VolumeSize = VolumeSize;
        state.VolumeUnit = VolumeUnit;
        state.RecoveryRecordPercent = RecoveryRecordPercent;
        state.LockArchive = LockArchive;
        state.QuickOpen = QuickOpen;
        state.TestArchive = TestArchive;
        state.EnableComment = EnableComment;
        state.CommentFilePath = CommentFilePath;
        state.TempDirectory = TempDirectory;
        state.AddEnclosures = AddEnclosures;
        state.EnclosureList = EnclosureList;
    }

    partial void OnSourceModeChanged(int value)
    {
        if (_isSwitchingOperationTab)
        {
            return;
        }

        // 来源模式改变后重新加载文件列表。
        QueueRefresh(includeTotalSize: false);
    }

    partial void OnSourcePathChanged(string value)
    {
        if (_isSwitchingOperationTab)
        {
            return;
        }

        if (!string.IsNullOrEmpty(value))
        {
            // 当前页的 TXT 来源在路径改变后重新解析清单。
            if (SourceMode == 0)
            {
                QueueRefresh(includeTotalSize: true, delay: true);
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
        if (_isSwitchingOperationTab)
        {
            return;
        }

        UpdateOutputSize();

        if (!string.IsNullOrEmpty(value))
        {
            TempDirectory = value;
        }
    }

    partial void OnExtensionChanged(string value)
    {
        RefreshPasswordNameModeOptions();

        if (_isSwitchingOperationTab)
        {
            return;
        }

        // GPT-5, 2026-08-06：7z 已由官方 7zz 完整支持；这里只提示该格式不具备 RAR 专属恢复记录和快速打开能力。
        if (value.Trim().TrimStart('.').Equals("7z", StringComparison.OrdinalIgnoreCase))
        {
            CommandLog += "7z 使用官方 7-Zip；恢复记录、快速打开和 RAR 注释选项不会应用。\n";
        }
    }

    partial void OnCompressionLevelChanged(int value)
    {
        // 选择存储模式时关闭固实压缩，因为该组合没有意义。
        if (value == 0 && SolidArchive)
        {
            SolidArchive = false;
            CommandLog += "Solid archive disabled for Store mode\n";
        }
    }

    partial void OnDeleteSourceAfterChanged(bool value)
    {
        if (_isSwitchingOperationTab)
        {
            return;
        }

        if (value && MoveSourceAfter)
        {
            MoveSourceAfter = false;
        }
    }

    partial void OnMoveSourceAfterChanged(bool value)
    {
        if (_isSwitchingOperationTab)
        {
            return;
        }

        if (value && DeleteSourceAfter)
        {
            DeleteSourceAfter = false;
        }
    }

    partial void OnSaveFilePathChanged(string value)
    {
        if (_isSwitchingOperationTab)
        {
            return;
        }

        if (!string.IsNullOrEmpty(value))
        {
            QueueRefresh(includeTotalSize: true, delay: true);
        }
        else
        {
            TotalSizeGB = 0;
        }
    }

    private async Task UpdateTotalSizeAsync()
    {
        var targetPath = SaveFilePath;
        var calculation = await Task.Run(() => CalculateDirectorySize(targetPath));
        if (!string.Equals(SaveFilePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        TotalSizeGB = calculation.Size / (1024.0 * 1024.0 * 1024.0);
        foreach (var error in calculation.Errors)
        {
            CommandLog += error + "\n";
        }
    }

    private static (long Size, List<string> Errors) CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return (0, []);

        long size = 0;
        var errors = new List<string>();

        try
        {
            // 枚举目录中的候选文件。
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(file => !SystemMetadataFileFilter.ShouldSkip(file))
                .ToArray();

            // 汇总候选文件大小并换算为 GB。
            foreach (string file in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    size += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error getting file size for {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error accessing directory {path}: {ex.Message}");
        }

        return (size, errors);
    }

    private void QueueRefresh(bool includeTotalSize, bool delay = false)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (delay)
            {
                await Task.Delay(500);
            }

            await RefreshFileListAsync();
            if (includeTotalSize)
            {
                await UpdateTotalSizeAsync();
            }
        });
    }
}
