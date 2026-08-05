using System;
using System.IO;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-05：集中处理输出目录回退，使空输出设置统一使用来源文件父目录，并向调用方返回已存在的绝对目录。
public static class OutputPathResolver
{
    public static string ResolveAndCreate(string? configuredPath, string sourcePath)
    {
        var outputDirectory = string.IsNullOrWhiteSpace(configuredPath)
            ? GetSourceParent(sourcePath)
            : Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static string GetSourceParent(string sourcePath)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        return Path.GetDirectoryName(fullSourcePath) ?? Directory.GetCurrentDirectory();
    }
}
