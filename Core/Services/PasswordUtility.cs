using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// MD5-based password generation utilities
/// Ported from the original WinForms application
/// </summary>
// GPT-5, 2026-08-05: Centralizes deterministic legacy-compatible password derivation. These helpers are
// intentionally not cryptographic protection for user data; they reproduce historical archive passwords.
public static class PasswordUtility
{
    /// <summary>
    /// MD5 hash with UTF8 encoding, returns 8 characters starting from position 7
    /// </summary>
    public static string MD5UTF878(string text)
    {
        // GPT-5, 2026-08-05: Preserve UTF-8 byte encoding and the historical substring offset for compatibility.
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
        // GPT-5, 2026-08-05: GB2312 is not registered by default on modern .NET; register it only for legacy queries.
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
        // GPT-5, 2026-08-05: Return candidates in the original WinForms order so users can compare familiar values.
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
