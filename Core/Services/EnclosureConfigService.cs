using System;
using System.IO;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 附件联系方式的配置服务：把原先硬编码在 MainWindowViewModel 中的默认联系文案
/// 外置为用户可编辑的文本配置文件，解决硬编码技术债。
/// </summary>
// GLM-5.3, 2026-11-04：批次 8——附件联系方式外置。
// 配置文件查找优先级（跨平台）：
//   1) 构造时显式指定的 explicitConfigPath（自定义路径，测试与嵌入式场景）；
//   2) 应用目录相邻的 enclosures.txt（便携模式：存在即读写，与 logs/ 目录同一先例）；
//   3) 用户应用数据目录（Unix: ~/.config/batchcompress/enclosures.txt，
//      Windows: %APPDATA%\batchcompress\enclosures.txt）——均不存在时首次保存落在这里。
// 读取：配置文件存在且内容非空白则返回其内容，否则回退默认联系文案模版；
// 保存/读取的任何 IO 失败都不抛出（写警告到控制台并尽力而为），保证 GUI 启动路径安全。
public sealed class EnclosureConfigService
{
    private const string ConfigFileName = "enclosures.txt";

    private const string ConfigDirectoryName = "batchcompress";

    private readonly string? _explicitConfigPath;

    public EnclosureConfigService(string? explicitConfigPath = null)
    {
        _explicitConfigPath = string.IsNullOrWhiteSpace(explicitConfigPath)
            ? null
            : explicitConfigPath;
    }

    // GLM-5.3, 2026-11-04：批次 8——原 MainWindowViewModel 内硬编码的默认联系文案，
    // 逐字保持不变（存量用户的附件行为零漂移）。
    public static string GetDefaultEnclosuresText() =>
        "【解压密码】发邮件给 qgkc520@Gmail.com\n" +
        "【解压密码】微信号：i17269637581\n" +
        "【解压密码】QQ号：2027123419\n" +
        "【解压密码】微信号可能会改名，如果搜不到，请通过邮箱联系";

    /// <summary>
    /// 解析当前生效的配置文件路径：显式自定义路径 > 应用目录相邻 enclosures.txt（存在时）
    /// > 用户应用数据目录。仅计算路径，不做任何 IO。
    /// </summary>
    public string GetConfigFilePath()
    {
        if (_explicitConfigPath is not null)
        {
            return _explicitConfigPath;
        }

        var portablePath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (File.Exists(portablePath))
        {
            return portablePath;
        }

        return GetUserConfigPath();
    }

    /// <summary>
    /// 加载附件联系文本：配置文件存在且内容非空白时返回其内容（原样保留，含换行），
    /// 否则返回 <see cref="GetDefaultEnclosuresText"/> 默认模版；IO 异常同样回退默认模版。
    /// </summary>
    public string LoadEnclosuresText()
    {
        try
        {
            var path = GetConfigFilePath();
            if (!File.Exists(path))
            {
                return GetDefaultEnclosuresText();
            }

            var text = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? GetDefaultEnclosuresText() : text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load enclosure config '{GetConfigFilePath()}': {ex.Message}");
            return GetDefaultEnclosuresText();
        }
    }

    /// <summary>
    /// 保存用户编辑的附件联系文本到配置文件（UTF-8，必要时创建父目录）。
    /// 返回是否保存成功；失败写警告到控制台且不抛出，调用方可安全忽略。
    /// </summary>
    public bool SaveEnclosuresText(string text)
    {
        try
        {
            var path = GetConfigFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, text);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save enclosure config '{GetConfigFilePath()}': {ex.Message}");
            return false;
        }
    }

    // 用户应用数据目录：.NET 在 Unix（macOS/Linux）把 ApplicationData 映射为
    // $XDG_CONFIG_HOME 或 ~/.config，在 Windows 映射为 %APPDATA%，天然跨平台。
    private static string GetUserConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            // 极端环境下取不到用户目录时退回应用目录，保证服务仍可用。
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, ConfigDirectoryName, ConfigFileName);
    }
}
