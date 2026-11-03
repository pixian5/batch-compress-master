using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BatchCompress.Avalonia.Localization;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// 后处理模式：无、删除源、移动源到已处理目录。
/// </summary>
public enum PostProcessMode
{
    None,
    Delete,
    Move
}

/// <summary>
/// 后处理目标种类：压缩源（文件或目录）或解压归档（可能为整组分卷）。
/// GLM-5.3, 2026-11-04：外部审核指出删除失败词条此前借用 PreflightWholeGroup
/// 布尔值区分压缩/解压属于语义偷渡；显式枚举解耦，两者不再相互隐含。
/// </summary>
public enum PostProcessItemKind
{
    CompressionSource,
    DecompressionArchive
}

/// <summary>
/// GLM-5.3, 2026-11-04：批次 2 从压缩/解压双循环中抽出的后处理请求。
/// PreflightWholeGroup 为 true（解压多卷）时移动前先整组检查目标，
/// 任一冲突即保留整组；false（压缩单源）不预检。
/// </summary>
public sealed record PostProcessRequest(
    PostProcessMode Mode,
    IReadOnlyList<string> Targets,
    string ProcessedFolderName,
    string ItemLabel,
    PostProcessItemKind ItemKind,
    bool PreflightWholeGroup);

/// <summary>
/// GLM-5.3, 2026-11-04：后处理策略服务。统一执行压缩源与解压归档的删除/移动后处理，
/// 返回按发生顺序排列的本地化失败消息；计数与日志由调用方（BatchOperationService）负责，
/// 保持现有“每条失败计一次 PostProcessFailCount 并写入警告日志”的行为。
/// </summary>
public sealed class PostProcessService
{
    private readonly ILogger? _logger;

    public PostProcessService(ILogger? logger = null)
    {
        _logger = logger;
    }

    // 后处理失败消息跟随当前界面语言，与 BatchOperationService 的报告口径一致。
    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public IReadOnlyList<string> Apply(PostProcessRequest request)
    {
        var failures = new List<string>();
        if (request.Mode == PostProcessMode.None || request.Targets.Count == 0)
        {
            return failures;
        }

        if (request.Mode == PostProcessMode.Delete)
        {
            foreach (var target in request.Targets)
            {
                try
                {
                    Log(LogLevel.Debug, $"Post-process deleting: {target}");
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }
                    else if (File.Exists(target))
                    {
                        File.Delete(target);
                    }
                }
                catch (Exception ex)
                {
                    // 失败词条沿用双循环时期的差异：压缩源用 PostProcessDeleteSourceFailed，
                    // 解压归档（整组卷）用 PostProcessHandleArchiveFailed，由显式 ItemKind 区分。
                    failures.Add(string.Format(
                        request.ItemKind == PostProcessItemKind.DecompressionArchive
                            ? L.PostProcessHandleArchiveFailed
                            : L.PostProcessDeleteSourceFailed,
                        target,
                        ex.Message));
                }
            }

            return failures;
        }

        // 移动模式。
        if (request.PreflightWholeGroup)
        {
            // 整组预检：任一分卷目标已存在时保留整组，只计一次失败，避免半组状态。
            var conflictingTarget = request.Targets
                .Select(target => BuildMoveTarget(target, request.ProcessedFolderName))
                .FirstOrDefault(target => File.Exists(target) || Directory.Exists(target));
            if (conflictingTarget != null)
            {
                failures.Add(string.Format(L.PostProcessArchiveTargetExists, conflictingTarget));
                return failures;
            }
        }

        foreach (var target in request.Targets)
        {
            var targetPath = BuildMoveTarget(target, request.ProcessedFolderName);
            try
            {
                var processedDir = Path.GetDirectoryName(targetPath) ?? string.Empty;
                if (!Directory.Exists(processedDir))
                {
                    Directory.CreateDirectory(processedDir);
                }

                Log(LogLevel.Debug, $"Post-process moving: {target} -> {targetPath}");
                if (!TryMoveWithoutOverwrite(target, targetPath, request.ItemLabel, out var moveFailure))
                {
                    failures.Add(moveFailure);
                }
            }
            catch (Exception ex)
            {
                // 外层只覆盖目录创建等基础设施异常；移动自身的失败已在 TryMoveWithoutOverwrite 内本地化。
                // 压缩与解压沿用双循环时期的词条差异：压缩 3 参、解压 2 参，由 ItemKind 显式区分。
                if (request.ItemKind == PostProcessItemKind.DecompressionArchive)
                {
                    failures.Add(string.Format(L.PostProcessHandleArchiveFailed, target, ex.Message));
                }
                else
                {
                    failures.Add(string.Format(L.PostProcessMoveSourceFailed, request.ItemLabel, target, ex.Message));
                }
            }
        }

        return failures;
    }

    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }

    private static string BuildMoveTarget(string sourcePath, string processedFolderName) =>
        Path.Combine(
            Path.GetDirectoryName(sourcePath) ?? string.Empty,
            processedFolderName,
            Path.GetFileName(sourcePath));

    // 移动属于可选后处理，目标冲突时必须保留原目标和源文件，禁止先删除目标。
    private static bool TryMoveWithoutOverwrite(
        string sourcePath,
        string targetPath,
        string itemType,
        out string failureReason)
    {
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            failureReason = string.Format(L.PostProcessMoveTargetExists, itemType, targetPath);
            return false;
        }

        try
        {
            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, targetPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, targetPath);
            }
            else
            {
                throw new FileNotFoundException("源文件或目录不存在", sourcePath);
            }

            failureReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = string.Format(L.PostProcessMoveFailed, itemType, sourcePath, targetPath, ex.Message);
            return false;
        }
    }
}
