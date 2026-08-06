using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Interfaces;

/// <summary>
/// 系统集成功能接口
/// </summary>
// GPT-5, 2026-08-05：隔离桌面能力与批处理编排，使 GUI 和无界面调用方可以按操作系统提供通知、剪贴板、目录和关机行为。
public interface ISystemIntegration
{
    /// <summary>
    /// 在系统文件管理器中打开目录。
    /// </summary>
    Task OpenFolderAsync(string path);
    
    /// <summary>
    /// 从系统剪贴板读取文本。
    /// </summary>
    Task<string?> ReadClipboardTextAsync();
    
    /// <summary>
    /// 将文本写入系统剪贴板。
    /// </summary>
    Task WriteClipboardTextAsync(string text);
    
    /// <summary>
    /// 显示系统通知。
    /// </summary>
    void ShowNotification(string title, string message);
    
    /// <summary>
    /// 请求系统关机（如果当前平台支持）。
    /// </summary>
    Task ShutdownAsync();
    
    /// <summary>
    /// 取消系统关机（如果当前平台支持）。
    /// </summary>
    Task CancelShutdownAsync();
}
