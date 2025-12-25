using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// Cross-platform system integration implementation
/// </summary>
public class SystemIntegrationService : ISystemIntegration
{
    public async Task OpenFolderAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        
        await Task.Run(() =>
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("explorer.exe", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", path);
                }
            }
            catch { }
        });
    }
    
    public async Task<string?> ReadClipboardTextAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    return await clipboard.GetTextAsync() ?? string.Empty;
                }
            }
        }
        catch { }
        
        return null;
    }
    
    public async Task WriteClipboardTextAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
        catch { }
    }
    
    public void ShowNotification(string title, string message)
    {
        // Avalonia doesn't have built-in notification support
        // This can be implemented with platform-specific code or third-party libraries
        // For now, just log it
        Debug.WriteLine($"Notification: {title} - {message}");
        
        // TODO: Implement platform-specific notifications
        // Windows: Use ToastNotification
        // Linux: Use libnotify
        // macOS: Use NSUserNotificationCenter
    }
    
    public async Task ShutdownAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("shutdown", "/s /t 60");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("shutdown", "-h +1");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("shutdown", "-h +1");
                }
            }
            catch { }
        });
    }
    
    public async Task CancelShutdownAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("shutdown", "/a");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("shutdown", "-c");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("killall", "shutdown");
                }
            }
            catch { }
        });
    }
}
