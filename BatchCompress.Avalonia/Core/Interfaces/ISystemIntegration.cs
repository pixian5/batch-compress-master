using System.Threading.Tasks;

namespace BatchCompress.Avalonia.Core.Interfaces;

/// <summary>
/// Interface for system integration features
/// </summary>
public interface ISystemIntegration
{
    /// <summary>
    /// Open folder in system file manager
    /// </summary>
    Task OpenFolderAsync(string path);
    
    /// <summary>
    /// Read text from clipboard
    /// </summary>
    Task<string?> ReadClipboardTextAsync();
    
    /// <summary>
    /// Write text to clipboard
    /// </summary>
    Task WriteClipboardTextAsync(string text);
    
    /// <summary>
    /// Show system notification
    /// </summary>
    void ShowNotification(string title, string message);
    
    /// <summary>
    /// Shutdown the system (if supported)
    /// </summary>
    Task ShutdownAsync();
    
    /// <summary>
    /// Cancel system shutdown (if supported)
    /// </summary>
    Task CancelShutdownAsync();
}
