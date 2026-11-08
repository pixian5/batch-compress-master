using System.Collections.Generic;
using System.IO.Compression;
using CompressionLevel = BatchCompress.Avalonia.Core.Interfaces.CompressionLevel;
using System.Text;
using System.Text.RegularExpressions;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;
using BatchCompress.Avalonia.Localization;

internal static partial class Program
{
    // 并发批处理、管道流转、取消与失败回滚测试


    private static async Task TestCancellation()
    {
        var (executable, arguments) = GetSleepCommand();
        var runner = new WinRarProcessRunner(executable);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await AssertThrowsAsync<OperationCanceledException>(runner.RunAsync(arguments, cancellation.Token));
    }

    private static async Task TestProcessOutputAndArgumentBoundaries()
    {
        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("SKIP 异步输出与参数边界: Windows 使用单独的进程行为测试");
            return;
        }

        var runner = new WinRarProcessRunner("/bin/sh");
        const string rawPassword = "password must stay visible";
        var result = await runner.RunAsync(
            ["-c", "printf '%s' \"$1\"; printf '%s' stderr-marker >&2; exit 7", "sh", rawPassword],
            CancellationToken.None);

        AssertEqual(7, result.ExitCode);
        // GPT-5, 2026-08-06：进程输出是原始诊断记录，密码文本明确不得被替换为 ***。
        AssertEqual(rawPassword, result.StandardOutput);
        AssertEqual("stderr-marker", result.StandardError);
        Assert(result.CommandLine.Contains(rawPassword, StringComparison.Ordinal),
            "完整命令行必须保留原始密码参数，不得脱敏");
    }

    private static async Task TestCommandLogArguments()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-command-log-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(source, "source");

        var snapshots = new List<OperationProgressInfo>();
        try
        {
            var engine = new CommandReportingArchiveEngine();
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "out"),
                    Extension = "rar",
                    ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            var commandMessage = snapshots
                .Select(snapshot => snapshot.Message)
                .FirstOrDefault(message => message.Contains("[压缩命令] command", StringComparison.Ordinal));
            Assert(commandMessage?.Contains("secret password", StringComparison.Ordinal) == true,
                "批处理命令日志必须原样包含引擎返回的完整参数");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task TestCurrentSourcePathProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-progress-path-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "same-name.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(source, "source");

        try
        {
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(new TestArchiveEngine(), new TestSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "out"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            Assert(snapshots.Any(snapshot => string.Equals(snapshot.CurrentSourcePath, source, StringComparison.Ordinal)),
                "进度快照必须保留完整来源路径，以便 GUI 定位并高亮当前行");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // 消息文本、七类计数、关机时机与文件系统副作用，重构期间任何行为漂移都会在此暴露。
    private static async Task TestCompressionLoopCharacterization()
    {
        var L = LocalizationService.Instance.Strings;
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-c-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 场景 1：不可创建格式计 1 条失败、返回格式消息、不进循环、也不触发关机。
            var engine = new ScriptedArchiveEngine();
            var system = new RecordingSystemIntegration();
            var snapshots = new List<OperationProgressInfo>();
            var service = new BatchOperationService(engine, system);
            await service.BatchCompressAsync(
                [root],
                new BatchOperationOptions { OutputPath = root, Extension = "iso", ShutdownAfterComplete = true },
                new SnapshotProgress(snapshots), CancellationToken.None);
            var final = snapshots.Last();
            AssertEqual(1, final.FailCount);
            AssertEqual(0, final.SuccessCount);
            AssertEqual(0, engine.CompressionInputs.Count);
            AssertEqual(0, system.ShutdownCalls);
            Assert(final.Message == string.Format(L.FormatCannotCreate, "iso", ArchiveFormatCatalog.CreateFormatsText),
                "格式拒绝消息必须与词条完全一致");

            // 场景 2：源不存在计 NotFound、消息含原始路径、引擎零调用。
            var missing = Path.Combine(root, "missing-item");
            snapshots.Clear();
            await service.BatchCompressAsync(
                [missing],
                new BatchOperationOptions { OutputPath = root, Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            final = snapshots.Last();
            AssertEqual(1, final.NonExistCount);
            AssertEqual(0, engine.CompressionInputs.Count);
            Assert(final.Message == string.Format(L.ItemNotFound, missing), "源不存在消息必须与词条一致");

            // 场景 3：SkipAlreadyProcessed 对名称含【已压缩】的源静默跳过（计数增加但不产生任何进度报告）。
            var doneSource = Path.Combine(root, "proj【已压缩】");
            Directory.CreateDirectory(doneSource);
            snapshots.Clear();
            await service.BatchCompressAsync(
                [doneSource],
                new BatchOperationOptions
                {
                    OutputPath = root, Extension = "7z", SkipAlreadyProcessed = true,
                    ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            Assert(snapshots.Count == 0, "跳过已处理项不得产生进度报告");
            AssertEqual(0, engine.CompressionInputs.Count);
            Directory.Delete(doneSource);

            // 场景 4：Overwrite 删除已有输出，包括新式分卷与旧式 .r00 遗留（rar 双组枚举）。
            var overwriteSource = Path.Combine(root, "ow-src");
            var overwriteOutput = Path.Combine(root, "ow-out");
            Directory.CreateDirectory(overwriteSource);
            Directory.CreateDirectory(overwriteOutput);
            File.WriteAllText(Path.Combine(overwriteSource, "a.txt"), "a");
            File.WriteAllText(Path.Combine(overwriteOutput, "ow-src.rar"), "old");
            File.WriteAllText(Path.Combine(overwriteOutput, "ow-src.part1.rar"), "old");
            File.WriteAllText(Path.Combine(overwriteOutput, "ow-src.r00"), "old");
            await service.BatchCompressAsync(
                [overwriteSource],
                new BatchOperationOptions { OutputPath = overwriteOutput, Extension = "rar", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, engine.CompressionInputs.Count);
            AssertEqual(1, snapshots.Last().SuccessCount);
            Assert(!File.Exists(Path.Combine(overwriteOutput, "ow-src.rar")), "覆盖模式必须删除旧主卷");
            Assert(!File.Exists(Path.Combine(overwriteOutput, "ow-src.part1.rar")), "覆盖模式必须删除旧 part 分卷");
            Assert(!File.Exists(Path.Combine(overwriteOutput, "ow-src.r00")), "覆盖模式必须删除旧式 .r00 遗留分卷");

            // 场景 5：DeleteSourceAfter 成功时源被删、成功消息为 ItemSucceeded、无后处理失败。
            snapshots.Clear();
            var deleteSource = Path.Combine(root, "del-src");
            Directory.CreateDirectory(deleteSource);
            File.WriteAllText(Path.Combine(deleteSource, "a.txt"), "a");
            await service.BatchCompressAsync(
                [deleteSource],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "del-out"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, DeleteSourceAfter = true
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            final = snapshots.Last();
            AssertEqual(1, final.SuccessCount);
            AssertEqual(0, final.PostProcessFailCount);
            Assert(!Directory.Exists(deleteSource), "删除源后处理必须移除源目录");
            Assert(final.Message == string.Format(L.ItemSucceeded, "del-src"), "纯成功消息必须与词条一致");

            // 场景 6：MoveSourceAfter 成功时源移入【已压缩】目录。
            var moveSource = Path.Combine(root, "mv-src");
            Directory.CreateDirectory(moveSource);
            File.WriteAllText(Path.Combine(moveSource, "a.txt"), "a");
            await service.BatchCompressAsync(
                [moveSource],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "mv-out"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, MoveSourceAfter = true
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            Assert(!Directory.Exists(moveSource), "移动后源不得留在原位置");
            Assert(Directory.Exists(Path.Combine(root, "【已压缩】", "mv-src")), "源必须移入【已压缩】目录");

            // 场景 7：引擎失败计 FailCount 并输出 ItemFailed 消息（含名称与引擎错误）。
            var failingEngine = new ScriptedArchiveEngine(input => true);
            var failSource = Path.Combine(root, "fail-src");
            Directory.CreateDirectory(failSource);
            snapshots.Clear();
            await new BatchOperationService(failingEngine, system).BatchCompressAsync(
                [failSource],
                new BatchOperationOptions { OutputPath = root, Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            final = snapshots.Last();
            AssertEqual(1, final.FailCount);
            Assert(final.Message == string.Format(L.ItemFailed, "fail-src", "scripted failure"),
                "失败消息必须与词条一致并包含引擎错误");

            // 场景 8：MaxSizeGB 达到后停止后续任务（真实 1MB 输出超过 0.0005GB 上限）。
            var sizeEngine = new ScriptedArchiveEngine { WriteRealOutput = true };
            var firstSource = Path.Combine(root, "size-a");
            var secondSource = Path.Combine(root, "size-b");
            Directory.CreateDirectory(firstSource);
            Directory.CreateDirectory(secondSource);
            snapshots.Clear();
            await new BatchOperationService(sizeEngine, system).BatchCompressAsync(
                [firstSource, secondSource],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "size-out"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, MaxSizeGB = 0.0005
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, sizeEngine.CompressionInputs.Count);
            AssertEqual(1, snapshots.Last().SuccessCount);
            Assert(snapshots.Any(info => info.Message == L.SizeLimitReached), "达到大小上限必须报告 SizeLimitReached");

            // 场景 9：全部任务结束后才请求关机（含跳过项的批次同样触发）。
            var shutdownEngine = new ScriptedArchiveEngine();
            var shutdownSource = Path.Combine(root, "sd-src");
            Directory.CreateDirectory(shutdownSource);
            var shutdownSystem = new RecordingSystemIntegration();
            await new BatchOperationService(shutdownEngine, shutdownSystem).BatchCompressAsync(
                [shutdownSource],
                new BatchOperationOptions
                {
                    OutputPath = root, Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite,
                    ShutdownAfterComplete = true
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, shutdownSystem.ShutdownCalls);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-03：批次 2 表征测试——解压循环。
    private static async Task TestDecompressionLoopCharacterization()
    {
        var L = LocalizationService.Instance.Strings;
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-d-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var engine = new ScriptedArchiveEngine();
            var system = new RecordingSystemIntegration();
            var service = new BatchOperationService(engine, system);
            var snapshots = new List<OperationProgressInfo>();

            // 场景 1：分卷编号重复（.1 与 .001 并存）计歧义并跳过、引擎零调用。
            File.WriteAllText(Path.Combine(root, "dup.7z.1"), "1");
            File.WriteAllText(Path.Combine(root, "dup.7z.001"), "001");
            await service.BatchDecompressAsync(
                [new FileEntry { FilePath = Path.Combine(root, "dup.7z.001"), FileSize = 3 }],
                new BatchOperationOptions { OutputPath = Path.Combine(root, "o1"), Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            var final = snapshots.Last();
            AssertEqual(1, final.AmbiguousArchiveCount);
            AssertEqual(1, final.IgnoreCount);
            AssertEqual(0, engine.ExtractionInputs.Count);
            Assert(final.Message == string.Format(L.DuplicateVolumeSkipped, Path.Combine(root, "dup.7z.001")),
                "重复编号消息必须与词条一致");

            // 场景 2：同一分卷组重复出现时第二个任务静默去重。
            File.WriteAllText(Path.Combine(root, "same.7z.001"), "a");
            File.WriteAllText(Path.Combine(root, "same.7z.002"), "b");
            var sameEntry = new FileEntry { FilePath = Path.Combine(root, "same.7z.001"), FileSize = 1 };
            snapshots.Clear();
            await service.BatchDecompressAsync(
                [sameEntry, sameEntry],
                new BatchOperationOptions { OutputPath = Path.Combine(root, "o2"), Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            final = snapshots.Last();
            AssertEqual(1, engine.ExtractionInputs.Count);
            AssertEqual(1, final.IgnoreCount);

            // 场景 3：SkipAlreadyProcessed 对名称含【已解压】的归档静默跳过。
            var processedArchive = Path.Combine(root, "done【已解压】.7z");
            File.WriteAllText(processedArchive, "x");
            snapshots.Clear();
            await service.BatchDecompressAsync(
                [new FileEntry { FilePath = processedArchive, FileSize = 1 }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "o3"), Extension = "7z", SkipAlreadyProcessed = true,
                    ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            Assert(snapshots.Count == 0, "跳过已处理归档不得产生进度报告");
            AssertEqual(1, engine.ExtractionInputs.Count);

            // 场景 4：TXT 条目密码优先于全局 CustomPassword，条目为空时才回退。
            var pwArchive = Path.Combine(root, "pw.7z");
            File.WriteAllText(pwArchive, "x");
            await service.BatchDecompressAsync(
                [
                    new FileEntry { FilePath = pwArchive, Password = "txt-pw", FileSize = 1 },
                    new FileEntry { FilePath = processedArchive, Password = null, FileSize = 1 }
                ],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "o4"), Extension = "7z",
                    CustomPassword = "global-pw", ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual("txt-pw", engine.ExtractionOptions[1].Password);
            AssertEqual("global-pw", engine.ExtractionOptions[2].Password);

            // 场景 5：数字分卷传递归一化格式（.001 不当作格式）。
            AssertEqual("7z", engine.ExtractionOptions[1].ArchiveFormat);

            // 场景 6：DeleteSourceAfter 删除整组分卷。
            var group = new[] { "grp.7z.001", "grp.7z.002", "grp.7z.003" };
            foreach (var name in group)
            {
                File.WriteAllText(Path.Combine(root, name), name);
            }

            await service.BatchDecompressAsync(
                [new FileEntry { FilePath = Path.Combine(root, group[0]), FileSize = 3 }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "o6"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, DeleteSourceAfter = true
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            foreach (var name in group)
            {
                Assert(!File.Exists(Path.Combine(root, name)), $"删除源必须移除整组成员：{name}");
            }

            // 场景 7：MaxSizeGB 达到后停止后续任务（第一组真实 3MB 超过 0.002GB）。
            var sizeRoot = Path.Combine(root, "sizes");
            Directory.CreateDirectory(sizeRoot);
            foreach (var name in new[] { "s1.7z.001", "s1.7z.002", "s1.7z.003" })
            {
                File.WriteAllBytes(Path.Combine(sizeRoot, name), new byte[1024 * 1024]);
            }

            File.WriteAllBytes(Path.Combine(sizeRoot, "s2.7z"), new byte[1024 * 1024]);
            var sizeEngine = new ScriptedArchiveEngine();
            snapshots.Clear();
            await new BatchOperationService(sizeEngine, system).BatchDecompressAsync(
                [
                    new FileEntry { FilePath = Path.Combine(sizeRoot, "s1.7z.001"), FileSize = 3 },
                    new FileEntry { FilePath = Path.Combine(sizeRoot, "s2.7z"), FileSize = 1 }
                ],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "o7"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, MaxSizeGB = 0.002
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, sizeEngine.ExtractionInputs.Count);
            Assert(snapshots.Any(info => info.Message == L.SizeLimitReached), "解压达到大小上限必须报告");

            // 场景 8：全部解压任务结束后请求一次关机。
            var shutdownEngine = new ScriptedArchiveEngine();
            var shutdownSystem = new RecordingSystemIntegration();
            var shutdownArchive = Path.Combine(root, "sd.7z");
            File.WriteAllText(shutdownArchive, "x");
            await new BatchOperationService(shutdownEngine, shutdownSystem).BatchDecompressAsync(
                [new FileEntry { FilePath = shutdownArchive, FileSize = 1 }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "o8"), Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite, ShutdownAfterComplete = true
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, shutdownSystem.ShutdownCalls);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-03：批次 2 表征测试——来源目录扫描。
    private static Task TestSourceCollectionCharacterization()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-s-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var name in new[] { "keep", "done【已压缩】", "done【已解压】", ".tmp-partial" })
            {
                Directory.CreateDirectory(Path.Combine(root, name));
            }

            File.WriteAllText(Path.Combine(root, "desktop.ini"), "meta");
            File.WriteAllText(Path.Combine(root, ".DS_Store"), "meta");

            var service = new BatchOperationService(new ScriptedArchiveEngine(), new RecordingSystemIntegration());
            var skipped = service.LoadCompressionSourcesFromFolder(root, skipProcessed: true)
                .Select(Path.GetFileName)
                .ToArray();
            AssertEqual(1, skipped.Length);
            AssertEqual("keep", skipped[0]);

            var all = service.LoadCompressionSourcesFromFolder(root, skipProcessed: false)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            AssertEqual(3, all.Length);
            Assert(!all.Contains("desktop.ini"), "系统元数据必须始终被过滤");
            Assert(!all.Contains(".DS_Store"), "macOS 元数据必须始终被过滤");
            Assert(!all.Contains(".tmp-partial"), ".tmp 前缀必须始终被过滤");
        }
        finally
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-03：大小写歧义只在区分大小写的文件系统（Linux CI）可构造，
    // 其余平台输出 SKIP。锁定：歧义计 Ambiguous+Ignore、消息一致、引擎零调用。
    private static async Task TestCaseAmbiguitySkip()
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("SKIP 大小写歧义跳过: 需要区分大小写的文件系统");
            return;
        }

        var L = LocalizationService.Instance.Strings;
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "v.7z"), "one");
            File.WriteAllText(Path.Combine(root, "V.7z"), "two");
            var engine = new ScriptedArchiveEngine();
            var snapshots = new List<OperationProgressInfo>();
            // 请求不存在的字面变体触发忽略大小写枚举，命中两个条目即歧义。
            var requested = Path.Combine(root, "v.7Z");
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = requested, FileSize = 1 }],
                new BatchOperationOptions { OutputPath = Path.Combine(root, "out"), Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite },
                new SnapshotProgress(snapshots), CancellationToken.None);
            var final = snapshots.Last();
            AssertEqual(1, final.AmbiguousArchiveCount);
            AssertEqual(1, final.IgnoreCount);
            AssertEqual(0, engine.ExtractionInputs.Count);
            Assert(final.Message == string.Format(L.CaseAmbiguitySkipped, requested), "大小写歧义消息必须与词条一致");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-04：批次 2 阶段 2——尾部斜杠缺陷阻断的规范化基线。
    // 锁定：SourceCollector.NormalizePath 消除尾部目录分隔符（文件名不得为空串）、
    // 根路径保持原样、相对路径转绝对、空输入与非法路径防御。
    private static Task TestPathNormalization()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-norm-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "dir");
        Directory.CreateDirectory(directory);
        try
        {
            // 尾部主分隔符消除：GetFileName("/x/dir/") 历史上返回空串导致生成 ".7z" 空名归档。
            var withSlash = SourceCollector.NormalizePath(directory + Path.DirectorySeparatorChar);
            AssertEqual(directory, withSlash);
            AssertEqual("dir", Path.GetFileName(withSlash));

            // 备用分隔符（Windows 的 “/”）与连续多个尾部斜杠同样消除。
            AssertEqual(directory, SourceCollector.NormalizePath(
                directory + Path.AltDirectorySeparatorChar));
            AssertEqual(directory, SourceCollector.NormalizePath(
                directory + new string(Path.DirectorySeparatorChar, 3)));

            // 文件系统根不得截断：TrimEnd("/") 会得到空串，Windows 上 “C:\”→“C:” 会变成盘符相对路径。
            var volumeRoot = Path.GetPathRoot(Path.GetFullPath(root)) ?? Path.DirectorySeparatorChar.ToString();
            AssertEqual(volumeRoot, SourceCollector.NormalizePath(volumeRoot));

            // 相对路径转绝对；首尾空格剔除。
            AssertEqual(
                Path.Combine(Directory.GetCurrentDirectory(), "dir"),
                SourceCollector.NormalizePath("  dir  "));

            // 空与空白输入返回空串；非法路径不抛异常、原样返回（交由服务层按 ItemNotFound 报告）。
            AssertEqual(string.Empty, SourceCollector.NormalizePath("   "));
            AssertEqual(string.Empty, SourceCollector.NormalizePath(null));
            var invalid = "bad\0path";
            AssertEqual(invalid, SourceCollector.NormalizePath(invalid));
        }
        finally
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 2 阶段 2——统一来源收集行为基线。
    // 锁定：GUI/CLI 共用收集器的空行与元数据过滤、保序去重、尾部斜杠识别、
    // 密码本交替行配对、解压格式过滤与 CLI 目录展开/首卷折叠/种子优先去重。
    private static Task TestUnifiedSourceCollection()
    {
        var L = LocalizationService.Instance.Strings;
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-collect-{Guid.NewGuid():N}");
        var project = Path.Combine(root, "proj");
        var archives = Path.Combine(root, "archives");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(archives);
        File.WriteAllText(Path.Combine(root, "a.7z"), "a");
        File.WriteAllText(Path.Combine(root, "b.rar"), "b");
        File.WriteAllText(Path.Combine(root, "note.txt"), "n");
        File.WriteAllText(Path.Combine(root, ".DS_Store"), "meta");
        File.WriteAllText(Path.Combine(archives, "g.7z.001"), "1");
        File.WriteAllText(Path.Combine(archives, "g.7z.002"), "2");
        File.WriteAllText(Path.Combine(archives, "other.rar"), "rar");
        try
        {
            // 1. 压缩来源（GUI 多行文本与 CLI 路径集合共用口径）：
            //    空行/空白行/元数据行剔除；目录尾斜杠规范化后文件名为 “proj” 而非空串；
            //    等价写法（尾斜杠、/./ 段）保序去重为一条。
            var projectWithSlash = project + Path.DirectorySeparatorChar;
            var multiLineText = string.Join("\r\n",
            [
                "",
                "   ",
                "  " + projectWithSlash,
                Path.Combine(root, "a.7z"),
                Path.Combine(root, ".DS_Store"),
                projectWithSlash,
                Path.Combine(root, ".", "proj")
            ]);
            var sources = SourceCollector.CollectCompressionSourcesFromText(multiLineText);
            AssertEqual(2, sources.Count);
            AssertEqual(project, sources[0]);
            AssertEqual("proj", Path.GetFileName(sources[0]));
            AssertEqual(Path.Combine(root, "a.7z"), sources[1]);

            // CLI 入口等价：同一批原始路径得到同一结果。
            var cliSources = SourceCollector.CollectCompressionSources(
            [
                projectWithSlash,
                Path.Combine(root, "a.7z"),
                projectWithSlash
            ]);
            AssertEqual(sources.Count, cliSources.Count);
            AssertEqual(sources[0], cliSources[0]);
            AssertEqual(sources[1], cliSources[1]);

            // 2. 密码本交替行：缺失文件连同密码整对跳过；文件行尾斜杠仍可识别；密码配对不漂移。
            var missing = Path.Combine(root, "missing.7z");
            var bookText = string.Join("\n",
            [
                Path.Combine(root, "a.7z"), "pw-a",
                missing, "pw-missing",
                Path.Combine(root, "b.rar") + Path.DirectorySeparatorChar, "pw-b"
            ]);
            var book = SourceCollector.CollectPasswordBookEntries(bookText);
            AssertEqual(2, book.Count);
            AssertEqual(Path.Combine(root, "a.7z"), book[0].FilePath);
            AssertEqual("pw-a", book[0].Password);
            AssertEqual(Path.Combine(root, "b.rar"), book[1].FilePath);
            AssertEqual("pw-b", book[1].Password);

            // 3. 解压文件行：存在性 + 格式过滤（txt 与 rar 不入 7z 任务）、路径规范化。
            var files = SourceCollector.CollectDecompressionFilesFromText(
                string.Join("\n", [Path.Combine(root, "b.rar"), "  " + Path.Combine(root, "a.7z") + "  ", Path.Combine(root, "note.txt")]),
                "7z");
            AssertEqual(1, files.Count);
            AssertEqual(Path.Combine(root, "a.7z"), files[0].FilePath);
            AssertEqual(1L, files[0].FileSize);

            // 4. CLI 解压收集：目录展开按目标格式过滤、--input 与目录展开折叠到同一首卷后去重、
            //    密码本种子条目优先保留（含密码）。
            var service = new BatchOperationService(new ScriptedArchiveEngine(), new RecordingSystemIntegration());
            var seed = new List<FileEntry>
            {
                new() { FilePath = Path.Combine(archives, "g.7z.001"), Password = "seed-pw", FileSize = 1 }
            };
            var entries = SourceCollector.CollectDecompressionEntries(
                seed,
                [archives, Path.Combine(archives, "g.7z.001"), missing],
                "7z",
                skipProcessed: false,
                batchService: service);
            AssertEqual(1, entries.Count);
            AssertEqual(Path.Combine(archives, "g.7z.001"), entries[0].FilePath);
            AssertEqual("seed-pw", entries[0].Password);

            // 尾部斜杠目录（GUI 风格输入走 CLI 收集口径）同样完成展开与去重。
            var slashedEntries = SourceCollector.CollectDecompressionEntries(
                null,
                [archives + Path.DirectorySeparatorChar],
                "7z",
                skipProcessed: false,
                batchService: service);
            AssertEqual(1, slashedEntries.Count);
            AssertEqual(Path.Combine(archives, "g.7z.001"), slashedEntries[0].FilePath);

            // 端到端守卫：带尾斜杠的来源经收集后进入批处理循环，消息必须携带真实目录名。
            var snapshots = new List<OperationProgressInfo>();
            new BatchOperationService(new ScriptedArchiveEngine(), new RecordingSystemIntegration())
                .BatchCompressAsync(
                    SourceCollector.CollectCompressionSources([projectWithSlash]),
                    new BatchOperationOptions
                    {
                        OutputPath = Path.Combine(root, "out"),
                        Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite
                    },
                    new SnapshotProgress(snapshots),
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(snapshots.Count > 0 && snapshots.Last().CurrentFile == "proj",
                "尾斜杠来源必须以目录名进入压缩任务，禁止空名归档");
            Assert(snapshots.Last().Message == string.Format(L.ItemSucceeded, "proj"),
                "成功消息必须携带真实目录名，禁止空名归档任务");
        }
        finally
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：外部审核补强——取消传播表征。锁定：
    // 循环头预取消与引擎执行中取消均抛 OperationCanceledException、绝不触发关机、
    // 附件暂存目录在 finally 中可靠清理。
    private static async Task TestCancellationPropagation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        try
        {
            // 场景 1：进入循环前已取消——循环头即抛、引擎零调用、不关机。
            var engine = new ScriptedArchiveEngine { ThrowCancellationOnCompress = true };
            var system = new RecordingSystemIntegration();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(
                new BatchOperationService(engine, system).BatchCompressAsync(
                    [source],
                    new BatchOperationOptions
                    {
                        OutputPath = Path.Combine(root, "out"), Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite, ShutdownAfterComplete = true
                    },
                    new SnapshotProgress([]), cancelled.Token));
            AssertEqual(0, engine.CompressionInputs.Count);
            AssertEqual(0, system.ShutdownCalls);

            // 场景 2：引擎执行中取消——附件暂存目录必须被 finally 清理、不关机。
            var delayingEngine = new ScriptedArchiveEngine { CancelAfterMilliseconds = 400 };
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);
            await AssertThrowsAsync<OperationCanceledException>(
                new BatchOperationService(delayingEngine, system).BatchCompressAsync(
                    [source],
                    new BatchOperationOptions
                    {
                        OutputPath = Path.Combine(root, "out2"), Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite, ShutdownAfterComplete = true,
                        AddEnclosures = true,
                        EnclosureDirectories = [Path.Combine(root, "definitely-not-exists-attachment")]
                    },
                    new SnapshotProgress([]), cts.Token));
            AssertEqual(1, delayingEngine.CompressionInputs.Count);
            AssertEqual(0, system.ShutdownCalls);
            Assert(Directory.GetDirectories(Path.GetTempPath(), "batch-compress-attachments-*").Length == 0,
                "取消后附件暂存目录必须被 finally 可靠清理，不得残留");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-04：外部审核补强——Update 模式在服务层原样透传：
    // 不预删除已有输出、不跳过任务，ExistingFileMode 原样传给引擎（差异化由命令构建层实现）。
    private static async Task TestUpdateModePassthrough()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 压缩侧：已有输出存在时 Update 仍调用引擎且不删除旧输出。
            var source = Path.Combine(root, "src");
            var output = Path.Combine(root, "out");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(source, "a.txt"), "a");
            var existing = Path.Combine(output, "src.7z");
            File.WriteAllText(existing, "old output");
            var engine = new ScriptedArchiveEngine();
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions { OutputPath = output, Extension = "7z", ExistingFileMode = ExistingFileMode.Update },
                new SnapshotProgress([]), CancellationToken.None);
            AssertEqual(1, engine.CompressionInputs.Count);
            Assert(File.Exists(existing), "Update 模式不得预删除已有输出");
            AssertEqual(ExistingFileMode.Update, engine.CompressionOptions[0].ExistingFileMode);

            // 解压侧：ExistingFileMode 原样透传给引擎选项。
            var archive = Path.Combine(root, "book.7z");
            File.WriteAllText(archive, "archive");
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = archive, FileSize = 7 }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "ex"), Extension = "7z", ExistingFileMode = ExistingFileMode.Update
                },
                new SnapshotProgress([]), CancellationToken.None);
            AssertEqual(ExistingFileMode.Update, engine.ExtractionOptions[0].ExistingFileMode);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-04：外部审核补强——CurrentFile 刷新基线。
    // 锁定：解压预检失败（缺首卷等）与压缩源不存在分支均在刷新 CurrentFile 之前 continue，
    // 最后一次进度报告的 CurrentFile 保持上一个成功项，不显示失败项名。
    private static async Task TestCurrentFileBaseline()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-char-cf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 解压侧：先成功一项，再遇到缺首卷组——CurrentFile 不得刷新为失败项。
            File.WriteAllText(Path.Combine(root, "ok.7z"), "x");
            File.WriteAllText(Path.Combine(root, "broken.7z.002"), "x");
            File.WriteAllText(Path.Combine(root, "broken.7z.003"), "x");
            var engine = new ScriptedArchiveEngine();
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchDecompressAsync(
                [
                    new FileEntry { FilePath = Path.Combine(root, "ok.7z"), FileSize = 1 },
                    new FileEntry { FilePath = Path.Combine(root, "broken.7z.003"), FileSize = 1 }
                ],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "ex"), Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            var final = snapshots.Last();
            AssertEqual(1, final.IncompleteVolumeCount);
            AssertEqual("ok.7z", final.CurrentFile);

            // 压缩侧：源不存在分支在 CurrentFile 赋值前 continue，保持上一项。
            snapshots.Clear();
            var good = Path.Combine(root, "good");
            Directory.CreateDirectory(good);
            await new BatchOperationService(new ScriptedArchiveEngine(), new RecordingSystemIntegration()).BatchCompressAsync(
                [good, Path.Combine(root, "gone")],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "co"), Extension = "7z", ExistingFileMode = ExistingFileMode.Overwrite
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual("good", snapshots.Last().CurrentFile);
            AssertEqual(1, snapshots.Last().NonExistCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestVolumeValidationAndProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-volume-progress-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var missingSecond = Path.Combine(root, "broken.7z.001");
            var third = Path.Combine(root, "broken.7z.003");
            File.WriteAllText(missingSecond, "1");
            File.WriteAllText(third, "3");
            var engine = new RecordingArchiveEngine();
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = missingSecond, FileSize = 1 }],
                new BatchOperationOptions { OutputPath = Path.Combine(root, "out"), Extension = "7z" },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            var final = snapshots.Last();
            AssertEqual(1, final.IncompleteVolumeCount);
            AssertEqual(1, final.IgnoreCount);
            AssertEqual(0, engine.ExtractionCalls);

            var absoluteWithoutExtension = Path.Combine(root, "absolute-name");
            File.WriteAllText(absoluteWithoutExtension + ".rar", "rar");
            var passwordBook = Path.Combine(root, "passwords.txt");
            File.WriteAllLines(passwordBook, [absoluteWithoutExtension, "password"]);
            var imported = new BatchOperationService(engine, new TestSystemIntegration())
                .LoadFilesFromTextFileWithDiagnostics(passwordBook, Path.Combine(root, "other"), "rar");
            AssertEqual(1, imported.RequestedCount);
            AssertEqual(1, imported.Entries.Count);
            AssertEqual(absoluteWithoutExtension + ".rar", imported.Entries[0].FilePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestCompressedVolumeSizeProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-output-size-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.txt");
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(root);
        File.WriteAllText(source, "source");
        var snapshots = new List<OperationProgressInfo>();
        try
        {
            await new BatchOperationService(new VolumeWritingArchiveEngine(), new TestSystemIntegration())
                .BatchCompressAsync(
                    [source],
                    new BatchOperationOptions
                    {
                        OutputPath = output,
                        Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite
                    },
                    new SnapshotProgress(snapshots),
                    CancellationToken.None);

            var final = snapshots.Last();
            var expected = 3072d / (1024d * 1024d * 1024d);
            Assert(Math.Abs(final.ProcessedSizeGB - expected) < 0.000000001,
                $"分卷统计必须累计全部卷大小，期望 {expected}，实际 {final.ProcessedSizeGB}");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task TestFailedExtractionRollback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-extraction-rollback-{Guid.NewGuid():N}");
        var archive = Path.Combine(root, "broken.7z");
        var existingOutput = Path.Combine(root, "existing-output");
        var freshOutput = Path.Combine(root, "fresh-output");
        Directory.CreateDirectory(existingOutput);
        File.WriteAllText(archive, "archive placeholder");
        File.WriteAllText(Path.Combine(existingOutput, "keep.txt"), "keep this file");
        try
        {
            var engine = new PartialFailureArchiveEngine();
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = archive, FileSize = new FileInfo(archive).Length }],
                new BatchOperationOptions { OutputPath = existingOutput, Extension = "7z" },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            AssertEqual(1, snapshots.Last().FailCount);
            Assert(File.Exists(Path.Combine(existingOutput, "keep.txt")), "解压失败不得删除输出目录中原有文件");
            Assert(!File.Exists(Path.Combine(existingOutput, "partial.bin")), "解压失败必须清理新增部分文件");
            Assert(!Directory.Exists(Path.Combine(existingOutput, "partial")), "解压失败必须清理新增目录");

            engine.Reset();
            snapshots.Clear();
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = archive, FileSize = new FileInfo(archive).Length }],
                new BatchOperationOptions { OutputPath = freshOutput, Extension = "7z" },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            AssertEqual(1, snapshots.Last().FailCount);
            Assert(!Directory.Exists(freshOutput), "新建输出目录在解压失败后不得残留空目录");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task TestArchiveFolderScanning()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-folder-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "group.zip.001");
            var second = Path.Combine(root, "group.zip.002");
            File.WriteAllText(first, "1");
            File.WriteAllText(second, "2");
            File.WriteAllText(Path.Combine(root, "other.rar"), "rar");
            File.WriteAllText(Path.Combine(root, "note.txt"), "text");
            Directory.CreateDirectory(Path.Combine(root, "folder"));

            var engine = new RecordingArchiveEngine();
            var service = new BatchOperationService(engine, new TestSystemIntegration());
            var archives = service.LoadArchivesFromFolder(root, "zip", skipProcessed: false);
            AssertEqual(1, archives.Count);
            AssertEqual(first, archives[0]);

            var compressionSources = service.LoadCompressionSourcesFromFolder(root, skipProcessed: false);
            Assert(compressionSources.Contains(Path.Combine(root, "folder")), "压缩来源扫描必须保留直接子目录");
            Assert(compressionSources.Contains(Path.Combine(root, "note.txt")), "压缩来源扫描不能按归档格式过滤");

            var snapshots = new List<OperationProgressInfo>();
            await service.BatchDecompressAsync(
                [
                    new FileEntry { FilePath = second, FileSize = 1 },
                    new FileEntry { FilePath = first, FileSize = 1 }
                ],
                new BatchOperationOptions { OutputPath = Path.Combine(root, "out"), Extension = "zip" },
                new SnapshotProgress(snapshots),
                CancellationToken.None);
            AssertEqual(1, engine.ExtractionCalls);
            AssertEqual(1, snapshots.Last().SuccessCount);
            AssertEqual(1, snapshots.Last().IgnoreCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task TestAttachmentRootInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-attachment-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source");
        var existing = Path.Combine(root, "外部附件");
        var missing = Path.Combine(root, "待创建附件");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "attachment.txt"), "attachment");
        var engine = new RecordingArchiveEngine();
        try
        {
            var service = new BatchOperationService(engine, new TestSystemIntegration());
            await service.BatchCompressAsync([source], new BatchOperationOptions
            {
                OutputPath = Path.Combine(root, "out"), Extension = "7z", AddEnclosures = true,
                EnclosureDirectories = [existing, missing], ExistingFileMode = ExistingFileMode.Overwrite
            }, new Progress<OperationProgressInfo>(), CancellationToken.None);
            Assert(engine.LastOptions?.AdditionalInputs?.Any(path => Path.GetFileName(path) == "外部附件") == true,
                "存在的附件必须作为根级输入传递");
            Assert(engine.LastOptions?.AdditionalInputs?.Any(path => Path.GetFileName(path) == "待创建附件") == true,
                "不存在的附件必须在暂存目录创建根级空目录");
            Assert(!Directory.Exists(Path.Combine(source, "外部附件")), "不得把附件目录写入源目录");
            Assert(!Directory.Exists(Path.Combine(source, "待创建附件")), "不得把缺失附件目录写入源目录");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task TestExistingSkipProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-skip-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        File.WriteAllText(Path.Combine(output, "source.7z"), "already");
        File.WriteAllText(Path.Combine(output, "source.7z.001"), "volume 1");
        File.WriteAllText(Path.Combine(output, "source.7z.002"), "volume 2");
        var engine = new RecordingArchiveEngine();
        var snapshots = new List<OperationProgressInfo>();
        try
        {
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchCompressAsync(
                [source], new BatchOperationOptions { OutputPath = output, Extension = "7z", ExistingFileMode = ExistingFileMode.Skip },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, snapshots.Last().IgnoreCount);
            AssertEqual(0, snapshots.Last().FailCount);
            AssertEqual(0, engine.CompressionCalls);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task TestPostProcessConflict()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-post-process-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "out");
        var processed = Path.Combine(root, "【已压缩】", "source");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(processed);
        File.WriteAllText(Path.Combine(source, "source.txt"), "source remains");
        File.WriteAllText(Path.Combine(processed, "existing.txt"), "target remains");
        var snapshots = new List<OperationProgressInfo>();
        try
        {
            await new BatchOperationService(new TestArchiveEngine(), new TestSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions
                {
                    OutputPath = output,
                    Extension = "rar",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MoveSourceAfter = true
                },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            var final = snapshots.Last();
            AssertEqual(1, final.SuccessCount);
            AssertEqual(0, final.FailCount);
            AssertEqual(1, final.PostProcessFailCount);
            // GPT-5, 2026-09-30：失败记录必须包含具体原因（含目标路径），而不是只有“后处理失败”。
            Assert(final.Message.Contains("【已压缩】"), "移动目标冲突原因必须进入失败记录消息");
            Assert(Directory.Exists(source), "移动目标冲突时必须保留源目录");
            Assert(File.Exists(Path.Combine(processed, "existing.txt")), "移动目标冲突时必须保留目标目录");

            var firstVolume = Path.Combine(root, "archive.7z.001");
            var secondVolume = Path.Combine(root, "archive.7z.002");
            var extractedProcessed = Path.Combine(root, "【已解压】");
            var conflictingSecondVolume = Path.Combine(extractedProcessed, "archive.7z.002");
            File.WriteAllText(firstVolume, "first volume");
            File.WriteAllText(secondVolume, "second volume");
            Directory.CreateDirectory(extractedProcessed);
            File.WriteAllText(conflictingSecondVolume, "existing target");
            snapshots.Clear();

            await new BatchOperationService(new TestArchiveEngine(), new TestSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = firstVolume, FileSize = new FileInfo(firstVolume).Length }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "extracted"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MoveSourceAfter = true
                },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            final = snapshots.Last();
            AssertEqual(1, final.SuccessCount);
            AssertEqual(0, final.FailCount);
            AssertEqual(1, final.PostProcessFailCount);
            // GPT-5, 2026-09-30：解压侧同样要求原因可见，且包含冲突分卷的具体路径。
            Assert(final.Message.Contains("archive.7z.002"), "解压目标冲突原因必须进入失败记录消息");
            Assert(File.Exists(firstVolume) && File.Exists(secondVolume), "任一分卷目标冲突时必须保留整组源卷");
            Assert(!File.Exists(Path.Combine(extractedProcessed, "archive.7z.001")), "分卷冲突时不得移动部分分卷");
            AssertEqual("existing target", File.ReadAllText(conflictingSecondVolume));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // 真并发（峰值==并发度）、全部计数无丢失、串行路径不受并发选项影响。
    private static async Task TestConcurrentBatchExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-concurrent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 压缩：5 个来源、并发 3——引擎在途峰值必须恰为 3，全部成功、计数无丢失。
            var engine = new ConcurrentTrackingEngine();
            var sources = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                var dir = Path.Combine(root, $"src{i}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
                sources.Add(dir);
            }

            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchCompressAsync(
                sources,
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "out"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MaxConcurrency = 3
                },
                new SnapshotProgress(snapshots), CancellationToken.None);

            AssertEqual(5, engine.CompressionInputs.Count);
            AssertEqual(3, engine.PeakInFlight);
            AssertEqual(5, snapshots.Last().SuccessCount);
            AssertEqual(0, snapshots.Last().FailCount);

            // 解压：5 个归档、并发 3——同样断言峰值与计数。
            engine = new ConcurrentTrackingEngine();
            var archives = new List<FileEntry>();
            for (var i = 0; i < 5; i++)
            {
                var archive = Path.Combine(root, $"arc{i}.7z");
                File.WriteAllText(archive, "x");
                archives.Add(new FileEntry { FilePath = archive, FileSize = 1 });
            }

            snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchDecompressAsync(
                archives,
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "ex"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MaxConcurrency = 3
                },
                new SnapshotProgress(snapshots), CancellationToken.None);

            AssertEqual(5, engine.ExtractionInputs.Count);
            AssertEqual(3, engine.PeakInFlight);
            AssertEqual(5, snapshots.Last().SuccessCount);

            // 串行路径回归：并发度 1 时峰值必须为 1（不进入并发分支）。
            engine = new ConcurrentTrackingEngine();
            snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new RecordingSystemIntegration()).BatchCompressAsync(
                sources,
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "out"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MaxConcurrency = 1
                },
                new SnapshotProgress(snapshots), CancellationToken.None);
            AssertEqual(1, engine.PeakInFlight);
            AssertEqual(5, snapshots.Last().SuccessCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-04：批次 5——并发下的取消传播与大小上限：
    // 外部取消即时中止全部并发任务且绝不关机；达限只停排队任务、在途自然完成、
    // SizeLimitReached 恰好一次、非取消结束关机恰好一次。
    private static async Task TestConcurrentCancellationAndLimit()
    {
        var L = LocalizationService.Instance.Strings;
        var root = Path.Combine(Path.GetTempPath(), $"bc-concurrent-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 1) 并发执行中外部取消：所有已启动任务抛取消异常聚合传播、不触发关机。
            var cancelEngine = new ScriptedArchiveEngine { CancelAfterMilliseconds = 400 };
            var cancelSources = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                var dir = Path.Combine(root, $"cancel{i}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
                cancelSources.Add(dir);
            }

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);
            var cancelSystem = new RecordingSystemIntegration();
            await AssertThrowsAsync<OperationCanceledException>(
                new BatchOperationService(cancelEngine, cancelSystem).BatchCompressAsync(
                    cancelSources,
                    new BatchOperationOptions
                    {
                        OutputPath = Path.Combine(root, "cancel-out"),
                        Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite,
                        MaxConcurrency = 3,
                        ShutdownAfterComplete = true
                    },
                    new SnapshotProgress([]), cts.Token));
            AssertEqual(3, cancelEngine.CompressionInputs.Count);
            AssertEqual(0, cancelSystem.ShutdownCalls);

            // 2) 大小上限：4 个来源、并发 2、每项真实写出 1MB、上限 0.0005GB——
            //    首个完成项即达限；两个初始槽位任务自然完成，剩余排队任务全部跳过。
            var limitEngine = new ConcurrentTrackingEngine { DelayMilliseconds = 60, WriteRealOutput = true };
            var limitSources = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var dir = Path.Combine(root, $"limit{i}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
                limitSources.Add(dir);
            }

            var snapshots = new List<OperationProgressInfo>();
            var limitSystem = new RecordingSystemIntegration();
            await new BatchOperationService(limitEngine, limitSystem).BatchCompressAsync(
                limitSources,
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "limit-out"),
                    Extension = "7z",
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    MaxConcurrency = 2,
                    MaxSizeGB = 0.0005,
                    ShutdownAfterComplete = true
                },
                new CloningProgress(snapshots), CancellationToken.None);

            AssertEqual(2, limitEngine.CompressionInputs.Count);
            AssertEqual(2, snapshots.Last().SuccessCount);
            AssertEqual(0, snapshots.Last().FailCount);
            Assert(snapshots.Count(info => info.Message == L.SizeLimitReached) == 1,
                "SizeLimitReached 必须恰好上报一次");
            AssertEqual(1, limitSystem.ShutdownCalls);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // GLM-5.3, 2026-11-04：批次 5——并发测试用引擎：互斥记录在途数与峰值，

    // Antigravity, 2026-11-07：批次 13——压缩失败时回滚本次新增的在途未完成文件与分卷残片。
    private static async Task TestFailedCompressionRollback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-comp-rollback-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.txt");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(source, "source data");
        var keepFile = Path.Combine(outputDir, "keep.txt");
        File.WriteAllText(keepFile, "keep me");

        try
        {
            var engine = new PartialFailureArchiveEngine();
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions { OutputPath = outputDir, Extension = "7z" },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            AssertEqual(1, snapshots.Last().FailCount);
            Assert(File.Exists(keepFile), "压缩失败不得删除输出目录中原有无关文件");

            var targetArchive = Path.Combine(outputDir, "source.txt.7z");
            Assert(!File.Exists(targetArchive), "压缩失败必须清理本次生成的不完整归档文件");
            Assert(!File.Exists(targetArchive + ".001"), "压缩失败必须清理本次生成的分卷文件 001");
            Assert(!File.Exists(targetArchive + ".002"), "压缩失败必须清理本次生成的分卷文件 002");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // Antigravity, 2026-11-07：批次 13——压缩中途取消时可靠清理在途未完成文件与分卷残片。
    private static async Task TestCancelledCompressionRollback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-comp-cancel-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.txt");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(source, "source data");
        var keepFile = Path.Combine(outputDir, "keep.txt");
        File.WriteAllText(keepFile, "keep me");

        try
        {
            var engine = new CancellingFailureArchiveEngine();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            await AssertThrowsAsync<OperationCanceledException>(
                new BatchOperationService(engine, new TestSystemIntegration()).BatchCompressAsync(
                    [source],
                    new BatchOperationOptions { OutputPath = outputDir, Extension = "7z" },
                    new SnapshotProgress([]),
                    cts.Token));

            Assert(File.Exists(keepFile), "压缩取消不得删除输出目录中原有无关文件");

            var targetArchive = Path.Combine(outputDir, "source.txt.7z");
            Assert(!File.Exists(targetArchive), "压缩取消必须清理在途未完成归档文件");
            Assert(!File.Exists(targetArchive + ".001"), "压缩取消必须清理在途分卷文件");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // Antigravity, 2026-11-07：批次 13——解压中途取消时可靠清理在途部分解压文件与新建目录。
    private static async Task TestCancelledExtractionRollback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-ext-cancel-{Guid.NewGuid():N}");
        var archive = Path.Combine(root, "archive.7z");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(archive, "placeholder");
        var keepFile = Path.Combine(outputDir, "keep.txt");
        File.WriteAllText(keepFile, "keep me");

        try
        {
            var engine = new CancellingFailureArchiveEngine();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            await AssertThrowsAsync<OperationCanceledException>(
                new BatchOperationService(engine, new TestSystemIntegration()).BatchDecompressAsync(
                    [new FileEntry { FilePath = archive, FileSize = 100 }],
                    new BatchOperationOptions { OutputPath = outputDir, Extension = "7z" },
                    new SnapshotProgress([]),
                    cts.Token));

            Assert(File.Exists(keepFile), "解压取消不得删除原有文件");
            Assert(!File.Exists(Path.Combine(outputDir, "partial-cancel.bin")), "解压取消必须清理未完成解压文件");
            Assert(!Directory.Exists(Path.Combine(outputDir, "partial-cancel")), "解压取消必须清理新建目录");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // Antigravity, 2026-11-08：批次 17（P3-11）——模拟输出行进度回调的测试引擎。
    private sealed class StreamingMockArchiveEngine : IArchiveEngine
    {
        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            // 触发 35% 与 80% 进度流
            options.OutputLineHandler?.Invoke("  35% 1 + test.txt");
            options.OutputLineHandler?.Invoke("  80% 1 + test.txt");
            return Task.FromResult(new ArchiveResult
            {
                Success = true,
                ExitCode = 0,
                StandardOutput = "Everything is Ok\n"
            });
        }

        public Task<ArchiveResult> ExtractAsync(string archivePath, string outputDir, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            options.OutputLineHandler?.Invoke("  50%");
            return Task.FromResult(new ArchiveResult
            {
                Success = true,
                ExitCode = 0,
                StandardOutput = "Everything is Ok\n"
            });
        }
    }

    // Antigravity, 2026-11-08：批次 17（P3-11）——验证管线在归档引擎调用期间实时解析并上报子进度。
    private static async Task TestPipelineSubProgressStreaming()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-subprog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var src = Path.Combine(root, "input.txt");
        File.WriteAllText(src, "test content");

        try
        {
            var progressSnapshots = new List<OperationProgressInfo>();
            var engine = new StreamingMockArchiveEngine();
            var service = new BatchOperationService(engine, new TestSystemIntegration());
            var progress = new CloningProgress(progressSnapshots);

            await service.BatchCompressAsync(
                [src],
                new BatchOperationOptions { Extension = "7z" },
                progress,
                CancellationToken.None);

            Assert(progressSnapshots.Any(p => p.SubProgressPercent == 35), "管线必须捕获并上报引擎 35% 子进度");
            Assert(progressSnapshots.Any(p => p.SubProgressPercent == 80), "管线必须捕获并上报引擎 80% 子进度");
            Assert(progressSnapshots.Last().SubProgressPercent == null, "条目完成后子进度必须重置为 null");
            AssertEqual(1, progressSnapshots.Last().SuccessCount);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
