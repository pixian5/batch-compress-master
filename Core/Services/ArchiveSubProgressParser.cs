using System;
using System.Text.RegularExpressions;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 归档进程输出解析器，用于从 7zz 和 WinRAR 的实时标准输出/错误输出提取分卷序号和进度百分比。
/// Antigravity, 2026-11-08：批次 16（P3-11）——解析 7zz/RAR 输出换算分卷子进度并由 NDJSON/UI 展示。
/// </summary>
public static class ArchiveSubProgressParser
{
    private static readonly Regex PercentageRegex = new(
        @"(?:^|\s)(\d{1,3})%",
        RegexOptions.Compiled);

    private static readonly Regex Volume7zRegex = new(
        @"\.(?:7z|zip)\.(\d{3,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VolumeRarRegex = new(
        @"\.part(\d+)\.rar",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 从输出行提取进度百分比（0-100）。若无匹配返回 null。
    /// </summary>
    public static int? TryParsePercentage(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var match = PercentageRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
        {
            return Math.Clamp(percent, 0, 100);
        }

        return null;
    }

    /// <summary>
    /// 从文件名或输出行提取分卷序号。
    /// </summary>
    public static int? TryParseVolumeNumber(string? pathOrText)
    {
        if (string.IsNullOrWhiteSpace(pathOrText)) return null;

        var m7z = Volume7zRegex.Match(pathOrText);
        if (m7z.Success && int.TryParse(m7z.Groups[1].Value, out var vol7z))
        {
            return vol7z;
        }

        var mRar = VolumeRarRegex.Match(pathOrText);
        if (mRar.Success && int.TryParse(mRar.Groups[1].Value, out var volRar))
        {
            return volRar;
        }

        return null;
    }
}
