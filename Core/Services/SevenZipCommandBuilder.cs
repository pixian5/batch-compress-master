using System;
using System.Collections.Generic;
using System.IO;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-06：7-Zip 参数以列表表达，不生成可被 Shell 再解析的命令字符串。
// 密码与路径即使包含空格或特殊字符，也始终作为单个 ArgumentList 元素传入 7zz。
public static class SevenZipCommandBuilder
{
    public static IReadOnlyList<string> BuildCompressionArguments(
        string input,
        string output,
        ArchiveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        ArgumentNullException.ThrowIfNull(options);
        var definition = GetCreatableDefinition(options.ArchiveFormat);
        if (definition.SupportsSingleFileOnly &&
            (Directory.Exists(input) || options.AdditionalInputs is { Length: > 0 }))
        {
            throw new NotSupportedException(
                $"{definition.Extension} 是单文件压缩流格式，不能直接压缩目录；请先选择 tar 或 7z。");
        }

        var format = definition.Extension;
        var arguments = new List<string>
        {
            "a",
            $"-t{definition.ToolFormat}",
            $"-mx={MapCompressionLevel(options.CompressionLevel)}"
        };
        if (format == "7z")
        {
            arguments.Add(options.SolidArchive ? "-ms=on" : "-ms=off");
        }

        if (!string.IsNullOrEmpty(options.Password) && !definition.SupportsPassword)
        {
            throw new NotSupportedException($"{format} 格式不支持密码加密。");
        }

        AddPassword(arguments, options.Password, encryptFileNames: format == "7z");
        if (format == "zip" && !string.IsNullOrEmpty(options.Password))
        {
            arguments.Add("-mem=AES256");
        }

        if (!string.IsNullOrWhiteSpace(options.VolumeSize))
        {
            arguments.Add($"-v{options.VolumeSize.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            Directory.CreateDirectory(options.TempDirectory);
            arguments.Add($"-w{options.TempDirectory}");
        }

        arguments.Add(output);
        arguments.Add(input);
        if (options.AdditionalInputs is { Length: > 0 })
        {
            arguments.AddRange(options.AdditionalInputs);
        }
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

        var arguments = new List<string> { "x" };
        arguments.Add(options.ExistingFileMode switch
        {
            ExistingFileMode.Skip => "-aos",
            ExistingFileMode.Update => "-aou",
            ExistingFileMode.Overwrite => "-aoa",
            _ => throw new ArgumentOutOfRangeException(nameof(options.ExistingFileMode))
        });
        AddPassword(arguments, options.Password, encryptFileNames: false);
        arguments.Add($"-o{outputDirectory}");
        arguments.Add(archivePath);
        return arguments;
    }

    public static IReadOnlyList<string> BuildTestArguments(string archivePath, string? password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var arguments = new List<string> { "t" };
        AddPassword(arguments, password, encryptFileNames: false);
        arguments.Add(archivePath);
        return arguments;
    }

    public static string NormalizeArchiveFormat(string? archiveFormat)
    {
        return GetCreatableDefinition(archiveFormat).Extension;
    }

    public static string GetToolFormat(string? archiveFormat) => GetCreatableDefinition(archiveFormat).ToolFormat;

    private static ArchiveFormatDefinition GetCreatableDefinition(string? archiveFormat)
    {
        if (ArchiveFormatCatalog.TryGet(archiveFormat, out var definition) && definition.CanCreate &&
            string.Equals(definition.Backend, "7zz", StringComparison.OrdinalIgnoreCase))
        {
            return definition;
        }

        throw new NotSupportedException($"7-Zip 后端不支持创建格式: {archiveFormat}。");
    }

    private static int MapCompressionLevel(CompressionLevel level) => level switch
    {
        CompressionLevel.Store => 0,
        CompressionLevel.Fastest => 1,
        CompressionLevel.Fast => 3,
        CompressionLevel.Normal => 5,
        CompressionLevel.Good => 7,
        CompressionLevel.Best => 9,
        _ => 5
    };

    private static void AddPassword(List<string> arguments, string? password, bool encryptFileNames)
    {
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        arguments.Add($"-p{password}");
        if (encryptFileNames)
        {
            arguments.Add("-mhe=on");
        }
    }
}

public static class SevenZipExitCodes
{
    // GPT-5, 2026-08-06：7-Zip 定义 0 为无错误、1 为非致命警告；两者均可能产生完整可用的输出。
    public static bool IsSuccess(int exitCode) => exitCode is 0 or 1;
}
