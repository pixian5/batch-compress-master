using System.Collections.Generic;
using System.IO.Compression;
// GLM-5.3, 2026-11-04：批次 8——System.IO.Compression.CompressionLevel 与项目自有
// Core.Interfaces.CompressionLevel 重名，用别名把既有引用钉回项目枚举（语义零变化）。
using CompressionLevel = BatchCompress.Avalonia.Core.Interfaces.CompressionLevel;
using System.Text;
using System.Text.RegularExpressions;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;
using BatchCompress.Avalonia.Localization;

// GPT-5, 2026-08-05：用于命令构建、进程取消和路径回退的轻量可执行回归测试。
// 不依赖 UI，因此可在 macOS、Windows 和 Linux 一致运行。
internal static partial class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("格式参数", TestFormatArguments),
            ("格式能力目录", TestFormatCatalog),
            ("密码与失败返回码", TestPasswordAndFailureExitCodes),
            ("密码命名与 RAR 仅存储", TestPasswordNamingAndStoreOnlyExtensions),
            ("取消传播", TestCancellation),
            ("异步输出与参数边界", TestProcessOutputAndArgumentBoundaries),
            ("完整命令日志参数", TestCommandLogArguments),
            ("当前来源路径进度", TestCurrentSourcePathProgress),
            ("空保存路径回退", TestOutputPathFallback),
            ("恢复记录与旧密码", TestRecoveryRecordAndLegacyPasswords),
            ("跨平台系统元数据过滤", TestSystemMetadataFiltering),
            ("7z 压缩与解压参数", TestSevenZipArguments),
            ("7z 返回码与格式路由", TestSevenZipExitCodesAndRouting),
            ("压缩格式别名规范化", TestCanonicalCompressionFormat),
            ("统一归档分卷解析", TestArchiveVolumeResolver),
            ("旧式分卷识别", TestOldStyleVolumeResolution),
            ("压缩循环表征", TestCompressionLoopCharacterization),
            ("解压循环表征", TestDecompressionLoopCharacterization),
            ("取消传播表征", TestCancellationPropagation),
            ("Update模式透传", TestUpdateModePassthrough),
            ("CurrentFile刷新基线", TestCurrentFileBaseline),
            ("来源收集表征", TestSourceCollectionCharacterization),
            ("路径规范化与尾部斜杠", TestPathNormalization),
            ("统一来源收集", TestUnifiedSourceCollection),
            ("大小写歧义跳过", TestCaseAmbiguitySkip),
            ("分卷完整性与解压统计", TestVolumeValidationAndProgress),
            ("解压失败残留回滚", TestFailedExtractionRollback),
            ("压缩分卷大小统计", TestCompressedVolumeSizeProgress),
            ("解压目录扫描与首卷去重", TestArchiveFolderScanning),
            ("附件根目录与空目录", TestAttachmentRootInputs),
            ("跳过已有归档统计", TestExistingSkipProgress),
            ("后处理冲突保留源和目标", TestPostProcessConflict),
            ("官方 7zz 真实压缩解压", TestOfficialSevenZipSmoke),
            ("完整命令行解析", TestCommandLineParsing),
            ("命令行错误校验", TestCommandLineValidation),
            ("CLI语言参数", TestCliLanguageOptions),
            ("CLI语言默认GUI启动", TestCliLanguageDefaultGui),
            ("CLI输出格式参数", TestCliOutputFormatOptions),
            ("CLI错误文案本地化", TestCliErrorLocalization),
            ("日志权限与轮转", TestLogFilePermissionsAndRotation),
            ("日志多代轮转", TestLogMultiGenerationRotation),
            ("日志旧代Gzip压缩", TestLogGzipCompressionRotation),
            ("日志压缩异常降级兜底", TestLogGzipDegradedFallback),
            ("日志自定义保留期清理", TestLogCustomRetentionPolicy),
            ("CLI日志保留天数参数", TestCliLogRetentionOption),
            ("密码读取错误多语言与标准输入", TestPasswordReadErrorLocalization),
            ("附件联系方式配置外置", TestEnclosureConfigService),
            ("附件配置多语言与词条完整性", TestEnclosureLocalizationAndTokens),
            ("并发批量执行", TestConcurrentBatchExecution),
            ("并发取消与大小上限", TestConcurrentCancellationAndLimit),
            ("NDJSON结构化输出", TestNdjsonOutputWriter),
            ("NDJSON端到端", TestNdjsonEndToEnd),
            ("TXT 清单与密码本诊断", TestTextFileImportModes),
            ("本地化窗口标题", TestLocalizedWindowTitles),
            ("英文与德语资源不含中文", TestLocalizedTextHasNoChinese),
            ("服务消息跟随界面语言", TestServiceMessagesFollowLanguage),
            ("界面本地化资源扫描", TestLocalizedUiResources),
            ("CLI帮助文本折行与宽度计算", TestCliHelpTextWrapping),
            ("无界面进度单行格式化", TestHeadlessProgressFormatting),
            ("压缩失败残留回滚", TestFailedCompressionRollback),
            ("压缩取消残留回滚", TestCancelledCompressionRollback),
            ("解压取消残留回滚", TestCancelledExtractionRollback),
            ("五语言CLI帮助选项完整性", TestAllHelpTextsContainAllOptions),
            ("RAR解压7zz回退与ARM64诊断", TestRarExtractionFallbackToSevenZip),
            ("NDJSON契约模式验证", TestNdjsonSchemaValidation),
            ("分卷与子进度输出解析", TestArchiveSubProgressParser),
            ("CLI诊断包导出参数", TestCliDiagnosticsOption),
            ("安全诊断日志脱敏导出", TestDiagnosticsExportRedaction),
            ("进程输出流式回调", TestArchiveProcessRunnerStreaming),
            ("管线子进度实时流式上报", TestPipelineSubProgressStreaming)
        };

        // GLM-5.3, 2026-11-03：跑完全部测试后统一输出失败汇总，CI 中一次可见所有失败项；
        // 有任一失败仍返回 1，保持非零退出状态的自动化约定。
        var failures = new List<string>();
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
                failures.Add(test.Name);
            }
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"共 {tests.Length} 项测试，失败 {failures.Count} 项：");
            foreach (var failure in failures)
            {
                Console.Error.WriteLine($"  - {failure}");
            }
            return 1;
        }

        Console.WriteLine($"全部 {tests.Length} 项测试通过。");
        return 0;
    }


    private sealed class TestArchiveEngine : IArchiveEngine
    {
        public Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult { Success = true });

        public Task<ArchiveResult> ExtractAsync(string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult { Success = true });

        public bool IsAvailable() => true;
    }

    private sealed class TestSystemIntegration : ISystemIntegration
    {
        public Task OpenFolderAsync(string path) => Task.CompletedTask;
        public Task<string?> ReadClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task WriteClipboardTextAsync(string text) => Task.CompletedTask;
        public void ShowNotification(string title, string message) { }
        public Task ShutdownAsync() => Task.CompletedTask;
        public Task CancelShutdownAsync() => Task.CompletedTask;
    }

    // 可配置延迟（保证初始槽位全部启动）与真实输出（驱动大小上限统计）。
    private sealed class ConcurrentTrackingEngine : IArchiveEngine
    {
        private readonly object _inputsLock = new();
        private int _inFlight;
        private int _peakInFlight;

        public List<string> CompressionInputs { get; } = [];
        public List<string> ExtractionInputs { get; } = [];
        // GLM-5.3, 2026-11-04：批次 8——80ms 在系统高负载（如构建余波）下偶发不足以让
        // 全部初始槽位任务进入引擎，导致 PeakInFlight 断言抖动；加宽到 400ms 提高调度容忍。
        public int DelayMilliseconds { get; set; } = 400;
        public bool WriteRealOutput { get; set; }
        public int PeakInFlight => _peakInFlight;

        public bool IsAvailable() => true;

        public async Task<ArchiveResult> CompressAsync(
            string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            lock (_inputsLock)
            {
                CompressionInputs.Add(Path.GetFileName(input));
            }
            TrackEntered();
            try
            {
                await Task.Delay(DelayMilliseconds, cancellationToken);
                if (WriteRealOutput)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
                    File.WriteAllBytes(output, new byte[1024 * 1024]);
                }

                return new ArchiveResult { Success = true };
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public async Task<ArchiveResult> ExtractAsync(
            string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            lock (_inputsLock)
            {
                ExtractionInputs.Add(Path.GetFileName(archivePath));
            }
            TrackEntered();
            try
            {
                await Task.Delay(DelayMilliseconds, cancellationToken);
                return new ArchiveResult { Success = true };
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private void TrackEntered()
        {
            var current = Interlocked.Increment(ref _inFlight);
            while (true)
            {
                var peak = _peakInFlight;
                if (current <= peak)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _peakInFlight, current, peak) == peak)
                {
                    return;
                }
            }
        }
    }


    private static ArchiveOptions CreateOptions() => new()
    {
        ArchiveFormat = "rar",
        ExistingFileMode = ExistingFileMode.Overwrite,
        CompressionLevel = CompressionLevel.Normal
    };

    private sealed class SnapshotProgress(List<OperationProgressInfo> snapshots) : IProgress<OperationProgressInfo>
    {
        private readonly object _lock = new();
        public void Report(OperationProgressInfo value)
        {
            lock (_lock)
            {
                snapshots.Add(value);
            }
        }
    }

    // GLM-5.3, 2026-11-04：批次 3——NDJSON 端到端测试用。服务层循环复用同一个可变进度对象，
    // 直接存引用会让所有快照指向最终状态；在报告时克隆字段才能按原始顺序回放事件流。
    private sealed class CloningProgress(List<OperationProgressInfo> snapshots) : IProgress<OperationProgressInfo>
    {
        public void Report(OperationProgressInfo value) => snapshots.Add(Clone(value));

        private static OperationProgressInfo Clone(OperationProgressInfo source) => new()
        {
            CurrentFile = source.CurrentFile,
            CurrentSourcePath = source.CurrentSourcePath,
            SuccessCount = source.SuccessCount,
            FailCount = source.FailCount,
            PostProcessFailCount = source.PostProcessFailCount,
            IgnoreCount = source.IgnoreCount,
            NonExistCount = source.NonExistCount,
            IncompleteVolumeCount = source.IncompleteVolumeCount,
            AmbiguousArchiveCount = source.AmbiguousArchiveCount,
            ProcessedSizeGB = source.ProcessedSizeGB,
            Message = source.Message,
            IsError = source.IsError,
            IsCommandOutput = source.IsCommandOutput,
            StartTime = source.StartTime,
            Elapsed = source.Elapsed,
            SubProgressPercent = source.SubProgressPercent
        };
    }

    private static (string Executable, IReadOnlyList<string> Arguments) GetSleepCommand()
    {
        return OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/c", "ping -n 30 127.0.0.1 >nul"])
            : ("/bin/sh", ["-c", "sleep 30"]);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}");
        }
    }

    private static void AssertContains(IReadOnlyList<string> values, string expected)
    {
        Assert(values.Contains(expected), $"参数列表缺少 {expected}");
    }

    private static void AssertNotContains(IReadOnlyList<string> values, string unexpected)
    {
        Assert(!values.Contains(unexpected), $"参数列表不应包含 {unexpected}");
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"应抛出 {typeof(TException).Name}");
    }

    private static async Task AssertThrowsAsync<TException>(Task task) where TException : Exception
    {
        try
        {
            await task;
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"应抛出 {typeof(TException).Name}");
    }

    private static void AssertCommandLineFails(string[] arguments, string expectedErrorPart)
    {
        var outcome = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(arguments);
        Assert(!outcome.Success, "无效命令行必须解析失败");
        Assert(
            outcome.Errors.Any(error => error.Contains(expectedErrorPart, StringComparison.OrdinalIgnoreCase)),
            $"错误信息必须包含 {expectedErrorPart}，实际: {string.Join(" | ", outcome.Errors)}");
    }

    private sealed class RecordingArchiveEngine : IArchiveEngine
    {
        public int CompressionCalls { get; private set; }
        public int ExtractionCalls { get; private set; }
        public ArchiveOptions? LastOptions { get; private set; }
        public string? LastCompressionOutput { get; private set; }

        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            CompressionCalls++;
            LastOptions = options;
            LastCompressionOutput = output;
            return Task.FromResult(new ArchiveResult { Success = true });
        }

        public Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            ExtractionCalls++;
            return Task.FromResult(new ArchiveResult { Success = true });
        }
    }

    // GLM-5.3, 2026-11-03：批次 2 表征测试用。可按输入名编排成功/失败、真实写出输出文件，
    // 并记录每次调用的输入与选项，用于锁定批处理循环的现有行为。
    private sealed class ScriptedArchiveEngine : IArchiveEngine
    {
        private readonly Func<string, bool>? _shouldFail;
        public List<string> CompressionInputs { get; } = [];
        public List<string> ExtractionInputs { get; } = [];
        public List<ArchiveOptions> CompressionOptions { get; } = [];
        public List<ArchiveOptions> ExtractionOptions { get; } = [];
        public List<string> ExtractionOutputDirs { get; } = [];
        public bool WriteRealOutput { get; set; }
        public int OutputBytesPerFile { get; set; } = 1024 * 1024;
        // GLM-5.3, 2026-11-04：取消传播表征测试用——引擎收到调用即抛取消异常，
        // 驱动服务层 finally 清理附件暂存目录并向上传播 OperationCanceledException。
        public bool ThrowCancellationOnCompress { get; set; }
        // 引擎执行中取消：延迟指定毫秒后抛取消异常，配合外部 CancelAfter 模拟用户中途取消。
        public int CancelAfterMilliseconds { get; set; }

        public ScriptedArchiveEngine(Func<string, bool>? shouldFail = null) => _shouldFail = shouldFail;

        public bool IsAvailable() => true;

        public async Task<ArchiveResult> CompressAsync(
            string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            CompressionInputs.Add(Path.GetFileName(input));
            CompressionOptions.Add(options);
            if (CancelAfterMilliseconds > 0)
            {
                await Task.Delay(CancelAfterMilliseconds, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new ArchiveResult { Success = false, ErrorMessage = "不应到达：延迟后应已取消" };
            }

            if (ThrowCancellationOnCompress)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("ThrowCancellationOnCompress 需要已取消的令牌");
            }
            if (_shouldFail?.Invoke(input) == true)
            {
                return new ArchiveResult { Success = false, ErrorMessage = "scripted failure" };
            }

            if (WriteRealOutput)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
                File.WriteAllBytes(output, new byte[OutputBytesPerFile]);
            }

            return new ArchiveResult { Success = true };
        }

        public Task<ArchiveResult> ExtractAsync(
            string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            ExtractionInputs.Add(Path.GetFileName(archivePath));
            ExtractionOptions.Add(options);
            ExtractionOutputDirs.Add(outputDir);
            if (_shouldFail?.Invoke(archivePath) == true)
            {
                return Task.FromResult(new ArchiveResult { Success = false, ErrorMessage = "scripted failure" });
            }

            return Task.FromResult(new ArchiveResult { Success = true });
        }
    }

    // GLM-5.3, 2026-11-03：记录关机等系统集成调用次数，用于锁定关机时机行为。
    private sealed class RecordingSystemIntegration : ISystemIntegration
    {
        public int ShutdownCalls { get; private set; }
        public int CancelShutdownCalls { get; private set; }

        public Task OpenFolderAsync(string path) => Task.CompletedTask;
        public Task<string?> ReadClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task WriteClipboardTextAsync(string text) => Task.CompletedTask;
        public void ShowNotification(string title, string message) { }
        public Task ShutdownAsync()
        {
            ShutdownCalls++;
            return Task.CompletedTask;
        }

        public Task CancelShutdownAsync()
        {
            CancelShutdownCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CommandReportingArchiveEngine : IArchiveEngine
    {
        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult
            {
                Success = true,
                CommandLine = $"rar a -psecret password \"{output}\" \"{input}\""
            });

        public Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult { Success = true });
    }

    private sealed class VolumeWritingArchiveEngine : IArchiveEngine
    {
        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output + ".001", new byte[1024]);
            File.WriteAllBytes(output + ".002", new byte[2048]);
            return Task.FromResult(new ArchiveResult { Success = true });
        }

        public Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult { Success = true });
    }



    private sealed class PartialFailureArchiveEngine : IArchiveEngine
    {
        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, "incomplete archive");
            File.WriteAllText(output + ".001", "incomplete volume 1");
            File.WriteAllText(output + ".002", "incomplete volume 2");
            return Task.FromResult(new ArchiveResult { Success = false, ErrorMessage = "disk full" });
        }

        public Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.Combine(outputDir, "partial"));
            File.WriteAllText(Path.Combine(outputDir, "partial.bin"), "incomplete");
            File.WriteAllText(Path.Combine(outputDir, "partial", "nested.bin"), "incomplete");
            return Task.FromResult(new ArchiveResult { Success = false, ErrorMessage = "missing volume" });
        }

        public void Reset()
        {
        }
    }

    private sealed class CancellingFailureArchiveEngine : IArchiveEngine
    {
        public bool IsAvailable() => true;

        public async Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, "partial in-flight");
            File.WriteAllText(output + ".001", "partial in-flight 001");
            await Task.Delay(500, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new ArchiveResult { Success = true };
        }

        public async Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.Combine(outputDir, "partial-cancel"));
            File.WriteAllText(Path.Combine(outputDir, "partial-cancel.bin"), "incomplete cancel");
            await Task.Delay(500, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new ArchiveResult { Success = true };
        }
    }
}
