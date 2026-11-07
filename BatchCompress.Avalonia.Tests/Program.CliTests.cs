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
    // 命令行解析、错误校验、多语言帮助与无界面进度格式化测试


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

    // Antigravity, 2026-11-04：批次 10——CLI 日志保留天数参数解析与校验测试。
    private static Task TestCliLogRetentionOption()
    {
        // 1) 正常参数解析
        var result1 = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["compress", "-i", "a", "-o", "out", "-e", "7z", "--log-retention-days", "14"]);
        AssertEqual(0, result1.Errors.Count);
        AssertEqual(14, result1.Options.LogRetentionDays);

        // 2) 别名解析 --log-retention
        var result2 = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["compress", "-i", "a", "-o", "out", "-e", "7z", "--log-retention", "30"]);
        AssertEqual(0, result2.Errors.Count);
        AssertEqual(30, result2.Options.LogRetentionDays);

        // 3) 非整数报错
        var result3 = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["compress", "-i", "a", "-o", "out", "-e", "7z", "--log-retention-days", "abc"]);
        Assert(result3.Errors.Any(e => e.Contains("--log-retention-days") && e.Contains("整数")),
            $"非整数必须报错，实际 {string.Join(";", result3.Errors)}");

        // 4) 小于 1 报错
        var result4 = BatchCompress.Avalonia.CommandLineHandler.ParseArguments(["compress", "-i", "a", "-o", "out", "-e", "7z", "--log-retention-days", "0"]);
        Assert(result4.Errors.Any(e => e.Contains("--log-retention-days")),
            $"<= 0 必须触发校验错误，实际 {string.Join(";", result4.Errors)}");

        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-04：批次 11——标准输入密码读取失败文案多语言完整性测试。
    private static Task TestPasswordReadErrorLocalization()
    {
        var languages = new[] { "zh-CN", "zh-TW", "en", "ja", "de" };
        var service = LocalizationService.Instance;

        foreach (var lang in languages)
        {
            service.CurrentLanguage = lang;
            var s = service.Strings;

            Assert(!string.IsNullOrWhiteSpace(s.CliErrorPasswordReadFailed),
                $"{lang} CliErrorPasswordReadFailed 不能为空");

            if (lang is "en" or "de")
            {
                Assert(!Regex.IsMatch(s.CliErrorPasswordReadFailed, "[\u4e00-\u9fff]"),
                    $"{lang} CliErrorPasswordReadFailed 不能包含中文字符，实际: {s.CliErrorPasswordReadFailed}");
            }
        }

        service.CurrentLanguage = "zh-CN";
        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-04 / 2026-11-07：批次 12/14——CLI 五语言帮助文本获取、零中文断言、宽度计算与自动折行。
    private static Task TestCliHelpTextWrapping()
    {
        // 1) 帮助文本获取与各语言特征测试
        var zhCn = BatchCompress.Avalonia.CommandLineHandler.GetHelpText(null);
        Assert(zhCn.Contains("批量压缩解压工具"), "默认帮助应为简体中文");
        Assert(zhCn.Contains("--lock"), "简体帮助应包含 --lock 选项");

        var zhTw = BatchCompress.Avalonia.CommandLineHandler.GetHelpText("zh-TW");
        Assert(zhTw.Contains("批次壓縮解壓工具"), "zh-TW 帮助应为正体中文");

        var en = BatchCompress.Avalonia.CommandLineHandler.GetHelpText("en");
        Assert(en.Contains("Batch Compress & Extract Tool"), "en 帮助应为英文");

        var ja = BatchCompress.Avalonia.CommandLineHandler.GetHelpText("ja");
        Assert(ja.Contains("バッチ圧縮解凍ツール"), "ja 帮助应为日文");
        Assert(ja.Contains("--quick-open"), "ja 帮助应包含 --quick-open 选项");

        var de = BatchCompress.Avalonia.CommandLineHandler.GetHelpText("de");
        Assert(de.Contains("Batch-Komprimierungs- und Entpackungstool"), "de 帮助应为德文");
        Assert(de.Contains("--volume-size"), "de 帮助应包含 --volume-size 选项");

        // 德文帮助严格零中文字符断言
        var deChineseCount = de.Count(c => c >= 0x4e00 && c <= 0x9fff);
        AssertEqual(0, deChineseCount);

        // 2) 显示宽度计算（中英日混合与全半角字符）
        AssertEqual(7, BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth("abc 123"));
        AssertEqual(8, BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth("测试文本"));
        AssertEqual(15, BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth("CLI 参数 --lang"));
        // 日文片假名（全角 2 列）
        AssertEqual(6, BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth("バッチ"));
        AssertEqual(10, BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth("テスト abc"));

        // 3) 折行验证（maxWidth = 60 和 80，覆盖 en, zh-CN, ja, de）
        foreach (var width in new[] { 60, 80 })
        {
            foreach (var (langName, text) in new[] { ("en", en), ("zh-CN", zhCn), ("ja", ja), ("de", de) })
            {
                var wrapped = BatchCompress.Avalonia.CommandLineHandler.WrapHelpText(text, width);
                var lines = wrapped.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var displayWidth = BatchCompress.Avalonia.CommandLineHandler.GetDisplayWidth(line);
                    Assert(displayWidth <= width, $"{langName} 折行后行宽 {displayWidth} 超过限制 {width}：'{line}'");
                }
                Assert(wrapped.Contains("--shutdown"), $"{langName} 折行后不应丢失 --shutdown 选项");
                Assert(wrapped.Contains("--concurrency"), $"{langName} 折行后不应丢失 --concurrency 选项");
                Assert(wrapped.Contains("--existing"), $"{langName} 折行后不应丢失 --existing 选项");
            }
        }

        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-07：批次 14——验证全部 5 种语言的 CLI 帮助文本均包含完整 40+ 项选项名。
    private static Task TestAllHelpTextsContainAllOptions()
    {
        string[] requiredOptionTokens =
        [
            "compress",
            "extract",
            "gui",
            "--source",
            "--input",
            "--output",
            "--text-file",
            "--extension",
            "--level",
            "--solid",
            "--no-solid",
            "--volume-size",
            "--volume-unit",
            "--recovery",
            "--quick-open",
            "--test",
            "--comment",
            "--temp-dir",
            "--random-password",
            "--no-random-password",
            "--password",
            "--password-file",
            "--password-stdin",
            "--password-name",
            "--existing",
            "--lock",
            "--skip-processed",
            "--no-skip-processed",
            "--delete-source",
            "--move-source",
            "--max-size",
            "--shutdown",
            "--concurrency",
            "--add-enclosures",
            "--no-add-enclosures",
            "--enclosure",
            "--log-file",
            "--log-retention-days",
            "--verbose",
            "--quiet",
            "--dry-run",
            "--help",
            "--version",
            "--language",
            "--output-format"
        ];

        string[] languages = ["zh-CN", "zh-TW", "en", "ja", "de"];

        foreach (var lang in languages)
        {
            var helpText = BatchCompress.Avalonia.CommandLineHandler.GetHelpText(lang);
            Assert(!string.IsNullOrWhiteSpace(helpText), $"{lang} 帮助文本不应为空");

            foreach (var token in requiredOptionTokens)
            {
                Assert(helpText.Contains(token),
                    $"语言 [{lang}] 的 CLI 帮助文本缺少必要选项名 token: '{token}'");
            }
        }

        return Task.CompletedTask;
    }

    // Antigravity, 2026-11-04：批次 12——无界面批处理进度单行刷新格式测试。
    private static Task TestHeadlessProgressFormatting()
    {
        // 1) 无 CurrentFile 时的单行格式（严格保持既有前缀与计数）
        var infoWithoutFile = new OperationProgressInfo
        {
            SuccessCount = 2,
            FailCount = 1,
            PostProcessFailCount = 0,
            IgnoreCount = 3
        };
        var line1 = BatchCompress.Avalonia.HeadlessBatchRunner.FormatProgressLine(infoWithoutFile);
        AssertEqual("\rProcessed: 2 success, 1 failed, 0 post-process failed, 3 skipped    ", line1);

        // 2) 携带 CurrentFile 时的单行格式（追加文件名与分隔符）
        var infoWithFile = new OperationProgressInfo
        {
            SuccessCount = 5,
            FailCount = 0,
            PostProcessFailCount = 1,
            IgnoreCount = 2,
            CurrentFile = "/path/to/archive.tar.gz"
        };
        var line2 = BatchCompress.Avalonia.HeadlessBatchRunner.FormatProgressLine(infoWithFile);
        Assert(line2.StartsWith("\rProcessed: 5 success, 0 failed, 1 post-process failed, 2 skipped | archive.tar.gz"),
            $"应包含文件名后缀，实际为: {line2}");

        return Task.CompletedTask;
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

}
