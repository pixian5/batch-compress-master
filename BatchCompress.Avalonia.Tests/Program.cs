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
            ("跨平台系统元数据过滤", TestSystemMetadataFiltering)
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
        var result = await runner.RunAsync(
            ["-c", "printf '%s' \"$1\"; printf '%s' stderr-marker >&2; exit 7", "sh", "path with space"],
            CancellationToken.None);

        AssertEqual(7, result.ExitCode);
        AssertEqual("path with space", result.StandardOutput);
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
}
