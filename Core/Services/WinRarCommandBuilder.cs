using System;
using System.Collections.Generic;
using System.IO;
using BatchCompress.Avalonia.Core.Interfaces;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-05: Produces one argument per WinRAR token. Callers pass this list through
// ProcessStartInfo.ArgumentList so spaces and metacharacters in paths cannot alter command structure.
public static class WinRarCommandBuilder
{
    public static IReadOnlyList<string> SupportedFormats { get; } = ["rar", "zip"];

    public static string NormalizeArchiveFormat(string? archiveFormat)
    {
        var normalized = archiveFormat?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "rar" => "rar",
            "zip" => "zip",
            _ => throw new NotSupportedException($"WinRAR 不支持压缩格式: {archiveFormat}")
        };
    }

    public static IReadOnlyList<string> BuildCompressionArguments(
        string input,
        string output,
        ArchiveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        ArgumentNullException.ThrowIfNull(options);

        // GPT-5, 2026-08-05: Normalize early because format controls mutually incompatible switches such as -s and -afzip.
        var format = NormalizeArchiveFormat(options.ArchiveFormat);
        var arguments = new List<string> { "a", "-ep1", "-IBCK", "-SCf" };

        AddExistingFileMode(arguments, options.ExistingFileMode);
        AddPassword(arguments, options.Password);
        arguments.Add($"-m{(int)options.CompressionLevel}");

        if (format == "zip")
        {
            arguments.Add("-afzip");
        }
        else if (options.SolidArchive)
        {
            arguments.Add("-s");
            arguments.Add("-md32");
            arguments.Add("-k");
        }

        if (!string.IsNullOrWhiteSpace(options.VolumeSize))
        {
            arguments.Add($"-v{options.VolumeSize.Trim()}");
        }

        if (options.RecoveryRecordPercent is > 0 and <= 100)
        {
            arguments.Add($"-rr{options.RecoveryRecordPercent}");
        }

        if (options.QuickOpen && format == "rar")
        {
            arguments.Add("-qo+");
        }

        if (options.TestArchive)
        {
            arguments.Add("-t");
        }

        if (!string.IsNullOrWhiteSpace(options.CommentFile) && File.Exists(options.CommentFile))
        {
            arguments.Add($"-z{options.CommentFile}");
        }

        if (!string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            Directory.CreateDirectory(options.TempDirectory);
            arguments.Add($"-w{options.TempDirectory}");
        }

        if (options.ExcludeExtensions is { Length: > 0 })
        {
            arguments.Add($"-ms{string.Join(";", options.ExcludeExtensions)}");
        }

        arguments.Add("-oi:50000000");
        arguments.Add(output);
        arguments.Add(input);
        return arguments;
    }

    public static IReadOnlyList<string> BuildExtractionArguments(
        string archivePath,
        string outputDirectory,
        ArchiveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);

        var arguments = new List<string> { "x", "-IBCK" };
        AddExistingFileMode(arguments, options.ExistingFileMode);
        AddPassword(arguments, options.Password);
        arguments.Add(archivePath);
        arguments.Add(outputDirectory);
        return arguments;
    }

    private static void AddExistingFileMode(List<string> arguments, ExistingFileMode existingFileMode)
    {
        switch (existingFileMode)
        {
            case ExistingFileMode.Skip:
                arguments.Add("-o-");
                break;
            case ExistingFileMode.Update:
                arguments.Add("-u");
                break;
            case ExistingFileMode.Overwrite:
                arguments.Add("-o+");
                break;
        }
    }

    private static void AddPassword(List<string> arguments, string? password)
    {
        if (!string.IsNullOrEmpty(password))
        {
            arguments.Add($"-p{password}");
        }
    }
}

public static class WinRarExitCodes
{
    public static bool IsSuccess(int exitCode) => exitCode is 0 or 1;
}
