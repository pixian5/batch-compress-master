using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia;

// GPT-5, 2026-08-06：命令行数据对象覆盖 GUI 可脚本化的批处理能力。
// 密码可来自参数、文件或标准输入；进程原始输出明确不做脱敏，便于诊断压缩程序行为。
public sealed class CommandLineOptions
{
    public bool Compress { get; set; }
    public bool Decompress { get; set; }
    public bool Gui { get; set; } = true;
    public string? SourcePath { get; set; }
    public string[] InputPaths { get; set; } = [];
    public string? OutputPath { get; set; }
    public string? TextFile { get; set; }
    public string Extension { get; set; } = "rar";
    public bool UseRandomPassword { get; set; } = true;
    public string? Password { get; set; }
    public string? PasswordFile { get; set; }
    public bool ReadPasswordFromStandardInput { get; set; }
    public string PasswordName { get; set; } = "archive";
    public int CompressionLevel { get; set; } = 3;
    public bool Solid { get; set; } = true;
    public string? VolumeSize { get; set; }
    public string VolumeUnit { get; set; } = "g";
    public bool QuickOpen { get; set; }
    public bool TestArchive { get; set; }
    public string? CommentFile { get; set; }
    public string? TempDir { get; set; }
    public int RecoveryRecord { get; set; } = 3;
    public bool LockArchive { get; set; }
    public string ExistingFileMode { get; set; } = "overwrite";
    public bool SkipProcessed { get; set; } = true;
    public bool DeleteSource { get; set; }
    public bool MoveSource { get; set; }
    public double MaxSizeGB { get; set; } = 666;
    public bool ShutdownAfter { get; set; }
    public bool AddEnclosures { get; set; } = true;
    public string? EnclosureList { get; set; }
    public string[] EnclosurePaths { get; set; } = [];
    public string? LogFile { get; set; }
    public bool Verbose { get; set; }
    public bool Quiet { get; set; }
    public bool DryRun { get; set; }

    // GLM-5.3, 2026-11-04：批次 3——CLI 界面语言与结构化输出格式。
    // Language 为 LocalizationService 的语言键（en-US 等区域变体在解析时归一为 en 等）；
    // OutputFormat 仅 text（人类可读）与 json（NDJSON 逐行事件流）。
    public string Language { get; set; } = "zh-CN";
    public string OutputFormat { get; set; } = "text";
}

// GPT-5, 2026-08-06：解析结果显式携带错误，入口进程可用退出码 2 拒绝无效参数，
// 不会在解析失败后继续使用默认值并意外启动 GUI。
public sealed class CommandLineParseOutcome
{
    public CommandLineOptions Options { get; init; } = new();
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool Success => Errors.Count == 0;
}

// GPT-5, 2026-08-06：使用项目内轻量解析器维护命令行契约，避免绑定 System.CommandLine 预览期 API。
// 旧的 --compress/--decompress 仍可使用，compress/extract 动词会在解析前规范为相同开关。
public static class CommandLineHandler
{
    private enum ValueKind
    {
        Flag,
        Single,
        Multiple
    }

    private sealed record OptionDefinition(string CanonicalName, ValueKind Kind);

    private sealed class ParseState
    {
        public CommandLineOptions Options { get; } = new();
        public List<string> Errors { get; } = [];
        public bool ExplicitGui { get; set; }
        public bool RandomPassword { get; set; } = true;
        public bool NoRandomPassword { get; set; }
        public bool NoSolid { get; set; }
        public bool NoSkipProcessed { get; set; }
        public bool NoAddEnclosures { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, OptionDefinition> Definitions =
        BuildDefinitions();

    private const string HelpText =
        """
        批量压缩解压工具

        用法:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <路径...> -o <输出目录> [选项]
          BatchCompress.Avalonia extract -i <归档...> -o <输出目录> [选项]

        模式:
          compress, --compress, -c       执行无界面批量压缩
          extract, decompress, --decompress, -d
                                        执行无界面批量解压
          gui, --gui, -g                 显式启动图形界面

        来源与输出:
          --source, -s <目录>            批处理来源目录，压缩时处理其直接子项
          --input, -i <路径...>          明确输入的文件或目录，可重复指定
          --output, -o <目录>            输出目录
          --text-file, -t <文件>         解压文件名与逐项密码 TXT 清单

        压缩参数:
          --extension, --format, -e      创建：rar、7z、zip、tar、gz、bz2、xz、wim；解压：7zz 可读取格式
          --level, -l <0..5>             压缩级别
          --solid / --no-solid           启用或关闭固实压缩
          --volume-size, -v <数字>       分卷数值
          --volume-unit <b|k|m|g|t>      分卷单位
          --recovery <0..100>            RAR 恢复记录百分比
          --quick-open                   添加 RAR 快速打开信息
          --test                         创建后校验归档
          --comment <文件>               RAR/ZIP 注释文本文件
          --temp-dir <目录>              归档程序临时目录

        密码:
          --random-password, -r          按归档文件名生成兼容随机密码，默认开启
          --no-random-password           关闭随机密码派生
          --password, -p <密码>          直接提供自定义密码
          --password-file <文件>         从文件第一行读取密码
          --password-stdin               从标准输入第一行读取密码
          --password-name <archive|base> 密码依据：a.rar（默认）或 a

        处理策略:
          --existing <skip|update|overwrite>
          --lock                         锁定 RAR 归档（不能与 --existing update 同时使用）
                                        已有文件策略
          --skip-processed / --no-skip-processed
                                        启用或关闭跳过已处理项目
          --delete-source                成功后删除源文件
          --move-source                  成功后移动源文件
          --max-size, --max-size-gb <GB> 最大处理总量，0 表示不限
          --shutdown                     全部完成后请求关机
          --add-enclosures / --no-add-enclosures
                                        启用或关闭附件目录
          --enclosure-list <文件>        旧版兼容：换行分隔的附件目录
          --enclosure <目录...>          附件目录，可重复指定

        日志:
          --log-file <文件>              日志文件路径
          --verbose                      输出详细进度
          --quiet, -q                    仅向 stderr 输出错误
          --dry-run                      列出将处理的项目，不创建目录或归档
          --help, -h, -?, /?             显示帮助
          --version                      显示版本

        语言与输出:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        界面与服务层消息语言（接受 en-US、ja-JP、de-DE
                                        等区域变体，自动归一），默认 zh-CN
          --output-format <text|json>   stdout 输出格式，默认 text；json 模式按
                                        NDJSON 逐行输出 progress/item/summary 事件，
                                        命令与进程原始输出（含密码）不进入 JSON 流，
                                        人类可读提示改写入 stderr

        示例:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    // GLM-5.3, 2026-11-04：批次 3——英文帮助。--help 可与 --language/--lang 组合输出对应语言。
    private const string EnglishHelpText =
        """
        Batch Compress & Extract Tool

        Usage:
          BatchCompress.Avalonia [gui]
          BatchCompress.Avalonia compress -i <paths...> -o <output dir> [options]
          BatchCompress.Avalonia extract -i <archives...> -o <output dir> [options]

        Modes:
          compress, --compress, -c       Run headless batch compression
          extract, decompress, --decompress, -d
                                        Run headless batch extraction
          gui, --gui, -g                 Launch the graphical interface explicitly

        Sources and output:
          --source, -s <dir>             Batch source directory; compression processes
                                        its direct children
          --input, -i <paths...>        Explicit files or directories, repeatable
          --output, -o <dir>             Output directory
          --text-file, -t <file>        Extraction TXT list of names and per-item passwords

        Compression:
          --extension, -e <format>      Create: rar, 7z, zip, tar, gz, bz2, xz, wim;
                                        extract: formats readable by 7zz
          --level, -l <0..5>            Compression level
          --solid / --no-solid          Enable or disable solid archiving
          --volume-size, -v <number>    Split volume size
          --volume-unit <b|k|m|g|t>     Split volume unit
          --recovery <0..100>           RAR recovery record percent
          --quick-open                   Add RAR quick open information
          --test                         Verify archives after creation
          --comment <file>               RAR/ZIP comment text file
          --temp-dir <dir>               Temporary directory for archiver

        Passwords:
          --random-password, -r          Derive legacy random password from archive
                                        name (default on)
          --no-random-password           Disable random password derivation
          --password, -p <password>      Provide a custom password directly
          --password-file <file>         Read the password from the first line
          --password-stdin               Read the password from standard input
          --password-name <archive|base> Derive from a.rar (default) or a

        Processing:
          --existing <skip|update|overwrite>
                                        Existing file policy
          --lock                         Lock RAR archives (conflicts with --existing update)
          --skip-processed / --no-skip-processed
                                        Enable or disable skipping processed items
          --delete-source                Delete sources after success
          --move-source                  Move sources after success
          --max-size, --max-size-gb <GB> Total size cap, 0 means unlimited
          --shutdown                     Request shutdown after completion
          --add-enclosures / --no-add-enclosures
                                        Enable or disable enclosure directories
          --enclosure-list <file>        Legacy: newline separated enclosure list
          --enclosure <dir...>           Enclosure directories, repeatable

        Logging:
          --log-file <file>              Log file path
          --verbose                      Print detailed progress
          --quiet, -q                    Only print errors to stderr
          --dry-run                      List items without creating anything
          --help, -h, -?, /?             Show help
          --version                      Show version

        Language and output:
          --language, --lang <zh-CN|zh-TW|en|ja|de>
                                        Language for UI and service messages
                                        (regional variants such as en-US, ja-JP and
                                        de-DE are normalized), default zh-CN
          --output-format <text|json>    stdout format, default text; json emits
                                        NDJSON progress/item/summary events per line,
                                        raw command/process output (with passwords)
                                        never enters the JSON stream and human
                                        readable hints go to stderr

        Examples:
          BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt
          BatchCompress.Avalonia extract -i ./archive.7z -o ./out --password-stdin
        """;

    public static CommandLineParseOutcome ParseArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var effectiveArgs = NormalizeVerb(args);
        var state = new ParseState();

        for (var index = 0; index < effectiveArgs.Length; index++)
        {
            var token = effectiveArgs[index];
            if (token == "--")
            {
                AddRemainingAsInputs(effectiveArgs, index + 1, state.Options.InputPaths, out var allInputs);
                state.Options.InputPaths = allInputs;
                break;
            }

            if (!TryReadOptionToken(token, out var name, out var inlineValue) ||
                !Definitions.TryGetValue(name, out var definition))
            {
                state.Errors.Add($"未知参数: {token}");
                continue;
            }

            if (definition.Kind == ValueKind.Flag)
            {
                ApplyFlag(definition.CanonicalName, ParseFlagValue(token, inlineValue, state.Errors), state);
                continue;
            }

            if (definition.Kind == ValueKind.Single)
            {
                var value = ReadSingleValue(effectiveArgs, ref index, token, inlineValue, state.Errors);
                if (value is not null)
                {
                    ApplySingleValue(definition.CanonicalName, value, state);
                }
                continue;
            }

            var values = ReadMultipleValues(effectiveArgs, ref index, token, inlineValue, state.Errors);
            if (values.Count > 0)
            {
                ApplyMultipleValues(definition.CanonicalName, values, state);
            }
        }

        var options = state.Options;
        options.Extension = NormalizeFormat(options.Extension);
        options.VolumeUnit = NormalizeVolumeUnit(options.VolumeUnit);
        options.ExistingFileMode = (options.ExistingFileMode ?? "overwrite").Trim().ToLowerInvariant();
        options.PasswordName = (options.PasswordName ?? "archive").Trim().ToLowerInvariant();
        options.Gui = effectiveArgs.Length == 0 || state.ExplicitGui;
        options.Solid = options.Solid && !state.NoSolid;
        options.SkipProcessed = options.SkipProcessed && !state.NoSkipProcessed;
        options.AddEnclosures = options.AddEnclosures && !state.NoAddEnclosures;

        var explicitPasswordSourceCount = new[]
        {
            !string.IsNullOrEmpty(options.Password),
            !string.IsNullOrWhiteSpace(options.PasswordFile),
            options.ReadPasswordFromStandardInput
        }.Count(value => value);
        options.UseRandomPassword = state.RandomPassword && !state.NoRandomPassword && explicitPasswordSourceCount == 0;
        if (ArchiveFormatCatalog.TryGet(options.Extension, out var selectedFormat) &&
            !selectedFormat.SupportsPassword)
        {
            // 非加密格式不能因为全局默认随机密码而被构造器误传 -p 参数。
            options.UseRandomPassword = false;
        }

        Validate(options, state.ExplicitGui, explicitPasswordSourceCount, effectiveArgs, state.Errors);
        return new CommandLineParseOutcome { Options = options, Errors = state.Errors };
    }

    public static bool IsHelpRequested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(argument => argument is "--help" or "-h" or "-?" or "/?");
    }

    public static bool IsVersionRequested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(argument => argument.Equals("--version", StringComparison.OrdinalIgnoreCase));
    }

    public static void ShowHelp()
    {
        Console.Out.WriteLine(HelpText);
    }

    /// <summary>
    /// 按指定语言输出帮助。language 为 LocalizationService 语言键（zh-CN/zh-TW/en/ja/de），
    /// 目前提供中英双语帮助文本（zh-TW 暂用简体文本），其余语言与未指定（null）默认中文。
    /// </summary>
    public static void ShowHelp(string? language)
    {
        var useChinese = string.IsNullOrEmpty(language) || language is "zh-CN" or "zh-TW";
        Console.Out.WriteLine(useChinese ? HelpText : EnglishHelpText);
    }

    /// <summary>
    /// 在完整解析前从原始参数预扫描 --language/--lang（支持 “--lang en” 与 “--lang=en” 两种形式），
    /// 返回归一化语言键；未指定或值无效时返回 null。供 --help 等不经过完整解析的路径使用。
    /// </summary>
    public static string? DetectLanguage(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equalsIndex = token.IndexOf('=');
            var name = (equalsIndex < 0 ? token : token[..equalsIndex]).Trim();
            if (!name.Equals("--language", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("--lang", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = equalsIndex < 0
                ? index + 1 < args.Length ? args[index + 1] : null
                : token[(equalsIndex + 1)..];
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // 预扫描不做错误报告：无效值按“未指定”处理，回退默认语言。
            return TryNormalizeLanguage(value, out var language) ? language : null;
        }

        return null;
    }

    // GLM-5.3, 2026-11-04：批次 3——接受 BCP-47 区域变体并归一到 LocalizationService 的语言键。
    private static string NormalizeLanguage(string value, List<string>? errors)
    {
        if (TryNormalizeLanguage(value, out var language))
        {
            return language;
        }

        errors?.Add($"--language 仅支持 zh-CN、zh-TW、en、ja、de（接受 en-US、ja-JP、de-DE 等区域变体）: {value}");
        return value.Trim();
    }

    private static bool TryNormalizeLanguage(string value, out string language)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "zh" or "zh-cn" or "zh-hans":
                language = "zh-CN";
                return true;
            case "zh-tw" or "zh-hant" or "tw":
                language = "zh-TW";
                return true;
            case "en" or "en-us":
                language = "en";
                return true;
            case "ja" or "ja-jp":
                language = "ja";
                return true;
            case "de" or "de-de":
                language = "de";
                return true;
            default:
                language = string.Empty;
                return false;
        }
    }

    private static string NormalizeOutputFormat(string value, List<string>? errors)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "text" or "json")
        {
            return normalized;
        }

        errors?.Add($"--output-format 仅支持 text、json: {value}");
        return "text";
    }

    private static IReadOnlyDictionary<string, OptionDefinition> BuildDefinitions()
    {
        var definitions = new Dictionary<string, OptionDefinition>(StringComparer.OrdinalIgnoreCase);

        Add(definitions, "--compress", ValueKind.Flag, "--compress", "-c");
        Add(definitions, "--decompress", ValueKind.Flag, "--decompress", "-d");
        Add(definitions, "--gui", ValueKind.Flag, "--gui", "-g");
        Add(definitions, "--source", ValueKind.Single, "--source", "-s");
        Add(definitions, "--input", ValueKind.Multiple, "--input", "-i");
        Add(definitions, "--output", ValueKind.Single, "--output", "-o");
        Add(definitions, "--text-file", ValueKind.Single, "--text-file", "-t");
        Add(definitions, "--extension", ValueKind.Single, "--extension", "--format", "-e");
        Add(definitions, "--random-password", ValueKind.Flag, "--random-password", "-r");
        Add(definitions, "--no-random-password", ValueKind.Flag, "--no-random-password");
        Add(definitions, "--password", ValueKind.Single, "--password", "-p");
        Add(definitions, "--password-file", ValueKind.Single, "--password-file");
        Add(definitions, "--password-stdin", ValueKind.Flag, "--password-stdin");
        Add(definitions, "--password-name", ValueKind.Single, "--password-name");
        Add(definitions, "--level", ValueKind.Single, "--level", "-l");
        Add(definitions, "--solid", ValueKind.Flag, "--solid");
        Add(definitions, "--no-solid", ValueKind.Flag, "--no-solid");
        Add(definitions, "--volume-size", ValueKind.Single, "--volume-size", "-v");
        Add(definitions, "--volume-unit", ValueKind.Single, "--volume-unit");
        Add(definitions, "--quick-open", ValueKind.Flag, "--quick-open");
        Add(definitions, "--test", ValueKind.Flag, "--test");
        Add(definitions, "--comment", ValueKind.Single, "--comment");
        Add(definitions, "--temp-dir", ValueKind.Single, "--temp-dir");
        Add(definitions, "--recovery", ValueKind.Single, "--recovery");
        Add(definitions, "--existing", ValueKind.Single, "--existing");
        Add(definitions, "--lock", ValueKind.Flag, "--lock");
        Add(definitions, "--skip-processed", ValueKind.Flag, "--skip-processed");
        Add(definitions, "--no-skip-processed", ValueKind.Flag, "--no-skip-processed");
        Add(definitions, "--delete-source", ValueKind.Flag, "--delete-source");
        Add(definitions, "--move-source", ValueKind.Flag, "--move-source");
        Add(definitions, "--max-size", ValueKind.Single, "--max-size", "--max-size-gb");
        Add(definitions, "--shutdown", ValueKind.Flag, "--shutdown");
        Add(definitions, "--add-enclosures", ValueKind.Flag, "--add-enclosures");
        Add(definitions, "--no-add-enclosures", ValueKind.Flag, "--no-add-enclosures");
        Add(definitions, "--enclosure-list", ValueKind.Single, "--enclosure-list");
        Add(definitions, "--enclosure", ValueKind.Multiple, "--enclosure");
        Add(definitions, "--log-file", ValueKind.Single, "--log-file");
        Add(definitions, "--verbose", ValueKind.Flag, "--verbose");
        Add(definitions, "--quiet", ValueKind.Flag, "--quiet", "-q");
        Add(definitions, "--dry-run", ValueKind.Flag, "--dry-run");
        // GLM-5.3, 2026-11-04：批次 3——语言与结构化输出。注意 --format 已是 --extension
        // 的历史别名，不能挪用为输出格式别名，输出格式仅使用 --output-format。
        Add(definitions, "--language", ValueKind.Single, "--language", "--lang");
        Add(definitions, "--output-format", ValueKind.Single, "--output-format");

        return definitions;
    }

    private static void Add(
        Dictionary<string, OptionDefinition> definitions,
        string canonicalName,
        ValueKind kind,
        params string[] aliases)
    {
        var definition = new OptionDefinition(canonicalName, kind);
        foreach (var alias in aliases)
        {
            definitions[alias] = definition;
        }
    }

    private static string[] NormalizeVerb(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        var mode = args[0].Trim().ToLowerInvariant();
        var replacement = mode switch
        {
            "compress" => "--compress",
            "extract" or "decompress" => "--decompress",
            "gui" => "--gui",
            _ => null
        };
        return replacement == null ? args : [replacement, .. args.Skip(1)];
    }

    private static bool TryReadOptionToken(string token, out string name, out string? inlineValue)
    {
        name = string.Empty;
        inlineValue = null;
        if (string.IsNullOrWhiteSpace(token) ||
            token == "-" ||
            !token.StartsWith("-", StringComparison.Ordinal) ||
            LooksLikeNegativeNumber(token))
        {
            return false;
        }

        var equalsIndex = token.IndexOf('=');
        if (equalsIndex < 0)
        {
            name = token;
            return true;
        }

        name = token[..equalsIndex];
        inlineValue = token[(equalsIndex + 1)..];
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool LooksLikeNegativeNumber(string token)
    {
        return token.Length > 1 && char.IsDigit(token[1]);
    }

    private static bool ParseFlagValue(string token, string? inlineValue, List<string> errors)
    {
        if (inlineValue is null)
        {
            return true;
        }

        if (bool.TryParse(inlineValue, out var value))
        {
            return value;
        }

        if (inlineValue is "1" or "yes" or "on")
        {
            return true;
        }

        if (inlineValue is "0" or "no" or "off")
        {
            return false;
        }

        errors.Add($"{token} 只能使用 true 或 false。");
        return true;
    }

    private static string? ReadSingleValue(
        string[] args,
        ref int index,
        string token,
        string? inlineValue,
        List<string> errors)
    {
        if (inlineValue is not null)
        {
            return inlineValue;
        }

        if (index + 1 >= args.Length || args[index + 1] == "--")
        {
            errors.Add($"{token} 缺少参数值。");
            return null;
        }

        index++;
        return args[index];
    }

    private static List<string> ReadMultipleValues(
        string[] args,
        ref int index,
        string token,
        string? inlineValue,
        List<string> errors)
    {
        var values = new List<string>();
        if (inlineValue is not null)
        {
            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                values.Add(inlineValue);
            }
        }
        else
        {
            while (index + 1 < args.Length && !IsOptionBoundary(args[index + 1]))
            {
                index++;
                values.Add(args[index]);
            }
        }

        if (values.Count == 0)
        {
            errors.Add($"{token} 缺少参数值。");
        }

        return values;
    }

    private static bool IsOptionBoundary(string token)
    {
        return token == "--" ||
               (TryReadOptionToken(token, out _, out _) && !LooksLikeNegativeNumber(token));
    }

    private static void ApplyFlag(string canonicalName, bool value, ParseState state)
    {
        switch (canonicalName)
        {
            case "--compress":
                state.Options.Compress = value;
                break;
            case "--decompress":
                state.Options.Decompress = value;
                break;
            case "--gui":
                state.ExplicitGui = value;
                break;
            case "--random-password":
                state.RandomPassword = value;
                break;
            case "--no-random-password":
                state.NoRandomPassword = value;
                break;
            case "--password-stdin":
                state.Options.ReadPasswordFromStandardInput = value;
                break;
            case "--solid":
                state.Options.Solid = value;
                break;
            case "--no-solid":
                state.NoSolid = value;
                break;
            case "--quick-open":
                state.Options.QuickOpen = value;
                break;
            case "--test":
                state.Options.TestArchive = value;
                break;
            case "--lock":
                state.Options.LockArchive = value;
                break;
            case "--skip-processed":
                state.Options.SkipProcessed = value;
                break;
            case "--no-skip-processed":
                state.NoSkipProcessed = value;
                break;
            case "--delete-source":
                state.Options.DeleteSource = value;
                break;
            case "--move-source":
                state.Options.MoveSource = value;
                break;
            case "--shutdown":
                state.Options.ShutdownAfter = value;
                break;
            case "--add-enclosures":
                state.Options.AddEnclosures = value;
                break;
            case "--no-add-enclosures":
                state.NoAddEnclosures = value;
                break;
            case "--verbose":
                state.Options.Verbose = value;
                break;
            case "--quiet":
                state.Options.Quiet = value;
                break;
            case "--dry-run":
                state.Options.DryRun = value;
                break;
        }
    }

    private static void ApplySingleValue(string canonicalName, string value, ParseState state)
    {
        switch (canonicalName)
        {
            case "--source":
                state.Options.SourcePath = value;
                break;
            case "--output":
                state.Options.OutputPath = value;
                break;
            case "--text-file":
                state.Options.TextFile = value;
                break;
            case "--extension":
                state.Options.Extension = value;
                break;
            case "--password":
                state.Options.Password = value;
                break;
            case "--password-file":
                state.Options.PasswordFile = value;
                break;
            case "--password-name":
                state.Options.PasswordName = value;
                break;
            case "--level":
                state.Options.CompressionLevel = ParseInt(value, "--level", state.Errors, 3);
                break;
            case "--volume-size":
                state.Options.VolumeSize = value;
                break;
            case "--volume-unit":
                state.Options.VolumeUnit = value;
                break;
            case "--comment":
                state.Options.CommentFile = value;
                break;
            case "--temp-dir":
                state.Options.TempDir = value;
                break;
            case "--recovery":
                state.Options.RecoveryRecord = ParseInt(value, "--recovery", state.Errors, 3);
                break;
            case "--existing":
                state.Options.ExistingFileMode = value;
                break;
            case "--lock":
                state.Options.LockArchive = true;
                break;
            case "--max-size":
                state.Options.MaxSizeGB = ParseDouble(value, "--max-size", state.Errors, 666);
                break;
            case "--enclosure-list":
                state.Options.EnclosureList = value;
                break;
            case "--log-file":
                state.Options.LogFile = value;
                break;
            case "--language":
                state.Options.Language = NormalizeLanguage(value, state.Errors);
                break;
            case "--output-format":
                state.Options.OutputFormat = NormalizeOutputFormat(value, state.Errors);
                break;
        }
    }

    private static void ApplyMultipleValues(string canonicalName, IEnumerable<string> values, ParseState state)
    {
        switch (canonicalName)
        {
            case "--input":
                state.Options.InputPaths = [.. state.Options.InputPaths, .. values];
                break;
            case "--enclosure":
                state.Options.EnclosurePaths = [.. state.Options.EnclosurePaths, .. values];
                break;
        }
    }

    private static int ParseInt(string value, string optionName, List<string> errors, int fallback)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{optionName} 必须是整数。");
        return fallback;
    }

    private static double ParseDouble(string value, string optionName, List<string> errors, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{optionName} 必须是数字。");
        return fallback;
    }

    private static void AddRemainingAsInputs(string[] args, int startIndex, string[] existing, out string[] inputs)
    {
        inputs = [.. existing, .. args.Skip(startIndex)];
    }

    private static void Validate(
        CommandLineOptions options,
        bool explicitGui,
        int explicitPasswordSourceCount,
        string[] effectiveArgs,
        List<string> errors)
    {
        if (options.Compress && options.Decompress)
        {
            errors.Add("压缩和解压模式不能同时指定。");
        }

        if (explicitGui && (options.Compress || options.Decompress))
        {
            errors.Add("--gui 不能与压缩或解压模式同时指定。");
        }

        var isHeadless = options.Compress || options.Decompress;
        if (!isHeadless && !explicitGui && effectiveArgs.Length > 0)
        {
            errors.Add("请指定 compress、extract、--compress、--decompress 或 --gui。");
        }

        if (!isHeadless)
        {
            return;
        }

        options.Gui = false;
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            errors.Add("无界面模式必须指定 --output。");
        }

        if (string.IsNullOrWhiteSpace(options.SourcePath) &&
            options.InputPaths.Length == 0 &&
            string.IsNullOrWhiteSpace(options.TextFile))
        {
            errors.Add("必须通过 --source、--input 或 --text-file 提供至少一个来源。");
        }

        if (options.Compress && !string.IsNullOrWhiteSpace(options.TextFile))
        {
            errors.Add("--text-file 是解压密码清单，只能用于 extract 模式。");
        }

        if (explicitPasswordSourceCount > 1)
        {
            errors.Add("--password、--password-file、--password-stdin 只能选择一种。");
        }

        if (options.CompressionLevel is < 0 or > 5)
        {
            errors.Add("--level 必须在 0 到 5 之间。");
        }

        if (options.RecoveryRecord is < 0 or > 100)
        {
            errors.Add("--recovery 必须在 0 到 100 之间。");
        }

        if (options.MaxSizeGB < 0)
        {
            errors.Add("--max-size 不能小于 0。");
        }

        if (!ArchiveFormatCatalog.TryGet(options.Extension, out var formatDefinition))
        {
            errors.Add($"--extension 不是已识别格式：{options.Extension}。可解压格式包括：{ArchiveFormatCatalog.ExtractFormatsText}。");
        }
        else if (options.Compress && !formatDefinition.CanCreate)
        {
            errors.Add($"--extension {options.Extension} 不支持创建；可创建格式包括：{ArchiveFormatCatalog.CreateFormatsText}。");
        }
        else if (options.Decompress && !formatDefinition.CanExtract)
        {
            errors.Add($"--extension {options.Extension} 不支持解压。");
        }

        if (options.Compress && formatDefinition is not null &&
            explicitPasswordSourceCount > 0 && !formatDefinition.SupportsPassword)
        {
            errors.Add($"{formatDefinition.Extension} 格式不支持密码加密，请移除密码参数。");
        }

        if (options.ExistingFileMode is not ("skip" or "update" or "overwrite"))
        {
            errors.Add("--existing 仅支持 skip、update、overwrite。");
        }
        if (options.PasswordName is not ("archive" or "base"))
        {
            errors.Add("--password-name 仅支持 archive、base。");
        }
        if (options.ExistingFileMode == "update" && options.LockArchive)
        {
            errors.Add("--existing update 不能与 --lock 同时使用。");
        }

        if (options.VolumeUnit is not ("b" or "k" or "m" or "g" or "t"))
        {
            errors.Add("--volume-unit 仅支持 b、k、m、g、t。");
        }

        if (!string.IsNullOrWhiteSpace(options.VolumeSize) &&
            (!double.TryParse(options.VolumeSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var volumeSize) || volumeSize <= 0))
        {
            errors.Add("--volume-size 必须是大于 0 的数字。");
        }

        if (options.DeleteSource && options.MoveSource)
        {
            errors.Add("--delete-source 和 --move-source 不能同时使用。");
        }

        if (options.Verbose && options.Quiet)
        {
            errors.Add("--verbose 和 --quiet 不能同时使用。");
        }

        ValidateExistingFile(options.TextFile, "--text-file", errors);
        ValidateExistingFile(options.PasswordFile, "--password-file", errors);
        ValidateExistingFile(options.CommentFile, "--comment", errors);
    }

    private static void ValidateExistingFile(string? path, string optionName, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
        {
            errors.Add($"{optionName} 指定的文件不存在: {path}");
        }
    }

    private static string NormalizeFormat(string? format)
    {
        return (format ?? "rar").Trim().TrimStart('.').ToLowerInvariant();
    }

    private static string NormalizeVolumeUnit(string? unit)
    {
        return (unit ?? "g").Trim().ToLowerInvariant() switch
        {
            "bytes" => "b",
            "kb" => "k",
            "mb" => "m",
            "gb" => "g",
            "tb" => "t",
            var value => value
        };
    }
}
