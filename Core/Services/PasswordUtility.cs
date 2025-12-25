using System;
using System.Security.Cryptography;
using System.Text;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// MD5-based password generation utilities
/// Ported from the original WinForms application
/// </summary>
public static class PasswordUtility
{
    /// <summary>
    /// MD5 hash with UTF8 encoding, returns 8 characters starting from position 7
    /// </summary>
    public static string MD5UTF878(string text)
    {
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
    
    /// <summary>
    /// Generate unlock password for advanced features
    /// Based on day of year
    /// </summary>
    public static string GenerateUnlockPassword()
    {
        int dayOfYear = DateTime.Now.DayOfYear;
        string source = dayOfYear.ToString();
        return MD5UTF878(source + "unlock");
    }
}
