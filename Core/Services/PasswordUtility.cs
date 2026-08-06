using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 基于 MD5 的密码生成工具。
/// 算法从原 WinForms 应用迁移而来。
/// </summary>
// GPT-5, 2026-08-05：集中处理确定性的旧版兼容密码派生。这些方法不是用户数据的密码学保护，
// 仅用于复现历史归档密码。
public static class PasswordUtility
{
    /// <summary>
    /// 使用 UTF-8 编码计算 MD5，并从第 7 位开始截取 8 个字符。
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
        
        // 保留旧版固定偏移，不能改为从摘要开头截取。
        return sb.ToString().Substring(7, 8);
    }
    
    /// <summary>
    /// 使用 UTF-8 编码计算 MD5，并从第 7 位开始截取 4 个字符。
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
        
        // 保留旧版固定偏移，确保历史密码仍可查询。
        return sb.ToString().Substring(7, 4);
    }
    
    /// <summary>
    /// 使用 GB2312 编码计算 MD5，并从第 7 位开始截取 4 个字符。
    /// 该算法仅用于兼容历史密码。
    /// </summary>
    public static string MD5GB2312(string text)
    {
        using var md5 = MD5.Create();
        
        // 现代 .NET 默认未注册 GB2312，只在需要旧版算法时注册编码提供程序。
        // GPT-5, 2026-08-05：现代 .NET 默认未注册 GB2312，仅在旧版密码查询时注册该编码。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] buffer = Encoding.GetEncoding("gb2312").GetBytes(text);
        byte[] hash = md5.ComputeHash(buffer);
        
        StringBuilder sb = new();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        
        // 截取位置必须与 WinForms 版本一致。
        return sb.ToString().Substring(7, 4);
    }
    
    /// <summary>
    /// 根据带扩展名的文件名生成压缩密码。
    /// </summary>
    public static string GenerateCompressionPassword(string filenameWithExtension)
    {
        string part1 = MD5UTF878(filenameWithExtension + "592ptt1314");
        string part2 = MD5UTF878(filenameWithExtension + "592pnn1314");
        return part1 + part2;
    }
    
    /// <summary>
    /// 根据归档文件名生成解压密码。
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
