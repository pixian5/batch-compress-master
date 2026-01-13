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
        }).ConfigureAwait(false);
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
#pragma warning disable CS0618 // Type or member is obsolete
                    return await clipboard.GetTextAsync().ConfigureAwait(false);
#pragma warning restore CS0618 // Type or member is obsolete
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
                    await clipboard.SetTextAsync(text).ConfigureAwait(false);
                }
            }
        }
        catch { }
    }
    
    public void ShowNotification(string title, string message)
    {
        Debug.WriteLine($"Notification: {title} - {message}");
        
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var script = $"display notification \"{message.Replace("\"", "\\\"")}\" with title \"{title.Replace("\"", "\\\"")}\"";
                Process.Start("osascript", $"-e '{script}'");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 使用 PowerShell 发送通知 (Windows 10+)
                var psCommand = $"[void] [System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms'); " +
                                $"$objNotifyIcon = New-Object System.Windows.Forms.NotifyIcon; " +
                                $"$objNotifyIcon.Icon = [System.Drawing.SystemIcons]::Information; " +
                                $"$objNotifyIcon.BalloonTipTitle = '{title.Replace("'", "''")}'; " +
                                $"$objNotifyIcon.BalloonTipText = '{message.Replace("'", "''")}'; " +
                                $"$objNotifyIcon.Visible = $True; " +
                                $"$objNotifyIcon.ShowBalloonTip(5000); " +
                                $"Start-Sleep -Seconds 1; " +
                                $"$objNotifyIcon.Dispose()";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("notify-send", $"\"{title}\" \"{message}\"");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show notification: {ex.Message}");
        }
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
        }).ConfigureAwait(false);
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
        }).ConfigureAwait(false);
    }
}
