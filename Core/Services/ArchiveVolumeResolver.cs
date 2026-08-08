using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 归档文件类型。RAR 单独使用官方 RAR，其他已识别格式由 7zz 解压。
/// </summary>
public enum ArchiveKind
{
    Unknown,
    Rar,
    SevenZip,
    Zip,
    Other
}

/// <summary>
/// 本程序明确支持的分卷命名形式。
/// </summary>
public enum ArchiveVolumeKind
{
    None,
    RarPart,
    SevenZipNumeric,
    ZipNumeric,
    SevenZipOtherNumeric
}

/// <summary>
/// 单个分卷及其数值编号。编号按数值排序，避免 part10 排在 part2 前面。
/// </summary>
public sealed record ArchiveVolume(string Path, long Number);

/// <summary>
/// 一次解析得到的全部归档信息。调用方不得再次用扩展名或正则自行猜测分卷组。
/// </summary>
public sealed class ArchiveVolumeResolveResult
{
    public string RequestedPath { get; init; } = string.Empty;
    public ArchiveKind ArchiveKind { get; init; }
    public ArchiveVolumeKind VolumeKind { get; init; }
    public string ActualExtension { get; init; } = string.Empty;
    public string LogicalArchiveName { get; init; } = string.Empty;
    public string? FirstVolumePath { get; init; }
    public IReadOnlyList<ArchiveVolume> Volumes { get; init; } = [];
    public IReadOnlyList<long> MissingNumbers { get; init; } = [];
    public bool HasCaseAmbiguity { get; init; }
    public bool HasDuplicateNumbers { get; init; }
    public bool HasRequiredFirstVolume { get; init; }
    public bool Exists => FirstVolumePath != null && File.Exists(FirstVolumePath);
    public bool IsMultiVolume => VolumeKind != ArchiveVolumeKind.None;
    public bool IsSequenceContiguous => MissingNumbers.Count == 0;
    public bool CanExtract => Exists && !HasCaseAmbiguity && !HasDuplicateNumbers &&
                              (!IsMultiVolume || HasRequiredFirstVolume && IsSequenceContiguous);
    public IReadOnlyList<string> FilesForPostProcessing => IsMultiVolume
        ? Volumes.Select(volume => volume.Path).ToArray()
        : FirstVolumePath == null ? [] : [FirstVolumePath];
}

/// <summary>
/// GPT-5, 2026-08-07：统一解析单卷、RAR part 分卷和 7zz 数字分卷。
/// 解析结果是解压入口、完整性校验和后处理文件集合的唯一依据。
/// </summary>
public static class ArchiveVolumeResolver
{
    private static readonly Regex RarPartRegex = new(
        @"^(?<base>.+)\.part(?<number>\d+)\.rar$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SevenZipNumericRegex = new(
        @"^(?<base>.+)\.7z\.(?<number>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ZipNumericRegex = new(
        @"^(?<base>.+)\.zip\.(?<number>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SevenZipOtherNumericRegex = new(
        $@"^(?<base>.+)\.(?<extension>{ArchiveFormatCatalog.GetSupportedExtensionPattern()})\.(?<number>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ArchiveVolumeResolveResult Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var requestedPath = Path.GetFullPath(path);
        var requestedName = Path.GetFileName(requestedPath);
        var directory = Path.GetDirectoryName(requestedPath) ?? Directory.GetCurrentDirectory();
        var requestedPattern = ParseVolumePattern(requestedName);

        string? actualPath = null;
        var hasCaseAmbiguity = false;
        if (File.Exists(requestedPath))
        {
            actualPath = requestedPath;
        }
        else if (Directory.Exists(directory))
        {
            var caseMatches = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(candidate => string.Equals(
                    Path.GetFileName(candidate), requestedName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            hasCaseAmbiguity = caseMatches.Length > 1;
            if (caseMatches.Length == 1)
            {
                actualPath = caseMatches[0];
            }
        }

        // 已存在同名单卷时优先使用单卷；只有单卷不存在时才推断同名分卷组。
        var effectivePattern = actualPath == null
            ? requestedPattern
            : ParseVolumePattern(Path.GetFileName(actualPath));
        if (actualPath != null && effectivePattern.Kind == ArchiveVolumeKind.None)
        {
            return CreateSingleVolumeResult(requestedPath, actualPath, hasCaseAmbiguity);
        }

        if (effectivePattern.Kind == ArchiveVolumeKind.None)
        {
            effectivePattern = CreatePatternFromSingleArchiveName(requestedName);
        }

        if (effectivePattern.Kind == ArchiveVolumeKind.None || !Directory.Exists(directory))
        {
            return new ArchiveVolumeResolveResult
            {
                RequestedPath = requestedPath,
                ArchiveKind = DetectArchiveKind(requestedName),
                ActualExtension = DetectArchiveExtension(requestedName),
                LogicalArchiveName = requestedName,
                HasCaseAmbiguity = hasCaseAmbiguity
            };
        }

        var volumes = EnumerateVolumes(directory, effectivePattern).ToArray();
        var duplicateNumbers = volumes.GroupBy(volume => volume.Number).Any(group => group.Count() > 1);
        var firstVolumes = volumes.Where(volume => volume.Number == 1).ToArray();
        var hasRequiredFirstVolume = firstVolumes.Length == 1;
        var presentNumbers = volumes.Select(volume => volume.Number).ToHashSet();
        var missingNumbers = FindMissingNumbers(presentNumbers);

        return new ArchiveVolumeResolveResult
        {
            RequestedPath = requestedPath,
            ArchiveKind = KindForVolume(effectivePattern),
            VolumeKind = effectivePattern.Kind,
            ActualExtension = effectivePattern.Extension,
            LogicalArchiveName = effectivePattern.LogicalArchiveName,
            FirstVolumePath = hasRequiredFirstVolume ? firstVolumes[0].Path : null,
            Volumes = volumes,
            MissingNumbers = missingNumbers,
            HasCaseAmbiguity = hasCaseAmbiguity,
            HasDuplicateNumbers = duplicateNumbers,
            HasRequiredFirstVolume = hasRequiredFirstVolume
        };
    }

    /// <summary>
    /// 返回创建输出对应的基础文件和全部已存在分卷。与 Resolve 的“单卷优先”语义不同，
    /// 此方法用于覆盖、跳过和大小统计，必须同时看到基础文件与遗留分卷。
    /// </summary>
    public static IReadOnlyList<string> ResolveOutputFiles(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var files = new List<string>();

        if (File.Exists(fullPath))
        {
            files.Add(fullPath);
        }

        if (Directory.Exists(directory))
        {
            var pattern = CreatePatternFromSingleArchiveName(Path.GetFileName(fullPath));
            if (pattern.Kind != ArchiveVolumeKind.None)
            {
                files.AddRange(EnumerateVolumes(directory, pattern).Select(volume => volume.Path));
            }
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ArchiveKind DetectArchiveKind(string path)
    {
        var name = Path.GetFileName(path);
        var pattern = ParseVolumePattern(name);
        if (pattern.Kind != ArchiveVolumeKind.None)
        {
            return KindForVolume(pattern);
        }

        var extension = ArchiveFormatCatalog.Normalize(DetectArchiveExtension(name));
        return extension switch
        {
            "rar" => ArchiveKind.Rar,
            "7z" => ArchiveKind.SevenZip,
            "zip" => ArchiveKind.Zip,
            { Length: > 0 } when ArchiveFormatCatalog.CanExtract(extension) => ArchiveKind.Other,
            _ => ArchiveKind.Unknown
        };
    }

    public static string DetectArchiveExtension(string path)
    {
        var name = Path.GetFileName(path);
        var pattern = ParseVolumePattern(name);
        return pattern.Kind != ArchiveVolumeKind.None
            ? pattern.Extension
            : Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
    }

    public static bool HasArchiveExtension(string path) => DetectArchiveKind(path) != ArchiveKind.Unknown;

    public static bool MatchesFormat(string path, string extension)
    {
        var normalized = ArchiveFormatCatalog.Normalize(extension);
        return ArchiveFormatCatalog.Normalize(DetectArchiveExtension(path))
            .Equals(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static ArchiveVolumeResolveResult CreateSingleVolumeResult(
        string requestedPath,
        string actualPath,
        bool hasCaseAmbiguity)
    {
        var actualName = Path.GetFileName(actualPath);
        return new ArchiveVolumeResolveResult
        {
            RequestedPath = requestedPath,
            ArchiveKind = DetectArchiveKind(actualName),
            ActualExtension = DetectArchiveExtension(actualName),
            LogicalArchiveName = actualName,
            FirstVolumePath = actualPath,
            HasCaseAmbiguity = hasCaseAmbiguity,
            HasRequiredFirstVolume = true
        };
    }

    private static IEnumerable<ArchiveVolume> EnumerateVolumes(string directory, VolumePattern pattern)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => (Path: path, Match: pattern.Matcher.Match(Path.GetFileName(path))))
            .Where(item => item.Match.Success)
            .Select(item => new ArchiveVolume(
                item.Path,
                long.Parse(item.Match.Groups["number"].Value, CultureInfo.InvariantCulture)))
            .OrderBy(volume => volume.Number)
            .ThenBy(volume => volume.Path, StringComparer.Ordinal)
            .ToArray();
    }

    // GPT-5, 2026-08-07：异常的大编号不能触发超大 Enumerable.Range 分配；诊断最多保留前 1000 个缺号。
    private static IReadOnlyList<long> FindMissingNumbers(HashSet<long> presentNumbers)
    {
        if (presentNumbers.Count == 0)
        {
            return [];
        }

        const int diagnosticLimit = 1000;
        var missing = new List<long>();
        var expected = 1L;
        foreach (var actual in presentNumbers.Order())
        {
            while (expected < actual && missing.Count < diagnosticLimit)
            {
                missing.Add(expected++);
            }

            if (missing.Count >= diagnosticLimit)
            {
                break;
            }

            expected = actual + 1;
        }

        return missing;
    }

    private static VolumePattern CreatePatternFromSingleArchiveName(string name)
    {
        var extension = DetectArchiveExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        return extension switch
        {
            "rar" => CreatePattern(ArchiveVolumeKind.RarPart, baseName, name),
            "7z" => CreatePattern(ArchiveVolumeKind.SevenZipNumeric, baseName, name),
            "zip" => CreatePattern(ArchiveVolumeKind.ZipNumeric, baseName, name),
            _ when ArchiveFormatCatalog.CanExtract(extension) =>
                CreatePattern(ArchiveVolumeKind.SevenZipOtherNumeric, baseName, name, extension),
            _ => VolumePattern.None
        };
    }

    private static VolumePattern ParseVolumePattern(string name)
    {
        foreach (var (kind, regex) in new[]
                 {
                     (ArchiveVolumeKind.RarPart, RarPartRegex),
                     (ArchiveVolumeKind.SevenZipNumeric, SevenZipNumericRegex),
                     (ArchiveVolumeKind.ZipNumeric, ZipNumericRegex)
                 })
        {
            var match = regex.Match(name);
            if (match.Success)
            {
                return CreatePattern(kind, match.Groups["base"].Value, name);
            }
        }

        var genericMatch = SevenZipOtherNumericRegex.Match(name);
        if (genericMatch.Success)
        {
            var extension = genericMatch.Groups["extension"].Value;
            var normalizedExtension = ArchiveFormatCatalog.Normalize(extension);
            if (normalizedExtension is not ("rar" or "7z" or "zip") &&
                ArchiveFormatCatalog.CanExtract(normalizedExtension))
            {
                return CreatePattern(
                    ArchiveVolumeKind.SevenZipOtherNumeric,
                    genericMatch.Groups["base"].Value,
                    name,
                    extension);
            }
        }

        return VolumePattern.None;
    }

    private static VolumePattern CreatePattern(
        ArchiveVolumeKind kind,
        string baseName,
        string logicalName,
        string? extension = null)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            return VolumePattern.None;
        }

        var escapedBase = Regex.Escape(baseName);
        var expression = kind switch
        {
            ArchiveVolumeKind.RarPart => $@"^{escapedBase}\.part(?<number>\d+)\.rar$",
            ArchiveVolumeKind.SevenZipNumeric => $@"^{escapedBase}\.7z\.(?<number>\d+)$",
            ArchiveVolumeKind.ZipNumeric => $@"^{escapedBase}\.zip\.(?<number>\d+)$",
            ArchiveVolumeKind.SevenZipOtherNumeric =>
                $@"^{escapedBase}\.{Regex.Escape(extension ?? string.Empty)}\.(?<number>\d+)$",
            _ => "(?!)"
        };
        var archiveName = kind switch
        {
            ArchiveVolumeKind.RarPart => baseName + ".rar",
            ArchiveVolumeKind.SevenZipNumeric => baseName + ".7z",
            ArchiveVolumeKind.ZipNumeric => baseName + ".zip",
            ArchiveVolumeKind.SevenZipOtherNumeric => baseName + "." + extension,
            _ => logicalName
        };
        var actualExtension = kind switch
        {
            ArchiveVolumeKind.RarPart => "rar",
            ArchiveVolumeKind.SevenZipNumeric => "7z",
            ArchiveVolumeKind.ZipNumeric => "zip",
            ArchiveVolumeKind.SevenZipOtherNumeric => extension ?? string.Empty,
            _ => string.Empty
        };
        return new VolumePattern(
            kind,
            archiveName,
            actualExtension,
            new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    private static ArchiveKind KindForVolume(VolumePattern pattern) => pattern.Kind switch
    {
        ArchiveVolumeKind.RarPart => ArchiveKind.Rar,
        ArchiveVolumeKind.SevenZipNumeric => ArchiveKind.SevenZip,
        ArchiveVolumeKind.ZipNumeric => ArchiveKind.Zip,
        ArchiveVolumeKind.SevenZipOtherNumeric => ArchiveKind.Other,
        _ => ArchiveKind.Unknown
    };

    private sealed record VolumePattern(
        ArchiveVolumeKind Kind,
        string LogicalArchiveName,
        string Extension,
        Regex Matcher)
    {
        public static VolumePattern None { get; } =
            new(ArchiveVolumeKind.None, string.Empty, string.Empty, new Regex("(?!)"));
    }
}
