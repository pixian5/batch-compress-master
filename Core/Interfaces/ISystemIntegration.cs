using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Interfaces;

/// <summary>
/// 系统集成功能接口
/// </summary>
// GPT-5, 2026-08-05: Isolates desktop capabilities from batch orchestration so GUI and headless callers
// can provide appropriate notification, clipboard, folder and shutdown behavior per operating system.
public interface ISystemIntegration
{
    /// <summary>
    /// 在系统文件管理器中打开文件夹
    /// </summary>
    Task OpenFolderAsync(string path);
    
    /// <summary>
    /// 从剪贴板读取文本
    /// </summary>
    Task<string?> ReadClipboardTextAsync();
    
    /// <summary>
    /// 写入文本到剪贴板
    /// </summary>
    Task WriteClipboardTextAsync(string text);
    
    /// <summary>
    /// 显示系统通知
    /// </summary>
    void ShowNotification(string title, string message);
    
    /// <summary>
    /// 关闭系统（如支持）
    /// </summary>
    Task ShutdownAsync();
    
    /// <summary>
    /// 取消系统关闭（如支持）
    /// </summary>
    Task CancelShutdownAsync();
}
