using System.Collections.Generic;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Core.Services;

// GPT-5, 2026-08-05：用于命令构建、进程取消和路径回退的轻量可执行回归测试。
// 不依赖 UI，因此可在 macOS、Windows 和 Linux 一致运行。
internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("格式参数", TestFormatArguments),
            ("密码与失败返回码", TestPasswordAndFailureExitCodes),
            ("密码命名与 RAR 仅存储", TestPasswordNamingAndStoreOnlyExtensions),
            ("取消传播", TestCancellation),
            ("异步输出与参数边界", TestProcessOutputAndArgumentBoundaries),
            ("空保存路径回退", TestOutputPathFallback),
            ("恢复记录与旧密码", TestRecoveryRecordAndLegacyPasswords),
            ("跨平台系统元数据过滤", TestSystemMetadataFiltering),
            ("7z 压缩与解压参数", TestSevenZipArguments),
            ("7z 返回码与格式路由", TestSevenZipExitCodesAndRouting),
            ("附件根目录与空目录", TestAttachmentRootInputs),
            ("跳过已有归档统计", TestExistingSkipProgress),
            ("后处理冲突保留源和目标", TestPostProcessConflict),
            ("官方 7zz 真实压缩解压", TestOfficialSevenZipSmoke),
            ("完整命令行解析", TestCommandLineParsing),
            ("命令行错误校验", TestCommandLineValidation),
            ("TXT 清单与密码本诊断", TestTextFileImportModes)
        };

        // GPT-5, 2026-08-05：首个失败即停止，为自动化保留明确的非零退出状态。
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
                return 1;
            }
        }

        return 0;
    }

    // GPT-5, 2026-08-06：验证压缩 TXT 每行一个路径，确保密码文本不会进入压缩任务。
    private static Task TestTextFileImportModes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-text-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.txt");
            var archive = Path.Combine(root, "matched.rar");
            var unmatched = Path.Combine(root, "not-in-book.rar");
            File.WriteAllText(source, "source");
            File.WriteAllText(archive, "archive");
            File.WriteAllText(unmatched, "archive");

            var compressionList = Path.Combine(root, "compress.txt");
            File.WriteAllLines(compressionList, new[] { source, "this is not a password" });
            var service = new BatchOperationService(new TestArchiveEngine(), new TestSystemIntegration());
            var compressionResult = service.LoadCompressionPathsFromTextFile(compressionList);
            AssertEqual(2, compressionResult.RequestedCount);
            AssertEqual(1, compressionResult.Paths.Count);
            Assert(compressionResult.MissingEntries.Any(path => path.EndsWith("this is not a password", StringComparison.Ordinal)),
                "压缩清单中的无效路径必须进入诊断，不得成为密码行");

            var passwordBook = Path.Combine(root, "passwords.txt");
            File.WriteAllLines(passwordBook, new[] { "matched", "secret" });
            var passwordResult = service.LoadFilesFromTextFileWithDiagnostics(passwordBook, root, "rar");
            AssertEqual(1, passwordResult.Entries.Count);
            AssertEqual("secret", passwordResult.Entries[0].Password);
            Assert(passwordResult.UnmatchedArchives.Contains(unmatched), "密码本诊断必须列出未匹配归档");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
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

    private static Task TestFormatArguments()
    {
        var options = CreateOptions();
        options.ArchiveFormat = "zip";
        options.SolidArchive = true;

        options.ArchiveFormat = "rar";
        var rarArguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/source", "/tmp/archive.rar", options);
        AssertContains(rarArguments, "-s");
        AssertNotContains(rarArguments, "-k");
        options.LockArchive = true;
        AssertContains(WinRarCommandBuilder.BuildCompressionArguments("/tmp/source", "/tmp/archive.rar", options), "-k");
        AssertThrows<NotSupportedException>(() => WinRarCommandBuilder.NormalizeArchiveFormat("zip"));
        return Task.CompletedTask;
    }

    private static Task TestPasswordAndFailureExitCodes()
    {
        const string password = "secret password";
        var options = CreateOptions();
        options.Password = password;
        var arguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/input", "/tmp/output.rar", options);

        AssertContains(arguments, "-psecret password");
        Assert(WinRarExitCodes.IsSuccess(0), "返回码 0 必须表示成功");
        Assert(WinRarExitCodes.IsSuccess(1), "返回码 1 必须表示警告成功");
        Assert(!WinRarExitCodes.IsSuccess(2), "返回码 2 必须表示失败");
        Assert(!WinRarExitCodes.IsSuccess(255), "返回码 255 必须表示失败");
        return Task.CompletedTask;
    }

    private static Task TestPasswordNamingAndStoreOnlyExtensions()
    {
        AssertEqual("a.rar", PasswordUtility.GetPasswordSourceName("a.part001.rar", PasswordNameMode.ArchiveName));
        AssertEqual("a", PasswordUtility.GetPasswordSourceName("a.part001.rar", PasswordNameMode.BaseName));
        AssertEqual("a.7z", PasswordUtility.GetPasswordSourceName("a.7z.001", PasswordNameMode.ArchiveName));
        AssertEqual("a", PasswordUtility.GetPasswordSourceName("a.7z.001", PasswordNameMode.BaseName));
        AssertEqual("a.rar", PasswordUtility.GetPasswordSourceName("a.rar", PasswordNameMode.ArchiveName));
        AssertEqual("a", PasswordUtility.GetPasswordSourceName("a.rar", PasswordNameMode.BaseName));
        Assert(!ArchiveDefaults.StoreOnlyExtensions.Intersect(ArchiveDefaults.CompressionFriendlyExtensions).Any(),
            "仅存储与适合压缩清单不得重叠");
        Assert(!ArchiveDefaults.StoreOnlyExtensions.Intersect(ArchiveDefaults.ContentDependentExtensions).Any(),
            "仅存储与内容相关清单不得重叠");
        Assert(!ArchiveDefaults.CompressionFriendlyExtensions.Intersect(ArchiveDefaults.ContentDependentExtensions).Any(),
            "适合压缩与内容相关清单不得重叠");
        AssertContains(ArchiveDefaults.CompressionFriendlyExtensions, "tar");
        AssertContains(ArchiveDefaults.ContentDependentExtensions, "pdf");

        var rarOptions = CreateOptions();
        rarOptions.RarStoreOnlyExtensions = ArchiveDefaults.StoreOnlyExtensions;
        var rarArguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/input", "/tmp/output.rar", rarOptions);
        AssertContains(rarArguments, "-ms7z;ace;arj;bz2;cab;gz;lha;lz;lzh;rar;taz;tgz;xz;z;zip;zipx;aac;avi;flac;flv;m4a;mkv;mov;mp3;mp4;ogg;opus;rm;rmvb;webm;avif;gif;heic;jpeg;jpg;png;webp;docx;odg;odp;ods;odt;pptx;xlsx");
        Assert(!rarArguments.Any(argument => argument is "-t7z" or "-tzip" || argument.StartsWith("-mx=", StringComparison.Ordinal)),
            "RAR 参数不得包含 7zz 的格式或压缩级别语法");

        var sevenZipOptions = CreateOptions();
        sevenZipOptions.ArchiveFormat = "7z";
        sevenZipOptions.RarStoreOnlyExtensions = ArchiveDefaults.StoreOnlyExtensions;
        var sevenZipArguments = SevenZipCommandBuilder.BuildCompressionArguments("/tmp/input", "/tmp/output.7z", sevenZipOptions);
        Assert(!sevenZipArguments.Any(argument => argument.StartsWith("-xr!", StringComparison.Ordinal)),
            "7z/ZIP 不得把 RAR 的仅存储规则转换为排除规则");
        Assert(!sevenZipArguments.Any(argument =>
                argument.StartsWith("-rr", StringComparison.Ordinal) ||
                argument == "-k" ||
                argument == "-qo+" ||
                argument.StartsWith("-z", StringComparison.Ordinal) ||
                argument.StartsWith("-ms7z;", StringComparison.Ordinal)),
            "7zz 参数不得包含 WinRAR 专属参数");
        AssertThrows<NotSupportedException>(() => SevenZipCommandBuilder.NormalizeArchiveFormat("rar"));
        return Task.CompletedTask;
    }

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
    }

    private static Task TestOutputPathFallback()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"batch-compress-tests-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(testRoot, "source");
        var configuredDirectory = Path.Combine(testRoot, "configured");
        Directory.CreateDirectory(sourceDirectory);

        try
        {
            var sourceFile = Path.Combine(sourceDirectory, "input.txt");
            File.WriteAllText(sourceFile, "test");

            AssertEqual(sourceDirectory, OutputPathResolver.ResolveAndCreate(string.Empty, sourceFile));
            AssertEqual(sourceDirectory, OutputPathResolver.ResolveAndCreate("   ", sourceFile));
            AssertEqual(configuredDirectory, OutputPathResolver.ResolveAndCreate(configuredDirectory, sourceFile));
            Assert(Directory.Exists(configuredDirectory), "配置的输出目录必须自动创建");

            var relativeSource = Path.GetRelativePath(Directory.GetCurrentDirectory(), sourceFile);
            AssertEqual(sourceDirectory, OutputPathResolver.ResolveAndCreate(null, relativeSource));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static Task TestRecoveryRecordAndLegacyPasswords()
    {
        var options = CreateOptions();
        options.RecoveryRecordPercent = 5;
        var arguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/input", "/tmp/output.rar", options);
        AssertContains(arguments, "-rr5");

        options.RecoveryRecordPercent = 101;
        var invalidArguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/input", "/tmp/output.rar", options);
        AssertNotContains(invalidArguments, "-rr101");

        var legacy = PasswordUtility.GetLegacyPasswordCandidates("archive.rar");
        AssertEqual(5, legacy.Count);
        Assert(legacy.All(password => !string.IsNullOrWhiteSpace(password)), "旧密码候选不能包含空值");
        return Task.CompletedTask;
    }

    private static Task TestSystemMetadataFiltering()
    {
        var skippedPaths = new[]
        {
            "/tmp/desktop.ini",
            "/tmp/Thumbs.db",
            "/tmp/.DS_Store",
            "/tmp/._document.pdf",
            "/tmp/.AppleDouble/data",
            "/tmp/.Spotlight-V100/index",
            "/tmp/.Trash-1000/file.rar",
            "/tmp/.directory",
            "/tmp/.gvfs/mount",
            "/tmp/lost+found/block",
            "/tmp/~$document.docx"
        };

        foreach (var path in skippedPaths)
        {
            Assert(SystemMetadataFileFilter.ShouldSkip(path), $"系统元数据必须跳过: {path}");
        }

        Assert(!SystemMetadataFileFilter.ShouldSkip("/tmp/source/report.rar"), "普通归档文件不能被跳过");
        Assert(!SystemMetadataFileFilter.ShouldSkip("/tmp/source/desktop-notes.txt"), "名称相似的普通文件不能被跳过");
        return Task.CompletedTask;
    }

    private static Task TestSevenZipArguments()
    {
        var options = CreateOptions();
        options.ArchiveFormat = "7z";
        options.Password = "secret password !@#";
        options.CompressionLevel = CompressionLevel.Best;
        options.SolidArchive = true;
        options.VolumeSize = "20m";
        options.TempDirectory = Path.Combine(Path.GetTempPath(), "7z temp");

        var compressionArguments = SevenZipCommandBuilder.BuildCompressionArguments(
            "/tmp/source with space",
            "/tmp/archive with space.7z",
            options);
        AssertContains(compressionArguments, "-t7z");
        AssertContains(compressionArguments, "-mx=9");
        AssertContains(compressionArguments, "-ms=on");
        AssertContains(compressionArguments, "-psecret password !@#");
        AssertContains(compressionArguments, "-mhe=on");
        AssertContains(compressionArguments, "-v20m");
        AssertContains(compressionArguments, $"-w{options.TempDirectory}");
        AssertEqual(1, compressionArguments.Count(value => value == "-psecret password !@#"));
        AssertEqual("/tmp/archive with space.7z", compressionArguments[^2]);
        AssertEqual("/tmp/source with space", compressionArguments[^1]);

        options.ExistingFileMode = ExistingFileMode.Skip;
        var skipArguments = SevenZipCommandBuilder.BuildExtractionArguments(
            "/tmp/archive with space.7z",
            "/tmp/output with space",
            options);
        AssertContains(skipArguments, "-aos");
        AssertContains(skipArguments, "-o/tmp/output with space");
        AssertEqual("/tmp/archive with space.7z", skipArguments[^1]);

        options.ExistingFileMode = ExistingFileMode.Update;
        AssertContains(SevenZipCommandBuilder.BuildExtractionArguments("a.7z", "out", options), "-aou");
        options.ExistingFileMode = ExistingFileMode.Overwrite;
        AssertContains(SevenZipCommandBuilder.BuildExtractionArguments("a.7z", "out", options), "-aoa");
        options.ArchiveFormat = "zip";
        options.SolidArchive = true;
        var zipArguments = SevenZipCommandBuilder.BuildCompressionArguments("/tmp/source", "/tmp/archive.zip", options);
        AssertContains(zipArguments, "-tzip");
        AssertNotContains(zipArguments, "-ms=on");
        AssertContains(zipArguments, "-mem=AES256");
        return Task.CompletedTask;
    }

    private static async Task TestSevenZipExitCodesAndRouting()
    {
        Assert(SevenZipExitCodes.IsSuccess(0), "7-Zip 返回码 0 必须表示成功");
        Assert(SevenZipExitCodes.IsSuccess(1), "7-Zip 返回码 1 必须表示非致命警告成功");
        foreach (var exitCode in new[] { 2, 7, 8, 255 })
        {
            Assert(!SevenZipExitCodes.IsSuccess(exitCode), $"7-Zip 返回码 {exitCode} 必须表示失败");
        }

        var rar = new RecordingArchiveEngine();
        var sevenZip = new RecordingArchiveEngine();
        var router = new ArchiveEngineRouter(rar, sevenZip);

        await router.CompressAsync("source", "archive.rar", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.CompressAsync("source", "archive.zip", new ArchiveOptions { ArchiveFormat = "zip" });
        await router.CompressAsync("source", "archive.7z", new ArchiveOptions { ArchiveFormat = ".7Z" });
        await router.ExtractAsync("archive.7z.001", "output", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.ExtractAsync("archive.rar", "output", new ArchiveOptions { ArchiveFormat = "rar" });

        AssertEqual(1, rar.CompressionCalls);
        AssertEqual(1, rar.ExtractionCalls);
        AssertEqual(2, sevenZip.CompressionCalls);
        AssertEqual(1, sevenZip.ExtractionCalls);
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
            Assert(File.Exists(firstVolume) && File.Exists(secondVolume), "任一分卷目标冲突时必须保留整组源卷");
            Assert(!File.Exists(Path.Combine(extractedProcessed, "archive.7z.001")), "分卷冲突时不得移动部分分卷");
            AssertEqual("existing target", File.ReadAllText(conflictingSecondVolume));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task TestOfficialSevenZipSmoke()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Console.WriteLine("SKIP 官方 7zz 真实压缩解压: 当前不是 macOS");
            return;
        }

        var executable = Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "macos", "7zz");
        Assert(File.Exists(executable), $"测试输出中缺少项目内官方 7zz: {executable}");

        var testRoot = Path.Combine(Path.GetTempPath(), $"batch-compress-7z-{Guid.NewGuid():N}");
        var source = Path.Combine(testRoot, "source folder");
        var attachment = Path.Combine(testRoot, "外部附件");
        var emptyAttachment = Path.Combine(testRoot, "空附件");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(attachment);
        Directory.CreateDirectory(emptyAttachment);
        File.WriteAllText(Path.Combine(source, "content.txt"), "official 7zz smoke test");
        File.WriteAllText(Path.Combine(attachment, "attachment.txt"), "attachment content");

        try
        {
            var engine = new SevenZipArchiveEngine(executable);
            Assert(engine.IsAvailable(), "项目内官方 7zz 必须通过身份校验");
            foreach (var format in new[] { "7z", "zip" })
            {
                var archive = Path.Combine(testRoot, $"archive with password.{format}");
                var output = Path.Combine(testRoot, $"extracted-{format}");
                var options = new ArchiveOptions
                {
                    ArchiveFormat = format,
                    Password = "test password with space",
                    CompressionLevel = CompressionLevel.Normal,
                    SolidArchive = true,
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    TestArchive = true,
                    AdditionalInputs = [attachment, emptyAttachment]
                };

                var compressed = await engine.CompressAsync(source, archive, options);
                Assert(compressed.Success, $"官方 7zz {format} 压缩失败: {compressed.ErrorMessage}");
                Assert(File.Exists(archive), $"官方 7zz 未生成 {format} 归档文件");
                var extracted = await engine.ExtractAsync(archive, output, options);
                Assert(extracted.Success, $"官方 7zz {format} 解压失败: {extracted.ErrorMessage}");
                var extractedFile = Directory.GetFiles(output, "content.txt", SearchOption.AllDirectories).SingleOrDefault();
                Assert(extractedFile != null, $"{format} 解压结果中缺少 content.txt");
                AssertEqual("official 7zz smoke test", File.ReadAllText(extractedFile!));
                AssertEqual("attachment content", File.ReadAllText(Path.Combine(output, "外部附件", "attachment.txt")));
                Assert(Directory.Exists(Path.Combine(output, "空附件")), $"{format} 归档根目录必须包含空附件目录");
            }
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static Task TestCommandLineParsing()
    {
        var outcome = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
        [
            "compress",
            "--input", "/tmp/one file",
            "--input", "/tmp/two",
            "--output", "/tmp/output",
            "--format", ".7Z",
            "--password", "visible password",
            "--no-solid",
            "--no-skip-processed",
            "--no-add-enclosures",
            "--max-size-gb", "0",
            "--volume-size", "20",
            "--volume-unit", "MB",
            "--password-name", "base",
            "--dry-run"
        ]);

        Assert(outcome.Success, string.Join(" | ", outcome.Errors));
        var options = outcome.Options;
        Assert(options.Compress && !options.Decompress && !options.Gui, "compress 动词必须进入无界面压缩模式");
        AssertEqual(2, options.InputPaths.Length);
        AssertEqual("7z", options.Extension);
        AssertEqual("m", options.VolumeUnit);
        AssertEqual("base", options.PasswordName);
        Assert(!options.UseRandomPassword, "显式密码必须关闭随机密码");
        Assert(!options.Solid, "--no-solid 必须关闭固实压缩");
        Assert(!options.SkipProcessed, "--no-skip-processed 必须生效");
        Assert(!options.AddEnclosures, "--no-add-enclosures 必须生效");
        Assert(options.DryRun, "--dry-run 必须生效");

        var legacy = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["--decompress", "-s", "/tmp", "-o", "/tmp/out", "-e", "rar"]);
        Assert(legacy.Success && legacy.Options.Decompress, "旧 --decompress 开关必须保持兼容");

        // GPT-5, 2026-08-06：内部解析器必须保留旧库的多值参数和等号赋值语义。
        var compact = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["compress", "--input", "/tmp/a", "/tmp/b", "--output=/tmp/out", "--format=zip", "--dry-run"]);
        Assert(compact.Success, string.Join(" | ", compact.Errors));
        AssertEqual(2, compact.Options.InputPaths.Length);
        AssertEqual("/tmp/out", compact.Options.OutputPath);
        AssertEqual("zip", compact.Options.Extension);

        Assert(BatchCompress.Avalonia.CommandLineHandler.IsHelpRequested(["-h"]), "-h 必须识别为帮助请求");
        Assert(BatchCompress.Avalonia.CommandLineHandler.IsVersionRequested(["--version"]), "--version 必须识别为版本请求");

        var gui = BatchCompress.Avalonia.CommandLineHandler.ParseArguments([]);
        Assert(gui.Success && gui.Options.Gui, "无参数必须继续启动 GUI");
        return Task.CompletedTask;
    }

    private static Task TestCommandLineValidation()
    {
        AssertCommandLineFails(
            ["--compress", "--decompress", "-i", "/tmp/a", "-o", "/tmp/out"],
            "不能同时指定");
        AssertCommandLineFails(["compress", "-i", "/tmp/a"], "--output");
        AssertCommandLineFails(["compress", "-i", "/tmp/a", "-o", "/tmp/out", "-e", "tar"], "仅支持 rar");
        AssertCommandLineFails(["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--level", "6"], "0 到 5");
        AssertCommandLineFails(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--password", "a", "--password-stdin"],
            "只能选择一种");
        AssertCommandLineFails(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--delete-source", "--move-source"],
            "不能同时使用");
        AssertCommandLineFails(["--source", "/tmp"], "请指定 compress");
        AssertCommandLineFails(["compress", "--input", "/tmp/a", "--output"], "缺少参数值");
        AssertCommandLineFails(["compress", "--input", "/tmp/a", "--output", "/tmp/out", "--unknown"], "未知参数");
        AssertCommandLineFails(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--existing", "update", "--lock"],
            "不能与 --lock 同时使用");
        AssertCommandLineFails(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--password-name", "unknown"],
            "--password-name");
        return Task.CompletedTask;
    }

    private static ArchiveOptions CreateOptions() => new()
    {
        ArchiveFormat = "rar",
        ExistingFileMode = ExistingFileMode.Overwrite,
        CompressionLevel = CompressionLevel.Normal
    };

    private sealed class SnapshotProgress(List<OperationProgressInfo> snapshots) : IProgress<OperationProgressInfo>
    {
        public void Report(OperationProgressInfo value) => snapshots.Add(value);
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

        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            CompressionCalls++;
            LastOptions = options;
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
}
