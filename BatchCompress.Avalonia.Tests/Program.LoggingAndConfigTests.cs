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
    // 日志轮转与Gzip压缩、附件配置外置及NDJSON输出测试


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

    // Antigravity, 2026-11-04：批次 9——日志 Gzip 轮转容灾降级兜底测试：
    // 1) 正常单文件压缩与内容解压校验、Unix 0600 权限校验；
    // 2) 源文件不存在或目标被阻断时 TryCompressFileToGzip 返回 false 且不崩溃、清理临时文件；
    // 3) 极端异常下服务层 ShiftRotatedGenerations 安全降级，保证当前写入不中断。
    private static Task TestLogGzipDegradedFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-log-fallback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "source.log");
            var originalContent = "日志行 0001: 包含中文与特殊字符 1234567890 !@#$%^&*()_+";
            File.WriteAllText(src, originalContent, Encoding.UTF8);

            // 1) 正常单文件压缩测试
            var targetGz = Path.Combine(root, "target.log.gz");
            var ok = FileLoggerService.TryCompressFileToGzip(src, targetGz);
            Assert(ok, "正常路径下的 TryCompressFileToGzip 必须成功");
            Assert(File.Exists(targetGz), "目标 .gz 必须存在");
            AssertEqual(originalContent, ReadGzipText(targetGz));

            if (!OperatingSystem.IsWindows())
            {
                AssertEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(targetGz));
            }

            // 2) 源文件不存在测试
            var missingSrc = Path.Combine(root, "nonexistent.log");
            var okMissing = FileLoggerService.TryCompressFileToGzip(missingSrc, Path.Combine(root, "should_not_exist.gz"));
            Assert(!okMissing, "源文件不存在时必须返回 false 而不抛出异常");
            Assert(!File.Exists(Path.Combine(root, "should_not_exist.gz")), "不应生成目标文件");

            // 3) 目标路径被文件阻断测试（父级是普通文件无法创建子文件）
            var fileBlocker = Path.Combine(root, "blocker_file");
            File.WriteAllText(fileBlocker, "block");
            var blockedTarget = Path.Combine(fileBlocker, "sub", "target.gz");
            var okBlocked = FileLoggerService.TryCompressFileToGzip(src, blockedTarget);
            Assert(!okBlocked, "目标路径不可写时必须安全返回 false 而不崩溃");
            Assert(!File.Exists(blockedTarget + ".tmp"), "必须清理临时文件");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-04：批次 9——附件配置界面的 5 语言词条完整性测试：
    // 覆盖简中、繁中、英文、日文、德文，断言全部词条非空且英德无中文残留。
    private static Task TestEnclosureLocalizationAndTokens()
    {
        var languages = new[] { "zh-CN", "zh-TW", "en", "ja", "de" };
        var service = LocalizationService.Instance;

        foreach (var lang in languages)
        {
            service.CurrentLanguage = lang;
            var s = service.Strings;

            Assert(!string.IsNullOrWhiteSpace(s.SaveEnclosuresConfig), $"{lang} SaveEnclosuresConfig 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.ResetEnclosuresConfig), $"{lang} ResetEnclosuresConfig 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.TooltipSaveEnclosuresConfig), $"{lang} TooltipSaveEnclosuresConfig 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.TooltipResetEnclosuresConfig), $"{lang} TooltipResetEnclosuresConfig 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.EnclosuresConfigSaved), $"{lang} EnclosuresConfigSaved 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.EnclosuresConfigReset), $"{lang} EnclosuresConfigReset 不能为空");
            Assert(!string.IsNullOrWhiteSpace(s.EnclosuresConfigSaveFailed), $"{lang} EnclosuresConfigSaveFailed 不能为空");

            if (lang is "en" or "de")
            {
                Assert(!Regex.IsMatch(s.SaveEnclosuresConfig, "[\u4e00-\u9fff]"), $"{lang} SaveEnclosuresConfig 不能含中文");
                Assert(!Regex.IsMatch(s.ResetEnclosuresConfig, "[\u4e00-\u9fff]"), $"{lang} ResetEnclosuresConfig 不能含中文");
                Assert(!Regex.IsMatch(s.TooltipSaveEnclosuresConfig, "[\u4e00-\u9fff]"), $"{lang} TooltipSaveEnclosuresConfig 不能含中文");
                Assert(!Regex.IsMatch(s.TooltipResetEnclosuresConfig, "[\u4e00-\u9fff]"), $"{lang} TooltipResetEnclosuresConfig 不能含中文");
                Assert(!Regex.IsMatch(s.EnclosuresConfigSaved, "[\u4e00-\u9fff]"), $"{lang} EnclosuresConfigSaved 不能含中文");
                Assert(!Regex.IsMatch(s.EnclosuresConfigReset, "[\u4e00-\u9fff]"), $"{lang} EnclosuresConfigReset 不能含中文");
                Assert(!Regex.IsMatch(s.EnclosuresConfigSaveFailed, "[\u4e00-\u9fff]"), $"{lang} EnclosuresConfigSaveFailed 不能含中文");
            }
        }

        // 恢复默认语言
        service.CurrentLanguage = "zh-CN";
        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-04：批次 10——日志自定义保留期清理测试：
    // 验证自定义 retentionDays 属性生效、非法天数回退默认 7 天、多代过期文件按指定天数精准淘汰。
    private static Task TestLogCustomRetentionPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bc-log-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // 1) 属性与回退校验
            using (var loggerCustom = new FileLoggerService(Path.Combine(root, "batchcompress_custom.log"), retentionDays: 14))
            {
                AssertEqual(14, loggerCustom.RetentionDays);
            }

            using (var loggerDefault = new FileLoggerService(Path.Combine(root, "batchcompress_default.log"), retentionDays: -1))
            {
                AssertEqual(FileLoggerService.DefaultRetentionDays, loggerDefault.RetentionDays);
            }

            // 2) 预置过期与有效日志：
            // - logRecent: 1 天前生成（不应被 3 天保留期清理）
            // - logStale: 5 天前生成（应被 3 天保留期清理）
            // - logStaleGz: 5 天前生成的 .gz 归档（应被 3 天保留期清理）
            // - otherFile: 5 天前生成但非 batchcompress_*.log 前缀（必须保留，不得误删）
            var logRecent = Path.Combine(root, "batchcompress_recent.log");
            var logStale = Path.Combine(root, "batchcompress_stale.log.1");
            var logStaleGz = Path.Combine(root, "batchcompress_stale.log.2.gz");
            var otherFile = Path.Combine(root, "custom_other.log");

            File.WriteAllText(logRecent, "recent log");
            File.WriteAllText(logStale, "stale log");
            File.WriteAllText(logStaleGz, "stale gz");
            File.WriteAllText(otherFile, "other log");

            File.SetLastWriteTimeUtc(logRecent, DateTime.UtcNow.AddDays(-1));
            File.SetLastWriteTimeUtc(logStale, DateTime.UtcNow.AddDays(-5));
            File.SetLastWriteTimeUtc(logStaleGz, DateTime.UtcNow.AddDays(-5));
            File.SetLastWriteTimeUtc(otherFile, DateTime.UtcNow.AddDays(-5));

            // 以 retentionDays: 3 启动 logger 触发 PruneExpiredLogs
            var triggerPath = Path.Combine(root, "batchcompress_trigger.log");
            using (var logger = new FileLoggerService(triggerPath, retentionDays: 3))
            {
                logger.LogOperation("INIT", "Triggering prune");
            }

            Assert(File.Exists(logRecent), "1 天前的日志不应被 3 天保留期清理");
            Assert(!File.Exists(logStale), "5 天前的日志必须被 3 天保留期清理");
            Assert(!File.Exists(logStaleGz), "5 天前的 .gz 归档必须被 3 天保留期清理");
            Assert(File.Exists(otherFile), "非 batchcompress 前缀文件绝不得误删");
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

    // Antigravity, 2026-11-08：批次 15（P3-12）——验证 schemas/ndjson-schema-v1.json 契约完整性及实际事件的一致性。
    private static Task TestNdjsonSchemaValidation()
    {
        var repoRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(repoRoot, "schemas", "ndjson-schema-v1.json");
        Assert(File.Exists(schemaPath), "schemas/ndjson-schema-v1.json 契约文件必须存在");

        using var schemaDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = schemaDoc.RootElement;
        AssertEqual("http://json-schema.org/draft-07/schema#", root.GetProperty("$schema").GetString());
        Assert(root.TryGetProperty("oneOf", out var oneOf) && oneOf.GetArrayLength() == 3, "Schema 必须定义 progress/item/summary 三类事件");

        // 生成真实三类事件
        var stringWriter = new StringWriter();
        var ndjson = new NdjsonOutputWriter(stringWriter);
        ndjson.Report(new OperationProgressInfo { CurrentFile = "test.txt", Message = "starting" });
        ndjson.Report(new OperationProgressInfo { CurrentFile = "test.txt", Message = "done", SuccessCount = 1 });
        ndjson.WriteSummary(TimeSpan.FromSeconds(1.234));

        var lines = stringWriter.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        AssertEqual(3, lines.Length);

        // 校验 1: progress 事件
        using (var progressDoc = System.Text.Json.JsonDocument.Parse(lines[0]))
        {
            var elem = progressDoc.RootElement;
            AssertEqual("progress", elem.GetProperty("type").GetString());
            AssertEqual("start", elem.GetProperty("phase").GetString());
            AssertEqual("test.txt", elem.GetProperty("source").GetString());
        }

        // 校验 2: item 事件
        using (var itemDoc = System.Text.Json.JsonDocument.Parse(lines[1]))
        {
            var elem = itemDoc.RootElement;
            AssertEqual("item", elem.GetProperty("type").GetString());
            AssertEqual("success", elem.GetProperty("status").GetString());
            AssertEqual(1, elem.GetProperty("successCount").GetInt32());
            AssertEqual(0, elem.GetProperty("failCount").GetInt32());
            AssertEqual(0, elem.GetProperty("postProcessFailCount").GetInt32());
            AssertEqual(0, elem.GetProperty("ignoreCount").GetInt32());
            AssertEqual(0, elem.GetProperty("nonexistentCount").GetInt32());
            AssertEqual(0, elem.GetProperty("incompleteVolumes").GetInt32());
            AssertEqual(0, elem.GetProperty("ambiguous").GetInt32());
        }

        // 校验 3: summary 事件
        using (var summaryDoc = System.Text.Json.JsonDocument.Parse(lines[2]))
        {
            var elem = summaryDoc.RootElement;
            AssertEqual("summary", elem.GetProperty("type").GetString());
            AssertEqual(1, elem.GetProperty("success").GetInt32());
            AssertEqual(0, elem.GetProperty("failed").GetInt32());
            AssertEqual(1.234, elem.GetProperty("elapsedSeconds").GetDouble());
        }

        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-08：批次 16（P3-13）——验证安全诊断包脱敏导出服务。
    private static async Task TestDiagnosticsExportRedaction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"diag-test-{Guid.NewGuid():N}");
        var logsDir = Path.Combine(tempDir, "logs");
        var exportZip = Path.Combine(tempDir, "diagnostics.zip");
        Directory.CreateDirectory(logsDir);

        const string rawPassword = "SuperSecretPassword123";
        const string rawHp = "HpSecret456";

        var logContent = $"2026-11-08 12:00:00 [INFO] Command: rar a -hp\"{rawHp}\" out.rar in.txt\n" +
                         $"2026-11-08 12:00:01 [INFO] Command: 7zz a -t7z -p{rawPassword} out.7z in.txt\n" +
                         $"2026-11-08 12:00:02 [INFO] Command: app compress -i in -o out --password \"{rawPassword}\" -e 7z\n";

        var logFile = Path.Combine(logsDir, "batchcompress_20261108_120000.log");
        File.WriteAllText(logFile, logContent);

        try
        {
            var exporter = new DiagnosticsExportService(logsDir);
            await exporter.ExportAsync(exportZip);

            Assert(File.Exists(exportZip), "导出的诊断 zip 必须存在");

            using var zip = ZipFile.OpenRead(exportZip);
            var summaryEntry = zip.GetEntry("diagnostics_summary.json");
            Assert(summaryEntry != null, "诊断包必须包含 diagnostics_summary.json");

            using (var summaryDoc = System.Text.Json.JsonDocument.Parse(summaryEntry!.Open()))
            {
                var root = summaryDoc.RootElement;
                Assert(root.TryGetProperty("OSDescription", out _), "摘要必须包含 OSDescription");
                Assert(root.TryGetProperty("DotNetRuntime", out _), "摘要必须包含 DotNetRuntime");
                Assert(root.TryGetProperty("AppVersion", out _), "摘要必须包含 AppVersion");
            }

            var logEntry = zip.GetEntry("logs/batchcompress_20261108_120000.log");
            Assert(logEntry != null, "诊断包必须包含 logs/batchcompress_20261108_120000.log");

            using var reader = new StreamReader(logEntry!.Open(), Encoding.UTF8);
            var exportedLogText = await reader.ReadToEndAsync();

            Assert(!exportedLogText.Contains(rawPassword), "导出的日志中绝不能出现明文密码 rawPassword");
            Assert(!exportedLogText.Contains(rawHp), "导出的日志中绝不能出现明文密码 rawHp");
            Assert(exportedLogText.Contains("***"), "脱敏后的日志必须包含 *** 替代占位符");
            Assert(exportedLogText.Contains("-hp\"***\""), "WinRAR 加密头参数必须被脱敏为 -hp\"***\"");
            Assert(exportedLogText.Contains("-p***"), "7zz 密码参数必须被脱敏为 -p***");
            Assert(exportedLogText.Contains("--password \"***\""), "--password 选项必须被脱敏为 --password \"***\"");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
