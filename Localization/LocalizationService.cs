using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BatchCompress.Avalonia.Localization;

/// <summary>
/// 管理当前语言并提供本地化字符串。
/// </summary>
// GPT-5, 2026-08-05：单例语言协调器。它发布完整的 LanguageStrings 实例，并向主窗口选择器提供支持的语言标签。
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();
    
    private LanguageStrings _strings;
    private string _currentLanguage = "zh-CN";

    private static string WithVersion(string title)
    {
        var version = typeof(LocalizationService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?.Split('+')[0];
        return string.IsNullOrWhiteSpace(version) ? title : $"{title} v{version}";
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private LocalizationService()
    {
        _strings = CreateChineseSimplified();
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// 对外通知属性改变。
    /// </summary>
    public void NotifyPropertyChanged(string? propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }
    
    /// <summary>
    /// 带显示名称的可用语言。
    /// </summary>
    public static Dictionary<string, string> AvailableLanguages { get; } = new()
    {
        { "zh-CN", "简体中文" },
        { "zh-TW", "繁體中文" },
        { "en", "English" },
        { "ja", "日本語" },
        { "de", "Deutsch" }
    };
    
    /// <summary>
    /// 获取或设置当前语言代码。
    /// </summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            // GPT-5, 2026-08-05：拒绝未知语言代码，避免绑定观察到未完整初始化的语言对象。
            if (_currentLanguage != value && AvailableLanguages.ContainsKey(value))
            {
                _currentLanguage = value;
                _strings = value switch
                {
                    "zh-CN" => CreateChineseSimplified(),
                    "zh-TW" => CreateChineseTraditional(),
                    "en" => CreateEnglish(),
                    "ja" => CreateJapanese(),
                    "de" => CreateGerman(),
                    _ => CreateChineseSimplified()
                };
                OnPropertyChanged();
                OnPropertyChanged(nameof(Strings));
                _strings.RaiseAllPropertiesChanged();
            }
        }
    }
    
    /// <summary>
    /// 获取当前本地化字符串集合。
    /// </summary>
    public LanguageStrings Strings
    {
        get => _strings;
        private set
        {
            _strings = value;
            OnPropertyChanged();
        }
    }
    
    private static LanguageStrings CreateChineseSimplified()
    {
        return new LanguageStrings
        {
            // 窗口标题。
            WindowTitle = WithVersion("批量压缩解压工具"),
            
            // 语言选择器。
            LanguageLabel = "语言：",
            
            // 来源与目标区域。
            SourceAndDestination = "来源与目标",
            FromTxtMode = "从txt读取要解压的文件：",
            CompressionTxtMode = "从txt读取待压缩路径：",
            CompressFolderMode = "压缩此文件夹内所有文件：",
            DecompressFolderMode = "解压此文件夹内的归档：",
            SavePathWatermark = "待压缩/解压文件保存路径",
            SelectDirectory = "选择目录",
            TxtPathWatermark = "TXT文件的路径",
            SelectTxt = "选txt",
            SameAsAbove = "同上",
            DestinationWatermark = "目的地",
            
            // 压缩选项区域。
            CompressionOptions = "压缩选项",
            DecompressionOptions = "解压选项",
            FileNameLabel = "文件名（不含扩展名）：",
            QueryPassword = "查询密码",
            RandomPassword = "随机密码",
            CustomPasswordWatermark = "自定义密码",
            CopiedToClipboard = "（已复制到剪贴板）",
            ExtensionLabel = "扩展名：",
            Verify = "校验",
            CompressionLevelLabel = "压缩率：",
            NoCompression = "不压缩",
            Light = "轻度",
            Fast = "快速",
            Standard = "标准",
            Better = "较好",
            Best = "最佳",
            Solid = "固实",
            QuickOpen = "快速打开",
            Volume = "分卷",
            SkipExisting = "跳过现有文件",
            UpdateExisting = "更新现有文件",
            OverwriteExisting = "覆盖现有文件",
            EnableComment = "启用注释",
            SkipProcessed = "跳过已处理",
            TempDirectory = "临时目录：",
            MaxSizeLabel = "最大处理大小(GB)：",
            AddAttachments = "添加附件",
            
            // 后处理选项。
            AfterProcessing = "压缩或解压后",
            AfterCompression = "压缩完成后",
            AfterDecompression = "解压完成后",
            DeleteSource = "删除源",
            MoveSource = "移动源",
            ShutdownAfterComplete = "完成后关机",
            
            // 操作按钮。
            Compress = "压缩",
            Decompress = "解压",
            Cancel = "中止",
            RefreshList = "更新列表",
            ClearLogs = "清空日志",
            OpenOutput = "打开输出",
            OpenSource = "打开源",
            ZoomIn = "放大",
            ZoomOut = "缩小",
            
            // 状态显示。
            CurrentFile = "当前文件：",
            Success = "成功：",
            Failure = "失败：",
            Ignored = "忽略：",
            ProcessedSize = "已处理大小：",
            TotalFileSize = "总文件大小：",
            ElapsedTime = "已用时间：",
            RemainingTime = "剩余时间：",
            ProcessingSpeed = "处理速度：",
            ProcessingSpeedUnit = "MB/秒",
            EstimatedCompletion = "预计完成：",
            
            // 标签页标题。
            FileListTab = "待处理文件列表",
            SuccessLogTab = "成功记录",
            FailLogTab = "失败记录",
            CommandLogTab = "命令日志",
            CompressionTab = "压缩配置",
            DecompressionTab = "解压配置",
            LogsTab = "日志",
            StartTab = "开始",
            
            // 对话框按钮。
            Ok = "确定",
            CancelDialog = "取消",
            Hint = "提示",
            SelectSaveDirectory = "请选择待解压文件保存目录",
            SelectPasswordTxt = "选择密码本TXT文件",
            TextFile = "文本文件",
            AllFiles = "所有文件",
            SelectSourceFolder = "选择来源文件夹",
            SelectOutputFolder = "选择输出文件夹",
            SelectSaveFolder = "选择待压缩/解压文件保存目录",
            SelectTextFile = "选择文本文件",
            
            // 日志文本。
            DroppedFolder = "拖入文件夹: ",
            DroppedTxtFile = "拖入TXT文件: ",
            Ready = "就绪",
            CompressionComplete = "压缩完成",
            DecompressionComplete = "解压完成",
            SuccessFailMessage = "成功: {0}, 失败: {1}",
            NoFilesToProcess = "没有要处理的文件",
            TryingToLoadAutomatically = "列表中没有文件，正在自动加载...",
            StillNoFiles = "仍然没有要处理的文件",
            CancellingOperation = "正在取消操作...",
            PasswordSetSuccessfully = "密码设置成功",
            Warning7zFormat = "7z 使用官方 7-Zip；恢复记录、快速打开和 RAR 注释选项不会应用。",
            SolidDisabledForStore = "存储模式下已禁用固实压缩",

            // 运行消息：压缩与解压流程日志、完成统计、通知与取消提示。
            CompressionPageOnly = "压缩命令只能从压缩配置页或开始页启动。",
            DecompressionPageOnly = "解压命令只能从解压配置页或开始页启动。",
            OptionConflict = "选项冲突",
            UpdateConflictsLockArchive = "更新现有文件不能与锁定归档同时使用。请取消其中一个选项。",
            OperationCompletionSummary = "\n完成: 成功={0}, 归档失败={1}, 后处理失败={2}, 忽略={3}, 未找到={4}, 分卷不完整={5}, 歧义={6}",
            OperationResultSummary = "成功: {0}, 归档失败: {1}, 后处理失败: {2}",
            OperationCancelled = "{0}已取消",
            OperationError = "错误: {0}",
            ShutdownScheduledLog = "已请求系统在一分钟后关机，可使用“取消关机”撤销。",
            ShutdownScheduledTitle = "关机计划",
            ShutdownScheduledBody = "系统将在一分钟后关机。可以在应用中取消。",
            ShutdownCancelledLog = "已请求取消关机。",
            ShutdownCancelledTitle = "已取消关机",
            ShutdownCancelledBody = "已向系统发送取消关机请求。",
            PasswordQueryArchiveName = "归档名: {0}",
            PasswordQueryBasis = "密码依据: {0}",
            PasswordQueryCompression = "压缩密码: {0}",
            PasswordQueryDecompression = "解压密码: {0}",
            PasswordQueryUtf8Full = "UTF8-8位: {0}",
            PasswordQueryUtf8Short = "UTF8-4位: {0}",
            PasswordQueryGb2312 = "GB2312-4位: {0}",
            PasswordQueryLegacyHeader = "旧版兼容密码:",
            ProgressNotificationTitle = "{0}进行中",
            ProgressNotificationBody = "已处理 {0} 项，成功 {1}，归档失败 {2}，后处理失败 {3}，当前：{4}{5}",
            ProgressNotificationRemaining = "，剩余约 {0}",
            CompressionListLabel = "压缩路径清单",
            PasswordBookLabel = "密码本",
            TextImportSummary = "[{0}] 请求={1}，已匹配={2}，大小={3} GB，预计约 {4} 秒",
            TextImportMissing = "[{0}] 未找到 {1} 项:",
            TextImportIncompleteVolumes = "[{0}] 分卷不完整 {1} 项:",
            TextImportAmbiguous = "[{0}] 名称或编号存在歧义 {1} 项:",
            PasswordBookUnmatched = "[密码本] 以下归档未在密码本找到，共 {0} 个:",
            PasswordBookVolumeCandidates = "[密码本] 以下归档疑似分卷，共 {0} 个:",
            DirectorySizeFileError = "读取文件大小失败：{0}：{1}",
            DirectorySizeAccessError = "访问目录失败：{0}：{1}",
            FormatCannotCreate = "不支持创建 {0} 格式归档。当前支持：{1}。",
            ItemNotFound = "未找到: {0}",
            SkipExistingOutput = "[跳过] 已存在：{0}",
            CompressionStarted = "[开始压缩] {0}",
            ExtractionStarted = "[开始解压] {0}",
            ItemSucceeded = "成功: {0}",
            ItemFailed = "失败: {0} - {1}",
            ItemSucceededWithPostProcessFailure = "成功但后处理失败: {0}",
            SizeLimitReached = "已达到大小上限",
            CannotResolveArchive = "无法解析: {0} - {1}",
            CaseAmbiguitySkipped = "文件名大小写存在歧义，已跳过: {0}",
            DuplicateVolumeSkipped = "分卷编号重复，已跳过: {0}",
            IncompleteVolumesSkipped = "分卷不完整，已跳过：{0}（{1}）",
            MissingFirstVolume = "缺少编号 1 的首卷",
            MissingVolumeNumbers = "缺少分卷编号 {0}",
            CompressionCommandLabel = "压缩命令",
            ExtractionCommandLabel = "解压命令"
        };
    }
    
    private static LanguageStrings CreateChineseTraditional()
    {
        return new LanguageStrings
        {
            // 窗口标题。
            WindowTitle = WithVersion("批量壓縮解壓工具"),
            
            // 语言选择器。
            LanguageLabel = "語言：",
            
            // 来源与目标区域。
            SourceAndDestination = "來源與目標",
            FromTxtMode = "從txt讀取要解壓的檔案：",
            CompressionTxtMode = "從txt讀取待壓縮路徑：",
            CompressFolderMode = "壓縮此資料夾內所有檔案：",
            DecompressFolderMode = "解壓此資料夾內的封存檔：",
            SavePathWatermark = "待壓縮/解壓檔案儲存路徑",
            SelectDirectory = "選擇目錄",
            TxtPathWatermark = "TXT檔案的路徑",
            SelectTxt = "選txt",
            SameAsAbove = "同上",
            DestinationWatermark = "目的地",
            
            // 压缩选项区域。
            CompressionOptions = "壓縮選項",
            DecompressionOptions = "解壓選項",
            FileNameLabel = "檔名（不含副檔名）：",
            QueryPassword = "查詢密碼",
            RandomPassword = "隨機密碼",
            CustomPasswordWatermark = "自訂密碼",
            CopiedToClipboard = "（已複製到剪貼簿）",
            ExtensionLabel = "副檔名：",
            Verify = "校驗",
            CompressionLevelLabel = "壓縮率：",
            NoCompression = "不壓縮",
            Light = "輕度",
            Fast = "快速",
            Standard = "標準",
            Better = "較好",
            Best = "最佳",
            Solid = "固實",
            QuickOpen = "快速開啟",
            Volume = "分卷",
            SkipExisting = "跳過現有檔案",
            UpdateExisting = "更新現有檔案",
            OverwriteExisting = "覆蓋現有檔案",
            EnableComment = "啟用註釋",
            SkipProcessed = "跳過已處理",
            TempDirectory = "臨時目錄：",
            MaxSizeLabel = "最大處理大小(GB)：",
            AddAttachments = "新增附件",
            
            // 后处理选项。
            AfterProcessing = "壓縮或解壓後",
            AfterCompression = "壓縮完成後",
            AfterDecompression = "解壓完成後",
            DeleteSource = "刪除源",
            MoveSource = "移動源",
            ShutdownAfterComplete = "完成後關機",
            
            // 操作按钮。
            Compress = "壓縮",
            Decompress = "解壓",
            Cancel = "中止",
            RefreshList = "更新列表",
            ClearLogs = "清空日誌",
            OpenOutput = "開啟輸出",
            OpenSource = "開啟源",
            ZoomIn = "放大",
            ZoomOut = "縮小",
            
            // 状态显示。
            CurrentFile = "當前檔案：",
            Success = "成功：",
            Failure = "失敗：",
            Ignored = "忽略：",
            ProcessedSize = "已處理大小：",
            TotalFileSize = "總檔案大小：",
            ElapsedTime = "已用時間：",
            RemainingTime = "剩餘時間：",
            ProcessingSpeed = "處理速度：",
            ProcessingSpeedUnit = "MB/秒",
            EstimatedCompletion = "預計完成：",
            
            // 标签页标题。
            FileListTab = "待處理檔案列表",
            SuccessLogTab = "成功記錄",
            FailLogTab = "失敗記錄",
            CommandLogTab = "命令日誌",
            CompressionTab = "壓縮設定",
            DecompressionTab = "解壓設定",
            LogsTab = "日誌",
            StartTab = "開始",
            
            // 对话框按钮。
            Ok = "確定",
            CancelDialog = "取消",
            Hint = "提示",
            SelectSaveDirectory = "請選擇待解壓檔案儲存目錄",
            SelectPasswordTxt = "選擇密碼本TXT檔案",
            TextFile = "文字檔案",
            AllFiles = "所有檔案",
            SelectSourceFolder = "選擇來源資料夾",
            SelectOutputFolder = "選擇輸出資料夾",
            SelectSaveFolder = "選擇待壓縮/解壓檔案儲存目錄",
            SelectTextFile = "選擇文字檔案",
            
            // 日志文本。
            DroppedFolder = "拖入資料夾: ",
            DroppedTxtFile = "拖入TXT檔案: ",
            Ready = "就緒",
            CompressionComplete = "壓縮完成",
            DecompressionComplete = "解壓完成",
            SuccessFailMessage = "成功: {0}, 失敗: {1}",
            NoFilesToProcess = "沒有要處理的檔案",
            TryingToLoadAutomatically = "列表中沒有檔案，正在自動載入...",
            StillNoFiles = "仍然沒有要處理的檔案",
            CancellingOperation = "正在取消操作...",
            PasswordSetSuccessfully = "密碼設定成功",
            Warning7zFormat = "7z 使用官方 7-Zip；恢復記錄、快速開啟和 RAR 註解選項不會套用。",
            SolidDisabledForStore = "儲存模式下已停用固實壓縮",

            // 執行訊息：壓縮與解壓流程日誌、完成統計、通知與取消提示。
            CompressionPageOnly = "壓縮命令只能從壓縮設定頁或開始頁啟動。",
            DecompressionPageOnly = "解壓命令只能從解壓設定頁或開始頁啟動。",
            OptionConflict = "選項衝突",
            UpdateConflictsLockArchive = "更新現有檔案不能與鎖定封存檔同時使用。請取消其中一個選項。",
            OperationCompletionSummary = "\n完成: 成功={0}, 封存失敗={1}, 後處理失敗={2}, 忽略={3}, 未找到={4}, 分卷不完整={5}, 歧義={6}",
            OperationResultSummary = "成功: {0}, 封存失敗: {1}, 後處理失敗: {2}",
            OperationCancelled = "{0}已取消",
            OperationError = "錯誤: {0}",
            ShutdownScheduledLog = "已請求系統在一分鐘後關機，可使用「取消關機」撤銷。",
            ShutdownScheduledTitle = "關機計劃",
            ShutdownScheduledBody = "系統將在一分鐘後關機。可以在應用中取消。",
            ShutdownCancelledLog = "已請求取消關機。",
            ShutdownCancelledTitle = "已取消關機",
            ShutdownCancelledBody = "已向系統傳送取消關機請求。",
            PasswordQueryArchiveName = "封存檔名: {0}",
            PasswordQueryBasis = "密碼依據: {0}",
            PasswordQueryCompression = "壓縮密碼: {0}",
            PasswordQueryDecompression = "解壓密碼: {0}",
            PasswordQueryUtf8Full = "UTF8-8位: {0}",
            PasswordQueryUtf8Short = "UTF8-4位: {0}",
            PasswordQueryGb2312 = "GB2312-4位: {0}",
            PasswordQueryLegacyHeader = "舊版相容密碼:",
            ProgressNotificationTitle = "{0}進行中",
            ProgressNotificationBody = "已處理 {0} 項，成功 {1}，封存失敗 {2}，後處理失敗 {3}，目前：{4}{5}",
            ProgressNotificationRemaining = "，剩餘約 {0}",
            CompressionListLabel = "壓縮路徑清單",
            PasswordBookLabel = "密碼本",
            TextImportSummary = "[{0}] 請求={1}，已符合={2}，大小={3} GB，預計約 {4} 秒",
            TextImportMissing = "[{0}] 未找到 {1} 項:",
            TextImportIncompleteVolumes = "[{0}] 分卷不完整 {1} 項:",
            TextImportAmbiguous = "[{0}] 名稱或編號存在歧義 {1} 項:",
            PasswordBookUnmatched = "[密碼本] 以下封存檔未在密碼本找到，共 {0} 個:",
            PasswordBookVolumeCandidates = "[密碼本] 以下封存檔疑似分卷，共 {0} 個:",
            DirectorySizeFileError = "讀取檔案大小失敗：{0}：{1}",
            DirectorySizeAccessError = "存取目錄失敗：{0}：{1}",
            FormatCannotCreate = "不支援建立 {0} 格式封存檔。目前支援：{1}。",
            ItemNotFound = "未找到: {0}",
            SkipExistingOutput = "[略過] 已存在：{0}",
            CompressionStarted = "[開始壓縮] {0}",
            ExtractionStarted = "[開始解壓] {0}",
            ItemSucceeded = "成功: {0}",
            ItemFailed = "失敗: {0} - {1}",
            ItemSucceededWithPostProcessFailure = "成功但後處理失敗: {0}",
            SizeLimitReached = "已達到大小上限",
            CannotResolveArchive = "無法解析: {0} - {1}",
            CaseAmbiguitySkipped = "檔案名大小寫存在歧義，已略過: {0}",
            DuplicateVolumeSkipped = "分卷編號重複，已略過: {0}",
            IncompleteVolumesSkipped = "分卷不完整，已略過：{0}（{1}）",
            MissingFirstVolume = "缺少編號 1 的首卷",
            MissingVolumeNumbers = "缺少分卷編號 {0}",
            CompressionCommandLabel = "壓縮命令",
            ExtractionCommandLabel = "解壓命令"
        };
    }
    
    private static LanguageStrings CreateEnglish()
    {
        return new LanguageStrings
        {
            // 窗口标题。
            WindowTitle = WithVersion("Batch Compress Tool"),
            
            // 语言选择器。
            LanguageLabel = "Language:",
            
            // 来源与目标区域。
            SourceAndDestination = "Source & Destination",
            FromTxtMode = "Read files to decompress from txt:",
            CompressionTxtMode = "Read paths to compress from txt:",
            CompressFolderMode = "Compress all files in folder:",
            DecompressFolderMode = "Extract archives in folder:",
            SavePathWatermark = "File save path for compress/decompress",
            SelectDirectory = "Browse",
            TxtPathWatermark = "Path to TXT file",
            SelectTxt = "Select txt",
            SameAsAbove = "Same",
            DestinationWatermark = "Destination",
            
            // 压缩选项区域。
            CompressionOptions = "Compression Options",
            DecompressionOptions = "Decompression Options",
            FileNameLabel = "File name (without extension):",
            QueryPassword = "Query Password",
            RandomPassword = "Random Password",
            CustomPasswordWatermark = "Custom password",
            CopiedToClipboard = "(Copied to clipboard)",
            ExtensionLabel = "Extension:",
            Verify = "Verify",
            CompressionLevelLabel = "Level:",
            NoCompression = "Store",
            Light = "Fastest",
            Fast = "Fast",
            Standard = "Normal",
            Better = "Good",
            Best = "Best",
            Solid = "Solid",
            QuickOpen = "Quick Open",
            Volume = "Split",
            SkipExisting = "Skip existing files",
            UpdateExisting = "Update existing files",
            OverwriteExisting = "Overwrite existing files",
            EnableComment = "Enable Comment",
            SkipProcessed = "Skip Processed",
            TempDirectory = "Temp Directory:",
            MaxSizeLabel = "Max Size (GB):",
            AddAttachments = "Add Attachments",
            
            // 后处理选项。
            AfterProcessing = "After Processing",
            AfterCompression = "After Compression",
            AfterDecompression = "After Decompression",
            DeleteSource = "Delete Source",
            MoveSource = "Move Source",
            ShutdownAfterComplete = "Shutdown After Complete",
            
            // 操作按钮。
            Compress = "Compress",
            Decompress = "Decompress",
            Cancel = "Cancel",
            RefreshList = "Refresh List",
            ClearLogs = "Clear Logs",
            OpenOutput = "Open Output",
            OpenSource = "Open Source",
            ZoomIn = "Maximize",
            ZoomOut = "Restore",
            
            // 状态显示。
            CurrentFile = "Current File:",
            Success = "Success:",
            Failure = "Failed:",
            Ignored = "Ignored:",
            ProcessedSize = "Processed Size:",
            TotalFileSize = "Total File Size:",
            ElapsedTime = "Elapsed Time:",
            RemainingTime = "Remaining Time:",
            ProcessingSpeed = "Speed:",
            ProcessingSpeedUnit = "MB/s",
            EstimatedCompletion = "Est. Completion:",
            
            // 标签页标题。
            FileListTab = "File List",
            SuccessLogTab = "Success Log",
            FailLogTab = "Error Log",
            CommandLogTab = "Command Log",
            CompressionTab = "Compression Config",
            DecompressionTab = "Decompression Config",
            LogsTab = "Logs",
            StartTab = "Start",
            
            // 对话框按钮。
            Ok = "OK",
            CancelDialog = "Cancel",
            Hint = "Notice",
            SelectSaveDirectory = "Please select save directory",
            SelectPasswordTxt = "Select password TXT file",
            TextFile = "Text Files",
            AllFiles = "All Files",
            SelectSourceFolder = "Select Source Folder",
            SelectOutputFolder = "Select Output Folder",
            SelectSaveFolder = "Select save directory for compress/decompress",
            SelectTextFile = "Select Text File",
            
            // 日志文本。
            DroppedFolder = "Dropped folder: ",
            DroppedTxtFile = "Dropped TXT file: ",
            Ready = "Ready",
            CompressionComplete = "Compression Complete",
            DecompressionComplete = "Decompression Complete",
            SuccessFailMessage = "Success: {0}, Failed: {1}",
            NoFilesToProcess = "No files to process",
            TryingToLoadAutomatically = "No files in list, trying to load automatically...",
            StillNoFiles = "Still no files to process",
            CancellingOperation = "Cancelling operation...",
            PasswordSetSuccessfully = "Password set successfully",
            Warning7zFormat = "7z uses official 7-Zip; recovery record, quick open and RAR comment options are not applied.",
            SolidDisabledForStore = "Solid archive disabled for Store mode",

            // 运行消息：压缩与解压流程日志、完成统计、通知与取消提示。
            CompressionPageOnly = "Compression can only be started from the Compression or Start tab.",
            DecompressionPageOnly = "Decompression can only be started from the Decompression or Start tab.",
            OptionConflict = "Option conflict",
            UpdateConflictsLockArchive = "Update existing files cannot be combined with locking the archive. Clear one of the options.",
            OperationCompletionSummary = "\nCompleted: Success={0}, Archive failures={1}, Post-process failures={2}, Skipped={3}, Not found={4}, Incomplete volumes={5}, Ambiguous={6}",
            OperationResultSummary = "Success: {0}, Archive failures: {1}, Post-process failures: {2}",
            OperationCancelled = "{0} cancelled",
            OperationError = "Error: {0}",
            ShutdownScheduledLog = "System shutdown requested in one minute. Use \"Cancel shutdown\" to undo it.",
            ShutdownScheduledTitle = "Shutdown scheduled",
            ShutdownScheduledBody = "The system will shut down in one minute. You can cancel it in the app.",
            ShutdownCancelledLog = "Shutdown cancellation requested.",
            ShutdownCancelledTitle = "Shutdown cancelled",
            ShutdownCancelledBody = "A shutdown cancellation request was sent to the system.",
            PasswordQueryArchiveName = "Archive name: {0}",
            PasswordQueryBasis = "Password basis: {0}",
            PasswordQueryCompression = "Compression password: {0}",
            PasswordQueryDecompression = "Decompression password: {0}",
            PasswordQueryUtf8Full = "UTF8-8 chars: {0}",
            PasswordQueryUtf8Short = "UTF8-4 chars: {0}",
            PasswordQueryGb2312 = "GB2312-4 chars: {0}",
            PasswordQueryLegacyHeader = "Legacy compatible passwords:",
            ProgressNotificationTitle = "{0} in progress",
            ProgressNotificationBody = "Processed {0} items, {1} succeeded, {2} archive failures, {3} post-process failures, current: {4}{5}",
            ProgressNotificationRemaining = ", about {0} remaining",
            CompressionListLabel = "Compression list",
            PasswordBookLabel = "Password book",
            TextImportSummary = "[{0}] Requested={1}, matched={2}, size={3} GB, about {4} seconds",
            TextImportMissing = "[{0}] {1} item(s) not found:",
            TextImportIncompleteVolumes = "[{0}] {1} item(s) with incomplete volumes:",
            TextImportAmbiguous = "[{0}] {1} item(s) with ambiguous name or number:",
            PasswordBookUnmatched = "[Password book] Archives not found in the password book: {0}",
            PasswordBookVolumeCandidates = "[Password book] Archives that look like volumes: {0}",
            DirectorySizeFileError = "Failed to read file size: {0}: {1}",
            DirectorySizeAccessError = "Failed to access directory: {0}: {1}",
            FormatCannotCreate = "Cannot create {0} archives. Supported formats: {1}.",
            ItemNotFound = "Not found: {0}",
            SkipExistingOutput = "[Skipped] Already exists: {0}",
            CompressionStarted = "[Compressing] {0}",
            ExtractionStarted = "[Extracting] {0}",
            ItemSucceeded = "Success: {0}",
            ItemFailed = "Failed: {0} - {1}",
            ItemSucceededWithPostProcessFailure = "Succeeded but post-processing failed: {0}",
            SizeLimitReached = "Size limit reached",
            CannotResolveArchive = "Cannot resolve: {0} - {1}",
            CaseAmbiguitySkipped = "File name differs only by letter case, skipped: {0}",
            DuplicateVolumeSkipped = "Duplicate volume number, skipped: {0}",
            IncompleteVolumesSkipped = "Incomplete volume set, skipped: {0} ({1})",
            MissingFirstVolume = "first volume with number 1 is missing",
            MissingVolumeNumbers = "missing volume number(s) {0}",
            CompressionCommandLabel = "Compression command",
            ExtractionCommandLabel = "Extraction command"
        };
    }
    
    private static LanguageStrings CreateJapanese()
    {
        return new LanguageStrings
        {
            // 窗口标题。
            WindowTitle = WithVersion("バッチ圧縮ツール"),
            
            // 语言选择器。
            LanguageLabel = "言語：",
            
            // 来源与目标区域。
            SourceAndDestination = "ソースと宛先",
            FromTxtMode = "txtから解凍するファイルを読み込む：",
            CompressionTxtMode = "txtから圧縮するパスを読み込む：",
            CompressFolderMode = "フォルダ内のすべてのファイルを圧縮：",
            DecompressFolderMode = "フォルダ内のアーカイブを解凍：",
            SavePathWatermark = "圧縮/解凍ファイルの保存パス",
            SelectDirectory = "参照",
            TxtPathWatermark = "TXTファイルのパス",
            SelectTxt = "txt選択",
            SameAsAbove = "同上",
            DestinationWatermark = "宛先",
            
            // 压缩选项区域。
            CompressionOptions = "圧縮オプション",
            DecompressionOptions = "解凍オプション",
            FileNameLabel = "ファイル名（拡張子なし）：",
            QueryPassword = "パスワード検索",
            RandomPassword = "ランダムパスワード",
            CustomPasswordWatermark = "カスタムパスワード",
            CopiedToClipboard = "（クリップボードにコピー済み）",
            ExtensionLabel = "拡張子：",
            Verify = "検証",
            CompressionLevelLabel = "圧縮率：",
            NoCompression = "無圧縮",
            Light = "最速",
            Fast = "高速",
            Standard = "標準",
            Better = "良好",
            Best = "最高",
            Solid = "ソリッド",
            QuickOpen = "クイックオープン",
            Volume = "分割",
            SkipExisting = "既存ファイルをスキップ",
            UpdateExisting = "既存ファイルを更新",
            OverwriteExisting = "既存ファイルを上書き",
            EnableComment = "コメント有効",
            SkipProcessed = "処理済みをスキップ",
            TempDirectory = "一時ディレクトリ：",
            MaxSizeLabel = "最大サイズ(GB)：",
            AddAttachments = "添付ファイル追加",
            
            // 后处理选项。
            AfterProcessing = "処理後",
            AfterCompression = "圧縮後",
            AfterDecompression = "解凍後",
            DeleteSource = "ソース削除",
            MoveSource = "ソース移動",
            ShutdownAfterComplete = "完了後シャットダウン",
            
            // 操作按钮。
            Compress = "圧縮",
            Decompress = "解凍",
            Cancel = "キャンセル",
            RefreshList = "リスト更新",
            ClearLogs = "ログクリア",
            OpenOutput = "出力を開く",
            OpenSource = "ソースを開く",
            ZoomIn = "最大化",
            ZoomOut = "元に戻す",
            
            // 状态显示。
            CurrentFile = "現在のファイル：",
            Success = "成功：",
            Failure = "失敗：",
            Ignored = "無視：",
            ProcessedSize = "処理済みサイズ：",
            TotalFileSize = "合計ファイルサイズ：",
            ElapsedTime = "経過時間：",
            RemainingTime = "残り時間：",
            ProcessingSpeed = "速度：",
            ProcessingSpeedUnit = "MB/秒",
            EstimatedCompletion = "完了予定：",
            
            // 标签页标题。
            FileListTab = "ファイルリスト",
            SuccessLogTab = "成功ログ",
            FailLogTab = "エラーログ",
            CommandLogTab = "コマンドログ",
            CompressionTab = "圧縮設定",
            DecompressionTab = "解凍設定",
            LogsTab = "ログ",
            StartTab = "開始",
            
            // 对话框按钮。
            Ok = "OK",
            CancelDialog = "キャンセル",
            Hint = "通知",
            SelectSaveDirectory = "保存ディレクトリを選択してください",
            SelectPasswordTxt = "パスワードTXTファイルを選択",
            TextFile = "テキストファイル",
            AllFiles = "すべてのファイル",
            SelectSourceFolder = "ソースフォルダを選択",
            SelectOutputFolder = "出力フォルダを選択",
            SelectSaveFolder = "圧縮/解凍ファイルの保存ディレクトリを選択",
            SelectTextFile = "テキストファイルを選択",
            
            // 日志文本。
            DroppedFolder = "ドロップされたフォルダ: ",
            DroppedTxtFile = "ドロップされたTXTファイル: ",
            Ready = "準備完了",
            CompressionComplete = "圧縮完了",
            DecompressionComplete = "解凍完了",
            SuccessFailMessage = "成功: {0}, 失敗: {1}",
            NoFilesToProcess = "処理するファイルがありません",
            TryingToLoadAutomatically = "リストにファイルがありません、自動読み込み中...",
            StillNoFiles = "まだ処理するファイルがありません",
            CancellingOperation = "操作をキャンセル中...",
            PasswordSetSuccessfully = "パスワードが設定されました",
            Warning7zFormat = "7z は公式 7-Zip を使用します。復元レコード、クイックオープン、RAR コメントの各オプションは適用されません。",
            SolidDisabledForStore = "ストアモードではソリッドアーカイブが無効になりました",

            // 実行メッセージ：圧縮・解凍のログ、完了統計、通知とキャンセル表示。
            CompressionPageOnly = "圧縮は「圧縮設定」または「開始」タブからのみ実行できます。",
            DecompressionPageOnly = "解凍は「解凍設定」または「開始」タブからのみ実行できます。",
            OptionConflict = "オプションの競合",
            UpdateConflictsLockArchive = "「既存ファイルを更新」と「アーカイブをロック」は同時に使用できません。どちらかを解除してください。",
            OperationCompletionSummary = "\n完了: 成功={0}, アーカイブ失敗={1}, 後処理失敗={2}, 無視={3}, 見つからない={4}, 分巻不完全={5}, 曖昧={6}",
            OperationResultSummary = "成功: {0}, アーカイブ失敗: {1}, 後処理失敗: {2}",
            OperationCancelled = "{0}をキャンセルしました",
            OperationError = "エラー: {0}",
            ShutdownScheduledLog = "1 分後のシャットダウンを要求しました。「シャットダウン取消」で取り消せます。",
            ShutdownScheduledTitle = "シャットダウン予定",
            ShutdownScheduledBody = "1 分後にシステムをシャットダウンします。アプリ内で取り消せます。",
            ShutdownCancelledLog = "シャットダウンの取り消しを要求しました。",
            ShutdownCancelledTitle = "シャットダウンを取り消しました",
            ShutdownCancelledBody = "システムにシャットダウン取り消し要求を送信しました。",
            PasswordQueryArchiveName = "アーカイブ名: {0}",
            PasswordQueryBasis = "パスワードの基準: {0}",
            PasswordQueryCompression = "圧縮パスワード: {0}",
            PasswordQueryDecompression = "解凍パスワード: {0}",
            PasswordQueryUtf8Full = "UTF8-8文字: {0}",
            PasswordQueryUtf8Short = "UTF8-4文字: {0}",
            PasswordQueryGb2312 = "GB2312-4文字: {0}",
            PasswordQueryLegacyHeader = "旧版互換パスワード:",
            ProgressNotificationTitle = "{0}を実行中",
            ProgressNotificationBody = "{0} 件処理済み、成功 {1}、アーカイブ失敗 {2}、後処理失敗 {3}、現在: {4}{5}",
            ProgressNotificationRemaining = "、残り約 {0}",
            CompressionListLabel = "圧縮リスト",
            PasswordBookLabel = "パスワード帳",
            TextImportSummary = "[{0}] 要求={1}、一致={2}、サイズ={3} GB、約 {4} 秒",
            TextImportMissing = "[{0}] 見つからない項目 {1} 件:",
            TextImportIncompleteVolumes = "[{0}] 分巻が不完全な項目 {1} 件:",
            TextImportAmbiguous = "[{0}] 名前または番号が曖昧な項目 {1} 件:",
            PasswordBookUnmatched = "[パスワード帳] パスワード帳に見つからないアーカイブ {0} 件:",
            PasswordBookVolumeCandidates = "[パスワード帳] 分巻の可能性があるアーカイブ {0} 件:",
            DirectorySizeFileError = "ファイルサイズの取得に失敗: {0}: {1}",
            DirectorySizeAccessError = "ディレクトリにアクセスできません: {0}: {1}",
            FormatCannotCreate = "{0} 形式のアーカイブは作成できません。対応形式: {1}。",
            ItemNotFound = "見つかりません: {0}",
            SkipExistingOutput = "[スキップ] 既に存在します: {0}",
            CompressionStarted = "[圧縮開始] {0}",
            ExtractionStarted = "[解凍開始] {0}",
            ItemSucceeded = "成功: {0}",
            ItemFailed = "失敗: {0} - {1}",
            ItemSucceededWithPostProcessFailure = "成功しましたが後処理に失敗: {0}",
            SizeLimitReached = "サイズ上限に達しました",
            CannotResolveArchive = "解析できません: {0} - {1}",
            CaseAmbiguitySkipped = "ファイル名の大文字小文字が曖昧なためスキップ: {0}",
            DuplicateVolumeSkipped = "分巻番号が重複しているためスキップ: {0}",
            IncompleteVolumesSkipped = "分巻が不完全なためスキップ: {0}（{1}）",
            MissingFirstVolume = "番号 1 の先頭巻がありません",
            MissingVolumeNumbers = "分巻番号 {0} がありません",
            CompressionCommandLabel = "圧縮コマンド",
            ExtractionCommandLabel = "解凍コマンド"
        };
    }
    
    private static LanguageStrings CreateGerman()
    {
        return new LanguageStrings
        {
            // 窗口标题。
            WindowTitle = WithVersion("Batch-Komprimierungstool"),
            
            // 语言选择器。
            LanguageLabel = "Sprache:",
            
            // 来源与目标区域。
            SourceAndDestination = "Quelle & Ziel",
            FromTxtMode = "Dateien zum Entpacken aus txt lesen:",
            CompressionTxtMode = "Zu komprimierende Pfade aus txt lesen:",
            CompressFolderMode = "Alle Dateien im Ordner komprimieren:",
            DecompressFolderMode = "Archive im Ordner entpacken:",
            SavePathWatermark = "Speicherpfad für Komprimierung/Dekomprimierung",
            SelectDirectory = "Durchsuchen",
            TxtPathWatermark = "Pfad zur TXT-Datei",
            SelectTxt = "txt wählen",
            SameAsAbove = "Gleich",
            DestinationWatermark = "Ziel",
            
            // 压缩选项区域。
            CompressionOptions = "Komprimierungsoptionen",
            DecompressionOptions = "Dekomprimierungsoptionen",
            FileNameLabel = "Dateiname (ohne Erweiterung):",
            QueryPassword = "Passwort abfragen",
            RandomPassword = "Zufälliges Passwort",
            CustomPasswordWatermark = "Benutzerdefiniertes Passwort",
            CopiedToClipboard = "(In Zwischenablage kopiert)",
            ExtensionLabel = "Erweiterung:",
            Verify = "Überprüfen",
            CompressionLevelLabel = "Stufe:",
            NoCompression = "Speichern",
            Light = "Schnellste",
            Fast = "Schnell",
            Standard = "Normal",
            Better = "Gut",
            Best = "Beste",
            Solid = "Solid",
            QuickOpen = "Schnellöffnung",
            Volume = "Teilen",
            SkipExisting = "Vorhandene Dateien überspringen",
            UpdateExisting = "Vorhandene Dateien aktualisieren",
            OverwriteExisting = "Vorhandene Dateien überschreiben",
            EnableComment = "Kommentar aktivieren",
            SkipProcessed = "Verarbeitete überspringen",
            TempDirectory = "Temp-Verzeichnis:",
            MaxSizeLabel = "Max Größe (GB):",
            AddAttachments = "Anhänge hinzufügen",
            
            // 后处理选项。
            AfterProcessing = "Nach der Verarbeitung",
            AfterCompression = "Nach der Komprimierung",
            AfterDecompression = "Nach dem Entpacken",
            DeleteSource = "Quelle löschen",
            MoveSource = "Quelle verschieben",
            ShutdownAfterComplete = "Nach Abschluss herunterfahren",
            
            // 操作按钮。
            Compress = "Komprimieren",
            Decompress = "Entpacken",
            Cancel = "Abbrechen",
            RefreshList = "Liste aktualisieren",
            ClearLogs = "Logs löschen",
            OpenOutput = "Ausgabe öffnen",
            OpenSource = "Quelle öffnen",
            ZoomIn = "Maximieren",
            ZoomOut = "Wiederherstellen",
            
            // 状态显示。
            CurrentFile = "Aktuelle Datei:",
            Success = "Erfolg:",
            Failure = "Fehler:",
            Ignored = "Ignoriert:",
            ProcessedSize = "Verarbeitete Größe:",
            TotalFileSize = "Gesamtdateigröße:",
            ElapsedTime = "Verstrichene Zeit:",
            RemainingTime = "Verbleibende Zeit:",
            ProcessingSpeed = "Geschwindigkeit:",
            ProcessingSpeedUnit = "MB/s",
            EstimatedCompletion = "Geschätzte Fertigstellung:",
            
            // 标签页标题。
            FileListTab = "Dateiliste",
            SuccessLogTab = "Erfolgsprotokoll",
            FailLogTab = "Fehlerprotokoll",
            CommandLogTab = "Befehlsprotokoll",
            CompressionTab = "Komprimierungskonfiguration",
            DecompressionTab = "Dekomprimierungskonfiguration",
            LogsTab = "Protokoll",
            StartTab = "Start",
            
            // 对话框按钮。
            Ok = "OK",
            CancelDialog = "Abbrechen",
            Hint = "Hinweis",
            SelectSaveDirectory = "Bitte Speicherverzeichnis auswählen",
            SelectPasswordTxt = "Passwort-TXT-Datei auswählen",
            TextFile = "Textdateien",
            AllFiles = "Alle Dateien",
            SelectSourceFolder = "Quellordner auswählen",
            SelectOutputFolder = "Ausgabeordner auswählen",
            SelectSaveFolder = "Speicherverzeichnis für Komprimierung/Dekomprimierung auswählen",
            SelectTextFile = "Textdatei auswählen",
            
            // 日志文本。
            DroppedFolder = "Abgelegter Ordner: ",
            DroppedTxtFile = "Abgelegte TXT-Datei: ",
            Ready = "Bereit",
            CompressionComplete = "Komprimierung abgeschlossen",
            DecompressionComplete = "Dekomprimierung abgeschlossen",
            SuccessFailMessage = "Erfolg: {0}, Fehler: {1}",
            NoFilesToProcess = "Keine Dateien zu verarbeiten",
            TryingToLoadAutomatically = "Keine Dateien in der Liste, versuche automatisch zu laden...",
            StillNoFiles = "Noch keine Dateien zu verarbeiten",
            CancellingOperation = "Vorgang wird abgebrochen...",
            PasswordSetSuccessfully = "Passwort erfolgreich festgelegt",
            Warning7zFormat = "7z verwendet das offizielle 7-Zip; Wiederherstellungsdatensatz, Schnellöffnen und RAR-Kommentaroptionen werden nicht angewendet.",
            SolidDisabledForStore = "Solid-Archiv für Speichermodus deaktiviert",

            // 运行消息：压缩与解压流程日志、完成统计、通知与取消提示。
            CompressionPageOnly = "Komprimieren kann nur über die Registerkarte „Komprimierung“ oder „Start“ gestartet werden.",
            DecompressionPageOnly = "Entpacken kann nur über die Registerkarte „Dekomprimierung“ oder „Start“ gestartet werden.",
            OptionConflict = "Optionskonflikt",
            UpdateConflictsLockArchive = "„Vorhandene Dateien aktualisieren“ kann nicht mit „Archiv sperren“ kombiniert werden. Bitte eine Option deaktivieren.",
            OperationCompletionSummary = "\nAbgeschlossen: Erfolg={0}, Archivfehler={1}, Nachbearbeitungsfehler={2}, Ignoriert={3}, Nicht gefunden={4}, Unvollständige Volumes={5}, Mehrdeutig={6}",
            OperationResultSummary = "Erfolg: {0}, Archivfehler: {1}, Nachbearbeitungsfehler: {2}",
            OperationCancelled = "{0} abgebrochen",
            OperationError = "Fehler: {0}",
            ShutdownScheduledLog = "Systemabschaltung in einer Minute angefordert. Mit „Abschaltung abbrechen“ rückgängig machen.",
            ShutdownScheduledTitle = "Abschaltung geplant",
            ShutdownScheduledBody = "Das System wird in einer Minute heruntergefahren. Die Abschaltung kann in der App abgebrochen werden.",
            ShutdownCancelledLog = "Abbruch der Abschaltung angefordert.",
            ShutdownCancelledTitle = "Abschaltung abgebrochen",
            ShutdownCancelledBody = "Die Anforderung zum Abbruch der Abschaltung wurde an das System gesendet.",
            PasswordQueryArchiveName = "Archivname: {0}",
            PasswordQueryBasis = "Passwortbasis: {0}",
            PasswordQueryCompression = "Komprimierungspasswort: {0}",
            PasswordQueryDecompression = "Entpackungspasswort: {0}",
            PasswordQueryUtf8Full = "UTF8-8 Zeichen: {0}",
            PasswordQueryUtf8Short = "UTF8-4 Zeichen: {0}",
            PasswordQueryGb2312 = "GB2312-4 Zeichen: {0}",
            PasswordQueryLegacyHeader = "Kompatible Altpasswörter:",
            ProgressNotificationTitle = "{0} läuft",
            ProgressNotificationBody = "{0} Elemente verarbeitet, {1} erfolgreich, {2} Archivfehler, {3} Nachbearbeitungsfehler, aktuell: {4}{5}",
            ProgressNotificationRemaining = ", noch etwa {0}",
            CompressionListLabel = "Komprimierungsliste",
            PasswordBookLabel = "Passwortliste",
            TextImportSummary = "[{0}] Angefordert={1}, zugeordnet={2}, Größe={3} GB, etwa {4} Sekunden",
            TextImportMissing = "[{0}] {1} Element(e) nicht gefunden:",
            TextImportIncompleteVolumes = "[{0}] {1} Element(e) mit unvollständigen Volumes:",
            TextImportAmbiguous = "[{0}] {1} Element(e) mit mehrdeutigem Namen oder Nummer:",
            PasswordBookUnmatched = "[Passwortliste] Archive, die nicht in der Passwortliste stehen: {0}",
            PasswordBookVolumeCandidates = "[Passwortliste] Archive, die Volumes sein könnten: {0}",
            DirectorySizeFileError = "Dateigröße konnte nicht gelesen werden: {0}: {1}",
            DirectorySizeAccessError = "Verzeichnis konnte nicht gelesen werden: {0}: {1}",
            FormatCannotCreate = "{0}-Archive können nicht erstellt werden. Unterstützte Formate: {1}.",
            ItemNotFound = "Nicht gefunden: {0}",
            SkipExistingOutput = "[Übersprungen] Bereits vorhanden: {0}",
            CompressionStarted = "[Komprimiere] {0}",
            ExtractionStarted = "[Entpacke] {0}",
            ItemSucceeded = "Erfolg: {0}",
            ItemFailed = "Fehler: {0} - {1}",
            ItemSucceededWithPostProcessFailure = "Erfolgreich, aber Nachbearbeitung fehlgeschlagen: {0}",
            SizeLimitReached = "Größenlimit erreicht",
            CannotResolveArchive = "Kann nicht aufgelöst werden: {0} - {1}",
            CaseAmbiguitySkipped = "Dateiname unterscheidet sich nur in der Groß-/Kleinschreibung, übersprungen: {0}",
            DuplicateVolumeSkipped = "Doppelte Volume-Nummer, übersprungen: {0}",
            IncompleteVolumesSkipped = "Unvollständige Volume-Gruppe, übersprungen: {0} ({1})",
            MissingFirstVolume = "erste Volume mit Nummer 1 fehlt",
            MissingVolumeNumbers = "fehlende Volume-Nummer(n) {0}",
            CompressionCommandLabel = "Komprimierungsbefehl",
            ExtractionCommandLabel = "Entpackungsbefehl"
        };
    }
}
