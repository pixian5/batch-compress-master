using System.Collections.Generic;
using BatchCompress.Avalonia.Core.Interfaces;
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
            ("取消传播", TestCancellation),
            ("异步输出与参数边界", TestProcessOutputAndArgumentBoundaries),
            ("空保存路径回退", TestOutputPathFallback),
            ("恢复记录与旧密码", TestRecoveryRecordAndLegacyPasswords),
            ("跨平台系统元数据过滤", TestSystemMetadataFiltering),
            ("7z 压缩与解压参数", TestSevenZipArguments),
            ("7z 返回码与格式路由", TestSevenZipExitCodesAndRouting),
            ("官方 7zz 真实压缩解压", TestOfficialSevenZipSmoke),
            ("完整命令行解析", TestCommandLineParsing),
            ("命令行错误校验", TestCommandLineValidation)
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

    private static Task TestFormatArguments()
    {
        var options = CreateOptions();
        options.ArchiveFormat = "zip";
        options.SolidArchive = true;

        var zipArguments = WinRarCommandBuilder.BuildCompressionArguments(
            "/tmp/source with space",
            "/tmp/archive with space.zip",
            options);

        AssertContains(zipArguments, "-afzip");
        AssertNotContains(zipArguments, "-s");
        AssertEqual("/tmp/archive with space.zip", zipArguments[^2]);
        AssertEqual("/tmp/source with space", zipArguments[^1]);

        options.ArchiveFormat = "rar";
        var rarArguments = WinRarCommandBuilder.BuildCompressionArguments("/tmp/source", "/tmp/archive.rar", options);
        AssertContains(rarArguments, "-s");
        AssertNotContains(rarArguments, "-afzip");
        AssertThrows<NotSupportedException>(() => WinRarCommandBuilder.NormalizeArchiveFormat("7z"));
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
        AssertThrows<NotSupportedException>(() => SevenZipCommandBuilder.NormalizeArchiveFormat("zip"));
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

        AssertEqual(2, rar.CompressionCalls);
        AssertEqual(1, rar.ExtractionCalls);
        AssertEqual(1, sevenZip.CompressionCalls);
        AssertEqual(1, sevenZip.ExtractionCalls);
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
        var archive = Path.Combine(testRoot, "archive with password.7z");
        var output = Path.Combine(testRoot, "extracted");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "content.txt"), "official 7zz smoke test");

        try
        {
            var engine = new SevenZipArchiveEngine(executable);
            Assert(engine.IsAvailable(), "项目内官方 7zz 必须通过身份校验");
            var options = new ArchiveOptions
            {
                ArchiveFormat = "7z",
                Password = "test password with space",
                CompressionLevel = CompressionLevel.Normal,
                SolidArchive = true,
                ExistingFileMode = ExistingFileMode.Overwrite,
                TestArchive = true
            };

            var compressed = await engine.CompressAsync(source, archive, options);
            Assert(compressed.Success, $"官方 7zz 压缩失败: {compressed.ErrorMessage}");
            Assert(File.Exists(archive), "官方 7zz 未生成归档文件");

            var extracted = await engine.ExtractAsync(archive, output, options);
            Assert(extracted.Success, $"官方 7zz 解压失败: {extracted.ErrorMessage}");
            var extractedFile = Directory.GetFiles(output, "content.txt", SearchOption.AllDirectories).SingleOrDefault();
            Assert(extractedFile != null, "解压结果中缺少 content.txt");
            AssertEqual("official 7zz smoke test", File.ReadAllText(extractedFile!));
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
            "--dry-run"
        ]);

        Assert(outcome.Success, string.Join(" | ", outcome.Errors));
        var options = outcome.Options;
        Assert(options.Compress && !options.Decompress && !options.Gui, "compress 动词必须进入无界面压缩模式");
        AssertEqual(2, options.InputPaths.Length);
        AssertEqual("7z", options.Extension);
        AssertEqual("m", options.VolumeUnit);
        Assert(!options.UseRandomPassword, "显式密码必须关闭随机密码");
        Assert(!options.Solid, "--no-solid 必须关闭固实压缩");
        Assert(!options.SkipProcessed, "--no-skip-processed 必须生效");
        Assert(!options.AddEnclosures, "--no-add-enclosures 必须生效");
        Assert(options.DryRun, "--dry-run 必须生效");

        var legacy = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["--decompress", "-s", "/tmp", "-o", "/tmp/out", "-e", "rar"]);
        Assert(legacy.Success && legacy.Options.Decompress, "旧 --decompress 开关必须保持兼容");

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
        return Task.CompletedTask;
    }

    private static ArchiveOptions CreateOptions() => new()
    {
        ArchiveFormat = "rar",
        ExistingFileMode = ExistingFileMode.Overwrite,
        CompressionLevel = CompressionLevel.Normal
    };

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

        public bool IsAvailable() => true;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            CompressionCalls++;
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
