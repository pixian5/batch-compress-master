using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchCompress.Avalonia.Localization;

/// <summary>
/// 应用程序的全部本地化字符串。
/// 该类实现 INotifyPropertyChanged，使语言切换后界面可以立即刷新。
/// </summary>
// GPT-5, 2026-08-05：供绑定使用的可变字符串集合。LocalizationService 整体替换字符串对象，
// 使视图无需硬编码语言资源即可刷新。
public class LanguageStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// 通知所有属性已改变，以刷新全部界面绑定。
    /// </summary>
    public void RaiseAllPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    
    // 窗口标题。
    public string WindowTitle { get; set; } = "批量压缩解压工具";
    
    // 语言选择器。
    public string LanguageLabel { get; set; } = "语言：";
    
    // 来源与目标区域。
    public string SourceAndDestination { get; set; } = "来源与目标";
    public string FromTxtMode { get; set; } = "从txt读取要解压的文件：";
    public string CompressionTxtMode { get; set; } = "从txt读取待压缩路径：";
    public string CompressFolderMode { get; set; } = "压缩此文件夹内所有文件：";
    public string DecompressFolderMode { get; set; } = "解压此文件夹内的归档：";
    public string SavePathWatermark { get; set; } = "待压缩/解压文件保存路径";
    public string SelectDirectory { get; set; } = "选择目录";
    public string TxtPathWatermark { get; set; } = "TXT文件的路径";
    public string SelectTxt { get; set; } = "选txt";
    public string SameAsAbove { get; set; } = "同上";
    public string DestinationWatermark { get; set; } = "目的地";
    
    // 压缩选项区域。
    public string CompressionOptions { get; set; } = "压缩选项";
    public string DecompressionOptions { get; set; } = "解压选项";
    public string FileNameLabel { get; set; } = "文件名（不含扩展名）：";
    public string QueryPassword { get; set; } = "查询密码";
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
    
    // 后处理选项。
    public string AfterProcessing { get; set; } = "压缩或解压后";
    public string AfterCompression { get; set; } = "压缩完成后";
    public string AfterDecompression { get; set; } = "解压完成后";
    public string DeleteSource { get; set; } = "删除源";
    public string MoveSource { get; set; } = "移动源";
    public string ShutdownAfterComplete { get; set; } = "完成后关机";
    
    // 操作按钮。
    public string Compress { get; set; } = "压缩";
    public string Decompress { get; set; } = "解压";
    public string Cancel { get; set; } = "中止";
    public string RefreshList { get; set; } = "更新列表";
    public string ClearLogs { get; set; } = "清空日志";
    public string OpenOutput { get; set; } = "打开输出";
    public string OpenSource { get; set; } = "打开源";
    public string ZoomIn { get; set; } = "放大";
    public string ZoomOut { get; set; } = "缩小";
    
    // 状态显示。
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
    
    // 标签页标题。
    public string FileListTab { get; set; } = "待处理文件列表";
    public string SuccessLogTab { get; set; } = "成功记录";
    public string FailLogTab { get; set; } = "失败记录";
    public string CommandLogTab { get; set; } = "命令日志";
    public string CompressionTab { get; set; } = "压缩配置";
    public string DecompressionTab { get; set; } = "解压配置";
    public string LogsTab { get; set; } = "日志";
    public string StartTab { get; set; } = "开始";
    
    // 对话框按钮。
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
    
    // 日志文本。
    public string DroppedFolder { get; set; } = "拖入文件夹: ";
    public string DroppedTxtFile { get; set; } = "拖入TXT文件: ";
    public string Ready { get; set; } = "就绪";
    public string CompressionComplete { get; set; } = "压缩完成";
    public string DecompressionComplete { get; set; } = "解压完成";
    public string SuccessFailMessage { get; set; } = "成功: {0}, 失败: {1}";
    public string NoFilesToProcess { get; set; } = "没有要处理的文件";
    public string TryingToLoadAutomatically { get; set; } = "列表中没有文件，正在自动加载...";
    public string StillNoFiles { get; set; } = "仍然没有要处理的文件";
    public string CancellingOperation { get; set; } = "正在取消操作...";
    public string PasswordSetSuccessfully { get; set; } = "密码设置成功";
    public string Warning7zFormat { get; set; } = "7z 使用官方 7-Zip；恢复记录、快速打开和 RAR 注释选项不会应用。";
    public string SolidDisabledForStore { get; set; } = "存储模式下已禁用固实压缩";

    // 运行消息：压缩与解压流程日志、完成统计、通知与取消提示。
    public string CompressionPageOnly { get; set; } = "压缩命令只能从压缩配置页或开始页启动。";
    public string DecompressionPageOnly { get; set; } = "解压命令只能从解压配置页或开始页启动。";
    public string OptionConflict { get; set; } = "选项冲突";
    public string UpdateConflictsLockArchive { get; set; } = "更新现有文件不能与锁定归档同时使用。请取消其中一个选项。";
    public string OperationCompletionSummary { get; set; } = "\n完成: 成功={0}, 归档失败={1}, 后处理失败={2}, 忽略={3}, 未找到={4}, 分卷不完整={5}, 歧义={6}";
    public string OperationResultSummary { get; set; } = "成功: {0}, 归档失败: {1}, 后处理失败: {2}";
    public string OperationCancelled { get; set; } = "{0}已取消";
    public string OperationError { get; set; } = "错误: {0}";

    // 关机计划与取消关机。
    public string ShutdownScheduledLog { get; set; } = "已请求系统在一分钟后关机，可使用“取消关机”撤销。";
    public string ShutdownScheduledTitle { get; set; } = "关机计划";
    public string ShutdownScheduledBody { get; set; } = "系统将在一分钟后关机。可以在应用中取消。";
    public string ShutdownCancelledLog { get; set; } = "已请求取消关机。";
    public string ShutdownCancelledTitle { get; set; } = "已取消关机";
    public string ShutdownCancelledBody { get; set; } = "已向系统发送取消关机请求。";

    // 密码查询结果行。
    public string PasswordQueryArchiveName { get; set; } = "归档名: {0}";
    public string PasswordQueryBasis { get; set; } = "密码依据: {0}";
    public string PasswordQueryCompression { get; set; } = "压缩密码: {0}";
    public string PasswordQueryDecompression { get; set; } = "解压密码: {0}";
    public string PasswordQueryUtf8Full { get; set; } = "UTF8-8位: {0}";
    public string PasswordQueryUtf8Short { get; set; } = "UTF8-4位: {0}";
    public string PasswordQueryGb2312 { get; set; } = "GB2312-4位: {0}";
    public string PasswordQueryLegacyHeader { get; set; } = "旧版兼容密码:";

    // 阶段性进度通知。
    public string ProgressNotificationTitle { get; set; } = "{0}进行中";
    public string ProgressNotificationBody { get; set; } = "已处理 {0} 项，成功 {1}，归档失败 {2}，后处理失败 {3}，当前：{4}{5}";
    public string ProgressNotificationRemaining { get; set; } = "，剩余约 {0}";

    // 文本清单导入诊断。
    public string CompressionListLabel { get; set; } = "压缩路径清单";
    public string PasswordBookLabel { get; set; } = "密码本";
    public string TextImportSummary { get; set; } = "[{0}] 请求={1}，已匹配={2}，大小={3} GB，预计约 {4} 秒";
    public string TextImportMissing { get; set; } = "[{0}] 未找到 {1} 项:";
    public string TextImportIncompleteVolumes { get; set; } = "[{0}] 分卷不完整 {1} 项:";
    public string TextImportAmbiguous { get; set; } = "[{0}] 名称或编号存在歧义 {1} 项:";
    public string PasswordBookUnmatched { get; set; } = "[密码本] 以下归档未在密码本找到，共 {0} 个:";
    public string PasswordBookVolumeCandidates { get; set; } = "[密码本] 以下归档疑似分卷，共 {0} 个:";

    // 目录大小统计错误。
    public string DirectorySizeFileError { get; set; } = "读取文件大小失败：{0}：{1}";
    public string DirectorySizeAccessError { get; set; } = "访问目录失败：{0}：{1}";

    // 批处理服务逐项状态（同时用于界面日志与命令行输出）。
    public string FormatCannotCreate { get; set; } = "不支持创建 {0} 格式归档。当前支持：{1}。";
    public string ItemNotFound { get; set; } = "未找到: {0}";
    public string SkipExistingOutput { get; set; } = "[跳过] 已存在：{0}";
    public string CompressionStarted { get; set; } = "[开始压缩] {0}";
    public string ExtractionStarted { get; set; } = "[开始解压] {0}";
    public string ItemSucceeded { get; set; } = "成功: {0}";
    public string ItemFailed { get; set; } = "失败: {0} - {1}";
    public string ItemSucceededWithPostProcessFailure { get; set; } = "成功但后处理失败: {0}";
    public string SizeLimitReached { get; set; } = "已达到大小上限";
    public string CannotResolveArchive { get; set; } = "无法解析: {0} - {1}";
    public string CaseAmbiguitySkipped { get; set; } = "文件名大小写存在歧义，已跳过: {0}";
    public string DuplicateVolumeSkipped { get; set; } = "分卷编号重复，已跳过: {0}";
    public string IncompleteVolumesSkipped { get; set; } = "分卷不完整，已跳过：{0}（{1}）";
    public string MissingFirstVolume { get; set; } = "缺少编号 1 的首卷";
    public string MissingVolumeNumbers { get; set; } = "缺少分卷编号 {0}";
    // 归档程序原始输出的日志前缀（后接 command/stdout/stderr 固定英文技术词）。
    public string CompressionCommandLabel { get; set; } = "压缩命令";
    public string ExtractionCommandLabel { get; set; } = "解压命令";
}
