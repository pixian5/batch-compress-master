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
/// RarOldStyle / ZipOldStyle 为旧式分卷：主卷 base.rar / base.zip 之外，
/// 后续卷命名为 base.r00…、base.z01…（编号至少两位）。
/// </summary>
public enum ArchiveVolumeKind
{
    None,
    RarPart,
    SevenZipNumeric,
    ZipNumeric,
    SevenZipOtherNumeric,
    RarOldStyle,
    ZipOldStyle
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

    // GLM-5.3, 2026-11-03：旧式 RAR / ZIP 分卷（base.r00…、base.z01…）。编号至少两位，
    // 避免把 file.r1、data.gz 之类普通扩展名误认成分卷；主卷 base.rar / base.zip 由
    // VolumePattern.MainVolumeFileName 显式并入，不依赖该正则匹配。
    private static readonly Regex RarOldStyleRegex = new(
        @"^(?<base>.+)\.r(?<number>\d{2,})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ZipOldStyleRegex = new(
        @"^(?<base>.+)\.z(?<number>\d{2,})$",
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
        // GLM-5.3, 2026-11-03：旧式分卷的主卷（base.rar / base.zip）本身就是普通归档名，
        // 不匹配任何分卷正则；若同目录存在分卷成员（base.rNN / base.zNN）却按单卷返回，
        // 解压后的删除/移动源只会处理主卷，成员会遗留成孤儿，且缺卷预检被跳过。
        // 因此进入单卷分支前先检查旧式成员，存在则按整组返回。
        var effectivePattern = actualPath == null
            ? requestedPattern
            : ParseVolumePattern(Path.GetFileName(actualPath));
        if (actualPath != null && effectivePattern.Kind == ArchiveVolumeKind.None)
        {
            var oldStyleGroup = TryBuildOldStyleGroupResult(directory, actualPath, requestedPath, hasCaseAmbiguity);
            if (oldStyleGroup != null)
            {
                return oldStyleGroup;
            }

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

        return BuildGroupResult(requestedPath, directory, effectivePattern, hasCaseAmbiguity);
    }

    /// <summary>
    /// 按分卷组模式枚举目录并构造完整解析结果；Resolve 主路径与旧式主卷入口共用，
    /// 保证两条路径的重复编号、首卷与缺卷判定完全一致。
    /// </summary>
    private static ArchiveVolumeResolveResult BuildGroupResult(
        string requestedPath,
        string directory,
        VolumePattern pattern,
        bool hasCaseAmbiguity)
    {
        var volumes = EnumerateVolumes(directory, pattern).ToArray();
        var duplicateNumbers = volumes.GroupBy(volume => volume.Number).Any(group => group.Count() > 1);
        var firstVolumes = volumes.Where(volume => volume.Number == 1).ToArray();
        var hasRequiredFirstVolume = firstVolumes.Length == 1;
        var presentNumbers = volumes.Select(volume => volume.Number).ToHashSet();
        var missingNumbers = FindMissingNumbers(presentNumbers);

        return new ArchiveVolumeResolveResult
        {
            RequestedPath = requestedPath,
            ArchiveKind = KindForVolume(pattern),
            VolumeKind = pattern.Kind,
            ActualExtension = pattern.Extension,
            LogicalArchiveName = pattern.LogicalArchiveName,
            FirstVolumePath = hasRequiredFirstVolume ? firstVolumes[0].Path : null,
            Volumes = volumes,
            MissingNumbers = missingNumbers,
            HasCaseAmbiguity = hasCaseAmbiguity,
            HasDuplicateNumbers = duplicateNumbers,
            HasRequiredFirstVolume = hasRequiredFirstVolume
        };
    }

    /// <summary>
    /// GLM-5.3, 2026-11-03：主卷入口（base.rar / base.zip）的旧式分卷组识别。
    /// 返回 null 表示旁边没有分卷成员，调用方按普通单卷处理。
    /// </summary>
    private static ArchiveVolumeResolveResult? TryBuildOldStyleGroupResult(
        string directory,
        string actualPath,
        string requestedPath,
        bool hasCaseAmbiguity)
    {
        var actualName = Path.GetFileName(actualPath);
        var extension = Path.GetExtension(actualName).TrimStart('.').ToLowerInvariant();
        var kind = extension switch
        {
            "rar" => ArchiveVolumeKind.RarOldStyle,
            "zip" => ArchiveVolumeKind.ZipOldStyle,
            _ => (ArchiveVolumeKind?)null
        };
        if (kind is null)
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(actualName);
        if (baseName.Length == 0)
        {
            return null;
        }

        var pattern = CreatePattern(kind.Value, baseName, actualName);
        // 只见到主卷、没有分卷成员（编号 ≥ 2）时保持单卷语义，不改变普通归档行为。
        if (!EnumerateVolumes(directory, pattern).Any(volume => volume.Number >= 2))
        {
            return null;
        }

        return BuildGroupResult(requestedPath, directory, pattern, hasCaseAmbiguity);
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
            // GLM-5.3, 2026-11-03：rar / zip 输出同时枚举新式（base.partN.rar、base.zip.N）
            // 与旧式（base.rNN、base.zNN）分卷组，覆盖或清理外部工具遗留的旧式分卷输出。
            var name = Path.GetFileName(fullPath);
            var patterns = CreateOutputPatterns(name);
            foreach (var pattern in patterns)
            {
                files.AddRange(EnumerateVolumes(directory, pattern).Select(volume => volume.Path));
            }
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 为单个输出归档名生成需要枚举的分卷组：主组始终是新式命名；
    /// rar / zip 额外附带旧式组，7z 等其它格式没有旧式命名，保持单组。
    /// </summary>
    private static IReadOnlyList<VolumePattern> CreateOutputPatterns(string name)
    {
        var primary = CreatePatternFromSingleArchiveName(name);
        if (primary.Kind == ArchiveVolumeKind.None)
        {
            return [];
        }

        var baseName = Path.GetFileNameWithoutExtension(name);
        if (baseName.Length == 0)
        {
            return [primary];
        }

        var extension = DetectArchiveExtension(name);
        return extension switch
        {
            "rar" => [primary, CreatePattern(ArchiveVolumeKind.RarOldStyle, baseName, name)],
            "zip" => [primary, CreatePattern(ArchiveVolumeKind.ZipOldStyle, baseName, name)],
            _ => [primary]
        };
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

    /// <summary>
    /// GLM-5.3, 2026-11-03：把组内编号换算为用户可直接对照的文件名后缀描述。
    /// 旧式分卷的组内编号与文件名后缀存在偏移（.r01 组内编号为 3），直接显示数字会误导，
    /// 因此旧式返回实际后缀（如 r01 / z02），part 分卷返回 partN.rar，数字分卷维持原数字。
    /// </summary>
    public static string DescribeVolumeSuffix(ArchiveVolumeResolveResult resolved, long number) =>
        resolved.VolumeKind switch
        {
            ArchiveVolumeKind.RarOldStyle => $"r{Math.Max(0, number - 2).ToString("D2", CultureInfo.InvariantCulture)}",
            ArchiveVolumeKind.ZipOldStyle => $"z{Math.Max(1, number - 1).ToString("D2", CultureInfo.InvariantCulture)}",
            ArchiveVolumeKind.RarPart => $"part{number.ToString(CultureInfo.InvariantCulture)}.rar",
            _ => number.ToString(CultureInfo.InvariantCulture)
        };

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
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();

        var volumes = files
            .Select(path => (Path: path, Match: pattern.Matcher.Match(Path.GetFileName(path))))
            .Where(item => item.Match.Success)
            .Select(item => new ArchiveVolume(
                item.Path,
                long.Parse(item.Match.Groups["number"].Value, CultureInfo.InvariantCulture) + pattern.NumberOffset));

        // GLM-5.3, 2026-11-03：旧式分卷的主卷（base.rar / base.zip）不匹配分卷正则，
        // 按主卷文件名显式并入并固定编号为 1；大小写变体会产生重复编号 1，
        // 由现有 HasDuplicateNumbers 检测自动判为歧义。
        if (pattern.MainVolumeFileName is not null)
        {
            volumes = volumes.Concat(files
                .Where(path => string.Equals(
                    Path.GetFileName(path), pattern.MainVolumeFileName, StringComparison.OrdinalIgnoreCase))
                .Select(path => new ArchiveVolume(path, 1)));
        }

        return volumes
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
                     (ArchiveVolumeKind.ZipNumeric, ZipNumericRegex),
                     (ArchiveVolumeKind.RarOldStyle, RarOldStyleRegex),
                     (ArchiveVolumeKind.ZipOldStyle, ZipOldStyleRegex)
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
            ArchiveVolumeKind.RarOldStyle => $@"^{escapedBase}\.r(?<number>\d{{2,}})$",
            ArchiveVolumeKind.ZipOldStyle => $@"^{escapedBase}\.z(?<number>\d{{2,}})$",
            ArchiveVolumeKind.SevenZipOtherNumeric =>
                $@"^{escapedBase}\.{Regex.Escape(extension ?? string.Empty)}\.(?<number>\d+)$",
            _ => "(?!)"
        };
        var archiveName = kind switch
        {
            ArchiveVolumeKind.RarPart => baseName + ".rar",
            ArchiveVolumeKind.SevenZipNumeric => baseName + ".7z",
            ArchiveVolumeKind.ZipNumeric => baseName + ".zip",
            ArchiveVolumeKind.RarOldStyle => baseName + ".rar",
            ArchiveVolumeKind.ZipOldStyle => baseName + ".zip",
            ArchiveVolumeKind.SevenZipOtherNumeric => baseName + "." + extension,
            _ => logicalName
        };
        var actualExtension = kind switch
        {
            ArchiveVolumeKind.RarPart => "rar",
            ArchiveVolumeKind.SevenZipNumeric => "7z",
            ArchiveVolumeKind.ZipNumeric => "zip",
            ArchiveVolumeKind.RarOldStyle => "rar",
            ArchiveVolumeKind.ZipOldStyle => "zip",
            ArchiveVolumeKind.SevenZipOtherNumeric => extension ?? string.Empty,
            _ => string.Empty
        };
        // GLM-5.3, 2026-11-03：旧式分卷的主卷文件名（base.rar / base.zip）不匹配分卷正则，
        // 由 MainVolumeFileName 显式标记并固定编号 1（组入口即主卷）。
        // 组内编号偏移按物理卷序设置：RAR 旧式 base.r00 是第 2 卷（0+2），ZIP 旧式 base.z01
        // 是第 2 卷（1+1），使全部卷编号从主卷的 1 起连续，缺卷检测复用现有编号模型。
        var mainVolumeFileName = kind switch
        {
            ArchiveVolumeKind.RarOldStyle or ArchiveVolumeKind.ZipOldStyle => archiveName,
            _ => null
        };
        var numberOffset = kind switch
        {
            ArchiveVolumeKind.RarOldStyle => 2,
            ArchiveVolumeKind.ZipOldStyle => 1,
            _ => 0
        };
        return new VolumePattern(
            kind,
            archiveName,
            actualExtension,
            new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            mainVolumeFileName,
            numberOffset);
    }

    private static ArchiveKind KindForVolume(VolumePattern pattern) => pattern.Kind switch
    {
        ArchiveVolumeKind.RarPart => ArchiveKind.Rar,
        ArchiveVolumeKind.SevenZipNumeric => ArchiveKind.SevenZip,
        ArchiveVolumeKind.ZipNumeric => ArchiveKind.Zip,
        ArchiveVolumeKind.RarOldStyle => ArchiveKind.Rar,
        ArchiveVolumeKind.ZipOldStyle => ArchiveKind.Zip,
        ArchiveVolumeKind.SevenZipOtherNumeric => ArchiveKind.Other,
        _ => ArchiveKind.Unknown
    };

    private sealed record VolumePattern(
        ArchiveVolumeKind Kind,
        string LogicalArchiveName,
        string Extension,
        Regex Matcher,
        string? MainVolumeFileName = null,
        long NumberOffset = 0)
    {
        public static VolumePattern None { get; } =
            new(ArchiveVolumeKind.None, string.Empty, string.Empty, new Regex("(?!)"));
    }
}
