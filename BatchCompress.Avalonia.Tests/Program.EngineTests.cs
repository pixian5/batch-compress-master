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
    // 归档引擎参数生成、分卷解析及真实7zz压解测试


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

    // Antigravity, 2026-11-08：批次 15（P2-10）——验证解压 RAR 遇到缺少 RAR 引擎时自动回退到 7zz 引擎。
    private static async Task TestRarExtractionFallbackToSevenZip()
    {
        var rarUnavailable = new FallbackProbeEngine(available: false, name: "rar");
        var sevenZipAvailable = new FallbackProbeEngine(available: true, name: "7zz");
        var router = new ArchiveEngineRouter(rarUnavailable, sevenZipAvailable);

        var options = CreateOptions();
        options.ArchiveFormat = "rar";

        // 1. 当 archivePath 后缀为 .rar 时，即使 rar 引擎不可用，也应回退并成功调用 7zz 引擎解压
        var result = await router.ExtractAsync("/tmp/archive.rar", "/tmp/extracted", options);
        Assert(result.Success, "RAR 引擎不可用时应成功回退到 7zz 解压");
        AssertEqual(1, sevenZipAvailable.ExtractCalls);
        AssertEqual(0, rarUnavailable.ExtractCalls);

        // 2. 当 archivePath 为未知扩展名但 options.ArchiveFormat == "rar" 时同样回退到 7zz
        sevenZipAvailable.Reset();
        rarUnavailable.Reset();
        var result2 = await router.ExtractAsync("/tmp/archive.bin", "/tmp/extracted", options);
        Assert(result2.Success, "未知后缀但格式选项为 rar 时应成功回退到 7zz 解压");
        AssertEqual(1, sevenZipAvailable.ExtractCalls);
        AssertEqual(0, rarUnavailable.ExtractCalls);

        // 3. 当两者均不可用时，应调用 RAR 引擎并返回不可用错误
        var sevenZipUnavailable = new FallbackProbeEngine(available: false, name: "7zz");
        var noEngineRouter = new ArchiveEngineRouter(rarUnavailable, sevenZipUnavailable);
        var result3 = await noEngineRouter.ExtractAsync("/tmp/archive.rar", "/tmp/extracted", options);
        Assert(!result3.Success, "引擎全不可用时解压必须失败");
        AssertEqual(1, rarUnavailable.ExtractCalls);

        // 4. 验证 Linux ARM64 错误诊断文案与通用提示
        var missingMsg = RarArchiveEngine.GetMissingRarMessage();
        Assert(!string.IsNullOrWhiteSpace(missingMsg), "RAR 缺失提示不能为空");
    }

    private sealed class FallbackProbeEngine(bool available, string name) : IArchiveEngine
    {
        public int CompressCalls { get; private set; }
        public int ExtractCalls { get; private set; }

        public bool IsAvailable() => available;

        public Task<ArchiveResult> CompressAsync(
            string input,
            string output,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            CompressCalls++;
            return Task.FromResult(new ArchiveResult
            {
                Success = available,
                ExitCode = available ? 0 : -2,
                ErrorMessage = available ? null : $"{name} unavailable"
            });
        }

        public Task<ArchiveResult> ExtractAsync(
            string archivePath,
            string outputDir,
            ArchiveOptions options,
            CancellationToken cancellationToken = default)
        {
            ExtractCalls++;
            return Task.FromResult(new ArchiveResult
            {
                Success = available,
                ExitCode = available ? 0 : -2,
                ErrorMessage = available ? null : $"{name} unavailable"
            });
        }

        public void Reset()
        {
            CompressCalls = 0;
            ExtractCalls = 0;
        }
    }
}
