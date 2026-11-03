using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 来源收集统一服务。
/// GLM-5.3, 2026-11-04：批次 2 阶段 2 引入。收敛 GUI（SourceFileList 按行解析）与
/// CLI（--input / --source / --text）两套来源解析逻辑，并在收集出口统一做路径规范化，
/// 彻底消灭尾部斜杠（如 /path/to/dir/）导致 Path.GetFileName 返回空串、
/// 生成 “.7z” 空名归档乃至后处理移动自身报错的缺陷。
/// TXT 密码本与压缩清单的文件级导入（含诊断）仍由 BatchOperationService 负责，两者协同。
/// </summary>
public static class SourceCollector
{
    /// <summary>
    /// 统一路径规范化：去首尾空格、相对路径转绝对、消除尾部目录分隔符。
    /// 文件系统根（“/” 或 “C:\”）保持原样——再截断会得到空串或盘符相对路径。
    /// 无法规范化的非法路径原样返回（去空格后），由批处理服务按“来源不存在”报告，
    /// 与历史 GUI 行为一致，收集阶段不抛异常。
    /// </summary>
    public static string NormalizePath(string? rawPath)
    {
        var trimmed = (rawPath ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (
            ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            // 非法字符等输入不在此处拦截；交给服务层的 ItemNotFound 分支处理。
            return trimmed;
        }

        // 根路径不能再截断：GetFullPath("/") == "/"，TrimEnd 会得到空串。
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath, root, StringComparison.Ordinal))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 按历史口径拆分多行文本：以 CR/LF 分隔并剔除空行（与旧版
    /// Split(new[]{'\r','\n'}, RemoveEmptyEntries) 完全一致），保留行内原始内容，
    /// 由各收集方法按需 Trim——密码本模式的空行剔除会改变文件/密码配对，不能提前 Trim。
    /// </summary>
    public static IReadOnlyList<string> SplitLines(string? multiLineText)
    {
        if (string.IsNullOrEmpty(multiLineText))
        {
            return [];
        }

        return multiLineText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 从多行文本（GUI SourceFileList / CLI 清单文本）统一收集压缩来源。
    /// </summary>
    public static List<string> CollectCompressionSourcesFromText(string? multiLineText) =>
        CollectCompressionSources(SplitLines(multiLineText));

    /// <summary>
    /// 统一收集压缩来源：逐行去空格、过滤空行与系统元数据、路径规范化（含尾部斜杠消除）、
    /// 忽略大小写去重并保持首次出现顺序。GUI 多行文本与 CLI --input / --source 精确路径共用。
    /// 不做存在性过滤：缺失项交给 BatchOperationService 统一按 ItemNotFound 报告计数。
    /// </summary>
    public static List<string> CollectCompressionSources(IEnumerable<string>? rawPaths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawPath in rawPaths ?? [])
        {
            var path = (rawPath ?? string.Empty).Trim();
            if (path.Length == 0 || SystemMetadataFileFilter.ShouldSkip(path))
            {
                continue;
            }

            var normalized = NormalizePath(path);
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    /// <summary>
    /// 从多行文本解析解压密码本条目（文件行、密码行交替，与 TXT 密码本同构）。
    /// 保持历史配对语义：空行已在拆分阶段剔除，步进为 2，缺失文件连同其密码整对跳过；
    /// 路径规范化后尾部斜杠的文件行也能被识别。
    /// </summary>
    public static List<FileEntry> CollectPasswordBookEntries(string? multiLineText)
    {
        var entries = new List<FileEntry>();
        var lines = SplitLines(multiLineText);
        for (var i = 0; i < lines.Count; i += 2)
        {
            var filePath = lines[i].Trim();
            var password = i + 1 < lines.Count ? lines[i + 1].Trim() : null;
            if (filePath.Length == 0 || SystemMetadataFileFilter.ShouldSkip(filePath))
            {
                continue;
            }

            var normalized = NormalizePath(filePath);
            if (!File.Exists(normalized))
            {
                continue;
            }

            entries.Add(new FileEntry
            {
                FilePath = normalized,
                Password = password,
                FileSize = new FileInfo(normalized).Length
            });
        }

        return entries;
    }

    /// <summary>
    /// 从多行文本收集解压归档行（GUI 目录模式列表）：过滤空行与元数据、
    /// 存在性 + 归档格式校验、路径规范化。不去重：跨条目的分卷折叠与去重
    /// 由 BatchOperationService 统一执行并计入 IgnoreCount，保持计数口径。
    /// </summary>
    public static List<FileEntry> CollectDecompressionFilesFromText(string? multiLineText, string extension) =>
        CollectDecompressionFiles(SplitLines(multiLineText), extension);

    /// <summary>
    /// 从路径集合收集解压归档行（CLI 精确路径等价入口）。
    /// </summary>
    public static List<FileEntry> CollectDecompressionFiles(IEnumerable<string>? rawPaths, string extension)
    {
        var entries = new List<FileEntry>();
        foreach (var rawPath in rawPaths ?? [])
        {
            var path = (rawPath ?? string.Empty).Trim();
            if (path.Length == 0 || SystemMetadataFileFilter.ShouldSkip(path))
            {
                continue;
            }

            var normalized = NormalizePath(path);
            if (!File.Exists(normalized) || !ArchiveVolumeResolver.MatchesFormat(normalized, extension))
            {
                continue;
            }

            entries.Add(new FileEntry
            {
                FilePath = normalized,
                Password = null,
                FileSize = new FileInfo(normalized).Length
            });
        }

        return entries;
    }

    /// <summary>
    /// CLI 解压任务收集：路径为文件时做格式校验并折叠到分卷首卷，
    /// 为目录时展开目录内匹配格式的归档任务（复用 BatchOperationService 的目录扫描）。
    /// 最终统一过滤元数据并按全路径忽略大小写去重（保留首次出现，密码本种子条目优先）。
    /// </summary>
    /// <param name="seedEntries">已加载的 TXT 密码本条目，优先保留（含条目密码）。</param>
    /// <param name="rawPaths">--source / --input 原始路径。</param>
    /// <param name="extension">目标归档格式扩展名。</param>
    /// <param name="skipProcessed">目录扫描时是否跳过【已解压】标记项。</param>
    /// <param name="batchService">目录扫描与归档折叠依赖的批处理服务。</param>
    public static List<FileEntry> CollectDecompressionEntries(
        IEnumerable<FileEntry>? seedEntries,
        IEnumerable<string>? rawPaths,
        string extension,
        bool skipProcessed,
        BatchOperationService batchService)
    {
        var entries = new List<FileEntry>(seedEntries ?? []);
        foreach (var rawPath in rawPaths ?? [])
        {
            AddDecompressionPath(entries, rawPath, extension, skipProcessed, batchService);
        }

        return entries
            .Where(entry => !SystemMetadataFileFilter.ShouldSkip(entry.FilePath))
            .GroupBy(entry => Path.GetFullPath(entry.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void AddDecompressionPath(
        List<FileEntry> entries,
        string path,
        string extension,
        bool skipProcessed,
        BatchOperationService batchService)
    {
        // 先规范化再做存在性判断：尾部斜杠的文件路径（/x/a.7z/）在 POSIX stat 下
        // 会被误判为不存在，规范化后才能正确识别。
        path = NormalizePath(path);
        if (File.Exists(path))
        {
            if (ArchiveVolumeResolver.MatchesFormat(path, extension))
            {
                AddResolvedArchive(entries, path);
            }
            return;
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in batchService.LoadArchivesFromFolder(path, extension, skipProcessed))
        {
            AddResolvedArchive(entries, file);
        }
    }

    // CLI 精确项与目录展开共用：折叠到分卷首卷后建立任务条目（大小取首卷文件实际大小）。
    private static void AddResolvedArchive(List<FileEntry> entries, string path)
    {
        var resolved = ArchiveVolumeResolver.Resolve(path);
        var taskPath = resolved.FirstVolumePath ?? resolved.Volumes.FirstOrDefault()?.Path ?? path;
        if (File.Exists(taskPath))
        {
            entries.Add(new FileEntry
            {
                FilePath = taskPath,
                FileSize = new FileInfo(taskPath).Length
            });
        }
    }
}
