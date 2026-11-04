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
internal static class Program
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
            ("附件联系方式配置外置", TestEnclosureConfigService),
            ("并发批量执行", TestConcurrentBatchExecution),
            ("并发取消与大小上限", TestConcurrentCancellationAndLimit),
            ("NDJSON结构化输出", TestNdjsonOutputWriter),
            ("NDJSON端到端", TestNdjsonEndToEnd),
            ("TXT 清单与密码本诊断", TestTextFileImportModes),
            ("本地化窗口标题", TestLocalizedWindowTitles),
            ("英文与德语资源不含中文", TestLocalizedTextHasNoChinese),
            ("服务消息跟随界面语言", TestServiceMessagesFollowLanguage),
            ("界面本地化资源扫描", TestLocalizedUiResources)
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

    private static Task TestLocalizedWindowTitles()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        try
        {
            foreach (var language in LocalizationService.AvailableLanguages.Keys)
            {
                localization.CurrentLanguage = language;
                var title = localization.Strings.WindowTitle;
                Assert(!title.Contains("Avalonia", StringComparison.OrdinalIgnoreCase),
                    $"窗口标题不得显示 UI 框架名称: {title}");
                Assert(title.Contains("v", StringComparison.OrdinalIgnoreCase),
                    $"窗口标题必须显示版本号: {title}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.CompressionTab),
                    $"压缩一级标签不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.DecompressionTab),
                    $"解压一级标签不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.LogsTab),
                    $"日志一级标签不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.StartTab),
                    $"开始一级标签不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.DecompressionOptions),
                    $"解压选项标题不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.DecompressFolderMode),
                    $"解压目录来源标题不得为空: {language}");
                Assert(!string.IsNullOrWhiteSpace(localization.Strings.AfterCompression) &&
                       !string.IsNullOrWhiteSpace(localization.Strings.AfterDecompression),
                    $"压缩与解压后处理标题不得为空: {language}");
            }
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
        }

        return Task.CompletedTask;
    }

    // GPT-5, 2026-09-29：英文与德语资源不得残留中文，避免“中文界面混英文、外语界面混中文”。
    // 逐项反射全部字符串属性，新增词条若漏译会直接失败。
    private static Task TestLocalizedTextHasNoChinese()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        try
        {
            foreach (var language in new[] { "en", "de" })
            {
                localization.CurrentLanguage = language;
                var strings = localization.Strings;
                foreach (var property in typeof(LanguageStrings).GetProperties())
                {
                    if (property.GetValue(strings) is not string value || value.Length == 0)
                    {
                        continue;
                    }

                    Assert(!value.Any(character => character >= '\u4e00' && character <= '\u9fff'),
                        $"{language} 的 {property.Name} 不得包含中文：{value}");
                }
            }
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
        }

        return Task.CompletedTask;
    }

    // GPT-5, 2026-09-29：服务层逐项状态消息必须跟随当前界面语言。
    // 英文模式下用真实调用链验证消息取自英文资源，杜绝“英文界面混中文日志”。
    private static async Task TestServiceMessagesFollowLanguage()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        var missingArchive = Path.Combine(Path.GetTempPath(), $"bc-locale-missing-{Guid.NewGuid():N}.7z");
        try
        {
            localization.CurrentLanguage = "en";
            var snapshots = new List<OperationProgressInfo>();
            await new BatchOperationService(new TestArchiveEngine(), new TestSystemIntegration()).BatchDecompressAsync(
                [new FileEntry { FilePath = missingArchive, FileSize = 0 }],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(Path.GetTempPath(), "bc-locale-out"),
                    Extension = "7z"
                },
                new SnapshotProgress(snapshots),
                CancellationToken.None);

            var messages = snapshots
                .Select(snapshot => snapshot.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
            Assert(messages.Length > 0, "服务层必须报告状态消息");
            Assert(messages.Any(message => message.Contains(localization.Strings.MissingFirstVolume, StringComparison.Ordinal)),
                "英文模式的分卷不完整消息必须取自英文资源: " + string.Join(" | ", messages));
            Assert(!messages.Any(message => message.Any(character => character >= '\u4e00' && character <= '\u9fff')),
                "英文模式下不得出现中文消息: " + string.Join(" | ", messages));
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
        }
    }

    // GPT-5, 2026-09-30：守护界面本地化收口成果：
    // 1) AXAML 中除 XML 注释与资源 URI 外不得残留中文硬编码；
    // 2) 所有 {Binding L.Xxx} 引用的属性必须在 LanguageStrings 上真实存在；
    // 3) 界面代码文件中的用户可见字符串不得含中文（Debug 诊断输出除外）。
    private static Task TestLocalizedUiResources()
    {
        var repoRoot = FindRepositoryRoot();
        var axamlFiles = new List<string>();
        var viewsDirectory = Path.Combine(repoRoot, "Views");
        if (Directory.Exists(viewsDirectory))
        {
            axamlFiles.AddRange(Directory.GetFiles(viewsDirectory, "*.axaml"));
        }

        var appAxaml = Path.Combine(repoRoot, "App.axaml");
        if (File.Exists(appAxaml))
        {
            axamlFiles.Add(appAxaml);
        }

        Assert(axamlFiles.Count > 0, "未找到任何 AXAML 界面文件，扫描测试无法进行");

        var declaredKeys = typeof(LanguageStrings).GetProperties().Select(property => property.Name).ToHashSet();
        foreach (var file in axamlFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);

            // XML 注释允许中文说明，先整体移除再扫描。
            var withoutComments = Regex.Replace(content, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
            // 资源 URI（如 /Assets/压缩.png）是文件名而非界面文案，一并剔除。
            var withoutAssets = Regex.Replace(withoutComments, "\"[^\"]*(?:avares://|/Assets/)[^\"]*\"", string.Empty);
            var leftoverChinese = Regex.Match(withoutAssets, "[\u4e00-\u9fff]+");
            Assert(!leftoverChinese.Success, $"{fileName} 残留硬编码中文：{leftoverChinese.Value}");

            foreach (Match binding in Regex.Matches(content, @"Binding L\.([A-Za-z0-9_]+)"))
            {
                var key = binding.Groups[1].Value;
                Assert(declaredKeys.Contains(key), $"{fileName} 引用了不存在的本地化键: {key}");
            }
        }

        foreach (var relativePath in new[] { Path.Combine("Views", "MainWindow.axaml.cs"), "App.axaml.cs" })
        {
            var path = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var code = StripLineComment(line);
                if (code.Contains("Debug.WriteLine", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match literal in Regex.Matches(code, "\"(?:[^\"\\\\]|\\\\.)*\""))
                {
                    Assert(!Regex.IsMatch(literal.Value, "[\u4e00-\u9fff]"),
                        $"{relativePath}:{lineNumber} 残留硬编码中文字符串：{literal.Value}");
                }
            }
        }

        return Task.CompletedTask;
    }

    // 逐字符剥离行尾注释，避免把字符串字面量内部的 // 误判为注释起点。
    private static string StripLineComment(string line)
    {
        var inString = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"' && (index == 0 || line[index - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && line[index] == '/' && index + 1 < line.Length && line[index + 1] == '/')
            {
                return line.Substring(0, index);
            }
        }

        return line;
    }

    // 测试进程从 bin 目录向上查找主工程文件，以定位仓库根目录。
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BatchCompress.Avalonia.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("未能定位仓库根目录（缺少 BatchCompress.Avalonia.csproj）");
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

    private static Task TestFormatCatalog()
    {
        foreach (var format in new[] { "rar", "7z", "zip", "tar", "gz", "bz2", "xz", "wim" })
        {
            Assert(ArchiveFormatCatalog.CanCreate(format), $"{format} 必须列为可创建格式");
            Assert(ArchiveFormatCatalog.CanExtract(format), $"{format} 必须列为可解压格式");
        }

        AssertEqual("gz", ArchiveFormatCatalog.Normalize("gzip"));
        AssertEqual("bz2", ArchiveFormatCatalog.Normalize("bzip2"));
        foreach (var format in new[]
                 {
                     "a", "obj", "chi", "chq", "chw", "pmd", "qcow2c", "avhdx", "pkg", "xip",
                     "ova", "tzst", "taz", "lzma86", "001", "apk", "swm", "esd", "ppkg"
                 })
        {
            Assert(ArchiveFormatCatalog.CanExtract(format), $"7zz 支持的后缀必须可解压：{format}");
        }

        Assert(ArchiveFormatCatalog.CanExtract("iso"), "iso 必须列为 7zz 可解压格式");
        AssertEqual("ppmd", ArchiveFormatCatalog.Normalize("pmd"));
        AssertEqual("split", ArchiveFormatCatalog.Normalize("001"));
        Assert(!ArchiveFormatCatalog.CanCreate("iso"), "iso 不应伪装为可创建格式");
        Assert(!ArchiveFormatCatalog.CanCreate("rar5"), "未知 RAR 后缀不应列为可创建格式");
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
        foreach (var format in new[] { "tar", "gz", "bz2", "xz", "wim" })
        {
            await router.CompressAsync("source", $"archive.{format}", new ArchiveOptions { ArchiveFormat = format });
        }
        await router.ExtractAsync("archive.7z.001", "output", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.ExtractAsync("archive.rar", "output", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.ExtractAsync("archive.zip", "output", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.ExtractAsync("archive.tar", "output", new ArchiveOptions { ArchiveFormat = "rar" });
        await router.ExtractAsync("archive", "output", new ArchiveOptions { ArchiveFormat = "rar" });

        AssertEqual(1, rar.CompressionCalls);
        AssertEqual(2, rar.ExtractionCalls);
        AssertEqual(7, sevenZip.CompressionCalls);
        AssertEqual(3, sevenZip.ExtractionCalls);
        AssertThrows<NotSupportedException>(() =>
            router.CompressAsync("source", "archive.iso", new ArchiveOptions { ArchiveFormat = "iso" })
                .GetAwaiter().GetResult());
    }

    private static async Task TestCanonicalCompressionFormat()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-alias-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(source, "source");
        var engine = new RecordingArchiveEngine();
        try
        {
            await new BatchOperationService(engine, new TestSystemIntegration()).BatchCompressAsync(
                [source],
                new BatchOperationOptions
                {
                    OutputPath = Path.Combine(root, "out"),
                    Extension = ".GZIP",
                    ExistingFileMode = ExistingFileMode.Overwrite
                },
                new Progress<OperationProgressInfo>(),
                CancellationToken.None);

            AssertEqual("gz", engine.LastOptions?.ArchiveFormat);
            Assert(engine.LastCompressionOutput?.EndsWith("source.txt.gz", StringComparison.Ordinal) == true,
                "GUI 格式别名必须生成规范的 .gz 输出名");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Task TestArchiveVolumeResolver()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-volume-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var name in new[]
                     {
                         "rar.part01.rar", "rar.part02.rar", "rar.part10.rar",
                         "seven.7z.001", "seven.7z.002", "zip.zip.001", "zip.zip.002",
                         "tar.tar.001", "tar.tar.002"
                     })
            {
                File.WriteAllText(Path.Combine(root, name), name);
            }

            var rar = ArchiveVolumeResolver.Resolve(Path.Combine(root, "rar.part02.rar"));
            AssertEqual(ArchiveVolumeKind.RarPart, rar.VolumeKind);
            AssertEqual("rar.part01.rar", Path.GetFileName(rar.FirstVolumePath!));
            AssertEqual(3, rar.Volumes.Count);
            AssertEqual(1L, rar.Volumes[0].Number);
            AssertEqual(2L, rar.Volumes[1].Number);
            AssertEqual(10L, rar.Volumes[2].Number);
            Assert(!rar.IsSequenceContiguous, "part01、part02、part10 必须识别出中间缺卷");

            var seven = ArchiveVolumeResolver.Resolve(Path.Combine(root, "seven.7z"));
            AssertEqual(ArchiveVolumeKind.SevenZipNumeric, seven.VolumeKind);
            AssertEqual("seven.7z.001", Path.GetFileName(seven.FirstVolumePath!));
            Assert(seven.CanExtract, "逻辑 7z 名应解析到完整数字分卷首卷");

            var zip = ArchiveVolumeResolver.Resolve(Path.Combine(root, "zip.zip.002"));
            AssertEqual(ArchiveVolumeKind.ZipNumeric, zip.VolumeKind);
            AssertEqual("zip.zip.001", Path.GetFileName(zip.FirstVolumePath!));
            Assert(zip.CanExtract, "ZIP 数字分卷应支持非首卷重定向");

            var tar = ArchiveVolumeResolver.Resolve(Path.Combine(root, "tar.tar.002"));
            AssertEqual(ArchiveVolumeKind.SevenZipOtherNumeric, tar.VolumeKind);
            AssertEqual("tar", tar.ActualExtension);
            AssertEqual("tar.tar.001", Path.GetFileName(tar.FirstVolumePath!));
            Assert(tar.CanExtract, "7zz 其他格式数字分卷应支持非首卷重定向");

            File.WriteAllText(Path.Combine(root, "duplicate.7z.1"), "1");
            File.WriteAllText(Path.Combine(root, "duplicate.7z.001"), "001");
            var duplicate = ArchiveVolumeResolver.Resolve(Path.Combine(root, "duplicate.7z.001"));
            Assert(duplicate.HasDuplicateNumbers, "相同编号不同宽度必须判为歧义");

            File.WriteAllText(Path.Combine(root, "missing.7z.002"), "2");
            File.WriteAllText(Path.Combine(root, "missing.7z.003"), "3");
            var missing = ArchiveVolumeResolver.Resolve(Path.Combine(root, "missing.7z.003"));
            Assert(!missing.HasRequiredFirstVolume, "缺少 .001 时不能把 .002 当首卷");
            Assert(!missing.CanExtract, "缺首卷的分卷组不得进入引擎");
        }
        finally
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-03：旧式分卷（base.rar+base.r00…、base.zip+base.z01…）识别、
    // 目录扫描折叠、输出清理与大小统计口径。
    private static Task TestOldStyleVolumeResolution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-oldstyle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 场景 1：完整 r00 组——从非主卷 .r00 解析应折叠到主卷 base.rar。
            foreach (var name in new[] { "old.rar", "old.r00", "old.r01" })
            {
                File.WriteAllText(Path.Combine(root, name), name);
            }

            var complete = ArchiveVolumeResolver.Resolve(Path.Combine(root, "old.r00"));
            AssertEqual(ArchiveVolumeKind.RarOldStyle, complete.VolumeKind);
            AssertEqual(ArchiveKind.Rar, complete.ArchiveKind);
            AssertEqual("old.rar", Path.GetFileName(complete.FirstVolumePath!));
            AssertEqual(3, complete.Volumes.Count);
            AssertEqual(1L, complete.Volumes[0].Number);
            AssertEqual(2L, complete.Volumes[1].Number);
            AssertEqual(3L, complete.Volumes[2].Number);
            Assert(complete.CanExtract, "完整旧式 RAR 分卷组必须可解压");
            Assert(ArchiveVolumeResolver.MatchesFormat(Path.Combine(root, "old.r00"), "rar"),
                ".r00 必须按 rar 格式匹配");

            // 场景 2：缺主卷——只剩 .r00/.r01 时不得把 .r00 当首卷。
            File.WriteAllText(Path.Combine(root, "nomain.r00"), "x");
            File.WriteAllText(Path.Combine(root, "nomain.r01"), "x");
            var noMain = ArchiveVolumeResolver.Resolve(Path.Combine(root, "nomain.r01"));
            Assert(!noMain.HasRequiredFirstVolume, "缺 base.rar 时不得把 .r00 当首卷");
            Assert(!noMain.CanExtract, "缺主卷的旧式分卷组不得进入引擎");

            // 场景 3：缺中间卷——.rar(1) + .r00(2) + .r02(4) 必须报缺 .r01（组内编号 3）。
            File.WriteAllText(Path.Combine(root, "gap.rar"), "x");
            File.WriteAllText(Path.Combine(root, "gap.r00"), "x");
            File.WriteAllText(Path.Combine(root, "gap.r02"), "x");
            var gap = ArchiveVolumeResolver.Resolve(Path.Combine(root, "gap.r02"));
            Assert(!gap.IsSequenceContiguous, "旧式分卷缺中间卷必须被识别");
            Assert(gap.MissingNumbers.Contains(3L), "缺 .r01 应报其组内编号 3 缺失");
            AssertEqual("r01", ArchiveVolumeResolver.DescribeVolumeSuffix(gap, gap.MissingNumbers[0]));

            // GLM-5.3, 2026-11-04：外部审核修复——后缀映射不得截断歧义：编号 1 是主卷，
            // 显式显示 rar / zip；编号 2 与 3 分别对应 .r00/.z02 与 .r01/.z02。
            AssertEqual("rar", ArchiveVolumeResolver.DescribeVolumeSuffix(gap, 1L));
            AssertEqual("r00", ArchiveVolumeResolver.DescribeVolumeSuffix(gap, 2L));
            AssertEqual("r01", ArchiveVolumeResolver.DescribeVolumeSuffix(gap, 3L));

            // 场景 4：z01 组——ZIP 旧式分卷归入 zip 格式。
            File.WriteAllText(Path.Combine(root, "zs.zip"), "x");
            File.WriteAllText(Path.Combine(root, "zs.z01"), "x");
            File.WriteAllText(Path.Combine(root, "zs.z02"), "x");
            var zipOld = ArchiveVolumeResolver.Resolve(Path.Combine(root, "zs.z02"));
            AssertEqual(ArchiveVolumeKind.ZipOldStyle, zipOld.VolumeKind);
            AssertEqual(ArchiveKind.Zip, zipOld.ArchiveKind);
            AssertEqual("zs.zip", Path.GetFileName(zipOld.FirstVolumePath!));
            Assert(zipOld.CanExtract, "完整旧式 ZIP 分卷组必须可解压");
            AssertEqual("zip", ArchiveVolumeResolver.DescribeVolumeSuffix(zipOld, 1L));
            AssertEqual("z01", ArchiveVolumeResolver.DescribeVolumeSuffix(zipOld, 2L));
            AssertEqual("z02", ArchiveVolumeResolver.DescribeVolumeSuffix(zipOld, 3L));

            // GLM-5.3, 2026-11-04：外部审核修复——.z00 不是合法 PKZIP 分卷，
            // 负向前瞻阻止其入组，避免与主卷编号 1 撞车被判重复。
            File.WriteAllText(Path.Combine(root, "noz.zip"), "x");
            File.WriteAllText(Path.Combine(root, "noz.z00"), "x");
            File.WriteAllText(Path.Combine(root, "noz.z01"), "x");
            var noz = ArchiveVolumeResolver.Resolve(Path.Combine(root, "noz.z01"));
            Assert(!noz.HasDuplicateNumbers, ".z00 不得与主卷构成编号重复");
            Assert(noz.CanExtract, "存在 .z00 文件不得影响正常分卷组解压");
            Assert(!noz.Volumes.Any(volume => Path.GetFileName(volume.Path) == "noz.z00"), ".z00 不得加入分卷组");

            // 场景 5：大小写混合——大写 .R00 仍归组到主卷；两个大小写变体并存判为编号重复。
            // macOS 默认文件系统大小写不敏感，case.r00 与 case.R00 是同一文件，重复编号断言只在
            // 目录中确实存在两个条目时执行（Linux CI 会覆盖该分支）。
            File.WriteAllText(Path.Combine(root, "case.rar"), "x");
            File.WriteAllText(Path.Combine(root, "case.R00"), "x");
            var upperCase = ArchiveVolumeResolver.Resolve(Path.Combine(root, "case.R00"));
            AssertEqual(ArchiveVolumeKind.RarOldStyle, upperCase.VolumeKind);
            AssertEqual("case.rar", Path.GetFileName(upperCase.FirstVolumePath!));

            File.WriteAllText(Path.Combine(root, "case.r00"), "x");
            var caseEntries = Directory.EnumerateFiles(root)
                .Count(path => string.Equals(Path.GetFileName(path), "case.r00", StringComparison.OrdinalIgnoreCase));
            if (caseEntries == 2)
            {
                var duplicated = ArchiveVolumeResolver.Resolve(Path.Combine(root, "case.r00"));
                Assert(duplicated.HasDuplicateNumbers, "base.r00 与 base.R00 并存必须判为编号重复");
            }

            // 防误报：单数字扩展与普通扩展不得当作旧式分卷。
            Assert(!ArchiveVolumeResolver.HasArchiveExtension("file.r1"), "file.r1 不是分卷");
            AssertEqual("gz", ArchiveVolumeResolver.DetectArchiveExtension("data.gz"));

            // 目录扫描：旧式分卷组折叠为单一主卷任务，成员不得各自成项。
            var service = new BatchOperationService(new RecordingArchiveEngine(), new TestSystemIntegration());
            var tasks = service.LoadArchivesFromFolder(root, "rar", skipProcessed: false);
            Assert(tasks.Any(task => Path.GetFileName(task) == "old.rar"), "主卷必须作为任务入口");
            Assert(!tasks.Any(task => Path.GetFileName(task) is "old.r00" or "old.r01"),
                "完整分卷组成员不得作为独立任务");

            // 覆盖输出清理：对 old.rar 的输出解析必须包含遗留旧式分卷。
            var outputs = ArchiveVolumeResolver.ResolveOutputFiles(Path.Combine(root, "old.rar"));
            Assert(outputs.Any(file => Path.GetFileName(file) == "old.r00"), "输出解析必须包含遗留 .r00 分卷");
            Assert(outputs.Any(file => Path.GetFileName(file) == "old.r01"), "输出解析必须包含遗留 .r01 分卷");

            // 场景 6：从主卷入口解析——旁边存在旧式分卷成员时必须返回整组，
            // 否则解压后的删除/移动源只会处理主卷，分卷成员会遗留成孤儿。
            var fromMain = ArchiveVolumeResolver.Resolve(Path.Combine(root, "old.rar"));
            AssertEqual(ArchiveVolumeKind.RarOldStyle, fromMain.VolumeKind);
            AssertEqual(3, fromMain.FilesForPostProcessing.Count);
            Assert(fromMain.FilesForPostProcessing.Any(file => Path.GetFileName(file) == "old.r00"),
                "主卷入口的后处理集合必须包含 .r00 成员");
            Assert(fromMain.FilesForPostProcessing.Any(file => Path.GetFileName(file) == "old.r01"),
                "主卷入口的后处理集合必须包含 .r01 成员");
            Assert(fromMain.CanExtract, "主卷入口的完整旧式组必须可解压");

            var fromZipMain = ArchiveVolumeResolver.Resolve(Path.Combine(root, "zs.zip"));
            AssertEqual(ArchiveVolumeKind.ZipOldStyle, fromZipMain.VolumeKind);
            AssertEqual(3, fromZipMain.FilesForPostProcessing.Count);

            // 场景 7：旁边没有旧式成员的普通单卷不受影响，保持单卷语义。
            File.WriteAllText(Path.Combine(root, "plain.rar"), "single");
            var plain = ArchiveVolumeResolver.Resolve(Path.Combine(root, "plain.rar"));
            AssertEqual(ArchiveVolumeKind.None, plain.VolumeKind);
            AssertEqual(1, plain.FilesForPostProcessing.Count);
            Assert(plain.CanExtract, "普通单卷必须保持可解压");

            // 大小统计口径：分卷（base.7z.001）与大写扩展（UP.RAR）都必须计入。
            File.WriteAllText(Path.Combine(root, "sz.7z.001"), new string('a', 1024));
            File.WriteAllText(Path.Combine(root, "sz.7z.002"), new string('a', 1024));
            File.WriteAllText(Path.Combine(root, "UP.RAR"), new string('a', 2048));
            Assert(service.CalculateTotalSizeGB(root, "7z") > 0, "分卷 sz.7z.001/002 必须计入 7z 输出大小");
            Assert(service.CalculateTotalSizeGB(root, "rar") > 0, "大写 UP.RAR 必须计入 rar 输出大小");
        }
        finally
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-03：批次 2 表征测试——压缩循环。锁定执行管线重构前的
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

            foreach (var format in new[] { "tar", "wim" })
            {
                var archive = Path.Combine(testRoot, $"archive.{format}");
                var output = Path.Combine(testRoot, $"extracted-{format}");
                var options = new ArchiveOptions
                {
                    ArchiveFormat = format,
                    CompressionLevel = CompressionLevel.Store,
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    TestArchive = true
                };
                var compressed = await engine.CompressAsync(source, archive, options);
                Assert(compressed.Success, $"官方 7zz {format} 压缩失败: {compressed.ErrorMessage}");
                var extracted = await engine.ExtractAsync(archive, output, options);
                Assert(extracted.Success, $"官方 7zz {format} 解压失败: {extracted.ErrorMessage}");
                var extractedFile = Directory.GetFiles(output, "content.txt", SearchOption.AllDirectories).SingleOrDefault();
                Assert(extractedFile != null, $"{format} 解压结果中缺少 content.txt");
                AssertEqual("official 7zz smoke test", File.ReadAllText(extractedFile!));
            }

            foreach (var format in new[] { "gz", "bz2", "xz" })
            {
                var input = Path.Combine(source, "content.txt");
                var archive = Path.Combine(testRoot, $"stream.{format}");
                var output = Path.Combine(testRoot, $"extracted-{format}");
                var options = new ArchiveOptions
                {
                    ArchiveFormat = format,
                    CompressionLevel = CompressionLevel.Normal,
                    ExistingFileMode = ExistingFileMode.Overwrite,
                    TestArchive = true
                };
                var compressed = await engine.CompressAsync(input, archive, options);
                Assert(compressed.Success, $"官方 7zz {format} 压缩失败: {compressed.ErrorMessage}");
                var extracted = await engine.ExtractAsync(archive, output, options);
                Assert(extracted.Success, $"官方 7zz {format} 解压失败: {extracted.ErrorMessage}");
                var extractedFile = Directory.GetFiles(output, "*", SearchOption.AllDirectories).SingleOrDefault();
                Assert(extractedFile != null, $"{format} 解压结果缺少文件");
                AssertEqual("official 7zz smoke test", File.ReadAllText(extractedFile!));
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

    // GLM-5.3, 2026-11-04：批次 3——--language/--lang 解析、区域变体归一、
    // DetectLanguage 预扫描、双语帮助与解析成功后的语言同步契约。
    private static Task TestCliLanguageOptions()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        var originalOut = Console.Out;
        try
        {
            // 区域变体归一到 LocalizationService 语言键。
            var en = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--lang", "en-US"]);
            Assert(en.Success, string.Join(" | ", en.Errors));
            AssertEqual("en", en.Options.Language);

            var ja = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--language=ja-JP"]);
            Assert(ja.Success, string.Join(" | ", ja.Errors));
            AssertEqual("ja", ja.Options.Language);

            var zh = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["gui", "--lang", "zh"]);
            Assert(zh.Success, string.Join(" | ", zh.Errors));
            AssertEqual("zh-CN", zh.Options.Language);
            AssertEqual("zh-CN",
                BatchCompress.Avalonia.CommandLineHandler.ParseArguments([]).Options.Language);

            // 未知语言报参数错误（退出码 2 路径）。
            AssertCommandLineFails(
                ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--lang", "fr"],
                "--language");

            // 帮助语言预扫描：分离与内联两种形式；未指定或无效时返回 null。
            AssertEqual("en", BatchCompress.Avalonia.CommandLineHandler.DetectLanguage(["--help", "--lang", "en-US"]));
            AssertEqual("ja", BatchCompress.Avalonia.CommandLineHandler.DetectLanguage(["--lang=ja"]));
            AssertEqual("zh-CN", BatchCompress.Avalonia.CommandLineHandler.DetectLanguage(["--help", "--language", "zh"]));
            Assert(BatchCompress.Avalonia.CommandLineHandler.DetectLanguage(["--help"]) == null,
                "未指定语言时 DetectLanguage 必须返回 null");
            Assert(BatchCompress.Avalonia.CommandLineHandler.DetectLanguage(["--lang", "fr"]) == null,
                "无效语言在预扫描中必须回退 null");

            // 双语帮助：中文默认、英文切换；两者都必须记载 --language 与 --output-format。
            var helpOut = new System.IO.StringWriter();
            Console.SetOut(helpOut);
            BatchCompress.Avalonia.CommandLineHandler.ShowHelp();
            var chineseHelp = helpOut.ToString();
            Assert(chineseHelp.Contains("批量压缩解压工具") && chineseHelp.Contains("--output-format"),
                "默认帮助必须为中文并包含新参数");

            helpOut = new System.IO.StringWriter();
            Console.SetOut(helpOut);
            BatchCompress.Avalonia.CommandLineHandler.ShowHelp("en");
            var englishHelp = helpOut.ToString();
            Assert(englishHelp.Contains("Batch Compress & Extract Tool") &&
                   englishHelp.Contains("--language") && englishHelp.Contains("--output-format"),
                "英文帮助必须完整且包含新参数");
            Assert(!englishHelp.Contains("批量压缩解压工具"), "英文帮助不得混入中文标题");

            // 解析成功后的语言同步契约：Language 值可安全赋给 CurrentLanguage。
            localization.CurrentLanguage = en.Options.Language;
            AssertEqual("en", localization.CurrentLanguage);
            Assert(localization.Strings.Compress.Contains("ompress", StringComparison.Ordinal),
                "英文语言键切换后服务层词条必须为英文");
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
            Console.SetOut(originalOut);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 7——--lang 纯 GUI 平滑启动：
    // 仅携带有效 --language/--lang（不带模式动词、不带批处理来源/输出）时默认进入 GUI
    // 并同步全局界面语言；携带 --input/--source/--output/--text-file 等批处理意图时
    // 仍必须显式指定模式（回归锁定）；无语言参数的非空命令行维持原报错。
    private static Task TestCliLanguageDefaultGui()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        try
        {
            // 分离/内联、区域变体归一：全部默认 GUI 且解析成功。
            foreach (var arguments in new[]
            {
                new[] { "--lang", "en" },
                new[] { "--lang", "ja" },
                new[] { "--language", "de" },
                new[] { "--lang=zh-TW" },
                new[] { "--lang", "en-US" }
            })
            {
                var outcome = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(arguments);
                Assert(outcome.Success, string.Join(" | ", outcome.Errors));
                Assert(outcome.Options.Gui, $"{string.Join(' ', arguments)} 必须默认进入 GUI 模式");
                Assert(!outcome.Options.Compress && !outcome.Options.Decompress,
                    "纯语言启动不得落入无界面模式");
            }

            // 语言精确切换：Options.Language 归一值可直接驱动 CurrentLanguage，
            // 且解析期预扫描已把全局语言切换到位（GUI 启动路径无需二次处理）。
            var ja = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["--lang", "ja"]);
            AssertEqual("ja", ja.Options.Language);
            AssertEqual("ja", localization.CurrentLanguage);

            // GUI 伴随的无害参数不阻断默认 GUI。
            var withVerbose = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["--lang", "en", "--verbose"]);
            Assert(withVerbose.Success && withVerbose.Options.Gui, "伴随 --verbose 不得阻断默认 GUI");

            // 显式 gui 动词 + 语言：既有语义保持不变。
            var explicitGui = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["gui", "--lang", "de"]);
            Assert(explicitGui.Success && explicitGui.Options.Gui, "gui --lang 必须保持显式 GUI");
            AssertEqual("de", explicitGui.Options.Language);

            // 批处理意图（来源/输入/输出/清单）存在时：仍必须显式指定模式。
            AssertCommandLineFails(["--lang", "en", "-i", "/tmp/a"], "compress");
            AssertCommandLineFails(["--lang", "en", "-s", "/tmp/src"], "compress");
            AssertCommandLineFails(["--lang", "en", "-o", "/tmp/out"], "compress");
            AssertCommandLineFails(["--lang", "en", "-t", "/tmp/missing.txt"], "compress");

            // 无语言参数的非空命令行：维持“必须指定模式”原报错（防止放宽被无限外推）。
            AssertCommandLineFails(["--verbose"], "compress");
            AssertCommandLineFails(["--level", "5"], "compress");

            // 无效语言单独出现：仍按语言取值错误拒绝（退出码 2 路径）。
            AssertCommandLineFails(["--lang", "fr"], "--language");
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 3——--output-format 解析与校验；
    // --format 是 --extension 的历史别名，不得被挪用为输出格式（回归锁定）。
    private static Task TestCliOutputFormatOptions()
    {
        var json = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--output-format", "JSON"]);
        Assert(json.Success, string.Join(" | ", json.Errors));
        AssertEqual("json", json.Options.OutputFormat);

        AssertEqual("text",
            BatchCompress.Avalonia.CommandLineHandler.ParseArguments([]).Options.OutputFormat);
        AssertCommandLineFails(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--output-format", "xml"],
            "--output-format");

        // --format 仍指向扩展名（历史兼容回归）。
        var formatAsExtension = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["extract", "-i", "/tmp/a.7z", "-o", "/tmp/out", "--format", "7z"]);
        Assert(formatAsExtension.Success, string.Join(" | ", formatAsExtension.Errors));
        AssertEqual("7z", formatAsExtension.Options.Extension);
        AssertEqual("text", formatAsExtension.Options.OutputFormat);

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 3——NDJSON 输出器单元行为：
    // 事件分类（progress/item/summary）、密码排除、计数呈现与非 ASCII 原样输出。
    // GLM-5.3, 2026-11-04：批次 4——CLI 解析期错误文案完全本地化：
    // 语言跟随命令行 --language/--lang（无论出现顺序）、四语抽检、zh-TW 繁体帮助、
    // zh-CN 文案与历史硬编码逐字一致（既有中文断言测试不回归）。
    private static Task TestCliErrorLocalization()
    {
        var localization = LocalizationService.Instance;
        var originalLanguage = localization.CurrentLanguage;
        var originalOut = Console.Out;
        try
        {
            // zh-CN 回归：文案经词条输出但与历史硬编码逐字一致。
            AssertCommandLineFails(
                ["--compress", "--decompress", "-i", "/tmp/a", "-o", "/tmp/out"],
                "不能同时指定");

            // 英文：--lang 在出错参数之后也生效（解析开始即按预扫描语言切换词条）。
            localization.CurrentLanguage = "zh-CN";
            var levelError = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "--level", "x", "--lang", "en"]);
            Assert(!levelError.Success, "必须报错");
            AssertEqual("en", localization.CurrentLanguage);
            Assert(levelError.Errors.Contains("--level must be an integer."),
                "英文错误必须逐字匹配: " + string.Join(" | ", levelError.Errors));

            var outputError = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["compress", "--lang", "en", "-i", "/tmp/a"]);
            Assert(outputError.Errors.Contains("Headless mode requires --output."),
                "英文输出缺失错误必须逐字匹配: " + string.Join(" | ", outputError.Errors));
            AssertEqual("Argument error: {0}", localization.Strings.CliErrorPrefix);

            // 德文与日文抽检。
            localization.CurrentLanguage = "zh-CN";
            var german = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["compress", "--lang", "de", "-i", "/tmp/a"]);
            Assert(german.Errors.Contains("Der Headless-Modus erfordert --output."),
                "德文错误必须逐字匹配: " + string.Join(" | ", german.Errors));

            localization.CurrentLanguage = "zh-CN";
            var japanese = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
                ["--whatever", "--lang", "ja"]);
            Assert(japanese.Errors.Contains("不明なオプション: --whatever"),
                "日文错误必须逐字匹配: " + string.Join(" | ", japanese.Errors));

            // zh-TW 繁体帮助：正体术语且不含简体标题。
            var helpOut = new System.IO.StringWriter();
            Console.SetOut(helpOut);
            BatchCompress.Avalonia.CommandLineHandler.ShowHelp("zh-TW");
            var traditionalHelp = helpOut.ToString();
            Assert(traditionalHelp.Contains("批次壓縮解壓工具") && traditionalHelp.Contains("--output-format"),
                "繁体帮助必须完整且包含新参数");
            Assert(!traditionalHelp.Contains("批量压缩解压工具") && !traditionalHelp.Contains("图形界面"),
                "繁体帮助不得混入简体用语");
        }
        finally
        {
            localization.CurrentLanguage = originalLanguage;
            Console.SetOut(originalOut);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 4——日志文件安全与体积治理：
    // Unix 0600 权限、超阈值轮转为 .1、7 天过期清理（仅匹配 batchcompress_ 模式）。
    // GLM-5.3, 2026-11-04：批次 5——受控并发执行：
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
    // 可配置延迟（保证初始槽位全部启动）与真实输出（驱动大小上限统计）。
    private sealed class ConcurrentTrackingEngine : IArchiveEngine
    {
        private int _inFlight;
        private int _peakInFlight;

        public List<string> CompressionInputs { get; } = [];
        public List<string> ExtractionInputs { get; } = [];
        // GLM-5.3, 2026-11-04：批次 8——80ms 在系统高负载（如构建余波）下偶发不足以让
        // 全部初始槽位任务进入引擎，导致 PeakInFlight 断言抖动；加宽到 200ms 提高调度容忍。
        public int DelayMilliseconds { get; set; } = 200;
        public bool WriteRealOutput { get; set; }
        public int PeakInFlight => _peakInFlight;

        public bool IsAvailable() => true;

        public async Task<ArchiveResult> CompressAsync(
            string input, string output, ArchiveOptions options, CancellationToken cancellationToken = default)
        {
            CompressionInputs.Add(Path.GetFileName(input));
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
            ExtractionInputs.Add(Path.GetFileName(archivePath));
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

    private static Task TestLogFilePermissionsAndRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-log-hygiene-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 1) Unix 权限：新建日志必须为 0600（仅当前用户读写）。
            var logPath = Path.Combine(root, "batchcompress_unit.log");
            using (var logger = new FileLoggerService(logPath))
            {
                logger.LogOperation("TEST", "权限检查");
            }

            Assert(File.Exists(logPath), "日志文件必须创建");
            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(logPath);
                AssertEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }

            // 2) 轮转：小阈值 + 足量写入后当前文件轮转为 .1，新文件继续写入。
            var rotatingPath = Path.Combine(root, "batchcompress_rotate.log");
            using (var logger = new FileLoggerService(rotatingPath, rotationBytes: 512))
            {
                for (var i = 0; i < 200; i++)
                {
                    logger.LogOperation("TEST", $"轮转压力行 {i} 0123456789012345678901234567890123456789");
                }
            }

            Assert(File.Exists(rotatingPath + ".1"), "超过阈值必须轮转出 .1 副本");
            Assert(File.Exists(rotatingPath), "轮转后必须新建同名日志继续写入");
            var rotatedSize = new FileInfo(rotatingPath + ".1").Length;
            Assert(rotatedSize > 512, $"轮转副本应超过阈值: {rotatedSize}");
            Assert(new FileInfo(rotatingPath).Length < rotatedSize, "新文件必须小于轮转副本");

            // 3) 过期清理：7 天前的 batchcompress_*.log 与 .1 被删，无关命名不受影响。
            var staleLog = Path.Combine(root, "batchcompress_20200101_000000.log");
            var staleRotated = Path.Combine(root, "batchcompress_20200101_000000.log.1");
            var unrelated = Path.Combine(root, "unrelated_old.log");
            foreach (var file in new[] { staleLog, staleRotated, unrelated })
            {
                File.WriteAllText(file, "stale");
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow - TimeSpan.FromDays(8));
            }

            using (var logger = new FileLoggerService(Path.Combine(root, "batchcompress_fresh.log")))
            {
                logger.LogOperation("TEST", "触发清理");
            }

            Assert(!File.Exists(staleLog) && !File.Exists(staleRotated), "过期批处理日志必须被清理");
            Assert(File.Exists(unrelated), "无关命名的旧文件不得被清理");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 7/8——多代日志轮转与边界：
    // .1→.2→…→.N 向上滚动、超限淘汰最旧一代（默认 5 代）、代序内容 .1 最新、
    // 轮转副本保持 0600、过期清理覆盖全部代次、上限 1 退化旧行为。
    // 批次 8 起 .2 及以上代次为 .gz 压缩归档（.1 保持纯文本），本测试同步改按新布局断言。
    private static Task TestLogMultiGenerationRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-log-multigen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 0) 默认代数常量锁定为 5（.1 至 .5）。
            AssertEqual(5, FileLoggerService.DefaultRotatedGenerations);

            // 1) 上限 3 代：足量写入触发多轮轮转后恰好保留 .1 与 .2.gz/.3.gz，不产生 .4*。
            var logPath = Path.Combine(root, "batchcompress_multi.log");
            using (var logger = new FileLoggerService(logPath, rotationBytes: 256, maxRotatedGenerations: 3))
            {
                for (var i = 0; i < 400; i++)
                {
                    logger.LogOperation("TEST", $"多代轮转行 {i:D4} 0123456789ABCDEF");
                }
            }

            Assert(File.Exists(logPath), "轮转后当前日志必须继续写入");
            Assert(File.Exists(logPath + ".1"), "第 1 代必须保持纯文本存在");
            Assert(File.Exists(logPath + ".2.gz"), "第 2 代必须为 .gz 压缩归档");
            Assert(File.Exists(logPath + ".3.gz"), "第 3 代必须为 .gz 压缩归档");
            Assert(!File.Exists(logPath + ".2") && !File.Exists(logPath + ".3"),
                "压缩形态存在时不得残留同名纯文本代");
            Assert(!File.Exists(logPath + ".4") && !File.Exists(logPath + ".4.gz"),
                "不得超过代数上限滚动出 .4");

            // 代序内容：.1 中的最大行号必须大于 .3.gz 解压内容（新内容滚入 .1，旧内容被推向高代）；
            // .1 是末段轮转代（含 300+ 行号但不含收尾写入），收尾写入留在当前日志。
            var newest = MaxLineNumberInText(File.ReadAllText(logPath + ".1"));
            var oldest = MaxLineNumberInText(ReadGzipText(logPath + ".3.gz"));
            Assert(newest > oldest, $"代序必须 .1 最新（{newest}）> .3.gz 最旧（{oldest}）");
            Assert(newest >= 300 && newest < 400, $"末段轮转代应落在 300-399 行段，实际 {newest}");
            Assert(MaxLineNumberInText(File.ReadAllText(logPath)) > newest,
                "当前日志必须继续接收轮转后的写入");

            // Unix 权限：纯文本第 1 代与全部 .gz 归档代均为 0600。
            if (!OperatingSystem.IsWindows())
            {
                AssertEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(logPath + ".1"));
                for (var generation = 2; generation <= 3; generation++)
                {
                    AssertEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        File.GetUnixFileMode($"{logPath}.{generation}.gz"));
                }
            }

            // 2) 上限 1 代：退化为旧单代行为——仅 .1 存在，不产生 .2.gz。
            var singlePath = Path.Combine(root, "batchcompress_single.log");
            using (var logger = new FileLoggerService(singlePath, rotationBytes: 256, maxRotatedGenerations: 1))
            {
                for (var i = 0; i < 200; i++)
                {
                    logger.LogOperation("TEST", $"单代轮转行 {i:D4} 0123456789ABCDEF");
                }
            }

            Assert(File.Exists(singlePath + ".1"), "上限 1 代仍必须轮转出 .1");
            Assert(!File.Exists(singlePath + ".2.gz"), "上限 1 代不得产生 .2.gz");

            // 3) 过期清理覆盖全部代次（纯文本与 .gz 归档）：7 天前的一并清理，无关命名不受影响。
            var staleRoot = Path.Combine(root, "stale");
            Directory.CreateDirectory(staleRoot);
            var staleFiles = new[]
            {
                Path.Combine(staleRoot, "batchcompress_20200101_000000.log"),
                Path.Combine(staleRoot, "batchcompress_20200101_000000.log.2"),
                Path.Combine(staleRoot, "batchcompress_20200101_000000.log.3.gz"),
                Path.Combine(staleRoot, "batchcompress_20200101_000000.log.5.gz"),
                Path.Combine(staleRoot, "unrelated_old.log.2.gz")
            };
            foreach (var file in staleFiles)
            {
                File.WriteAllText(file, "stale");
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow - TimeSpan.FromDays(8));
            }

            using (var logger = new FileLoggerService(Path.Combine(staleRoot, "batchcompress_fresh.log")))
            {
                logger.LogOperation("TEST", "触发清理");
            }

            for (var index = 0; index < staleFiles.Length - 1; index++)
            {
                Assert(!File.Exists(staleFiles[index]), $"过期文件必须被清理: {staleFiles[index]}");
            }
            Assert(File.Exists(staleFiles[^1]), "无关命名的旧文件不得被清理");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 7/8——从日志文本（纯文本或 gzip 解压结果）中提取
    // “[TEST] …行 NNNN …” 的最大行号，用于断言轮转代序（.1 最新、高代最旧）。无匹配行返回 -1。
    private static int MaxLineNumberInText(string text)
    {
        var max = -1;
        foreach (var line in text.Split('\n'))
        {
            var marker = line.IndexOf("行 ", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            var rest = line[(marker + 2)..].Split(' ')[0];
            if (int.TryParse(rest, out var number) && number > max)
            {
                max = number;
            }
        }

        return max;
    }

    // GLM-5.3, 2026-11-04：批次 8——旧代 Gzip 压缩归档：
    // .1 保持纯文本（无 gzip 魔数、可直接读取）；.2 及以上为有效 gzip（魔数 1F 8B）；
    // 解压内容与原始日志行一致且中文无乱码；压缩确有收益；.gz 权限 0600；
    // 0.7.1 遗留纯文本代在下一轮转后收敛为压缩形态；过期 .gz 归档被清理。
    private static Task TestLogGzipCompressionRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-log-gzip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 1) 默认 5 代 + 足量写入：.1 纯文本，.2.gz 至 .5.gz 逐代压缩归档。
            var logPath = Path.Combine(root, "batchcompress_gzip.log");
            using (var logger = new FileLoggerService(logPath, rotationBytes: 256))
            {
                for (var i = 0; i < 500; i++)
                {
                    logger.LogOperation("TEST", $"压缩轮转行 {i:D4} 0123456789ABCDEF中文内容");
                }
            }

            Assert(File.Exists(logPath + ".1"), "第 1 代必须存在且保持纯文本");
            for (var generation = 2; generation <= 5; generation++)
            {
                Assert(File.Exists($"{logPath}.{generation}.gz"), $"第 {generation} 代必须为 .gz 归档");
            }

            // .1 无 gzip 魔数（1F 8B）且能按纯文本直接读出中文日志行。
            var firstGen = File.ReadAllBytes(logPath + ".1");
            Assert(!(firstGen.Length > 1 && firstGen[0] == 0x1F && firstGen[1] == 0x8B),
                ".1 必须是纯文本而非 gzip");
            Assert(File.ReadAllText(logPath + ".1").Contains("压缩轮转行"),
                ".1 纯文本必须能直接读出日志行");

            // .2.gz：gzip 魔数 + 解压内容完整无乱码 + 压缩确有空间收益。
            var secondGenPath = logPath + ".2.gz";
            var magic = File.ReadAllBytes(secondGenPath);
            Assert(magic.Length > 1 && magic[0] == 0x1F && magic[1] == 0x8B,
                ".2.gz 必须是有效 gzip 文件（魔数 1F 8B）");
            var decompressed = ReadGzipText(secondGenPath);
            Assert(decompressed.Contains("压缩轮转行") && decompressed.Contains("中文内容"),
                "解压内容必须包含预期日志行且中文无乱码");
            var generationNumber = MaxLineNumberInText(decompressed);
            Assert(generationNumber >= 100, $"解压内容必须包含成段历史行号，实际最大 {generationNumber}");
            Assert(new FileInfo(secondGenPath).Length < decompressed.Length / 2,
                $"gzip 归档必须显著小于原文: {new FileInfo(secondGenPath).Length} vs {decompressed.Length}");

            // Unix 权限：.gz 归档同样收紧为 0600。
            if (!OperatingSystem.IsWindows())
            {
                for (var generation = 2; generation <= 5; generation++)
                {
                    AssertEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        File.GetUnixFileMode($"{logPath}.{generation}.gz"));
                }
            }

            // 2) 0.7.1 遗留纯文本布局（.1/.2/.3 均为纯文本）在下一轮转后收敛为压缩形态。
            var legacyPath = Path.Combine(root, "batchcompress_legacy.log");
            File.WriteAllText(legacyPath + ".1", $"[2026-11-04] [INFORMATION ] 遗留第1代 压缩轮转行 {1:D4}");
            File.WriteAllText(legacyPath + ".2", $"[2026-11-04] [INFORMATION ] 遗留第2代 压缩轮转行 {2:D4}");
            File.WriteAllText(legacyPath + ".3", $"[2026-11-04] [INFORMATION ] 遗留第3代 压缩轮转行 {3:D4}");
            File.WriteAllText(legacyPath, $"[2026-11-04] [INFORMATION ] 遗留当前 压缩轮转行 {4:D4}");
            using (var logger = new FileLoggerService(legacyPath, rotationBytes: 64, maxRotatedGenerations: 4))
            {
                for (var i = 0; i < 80; i++)
                {
                    logger.LogOperation("TEST", $"遗留收敛行 {i:D4} 0123456789ABCDEF");
                }
            }

            Assert(File.Exists(legacyPath + ".1"), "收敛后第 1 代必须为纯文本");
            Assert(!File.Exists(legacyPath + ".2") && !File.Exists(legacyPath + ".3"),
                "遗留纯文本旧代必须被压缩清除");
            for (var generation = 2; generation <= 4; generation++)
            {
                Assert(File.Exists($"{legacyPath}.{generation}.gz"),
                    $"收敛后第 {generation} 代必须为 .gz 归档");
            }

            // 3) 过期清理覆盖 .gz 归档代。
            var staleGz = Path.Combine(root, "batchcompress_20200101_000000.log.2.gz");
            File.WriteAllText(staleGz, "stale");
            File.SetLastWriteTimeUtc(staleGz, DateTime.UtcNow - TimeSpan.FromDays(8));
            using (var logger = new FileLoggerService(Path.Combine(root, "batchcompress_fresh.log")))
            {
                logger.LogOperation("TEST", "触发清理");
            }

            Assert(!File.Exists(staleGz), "过期的 .gz 归档必须被清理");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 8——解压 .gz 归档为 UTF-8 文本（BCL GZipStream）。
    private static string ReadGzipText(string path)
    {
        using var input = File.OpenRead(path);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // GLM-5.3, 2026-11-04：批次 8——附件联系方式配置外置：
    // 无配置回退默认模版、保存后重载一致、空白回退默认、非法路径保存不抛出且加载仍安全回退。
    private static Task TestEnclosureConfigService()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-enclosure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 1) 无配置文件：返回默认联系文案（与原硬编码逐字一致）。
            var configPath = Path.Combine(root, "enclosures.txt");
            var service = new EnclosureConfigService(configPath);
            AssertEqual(configPath, service.GetConfigFilePath());
            var defaultText = service.LoadEnclosuresText();
            AssertEqual(EnclosureConfigService.GetDefaultEnclosuresText(), defaultText);
            Assert(defaultText.Contains("qgkc520@Gmail.com") && defaultText.Contains("微信号") &&
                   defaultText.Contains("QQ号"), "默认模版必须保留原联系方式内容");

            // 2) 保存自定义配置后重新加载：原样读回（含中文与换行）。
            var custom = "【解压密码】备用邮箱 someone@example.com\n第二行联系说明";
            Assert(service.SaveEnclosuresText(custom), "保存自定义配置必须成功");
            Assert(File.Exists(configPath), "配置文件必须落盘");
            AssertEqual(custom, new EnclosureConfigService(configPath).LoadEnclosuresText());

            // 3) 空白配置回退默认模版（空文件不产生空附件文案）。
            Assert(service.SaveEnclosuresText("   \n  "), "保存空白配置必须成功");
            AssertEqual(EnclosureConfigService.GetDefaultEnclosuresText(), service.LoadEnclosuresText());

            // 4) 保存异常回退：目标路径的父目录是一个文件 → 不抛出、返回 false；
            //    随后加载仍回退默认模版（IO 失败不得阻断 GUI 启动路径）。
            var blocked = Path.Combine(root, "blocker");
            File.WriteAllText(blocked, "occupies");
            var bad = new EnclosureConfigService(Path.Combine(blocked, "enclosures.txt"));
            Assert(!bad.SaveEnclosuresText(custom), "非法路径保存必须返回 false 而不抛出");
            AssertEqual(EnclosureConfigService.GetDefaultEnclosuresText(), bad.LoadEnclosuresText());

            // 5) 默认路径解析：无显式路径时不做 IO 也必须给出可用绝对路径（指向 enclosures.txt）。
            var resolved = new EnclosureConfigService().GetConfigFilePath();
            Assert(Path.IsPathRooted(resolved) &&
                   resolved.EndsWith("enclosures.txt", StringComparison.Ordinal),
                $"默认配置路径必须指向 enclosures.txt，实际 {resolved}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static Task TestNdjsonOutputWriter()
    {
        using var output = new System.IO.StringWriter();
        var writer = new NdjsonOutputWriter(output);

        // 条目启动消息（计数不变）→ progress/phase=start。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "项目甲",
            Message = "开始压缩：项目甲",
            IsError = false
        });

        // 命令输出事件（含密码）必须整条丢弃。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "项目甲",
            Message = "[rar] command\nrar a -pSecretPassword123 out.rar 项目甲",
            IsCommandOutput = true
        });

        // 成功完成 → item/status=success，带七类计数。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "项目甲",
            Message = "成功：项目甲",
            SuccessCount = 1
        });

        // 归档成功但后处理失败：Success 与 PostProcessFail 同时增加 → 仍为 success，
        // postProcessFailCount 呈现 1。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "项目乙",
            Message = "成功（后处理失败）：项目乙",
            SuccessCount = 2,
            PostProcessFailCount = 1
        });

        // 不存在跳过 → item/status=skip（真实管线报告累计计数，此处保持同口径）。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "缺失项",
            Message = "不存在：/tmp/missing",
            SuccessCount = 2,
            PostProcessFailCount = 1,
            NonExistCount = 1,
            IsError = true
        });

        // 引擎失败 → item/status=fail。
        writer.Report(new OperationProgressInfo
        {
            CurrentFile = "失败项",
            Message = "失败：失败项",
            SuccessCount = 2,
            PostProcessFailCount = 1,
            NonExistCount = 1,
            FailCount = 1,
            IsError = true
        });

        writer.WriteSummary(TimeSpan.FromSeconds(3.25));

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
        AssertEqual(6, lines.Length);
        Assert(!output.ToString().Contains("SecretPassword123", StringComparison.Ordinal),
            "命令输出中的密码绝对不得进入 NDJSON 流");

        // 每行必须是合法 JSON（宽松转义保留中文原样）。
        var documents = lines
            .Select(line => System.Text.Json.JsonDocument.Parse(line))
            .ToArray();
        Assert(lines[0].Contains("项目甲", StringComparison.Ordinal), "非 ASCII 消息必须原样输出而非转义");

        var progress = documents[0].RootElement;
        AssertEqual("progress", progress.GetProperty("type").GetString());
        AssertEqual("start", progress.GetProperty("phase").GetString());
        AssertEqual("项目甲", progress.GetProperty("source").GetString());

        var success = documents[1].RootElement;
        AssertEqual("item", success.GetProperty("type").GetString());
        AssertEqual("success", success.GetProperty("status").GetString());
        AssertEqual(1, success.GetProperty("successCount").GetInt32());
        AssertEqual(0, success.GetProperty("postProcessFailCount").GetInt32());

        var postFail = documents[2].RootElement;
        AssertEqual("success", postFail.GetProperty("status").GetString());
        AssertEqual(2, postFail.GetProperty("successCount").GetInt32());
        AssertEqual(1, postFail.GetProperty("postProcessFailCount").GetInt32());

        var skip = documents[3].RootElement;
        AssertEqual("skip", skip.GetProperty("status").GetString());
        AssertEqual(1, skip.GetProperty("nonexistentCount").GetInt32());

        var fail = documents[4].RootElement;
        AssertEqual("fail", fail.GetProperty("status").GetString());

        var summary = documents[5].RootElement;
        AssertEqual("summary", summary.GetProperty("type").GetString());
        AssertEqual(2, summary.GetProperty("success").GetInt32());
        AssertEqual(1, summary.GetProperty("failed").GetInt32());
        AssertEqual(1, summary.GetProperty("postProcessFailed").GetInt32());
        AssertEqual(1, summary.GetProperty("nonexistent").GetInt32());
        Assert(Math.Abs(summary.GetProperty("elapsedSeconds").GetDouble() - 3.25) < 0.0001,
            "elapsedSeconds 必须保留毫秒精度");

        return Task.CompletedTask;
    }

    // GLM-5.3, 2026-11-04：批次 3——NDJSON 端到端：真实批处理循环（服务层管线）
    // 驱动输出器，锁定事件顺序与最终汇总计数。
    private static async Task TestNdjsonEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), $"batch-compress-ndjson-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "src-item");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "a");
        try
        {
            using var output = new System.IO.StringWriter();
            var writer = new NdjsonOutputWriter(output);
            var snapshots = new List<OperationProgressInfo>();

            await new BatchOperationService(new ScriptedArchiveEngine(), new RecordingSystemIntegration())
                .BatchCompressAsync(
                    [source, Path.Combine(root, "missing-item")],
                    new BatchOperationOptions
                    {
                        OutputPath = Path.Combine(root, "out"),
                        Extension = "7z",
                        ExistingFileMode = ExistingFileMode.Overwrite
                    },
                    new CloningProgress(snapshots),
                    CancellationToken.None);

            // 服务层消息流按原顺序回放到输出器，模拟 CLI InlineProgress 的转发。
            // CloningProgress 在报告时克隆可变快照，避免共享对象被后续事件覆盖。
            foreach (var info in snapshots)
            {
                writer.Report(info);
            }

            writer.WriteSummary(TimeSpan.FromSeconds(1));

            var lines = output.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .ToArray();
            var documents = lines
                .Select(line => System.Text.Json.JsonDocument.Parse(line))
                .ToArray();

            // 第一条非命令事件是条目启动（progress/phase=start，source 为真实来源名）。
            var first = documents[0].RootElement;
            AssertEqual("progress", first.GetProperty("type").GetString());
            AssertEqual("src-item", first.GetProperty("source").GetString());

            // 存在事件序列中存在 success 与 skip（missing-item 计 NonExistCount）。
            var statuses = documents
                .Where(document => document.RootElement.GetProperty("type").GetString() == "item")
                .Select(document => document.RootElement.GetProperty("status").GetString())
                .ToArray();
            Assert(statuses.Contains("success"), "必须有成功条目事件");
            Assert(statuses.Contains("skip"), "缺失来源必须产生 skip 事件");

            // 末行 summary 与服务层最终计数一致。
            var summary = documents[^1].RootElement;
            AssertEqual("summary", summary.GetProperty("type").GetString());
            AssertEqual(1, summary.GetProperty("success").GetInt32());
            AssertEqual(1, summary.GetProperty("nonexistent").GetInt32());
            AssertEqual(0, summary.GetProperty("failed").GetInt32());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task TestCommandLineValidation()
    {
        AssertCommandLineFails(
            ["--compress", "--decompress", "-i", "/tmp/a", "-o", "/tmp/out"],
            "不能同时指定");
        AssertCommandLineFails(["compress", "-i", "/tmp/a"], "--output");
        AssertCommandLineFails(["compress", "-i", "/tmp/a", "-o", "/tmp/out", "-e", "iso"], "不支持创建");
        var tar = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(
            ["compress", "-i", "/tmp/a", "-o", "/tmp/out", "-e", "tar", "--no-random-password"]);
        Assert(tar.Success, string.Join(" | ", tar.Errors));
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
            Elapsed = source.Elapsed
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArchiveResult { Success = true });

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
}
