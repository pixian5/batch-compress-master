using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchCompress.Avalonia.Localization;

/// <summary>
/// Contains all localized strings for the application.
/// This class implements INotifyPropertyChanged to allow UI to update when language changes.
/// </summary>
public class LanguageStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// Raises PropertyChanged for all properties to refresh all UI bindings.
    /// </summary>
    public void RaiseAllPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    
    // Window title
    public string WindowTitle { get; set; } = "批量压缩解压工具 - Avalonia Cross-Platform";
    
    // Language selector
    public string LanguageLabel { get; set; } = "语言：";
    
    // Source and Destination Section
    public string SourceAndDestination { get; set; } = "来源与目标";
    public string FromTxtMode { get; set; } = "从txt读取要解压的文件：";
    public string CompressFolderMode { get; set; } = "压缩此文件夹内所有文件：";
    public string SavePathWatermark { get; set; } = "待压缩/解压文件保存路径";
    public string SelectDirectory { get; set; } = "选择目录";
    public string TxtPathWatermark { get; set; } = "TXT文件的路径";
    public string SelectTxt { get; set; } = "选txt";
    public string SameAsAbove { get; set; } = "同上";
    public string DestinationWatermark { get; set; } = "目的地";
    
    // Compression Options Section
    public string CompressionOptions { get; set; } = "压缩选项";
    public string FileNameLabel { get; set; } = "文件名（不含扩展名）：";
    public string QueryPassword { get; set; } = "查询密码";
    public string ConfirmPassword { get; set; } = "确认密码/解锁";
    public string RandomPassword { get; set; } = "随机密码";
    public string CustomPasswordWatermark { get; set; } = "自定义密码";
    public string CopiedToClipboard { get; set; } = "（已复制到剪贴板）";
    public string ExtensionLabel { get; set; } = "扩展名：";
    public string Verify { get; set; } = "校验";
    public string CompressionLevelLabel { get; set; } = "压缩率：";
    public string NoCompression { get; set; } = "不压缩";
    public string Light { get; set; } = "轻度";
    public string Fast { get; set; } = "快速";
    public string Standard { get; set; } = "标准";
    public string Better { get; set; } = "较好";
    public string Best { get; set; } = "最佳";
    public string Solid { get; set; } = "固实";
    public string QuickOpen { get; set; } = "快速打开";
    public string Volume { get; set; } = "分卷";
    public string SkipExisting { get; set; } = "跳过现有文件";
    public string UpdateExisting { get; set; } = "更新现有文件";
    public string OverwriteExisting { get; set; } = "覆盖现有文件";
    public string EnableComment { get; set; } = "启用注释";
    public string SkipProcessed { get; set; } = "跳过已处理";
    public string TempDirectory { get; set; } = "临时目录：";
    public string MaxSizeLabel { get; set; } = "最大处理大小(GB)：";
    public string AddAttachments { get; set; } = "添加附件";
    
    // Post-Processing Options
    public string AfterProcessing { get; set; } = "压缩或解压后";
    public string DeleteSource { get; set; } = "删除源";
    public string MoveSource { get; set; } = "移动源";
    public string ShutdownAfterComplete { get; set; } = "完成后关机";
    
    // Operation Buttons
    public string Compress { get; set; } = "压缩";
    public string Decompress { get; set; } = "解压";
    public string Cancel { get; set; } = "中止";
    public string RefreshList { get; set; } = "更新列表";
    public string ClearLogs { get; set; } = "清空日志";
    public string OpenOutput { get; set; } = "打开输出";
    public string OpenSource { get; set; } = "打开源";
    
    // Status Display
    public string CurrentFile { get; set; } = "当前文件：";
    public string Success { get; set; } = "成功：";
    public string Failure { get; set; } = "失败：";
    public string Ignored { get; set; } = "忽略：";
    public string ProcessedSize { get; set; } = "已处理大小：";
    public string TotalFileSize { get; set; } = "总文件大小：";
    public string ElapsedTime { get; set; } = "已用时间：";
    public string RemainingTime { get; set; } = "剩余时间：";
    public string ProcessingSpeed { get; set; } = "处理速度：";
    public string ProcessingSpeedUnit { get; set; } = "MB/秒";
    public string EstimatedCompletion { get; set; } = "预计完成：";
    
    // Tab Headers
    public string FileListTab { get; set; } = "待处理文件列表";
    public string SuccessLogTab { get; set; } = "成功记录";
    public string FailLogTab { get; set; } = "失败记录";
    public string CommandLogTab { get; set; } = "命令日志";
    
    // Dialog buttons
    public string Ok { get; set; } = "确定";
    public string CancelDialog { get; set; } = "取消";
    public string Hint { get; set; } = "提示";
    public string SelectSaveDirectory { get; set; } = "请选择待解压文件保存目录";
    public string SelectPasswordTxt { get; set; } = "选择密码本TXT文件";
    public string TextFile { get; set; } = "文本文件";
    public string AllFiles { get; set; } = "所有文件";
    public string SelectSourceFolder { get; set; } = "选择来源文件夹";
    public string SelectOutputFolder { get; set; } = "选择输出文件夹";
    public string SelectSaveFolder { get; set; } = "选择待压缩/解压文件保存目录";
    public string SelectTextFile { get; set; } = "选择文本文件";
    
    // Log messages
    public string DroppedFolder { get; set; } = "拖入文件夹: ";
    public string DroppedTxtFile { get; set; } = "拖入TXT文件: ";
    public string Ready { get; set; } = "Ready";
    public string CompletedMessage { get; set; } = "Completed: Success={0}, Fail={1}, Ignore={2}, NotFound={3}";
    public string CompressionComplete { get; set; } = "压缩完成";
    public string DecompressionComplete { get; set; } = "解压完成";
    public string SuccessFailMessage { get; set; } = "成功: {0}, 失败: {1}";
    public string NoFilesToProcess { get; set; } = "No files to process";
    public string TryingToLoadAutomatically { get; set; } = "No files in list, trying to load automatically...";
    public string StillNoFiles { get; set; } = "Still no files to process";
    public string CancellingOperation { get; set; } = "Cancelling operation...";
    public string AdvancedFeaturesUnlocked { get; set; } = "Advanced features unlocked!";
    public string PasswordSetSuccessfully { get; set; } = "Password set successfully";
    public string Warning7zFormat { get; set; } = "Warning: WinRAR cannot compress to 7z format, only extract. Consider using rar or zip.";
    public string SolidDisabledForStore { get; set; } = "Solid archive disabled for Store mode";
}
