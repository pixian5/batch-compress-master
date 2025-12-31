using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchCompress.Avalonia.Localization;

/// <summary>
/// Manages the current language and provides localized strings.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();
    
    private LanguageStrings _strings;
    private string _currentLanguage = "zh-CN";
    
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
    /// Public method to notify property changes (for external use)
    /// </summary>
    public void NotifyPropertyChanged(string? propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }
    
    /// <summary>
    /// Available languages with display names.
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
    /// Gets or sets the current language code.
    /// </summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
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
    /// Gets the current localized strings.
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
            // Window title
            WindowTitle = "批量压缩解压工具 - Avalonia Cross-Platform",
            
            // Language selector
            LanguageLabel = "语言：",
            
            // Source and Destination Section
            SourceAndDestination = "来源与目标",
            FromTxtMode = "从txt读取要解压的文件：",
            CompressFolderMode = "压缩此文件夹内所有文件：",
            SavePathWatermark = "待压缩/解压文件保存路径",
            SelectDirectory = "选择目录",
            TxtPathWatermark = "TXT文件的路径",
            SelectTxt = "选txt",
            SameAsAbove = "同上",
            DestinationWatermark = "目的地",
            
            // Compression Options Section
            CompressionOptions = "压缩选项",
            FileNameLabel = "文件名（不含扩展名）：",
            QueryPassword = "查询密码",
            ConfirmPassword = "确认密码/解锁",
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
            
            // Volume Unit Options
            VolumeUnitGB = "GB",
            VolumeUnitMB = "MB",
            VolumeUnitKB = "KB",
            
            // Post-Processing Options
            AfterProcessing = "压缩或解压后",
            DeleteSource = "删除源",
            MoveSource = "移动源",
            ShutdownAfterComplete = "完成后关机",
            
            // Operation Buttons
            Compress = "压缩",
            Decompress = "解压",
            Cancel = "中止",
            RefreshList = "更新列表",
            ClearLogs = "清空日志",
            OpenOutput = "打开输出",
            OpenSource = "打开源",
            
            // Status Display
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
            
            // Tab Headers
            FileListTab = "待处理文件列表",
            SuccessLogTab = "成功记录",
            FailLogTab = "失败记录",
            CommandLogTab = "命令日志",
            
            // Dialog buttons
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
            
            // Log messages
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
            AdvancedFeaturesUnlocked = "高级功能已解锁！",
            PasswordSetSuccessfully = "密码设置成功",
            Warning7zFormat = "警告: WinRAR 无法压缩为7z格式，只能解压。请考虑使用rar或zip。",
            SolidDisabledForStore = "存储模式下已禁用固实压缩"
        };
    }
    
    private static LanguageStrings CreateChineseTraditional()
    {
        return new LanguageStrings
        {
            // Window title
            WindowTitle = "批量壓縮解壓工具 - Avalonia Cross-Platform",
            
            // Language selector
            LanguageLabel = "語言：",
            
            // Source and Destination Section
            SourceAndDestination = "來源與目標",
            FromTxtMode = "從txt讀取要解壓的檔案：",
            CompressFolderMode = "壓縮此資料夾內所有檔案：",
            SavePathWatermark = "待壓縮/解壓檔案儲存路徑",
            SelectDirectory = "選擇目錄",
            TxtPathWatermark = "TXT檔案的路徑",
            SelectTxt = "選txt",
            SameAsAbove = "同上",
            DestinationWatermark = "目的地",
            
            // Compression Options Section
            CompressionOptions = "壓縮選項",
            FileNameLabel = "檔名（不含副檔名）：",
            QueryPassword = "查詢密碼",
            ConfirmPassword = "確認密碼/解鎖",
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
            
            // Volume Unit Options
            VolumeUnitGB = "GB",
            VolumeUnitMB = "MB",
            VolumeUnitKB = "KB",
            
            // Post-Processing Options
            AfterProcessing = "壓縮或解壓後",
            DeleteSource = "刪除源",
            MoveSource = "移動源",
            ShutdownAfterComplete = "完成後關機",
            
            // Operation Buttons
            Compress = "壓縮",
            Decompress = "解壓",
            Cancel = "中止",
            RefreshList = "更新列表",
            ClearLogs = "清空日誌",
            OpenOutput = "開啟輸出",
            OpenSource = "開啟源",
            
            // Status Display
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
            
            // Tab Headers
            FileListTab = "待處理檔案列表",
            SuccessLogTab = "成功記錄",
            FailLogTab = "失敗記錄",
            CommandLogTab = "命令日誌",
            
            // Dialog buttons
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
            
            // Log messages
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
            AdvancedFeaturesUnlocked = "進階功能已解鎖！",
            PasswordSetSuccessfully = "密碼設定成功",
            Warning7zFormat = "警告: WinRAR 無法壓縮為7z格式，只能解壓。請考慮使用rar或zip。",
            SolidDisabledForStore = "儲存模式下已停用固實壓縮"
        };
    }
    
    private static LanguageStrings CreateEnglish()
    {
        return new LanguageStrings
        {
            // Window title
            WindowTitle = "Batch Compress Tool - Avalonia Cross-Platform",
            
            // Language selector
            LanguageLabel = "Language:",
            
            // Source and Destination Section
            SourceAndDestination = "Source & Destination",
            FromTxtMode = "Read files to decompress from txt:",
            CompressFolderMode = "Compress all files in folder:",
            SavePathWatermark = "File save path for compress/decompress",
            SelectDirectory = "Browse",
            TxtPathWatermark = "Path to TXT file",
            SelectTxt = "Select txt",
            SameAsAbove = "Same",
            DestinationWatermark = "Destination",
            
            // Compression Options Section
            CompressionOptions = "Compression Options",
            FileNameLabel = "File name (without extension):",
            QueryPassword = "Query Password",
            ConfirmPassword = "Confirm/Unlock",
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
            
            // Post-Processing Options
            AfterProcessing = "After Processing",
            DeleteSource = "Delete Source",
            MoveSource = "Move Source",
            ShutdownAfterComplete = "Shutdown After Complete",
            
            // Operation Buttons
            Compress = "Compress",
            Decompress = "Decompress",
            Cancel = "Cancel",
            RefreshList = "Refresh List",
            ClearLogs = "Clear Logs",
            OpenOutput = "Open Output",
            OpenSource = "Open Source",
            
            // Status Display
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
            
            // Tab Headers
            FileListTab = "File List",
            SuccessLogTab = "Success Log",
            FailLogTab = "Error Log",
            CommandLogTab = "Command Log",
            
            // Dialog buttons
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
            
            // Log messages
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
            AdvancedFeaturesUnlocked = "Advanced features unlocked!",
            PasswordSetSuccessfully = "Password set successfully",
            Warning7zFormat = "Warning: WinRAR cannot compress to 7z format, only extract. Consider using rar or zip.",
            SolidDisabledForStore = "Solid archive disabled for Store mode"
        };
    }
    
    private static LanguageStrings CreateJapanese()
    {
        return new LanguageStrings
        {
            // Window title
            WindowTitle = "バッチ圧縮ツール - Avalonia Cross-Platform",
            
            // Language selector
            LanguageLabel = "言語：",
            
            // Source and Destination Section
            SourceAndDestination = "ソースと宛先",
            FromTxtMode = "txtから解凍するファイルを読み込む：",
            CompressFolderMode = "フォルダ内のすべてのファイルを圧縮：",
            SavePathWatermark = "圧縮/解凍ファイルの保存パス",
            SelectDirectory = "参照",
            TxtPathWatermark = "TXTファイルのパス",
            SelectTxt = "txt選択",
            SameAsAbove = "同上",
            DestinationWatermark = "宛先",
            
            // Compression Options Section
            CompressionOptions = "圧縮オプション",
            FileNameLabel = "ファイル名（拡張子なし）：",
            QueryPassword = "パスワード検索",
            ConfirmPassword = "確認/解除",
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
            
            // Post-Processing Options
            AfterProcessing = "処理後",
            DeleteSource = "ソース削除",
            MoveSource = "ソース移動",
            ShutdownAfterComplete = "完了後シャットダウン",
            
            // Operation Buttons
            Compress = "圧縮",
            Decompress = "解凍",
            Cancel = "キャンセル",
            RefreshList = "リスト更新",
            ClearLogs = "ログクリア",
            OpenOutput = "出力を開く",
            OpenSource = "ソースを開く",
            
            // Status Display
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
            
            // Tab Headers
            FileListTab = "ファイルリスト",
            SuccessLogTab = "成功ログ",
            FailLogTab = "エラーログ",
            CommandLogTab = "コマンドログ",
            
            // Dialog buttons
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
            
            // Log messages
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
            AdvancedFeaturesUnlocked = "高度な機能がアンロックされました！",
            PasswordSetSuccessfully = "パスワードが設定されました",
            Warning7zFormat = "警告: WinRARは7z形式に圧縮できません。解凍のみ可能です。rarまたはzipの使用を検討してください。",
            SolidDisabledForStore = "ストアモードではソリッドアーカイブが無効になりました"
        };
    }
    
    private static LanguageStrings CreateGerman()
    {
        return new LanguageStrings
        {
            // Window title
            WindowTitle = "Batch-Komprimierungstool - Avalonia Cross-Platform",
            
            // Language selector
            LanguageLabel = "Sprache:",
            
            // Source and Destination Section
            SourceAndDestination = "Quelle & Ziel",
            FromTxtMode = "Dateien zum Entpacken aus txt lesen:",
            CompressFolderMode = "Alle Dateien im Ordner komprimieren:",
            SavePathWatermark = "Speicherpfad für Komprimierung/Dekomprimierung",
            SelectDirectory = "Durchsuchen",
            TxtPathWatermark = "Pfad zur TXT-Datei",
            SelectTxt = "txt wählen",
            SameAsAbove = "Gleich",
            DestinationWatermark = "Ziel",
            
            // Compression Options Section
            CompressionOptions = "Komprimierungsoptionen",
            FileNameLabel = "Dateiname (ohne Erweiterung):",
            QueryPassword = "Passwort abfragen",
            ConfirmPassword = "Bestätigen/Entsperren",
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
            
            // Post-Processing Options
            AfterProcessing = "Nach der Verarbeitung",
            DeleteSource = "Quelle löschen",
            MoveSource = "Quelle verschieben",
            ShutdownAfterComplete = "Nach Abschluss herunterfahren",
            
            // Operation Buttons
            Compress = "Komprimieren",
            Decompress = "Entpacken",
            Cancel = "Abbrechen",
            RefreshList = "Liste aktualisieren",
            ClearLogs = "Logs löschen",
            OpenOutput = "Ausgabe öffnen",
            OpenSource = "Quelle öffnen",
            
            // Status Display
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
            
            // Tab Headers
            FileListTab = "Dateiliste",
            SuccessLogTab = "Erfolgsprotokoll",
            FailLogTab = "Fehlerprotokoll",
            CommandLogTab = "Befehlsprotokoll",
            
            // Dialog buttons
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
            
            // Log messages
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
            AdvancedFeaturesUnlocked = "Erweiterte Funktionen freigeschaltet!",
            PasswordSetSuccessfully = "Passwort erfolgreich festgelegt",
            Warning7zFormat = "Warnung: WinRAR kann nicht in das 7z-Format komprimieren, nur extrahieren. Erwägen Sie die Verwendung von rar oder zip.",
            SolidDisabledForStore = "Solid-Archiv für Speichermodus deaktiviert"
        };
    }
}
