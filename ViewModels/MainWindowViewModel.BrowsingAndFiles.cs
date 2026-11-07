using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;
using BatchCompress.Avalonia.Localization;
using CommunityToolkit.Mvvm.Input;

namespace BatchCompress.Avalonia.ViewModels;

// Antigravity, 2026-11-08：批次 15——拆分 MainWindowViewModel 超千行大文件。
// 本文件聚合文件系统浏览回调路由、目录与 TXT 清单加载、大小计算与刷新。
public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        if (BrowseSourceRequested != null)
        {
            await BrowseSourceRequested();
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        if (BrowseOutputRequested != null)
        {
            await BrowseOutputRequested();
        }
    }

    [RelayCommand]
    private async Task BrowseTextFileAsync()
    {
        if (BrowseTextFileRequested != null)
        {
            await BrowseTextFileRequested();
        }
    }

    [RelayCommand]
    private async Task BrowseSaveFileAsync()
    {
        if (BrowseSaveFileRequested != null)
        {
            await BrowseSaveFileRequested();
        }
    }

    [RelayCommand]
    private async Task BrowseAttachmentAsync()
    {
        if (BrowseAttachmentRequested != null)
        {
            await BrowseAttachmentRequested();
        }
    }

    [RelayCommand]
    private async Task RefreshFileListAsync()
    {
        var operationTab = CurrentOperationTab;
        var sourceMode = SourceMode;

        if (sourceMode == 0)
        {
            // 从 TXT 清单加载。
            await LoadFromTextFileAsync(operationTab);
        }
        else
        {
            // 从目录扫描加载。
            await LoadFromFolderAsync(operationTab);
        }

        // 刷新输出目录的已有大小统计。
        if (IsCurrentOperationTab(operationTab) && SourceMode == sourceMode)
        {
            UpdateOutputSize();
        }
    }

    private async Task LoadFromTextFileAsync(int operationTab)
    {
        var textPath = SourcePath;
        var saveFilePath = SaveFilePath;
        var extension = Extension;
        var result = await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(textPath) || !File.Exists(textPath))
            {
                return new TextFileImportResult();
            }

            if (operationTab == 0)
            {
                return _batchOperationService.LoadCompressionPathsFromTextFile(textPath);
            }

            return _batchOperationService.LoadFilesFromTextFileWithDiagnostics(
                textPath, saveFilePath, extension);
        });

        if (!IsCurrentOperationTab(operationTab) || SourceMode != 0)
        {
            return;
        }

        if (operationTab == 0)
        {
            SourceFileList = string.Join(Environment.NewLine, result.Paths);
            AppendTextImportDiagnostics(result, false);
            return;
        }

        var lines = new List<string>();
        foreach (var entry in result.Entries)
        {
            lines.Add(entry.FilePath);
            lines.Add(entry.Password ?? string.Empty);
        }

        SourceFileList = string.Join(Environment.NewLine, lines);
        AppendTextImportDiagnostics(result, true);
    }

    // GPT-5, 2026-08-06：导入诊断写入命令日志，保留旧版的匹配统计和分卷提示，但不阻塞批处理。
    // GPT-5, 2026-09-29：诊断文案改为读取本地化资源，跟随当前界面语言。
    private void AppendTextImportDiagnostics(TextFileImportResult result, bool passwordBook)
    {
        var type = passwordBook ? L.PasswordBookLabel : L.CompressionListLabel;
        var count = passwordBook ? result.Entries.Count : result.Paths.Count;
        var sizeGB = result.MatchedBytes / (1024.0 * 1024.0 * 1024.0);
        var estimatedSeconds = result.MatchedBytes / (1024.0 * 1024.0) / 40.0;
        CommandLog += string.Format(
            L.TextImportSummary, type, result.RequestedCount, count,
            sizeGB.ToString("F3"), estimatedSeconds.ToString("F1")) + "\n";

        if (result.MissingEntries.Count > 0)
        {
            CommandLog += string.Format(L.TextImportMissing, type, result.MissingEntries.Count) + "\n" +
                          string.Join(Environment.NewLine, result.MissingEntries) + Environment.NewLine;
        }

        if (result.IncompleteVolumes.Count > 0)
        {
            CommandLog += string.Format(L.TextImportIncompleteVolumes, type, result.IncompleteVolumes.Count) + "\n" +
                          string.Join(Environment.NewLine, result.IncompleteVolumes) + Environment.NewLine;
        }

        if (result.AmbiguousEntries.Count > 0 || result.DuplicateVolumeEntries.Count > 0)
        {
            var ambiguous = result.AmbiguousEntries.Concat(result.DuplicateVolumeEntries);
            CommandLog += string.Format(L.TextImportAmbiguous, type, ambiguous.Count()) + "\n" +
                          string.Join(Environment.NewLine, ambiguous) + Environment.NewLine;
        }

        if (!passwordBook)
        {
            return;
        }

        if (result.UnmatchedArchives.Count > 0)
        {
            CommandLog += string.Format(L.PasswordBookUnmatched, result.UnmatchedArchives.Count) + "\n" +
                          string.Join(Environment.NewLine, result.UnmatchedArchives) + Environment.NewLine;
        }

        if (result.VolumeCandidates.Count > 0)
        {
            CommandLog += string.Format(L.PasswordBookVolumeCandidates, result.VolumeCandidates.Count) + "\n" +
                          string.Join(Environment.NewLine, result.VolumeCandidates) + Environment.NewLine;
        }
    }

    private async Task LoadFromFolderAsync(int operationTab)
    {
        var saveFilePath = SaveFilePath;
        var extension = Extension;
        var skipAlreadyProcessed = SkipAlreadyProcessed;
        var files = await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(saveFilePath) || !Directory.Exists(saveFilePath))
            {
                return new List<string>();
            }

            return operationTab == 0
                ? _batchOperationService.LoadCompressionSourcesFromFolder(
                    saveFilePath, skipAlreadyProcessed)
                : _batchOperationService.LoadArchivesFromFolder(
                    saveFilePath, extension, skipAlreadyProcessed);
        });

        if (IsCurrentOperationTab(operationTab) && SourceMode == 1)
        {
            SourceFileList = string.Join(Environment.NewLine, files);
        }
    }

    private void UpdateOutputSize()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            var size = _batchOperationService.CalculateTotalSizeGB(OutputPath, Extension);
            OutputSizeText = $"{size:F1}GB";
        }
    }

    [RelayCommand]
    private async Task OpenOutputFolderAsync()
    {
        if (!string.IsNullOrEmpty(OutputPath) && Directory.Exists(OutputPath))
        {
            await _systemIntegration.OpenFolderAsync(OutputPath);
        }
    }

    [RelayCommand]
    private async Task OpenSourceFolderAsync()
    {
        var sourceDirectory = !string.IsNullOrWhiteSpace(SaveFilePath) && Directory.Exists(SaveFilePath)
            ? SaveFilePath
            : File.Exists(SourcePath) ? Path.GetDirectoryName(Path.GetFullPath(SourcePath)) : SourcePath;

        if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            await _systemIntegration.OpenFolderAsync(sourceDirectory);
        }
    }

    private async Task UpdateTotalSizeAsync()
    {
        var targetPath = SaveFilePath;
        var calculation = await Task.Run(() => CalculateDirectorySize(targetPath));
        if (!string.Equals(SaveFilePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        TotalSizeGB = calculation.Size / (1024.0 * 1024.0 * 1024.0);
        foreach (var error in calculation.Errors)
        {
            CommandLog += error + "\n";
        }
    }

    private static (long Size, List<string> Errors) CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return (0, []);

        long size = 0;
        var errors = new List<string>();

        try
        {
            // 枚举目录中的候选文件。
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(file => !SystemMetadataFileFilter.ShouldSkip(file))
                .ToArray();

            // 汇总候选文件大小并换算为 GB。
            foreach (string file in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    size += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    errors.Add(string.Format(
                        LocalizationService.Instance.Strings.DirectorySizeFileError, file, ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(string.Format(
                LocalizationService.Instance.Strings.DirectorySizeAccessError, path, ex.Message));
        }

        return (size, errors);
    }

    private void QueueRefresh(bool includeTotalSize, bool delay = false)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (delay)
            {
                await Task.Delay(500);
            }

            await RefreshFileListAsync();
            if (includeTotalSize)
            {
                await UpdateTotalSizeAsync();
            }
        });
    }
}
