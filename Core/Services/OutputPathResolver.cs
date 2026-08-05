using System;
using System.IO;

namespace BatchCompress.Avalonia.Core.Services;

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
