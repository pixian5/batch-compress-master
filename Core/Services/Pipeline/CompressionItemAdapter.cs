using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Localization;
using Microsoft.Extensions.Logging;

namespace BatchCompress.Avalonia.Core.Services.Pipeline;

/// <summary>
/// 压缩条目：携带引擎调用与附件暂存目录清理所需的路径信息。
/// </summary>
internal sealed class PreparedCompressionItem : PreparedBatchItem
{
    /// <summary>待压缩的源文件或源目录。</summary>
    public required string SourcePath { get; init; }

    /// <summary>输出归档基础路径（可能由引擎生成多个分卷）。</summary>
    public required string OutputPath { get; init; }

    /// <summary>附件暂存目录；无附件或附件全部存在时为 null。</summary>
    public required string? StagingDirectory { get; init; }

    public override string BuildFailedLogMessage(string? errorMessage) =>
        $"Compression failed: {DisplayName} - {errorMessage}";
}

/// <summary>
/// 压缩循环适配器。
/// GLM-5.3, 2026-11-04：批次 2 阶段 1 从 BatchOperationService.BatchCompressAsync
/// 迁移出的逐项准备与执行逻辑；行为与迁移前逐字等价，由压缩循环表征测试锁定。
/// </summary>
internal sealed class CompressionItemAdapter : IBatchItemAdapter<string>
{
    private readonly IArchiveEngine _archiveEngine;
    private readonly string _normalizedFormat;
    private readonly ArchiveFormatDefinition _formatDefinition;
    private readonly ILogger? _logger;

    // 报告给界面与命令行的逐项状态消息跟随当前界面语言。
    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public CompressionItemAdapter(
        IArchiveEngine archiveEngine,
        string normalizedFormat,
        ArchiveFormatDefinition formatDefinition,
        ILogger? logger = null)
    {
        _archiveEngine = archiveEngine;
        _normalizedFormat = normalizedFormat;
        _formatDefinition = formatDefinition;
        _logger = logger;
    }

    private void Log(LogLevel level, string message)
    {
        _logger?.Log(level, message);
    }

    public string BuildBatchCompletedLogMessage(OperationProgressInfo progressInfo) =>
        $"Batch compression complete. Success: {progressInfo.SuccessCount}, Failed: {progressInfo.FailCount}";

    // 压缩侧历史上在关机前没有诊断日志。
    public string? ShutdownRequestLogMessage => null;

    public Task<PreparedBatchItem?> PrepareAsync(
        string sourcePath,
        BatchOperationOptions options,
        OperationProgressInfo progressInfo,
        IProgress<OperationProgressInfo> progress)
    {
        // GPT-5, 2026-08-05：防御手工编辑列表绕过正常枚举的情况。
        if (SystemMetadataFileFilter.ShouldSkip(sourcePath))
        {
            Log(LogLevel.Debug, $"Skipping system metadata path: {sourcePath}");
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        // 手工编辑列表后仍需再次检查文件或目录是否存在。
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            Log(LogLevel.Warning, $"Source not found: {sourcePath}");
            progressInfo.NonExistCount++;
            progressInfo.Message = string.Format(L.ItemNotFound, sourcePath);
            progressInfo.IsError = true;
            progress.Report(progressInfo);
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        var name = Path.GetFileName(sourcePath);
        progressInfo.CurrentFile = name;
        progressInfo.CurrentSourcePath = sourcePath;
        Log(LogLevel.Debug, $"Processing: {name}");

        // 按选项跳过已经完成压缩的项目。
        if (options.SkipAlreadyProcessed && name.Contains("【已压缩】"))
        {
            Log(LogLevel.Debug, $"Skipping already processed: {name}");
            progressInfo.IgnoreCount++;
            return Task.FromResult<PreparedBatchItem?>(null);
        }

        // 使用来源项目名称构造输出归档名称。
        var outputFileName = name + "." + _normalizedFormat;
        var outputDirectory = OutputPathResolver.ResolveAndCreate(options.OutputPath, sourcePath);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        // 根据用户选择处理已有输出文件。分卷输出可能没有基础文件，必须检查全部卷。
        var existingOutputFiles = ArchiveVolumeResolver.ResolveOutputFiles(outputPath);
        if (existingOutputFiles.Count > 0)
        {
            if (options.ExistingFileMode == ExistingFileMode.Skip)
            {
                Log(LogLevel.Debug, $"Skipping existing output: {outputPath}");
                progressInfo.IgnoreCount++;
                progressInfo.Message = string.Format(L.SkipExistingOutput, outputFileName);
                progressInfo.IsError = false;
                progress.Report(progressInfo);
                return Task.FromResult<PreparedBatchItem?>(null);
            }
            else if (options.ExistingFileMode == ExistingFileMode.Overwrite)
            {
                foreach (var existingOutputFile in existingOutputFiles)
                {
                    Log(LogLevel.Debug, $"Deleting existing output: {existingOutputFile}");
                    File.Delete(existingOutputFile);
                }
            }
        }

        // 按当前选项生成单个归档的密码。
        string? password = null;
        if (options.UseRandomPassword && _formatDefinition.SupportsPassword)
        {
            var passwordName = PasswordUtility.GetPasswordSourceName(outputFileName, options.PasswordNameMode);
            password = PasswordUtility.GenerateCompressionPassword(passwordName);
        }
        else if (!string.IsNullOrEmpty(options.CustomPassword))
        {
            password = options.CustomPassword;
        }

        // GPT-5, 2026-08-05：每个来源项目只转换一次批处理默认值到更窄的引擎契约。
        var archiveOptions = new ArchiveOptions
        {
            ArchiveFormat = _normalizedFormat,
            Password = password,
            CompressionLevel = options.CompressionLevel,
            SolidArchive = options.SolidArchive,
            QuickOpen = options.QuickOpen,
            TestArchive = options.TestArchive,
            CommentFile = options.CommentFile,
            TempDirectory = string.IsNullOrWhiteSpace(options.TempDirectory)
                ? outputDirectory
                : options.TempDirectory,
            ExistingFileMode = options.ExistingFileMode,
            RecoveryRecordPercent = options.RecoveryRecordPercent,
            LockArchive = options.LockArchive,
            RarStoreOnlyExtensions = _normalizedFormat.Equals("rar", StringComparison.OrdinalIgnoreCase)
                ? options.RarStoreOnlyExtensions
                : null,
            VolumeSize = !string.IsNullOrEmpty(options.VolumeSize) ?
                options.VolumeSize + options.VolumeSizeUnit : null
        };

        // GPT-5, 2026-08-06：附件直接作为额外输入交给归档程序。存在的路径保留其 basename，
        // 不存在的路径只在临时暂存目录创建空目录，避免修改用户源目录。
        var stagingDirectory = CreateAttachmentInputs(options, out var attachmentInputs);
        archiveOptions.AdditionalInputs = attachmentInputs;

        return Task.FromResult<PreparedBatchItem?>(new PreparedCompressionItem
        {
            DisplayName = name,
            CurrentSourcePath = sourcePath,
            Options = archiveOptions,
            StartedMessage = string.Format(L.CompressionStarted, name),
            StartedLogMessage = $"Compression started: {name} -> {outputFileName}",
            SucceededLogMessage = $"Compression successful: {name} -> {outputFileName}",
            CommandLabel = L.CompressionCommandLabel,
            SourcePath = sourcePath,
            OutputPath = outputPath,
            StagingDirectory = stagingDirectory
        });
    }

    public async Task<ArchiveResult> ExecuteAsync(PreparedBatchItem item, CancellationToken cancellationToken)
    {
        var prepared = (PreparedCompressionItem)item;
        try
        {
            return await _archiveEngine.CompressAsync(
                prepared.SourcePath, prepared.OutputPath, prepared.Options, cancellationToken);
        }
        finally
        {
            // 取消或异常路径同样必须清理附件暂存目录（表征测试：取消传播场景 2）。
            if (prepared.StagingDirectory != null)
            {
                try { Directory.Delete(prepared.StagingDirectory, recursive: true); }
                catch (Exception ex) { Log(LogLevel.Warning, $"清理附件暂存目录失败：{ex.Message}"); }
            }
        }
    }

    public long MeasuredBytes(PreparedBatchItem item, double processedSizeGBBefore)
    {
        var prepared = (PreparedCompressionItem)item;
        var outputBytes = CalculateArchiveOutputBytes(prepared.OutputPath);
        if (outputBytes > 0)
        {
            var sizeGB = outputBytes / (1024.0 * 1024.0 * 1024.0);
            Log(LogLevel.Debug,
                $"Output size: {sizeGB:F3} GB ({outputBytes} bytes), Total processed: {processedSizeGBBefore + sizeGB:F3} GB");
        }

        return outputBytes;
    }

    public PostProcessRequest BuildPostProcessRequest(PreparedBatchItem item, BatchOperationOptions options)
    {
        var prepared = (PreparedCompressionItem)item;
        return new PostProcessRequest(
            options.DeleteSourceAfter ? PostProcessMode.Delete :
            options.MoveSourceAfter ? PostProcessMode.Move : PostProcessMode.None,
            [prepared.SourcePath],
            "【已压缩】",
            L.PostProcessItemCompressionSource,
            PostProcessItemKind.CompressionSource,
            PreflightWholeGroup: false);
    }

    // 压缩失败没有输出回滚需求；引擎自身的失败输出清理由命令层处理。
    public void OnExecutionFailed(PreparedBatchItem item)
    {
    }

    private static string? CreateAttachmentInputs(BatchOperationOptions options, out string[] inputs)
    {
        inputs = [];
        if (!options.AddEnclosures || options.EnclosureDirectories is not { Length: > 0 })
        {
            return null;
        }

        string? stagingDirectory = null;
        var result = new List<string>();
        foreach (var rawPath in options.EnclosureDirectories)
        {
            var path = rawPath.Trim();
            if (path.Length == 0 || SystemMetadataFileFilter.ShouldSkip(path))
            {
                continue;
            }

            if (File.Exists(path) || Directory.Exists(path))
            {
                result.Add(Path.GetFullPath(path));
                continue;
            }

            stagingDirectory ??= Path.Combine(Path.GetTempPath(), $"batch-compress-attachments-{Guid.NewGuid():N}");
            var safeName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..")
            {
                safeName = "附件";
            }

            var emptyDirectory = Path.Combine(stagingDirectory, safeName);
            Directory.CreateDirectory(emptyDirectory);
            result.Add(emptyDirectory);
        }

        inputs = result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return stagingDirectory;
    }

    private static long CalculateArchiveOutputBytes(string outputPath)
    {
        try
        {
            var files = ArchiveVolumeResolver.ResolveOutputFiles(outputPath)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length > 0)
            {
                return files.Sum(path => new FileInfo(path).Length);
            }

            return File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        }
        catch
        {
            // 归档已成功时统计失败不应改变成功结果；记录后回退到基础路径。
            return File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        }
    }
}
