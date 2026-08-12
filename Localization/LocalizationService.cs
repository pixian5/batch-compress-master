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
            CompletedMessage = "完成: 成功={0}, 失败={1}, 忽略={2}, 未找到={3}",
            CompressionComplete = "压缩完成",
            DecompressionComplete = "解压完成",
            SuccessFailMessage = "成功: {0}, 失败: {1}",
            NoFilesToProcess = "没有要处理的文件",
            TryingToLoadAutomatically = "列表中没有文件，正在自动加载...",
            StillNoFiles = "仍然没有要处理的文件",
            CancellingOperation = "正在取消操作...",
            PasswordSetSuccessfully = "密码设置成功",
            Warning7zFormat = "7z、ZIP 及其他 7zz 格式由官方 7-Zip 创建和解压。",
            SolidDisabledForStore = "存储模式下已禁用固实压缩"
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
            CompletedMessage = "完成: 成功={0}, 失敗={1}, 忽略={2}, 未找到={3}",
            CompressionComplete = "壓縮完成",
            DecompressionComplete = "解壓完成",
            SuccessFailMessage = "成功: {0}, 失敗: {1}",
            NoFilesToProcess = "沒有要處理的檔案",
            TryingToLoadAutomatically = "列表中沒有檔案，正在自動載入...",
            StillNoFiles = "仍然沒有要處理的檔案",
            CancellingOperation = "正在取消操作...",
            PasswordSetSuccessfully = "密碼設定成功",
            Warning7zFormat = "7z、ZIP 及其他 7zz 格式由官方 7-Zip 創建和解壓。",
            SolidDisabledForStore = "儲存模式下已停用固實壓縮"
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
            CompletedMessage = "Completed: Success={0}, Fail={1}, Ignore={2}, NotFound={3}",
            CompressionComplete = "Compression Complete",
            DecompressionComplete = "Decompression Complete",
            SuccessFailMessage = "Success: {0}, Failed: {1}",
            NoFilesToProcess = "No files to process",
            TryingToLoadAutomatically = "No files in list, trying to load automatically...",
            StillNoFiles = "Still no files to process",
            CancellingOperation = "Cancelling operation...",
            PasswordSetSuccessfully = "Password set successfully",
            Warning7zFormat = "7z, ZIP, and other 7zz formats are created and extracted by official 7-Zip.",
            SolidDisabledForStore = "Solid archive disabled for Store mode"
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
            CompletedMessage = "完了: 成功={0}, 失敗={1}, 無視={2}, 見つからない={3}",
            CompressionComplete = "圧縮完了",
            DecompressionComplete = "解凍完了",
            SuccessFailMessage = "成功: {0}, 失敗: {1}",
            NoFilesToProcess = "処理するファイルがありません",
            TryingToLoadAutomatically = "リストにファイルがありません、自動読み込み中...",
            StillNoFiles = "まだ処理するファイルがありません",
            CancellingOperation = "操作をキャンセル中...",
            PasswordSetSuccessfully = "パスワードが設定されました",
            Warning7zFormat = "7z、ZIP、その他の 7zz 形式は公式 7-Zip で作成・解凍します。",
            SolidDisabledForStore = "ストアモードではソリッドアーカイブが無効になりました"
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
            CompletedMessage = "Abgeschlossen: Erfolg={0}, Fehler={1}, Ignoriert={2}, Nicht gefunden={3}",
            CompressionComplete = "Komprimierung abgeschlossen",
            DecompressionComplete = "Dekomprimierung abgeschlossen",
            SuccessFailMessage = "Erfolg: {0}, Fehler: {1}",
            NoFilesToProcess = "Keine Dateien zu verarbeiten",
            TryingToLoadAutomatically = "Keine Dateien in der Liste, versuche automatisch zu laden...",
            StillNoFiles = "Noch keine Dateien zu verarbeiten",
            CancellingOperation = "Vorgang wird abgebrochen...",
            PasswordSetSuccessfully = "Passwort erfolgreich festgelegt",
            Warning7zFormat = "7z-, ZIP- und andere 7zz-Formate werden mit dem offiziellen 7-Zip erstellt und entpackt.",
            SolidDisabledForStore = "Solid-Archiv für Speichermodus deaktiviert"
        };
    }
}
