using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// MD5-based password generation utilities
/// Ported from the original WinForms application
/// </summary>
// GPT-5, 2026-08-05：集中处理确定性的旧版兼容密码派生。这些方法不是用户数据的密码学保护，
// 仅用于复现历史归档密码。
public static class PasswordUtility
{
    /// <summary>
    /// MD5 hash with UTF8 encoding, returns 8 characters starting from position 7
    /// </summary>
    public static string MD5UTF878(string text)
    {
        // GPT-5, 2026-08-05：保留 UTF-8 字节编码和历史截取偏移，确保密码兼容。
        using var md5 = MD5.Create();
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        byte[] hash = md5.ComputeHash(buffer);
        
        StringBuilder sb = new();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        
        // Return 8 characters starting from position 7
        return sb.ToString().Substring(7, 8);
    }
    
    /// <summary>
    /// MD5 hash with UTF8 encoding, returns 4 characters starting from position 7
    /// </summary>
    public static string MD5UTF874(string text)
    {
        using var md5 = MD5.Create();
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        byte[] hash = md5.ComputeHash(buffer);
        
        StringBuilder sb = new();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        
        // Return 4 characters starting from position 7
        return sb.ToString().Substring(7, 4);
    }
    
    /// <summary>
    /// MD5 hash with GB2312 encoding, returns 4 characters starting from position 7
    /// Used for compatibility with legacy passwords
    /// </summary>
    public static string MD5GB2312(string text)
    {
        using var md5 = MD5.Create();
        
        // Register code pages provider for GB2312
        // GPT-5, 2026-08-05：现代 .NET 默认未注册 GB2312，仅在旧版密码查询时注册该编码。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] buffer = Encoding.GetEncoding("gb2312").GetBytes(text);
        byte[] hash = md5.ComputeHash(buffer);
        
        StringBuilder sb = new();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        
        // Return 4 characters starting from position 7
        return sb.ToString().Substring(7, 4);
    }
    
    /// <summary>
    /// Generate random password for compression
    /// Based on filename (with extension)
    /// </summary>
    public static string GenerateCompressionPassword(string filenameWithExtension)
    {
        string part1 = MD5UTF878(filenameWithExtension + "592ptt1314");
        string part2 = MD5UTF878(filenameWithExtension + "592pnn1314");
        return part1 + part2;
    }
    
    /// <summary>
    /// Generate random password for decompression
    /// Based on archive filename
    /// </summary>
    public static string GenerateDecompressionPassword(string archiveFilename)
    {
        return GenerateCompressionPassword(archiveFilename);
    }
    
    public static IReadOnlyList<string> GetLegacyPasswordCandidates(string filenameWithExtension)
    {
        // GPT-5, 2026-08-05：按原 WinForms 顺序返回候选值，方便用户核对熟悉的结果。
        return
        [
            MD5GB2312(filenameWithExtension + "5") + "@" + MD5GB2312(filenameWithExtension + "2") + ".com#" + MD5GB2312(filenameWithExtension + "tt"),
            MD5GB2312(filenameWithExtension + "592") + "@" + MD5GB2312(filenameWithExtension + "ptt") + ".com#" + MD5GB2312(filenameWithExtension + "1314"),
            MD5UTF874(filenameWithExtension + "5") + "@" + MD5UTF874(filenameWithExtension + "2") + ".com#" + MD5UTF874(filenameWithExtension + "tt"),
            MD5UTF874(filenameWithExtension + "592") + "@" + MD5UTF874(filenameWithExtension + "ptt") + ".com#" + MD5UTF874(filenameWithExtension + "1314"),
            MD5UTF878(filenameWithExtension + "592ptt1314") + MD5UTF878(filenameWithExtension + "592pnn1314 7,8")
        ];
    }
}
