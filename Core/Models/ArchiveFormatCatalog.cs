using System;
using System.Collections.Generic;
using System.Linq;

namespace BatchCompress.Avalonia.Core.Models;

/// <summary>
/// 归档格式的工具能力。创建能力来自随包 7zz/RAR 的真实命令支持，不能仅凭文件扩展名推断。
/// </summary>
public sealed record ArchiveFormatDefinition(
    string Extension,
    string ToolFormat,
    string Backend,
    bool CanCreate,
    bool CanExtract,
    bool SupportsVolumes,
    bool SupportsPassword,
    bool SupportsSingleFileOnly,
    bool SupportsSolid,
    bool SupportsComment,
    bool SupportsRecovery);

/// <summary>
/// CLI、GUI、路由和目录扫描共用的格式能力目录。
/// </summary>
public static class ArchiveFormatCatalog
{
    private static readonly IReadOnlyList<ArchiveFormatDefinition> Definitions =
    [
        new("rar", "rar", "rar", true, true, true, true, false, true, true, true),
        new("7z", "7z", "7zz", true, true, true, true, false, true, false, false),
        new("zip", "zip", "7zz", true, true, true, true, false, false, false, false),
        new("tar", "tar", "7zz", true, true, true, false, false, false, false, false),
        new("gz", "gzip", "7zz", true, true, true, false, true, false, false, false),
        new("bz2", "bzip2", "7zz", true, true, true, false, true, false, false, false),
        new("xz", "xz", "7zz", true, true, true, false, true, false, false, false),
        new("wim", "wim", "7zz", true, true, true, false, false, false, false, false),

        // 7zz 可读取但当前不用于创建的格式。保持在同一目录，避免 CLI/GUI 各自维护白名单。
        new("ar", "ar", "7zz", false, true, false, false, false, false, false, false),
        new("arj", "arj", "7zz", false, true, false, false, false, false, false, false),
        new("cab", "cab", "7zz", false, true, false, false, false, false, false, false),
        new("chm", "chm", "7zz", false, true, false, false, false, false, false, false),
        new("cpio", "cpio", "7zz", false, true, false, false, false, false, false, false),
        new("dmg", "dmg", "7zz", false, true, false, false, false, false, false, false),
        new("iso", "iso", "7zz", false, true, false, false, false, false, false, false),
        new("lha", "lzh", "7zz", false, true, false, false, false, false, false, false),
        new("lzh", "lzh", "7zz", false, true, false, false, false, false, false, false),
        new("rpm", "rpm", "7zz", false, true, false, false, false, false, false, false),
        new("deb", "deb", "7zz", false, true, false, false, false, false, false, false),
        new("z", "z", "7zz", false, true, false, false, true, false, false, false),
        new("lzma", "lzma", "7zz", false, true, false, false, true, false, false, false),
        new("lzma86", "lzma86", "7zz", false, true, false, false, true, false, false, false),
        new("zst", "zstd", "7zz", false, true, false, false, true, false, false, false),
        new("qcow", "qcow", "7zz", false, true, false, false, false, false, false, false),
        new("qcow2", "qcow2", "7zz", false, true, false, false, false, false, false, false),
        new("squashfs", "squashfs", "7zz", false, true, false, false, false, false, false, false),
        new("vdi", "vdi", "7zz", false, true, false, false, false, false, false, false),
        new("vhd", "vhd", "7zz", false, true, false, false, false, false, false, false),
        new("vhdx", "vhdx", "7zz", false, true, false, false, false, false, false, false),
        new("vmdk", "vmdk", "7zz", false, true, false, false, false, false, false, false),
        new("xar", "xar", "7zz", false, true, false, false, false, false, false, false),
        new("apfs", "apfs", "7zz", false, true, false, false, false, false, false, false),
        new("apm", "apm", "7zz", false, true, false, false, false, false, false, false),
        new("b64", "b64", "7zz", false, true, false, false, true, false, false, false),
        new("chd", "chd", "7zz", false, true, false, false, false, false, false, false),
        new("coff", "coff", "7zz", false, true, false, false, false, false, false, false),
        new("compound", "compound", "7zz", false, true, false, false, false, false, false, false),
        new("cramfs", "cramfs", "7zz", false, true, false, false, false, false, false, false),
        new("elf", "elf", "7zz", false, true, false, false, false, false, false, false),
        new("ext", "ext", "7zz", false, true, false, false, false, false, false, false),
        new("fat", "fat", "7zz", false, true, false, false, false, false, false, false),
        new("flv", "flv", "7zz", false, true, false, false, false, false, false, false),
        new("gpt", "gpt", "7zz", false, true, false, false, false, false, false, false),
        new("hfs", "hfs", "7zz", false, true, false, false, false, false, false, false),
        new("ihex", "ihex", "7zz", false, true, false, false, true, false, false, false),
        new("mbr", "mbr", "7zz", false, true, false, false, false, false, false, false),
        new("macho", "macho", "7zz", false, true, false, false, false, false, false, false),
        new("mslz", "mslz", "7zz", false, true, false, false, false, false, false, false),
        new("mub", "mub", "7zz", false, true, false, false, false, false, false, false),
        new("ntfs", "ntfs", "7zz", false, true, false, false, false, false, false, false),
        new("nsis", "nsis", "7zz", false, true, false, false, false, false, false, false),
        new("pe", "pe", "7zz", false, true, false, false, false, false, false, false),
        new("ppmd", "ppmd", "7zz", false, true, false, false, false, false, false, false),
        new("sparse", "sparse", "7zz", false, true, false, false, false, false, false, false),
        new("swf", "swf", "7zz", false, true, false, false, false, false, false, false),
        new("te", "te", "7zz", false, true, false, false, false, false, false, false),
        new("udf", "udf", "7zz", false, true, false, false, false, false, false, false),
        new("uefi", "uefi", "7zz", false, true, false, false, false, false, false, false),
        new("hxs", "hxs", "7zz", false, true, false, false, false, false, false, false),
        new("split", "split", "7zz", false, true, false, false, false, false, false, false)
    ];

    private static readonly IReadOnlyDictionary<string, ArchiveFormatDefinition> ByExtension =
        BuildLookup();

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gzip"] = "gz",
            ["tgz"] = "gz",
            ["tpz"] = "gz",
            ["apk"] = "zip",
            ["bzip2"] = "bz2",
            ["bzip"] = "bz2",
            ["txz"] = "xz",
            ["tbz"] = "bz2",
            ["tbz2"] = "bz2",
            ["tzst"] = "zst",
            ["taz"] = "z",
            ["zipx"] = "zip",
            ["z01"] = "zip",
            ["jar"] = "zip",
            ["xpi"] = "zip",
            ["odt"] = "zip",
            ["ods"] = "zip",
            ["docx"] = "zip",
            ["xlsx"] = "zip",
            ["epub"] = "zip",
            ["ipa"] = "zip",
            ["appx"] = "zip",
            ["msi"] = "compound",
            ["msp"] = "compound",
            ["msm"] = "compound",
            ["doc"] = "compound",
            ["xls"] = "compound",
            ["ppt"] = "compound",
            ["aaf"] = "compound",
            ["a"] = "ar",
            ["hfsx"] = "hfs",
            ["hxi"] = "hxs",
            ["hxr"] = "hxs",
            ["hxq"] = "hxs",
            ["hxw"] = "hxs",
            ["lit"] = "hxs",
            ["chi"] = "chm",
            ["chq"] = "chm",
            ["chw"] = "chm",
            ["obj"] = "coff",
            ["img"] = "iso",
            ["lpimg"] = "iso",
            ["ext2"] = "ext",
            ["ext3"] = "ext",
            ["ext4"] = "ext",
            ["exe"] = "pe",
            ["dll"] = "pe",
            ["sys"] = "pe",
            ["simg"] = "sparse",
            ["swm"] = "wim",
            ["esd"] = "wim",
            ["ppkg"] = "wim",
            ["udeb"] = "ar",
            ["lib"] = "ar",
            ["r00"] = "rar",
            ["qcow2c"] = "qcow",
            ["avhdx"] = "vhdx",
            ["pkg"] = "xar",
            ["xip"] = "xar",
            ["ova"] = "tar",
            ["pmd"] = "ppmd",
            ["scap"] = "uefi",
            ["uefic"] = "uefi",
            ["uefif"] = "uefi",
            ["001"] = "split"
        };

    public static IReadOnlyList<ArchiveFormatDefinition> All => Definitions;

    public static IReadOnlyList<ArchiveFormatDefinition> Creatable =>
        Definitions.Where(definition => definition.CanCreate).ToArray();

    public static IReadOnlyCollection<string> ExtractionExtensions => ByExtension.Keys.ToArray();

    public static string Normalize(string? format)
    {
        var normalized = format?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return Aliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static bool TryGet(string? format, out ArchiveFormatDefinition definition)
    {
        return ByExtension.TryGetValue(Normalize(format), out definition!);
    }

    public static bool CanCreate(string? format) => TryGet(format, out var definition) && definition.CanCreate;

    public static bool CanExtract(string? format) => TryGet(format, out var definition) && definition.CanExtract;

    public static string CreateFormatsText => string.Join(", ", Creatable.Select(definition => definition.Extension));

    public static string ExtractFormatsText => string.Join(", ", Definitions
        .Where(definition => definition.CanExtract)
        .Select(definition => definition.Extension));

    public static string NormalizeToolFormat(string? format)
    {
        if (!TryGet(format, out var definition))
        {
            return Normalize(format);
        }

        return definition.ToolFormat;
    }

    public static string GetSupportedExtensionPattern()
    {
        var extensions = Definitions
            .Select(definition => definition.Extension)
            .Concat(Aliases.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase);
        return string.Join("|", extensions.Select(System.Text.RegularExpressions.Regex.Escape));
    }

    private static IReadOnlyDictionary<string, ArchiveFormatDefinition> BuildLookup()
    {
        var lookup = new Dictionary<string, ArchiveFormatDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            lookup[definition.Extension] = definition;
        }

        return lookup;
    }
}
