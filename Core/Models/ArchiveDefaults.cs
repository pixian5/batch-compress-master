namespace BatchCompress.Avalonia.Core.Models;

// GPT-5, 2026-08-07：保留 WinForms 的 RAR 默认“仅存储”列表，避免再次压缩媒体和已有归档。
// 7z/ZIP 没有等价的按扩展名存储开关，因此不能将其映射为排除规则，否则会丢失文件。
public static class ArchiveDefaults
{
    public static readonly string[] StoreOnlyExtensions =
    [
        "7z", "ace", "arj", "bz2", "cab", "gz", "mp4", "mkv", "rm", "rmvb", "flv", "mov",
        "lha", "lz", "lzh", "mp3", "rar", "taz", "tgz", "xz", "z", "zip", "zipx"
    ];
}
