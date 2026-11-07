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
    // GPT-5, 2026-09-30：GUI 的文件日志器（设计器模式下为 null），把服务层诊断写入用户数据目录 logs。
    private readonly FileLoggerService? _fileLogger;
    // GLM-5.3, 2026-11-04：批次 9——附件配置服务（构造可注入，测试用显式临时路径隔离用户目录）。
    private readonly EnclosureConfigService _enclosureConfig;
    // GLM-5.3, 2026-11-04：批次 2 阶段 3——通知节流等会话状态迁至 OperationSession；取消令牌仍由 VM 持有。
    private CancellationTokenSource? _cancellationTokenSource;
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
        // GPT-5, 2026-09-30：密码依据选项改用本地化词条，随界面语言刷新。
        PasswordNameModeOptions.Add(string.Format(L.PasswordNameModeFileNameWithExtension, normalizedExtension));
        PasswordNameModeOptions.Add(L.PasswordNameModeFileName);
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
    public string SourceFileListTabHeader => $"{L.FileListTab} ({SourceCollector.SplitLines(SourceFileList).Count})";
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

    // GLM-5.3, 2026-11-04：批次 5——受控并发度（1-16，默认 1 串行），
    // 两个操作页签共享同一设置（机器级参数，不参与页签状态切换）。
    [ObservableProperty]
    private int _maxConcurrency = 1;

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

    // GPT-5, 2026-09-30：GUI 日志写到与窗口设置相同的用户数据目录（macOS 由 ApplicationData 映射），
    // 不写入应用包内部；日志是辅助能力，创建失败不能阻止窗口启动。
    private static FileLoggerService? CreateGuiFileLogger()
    {
        try
        {
            var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(applicationData))
            {
                applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            var baseDirectory = string.IsNullOrWhiteSpace(applicationData) ? AppContext.BaseDirectory : applicationData;
            var logPath = Path.Combine(
                baseDirectory,
                "BatchCompress.Avalonia",
                "logs",
                $"batchcompress_gui_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log");
            return new FileLoggerService(logPath);
        }
        catch
        {
            return null;
        }
    }

    public MainWindowViewModel(EnclosureConfigService? enclosureConfig = null)
    {
        // GLM-5.3, 2026-11-04：批次 9——附件配置服务可注入；默认行为与批次 8 一致。
        _enclosureConfig = enclosureConfig ?? new EnclosureConfigService();

        // GPT-5, 2026-08-06：按归档格式选择 WinRAR/RAR 或官方 7zz，界面不直接依赖具体命令行工具。
        _archiveEngine = new ArchiveEngineRouter();
        _systemIntegration = new SystemIntegrationService();
        // GPT-5, 2026-09-30：GUI 把文件日志器注入批处理服务，使后处理失败原因等诊断留痕；设计器模式不写日志。
        _fileLogger = Design.IsDesignMode ? null : CreateGuiFileLogger();
        _batchOperationService = new BatchOperationService(_archiveEngine, _systemIntegration, _fileLogger);

        // GPT-5, 2026-08-05：在绑定渲染前填充可观察选项集合，并在语言变化时刷新。
        RefreshAllDropdownOptions();

        // 设计器模式不执行运行时初始化。
        if (Design.IsDesignMode)
        {
            // 直接设置字段，避免设计器加载时触发异步任务。
            _sourceMode = 1;
            // GLM-5.3, 2026-11-04：批次 8——联系方式外置到 EnclosureConfigService；
            // 设计器只取默认模版（纯字符串，不做任何文件 IO）。
            _enclosureList = EnclosureConfigService.GetDefaultEnclosuresText();
            return;
        }

        // 检测并设置系统语言。
        DetectSystemLanguage();

        // 设置运行时默认值。
        SourceMode = 1;
        // GLM-5.3, 2026-11-04：批次 8——附件联系方式不再硬编码：
        // 优先读用户配置文件（~/.config/batchcompress/enclosures.txt 或应用目录相邻），
        // 不存在或为空时回退默认联系文案模版。
        // GLM-5.3, 2026-11-04：批次 9——改用注入的配置服务实例（测试可隔离）。
        EnclosureList = _enclosureConfig.LoadEnclosuresText();

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
            CommandLog += L.Warning7zFormat + "\n";
        }
    }

    partial void OnCompressionLevelChanged(int value)
    {
        // 选择存储模式时关闭固实压缩，因为该组合没有意义。
        if (value == 0 && SolidArchive)
        {
            SolidArchive = false;
            CommandLog += L.SolidDisabledForStore + "\n";
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

}
