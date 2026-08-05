using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BatchCompress.Avalonia;

// GPT-5, 2026-08-06：命令行数据对象覆盖 GUI 可脚本化的批处理能力。
// 密码可来自参数、文件或标准输入；下游不得把密码写入普通操作日志。
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
    public int CompressionLevel { get; set; } = 3;
    public bool Solid { get; set; } = true;
    public string? VolumeSize { get; set; }
    public string VolumeUnit { get; set; } = "g";
    public bool QuickOpen { get; set; }
    public bool TestArchive { get; set; }
    public string? CommentFile { get; set; }
    public string? TempDir { get; set; }
    public int RecoveryRecord { get; set; } = 3;
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
}

// GPT-5, 2026-08-06：解析结果显式携带错误，入口进程可用退出码 2 拒绝无效参数，
// 不会像旧实现那样在解析失败后继续使用默认值并意外启动 GUI。
public sealed class CommandLineParseOutcome
{
    public CommandLineOptions Options { get; init; } = new();
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool Success => Errors.Count == 0;
}

// GPT-5, 2026-08-06：集中定义命令行契约、兼容动词和跨选项验证。
// 旧的 --compress/--decompress 仍可使用，compress/extract 动词会在解析前规范为相同开关。
public static class CommandLineHandler
{
    private sealed class Definition
    {
        public RootCommand Root { get; } = new(
            "批量压缩解压工具。支持 compress、extract 和 GUI，归档格式为 rar、zip、7z。\n" +
            "示例: BatchCompress.Avalonia compress -i ./data -o ./out -e 7z --password-file ./password.txt");

        public Option<bool> Compress { get; } = new(["--compress", "-c"], "执行无界面批量压缩");
        public Option<bool> Decompress { get; } = new(["--decompress", "-d"], "执行无界面批量解压");
        public Option<bool> Gui { get; } = new(["--gui", "-g"], "显式启动图形界面");
        public Option<string?> Source { get; } = new(["--source", "-s"], "批处理来源目录，压缩时处理其直接子项");
        public Option<string[]> Inputs { get; } = new(
            ["--input", "-i"],
            () => [],
            "明确输入的文件或目录，可重复指定");
        public Option<string?> Output { get; } = new(["--output", "-o"], "输出目录");
        public Option<string?> TextFile { get; } = new(["--text-file", "-t"], "解压文件名与逐项密码 TXT 清单");
        public Option<string> Extension { get; } = new(
            ["--extension", "--format", "-e"],
            () => "rar",
            "归档格式：rar、zip、7z");
        public Option<bool> RandomPassword { get; } = new(
            ["--random-password", "-r"],
            () => true,
            "按归档文件名生成兼容随机密码（默认开启）");
        public Option<bool> NoRandomPassword { get; } = new(["--no-random-password"], "关闭按文件名生成密码");
        public Option<string?> Password { get; } = new(["--password", "-p"], "直接提供自定义密码");
        public Option<string?> PasswordFile { get; } = new(["--password-file"], "从文件第一行读取密码");
        public Option<bool> PasswordStdin { get; } = new(["--password-stdin"], "从标准输入第一行读取密码");
        public Option<int> Level { get; } = new(
            ["--level", "-l"],
            () => 3,
            "压缩级别：0 存储、1 最快、2 快速、3 标准、4 较好、5 最佳");
        public Option<bool> Solid { get; } = new(["--solid"], () => true, "启用固实压缩（默认开启）");
        public Option<bool> NoSolid { get; } = new(["--no-solid"], "关闭固实压缩");
        public Option<string?> VolumeSize { get; } = new(["--volume-size", "-v"], "分卷数值，例如 20");
        public Option<string> VolumeUnit { get; } = new(["--volume-unit"], () => "g", "分卷单位：b、k、m、g、t");
        public Option<bool> QuickOpen { get; } = new(["--quick-open"], "添加 RAR 快速打开信息");
        public Option<bool> Test { get; } = new(["--test"], "创建后校验归档");
        public Option<string?> Comment { get; } = new(["--comment"], "RAR/ZIP 注释文本文件");
        public Option<string?> TempDir { get; } = new(["--temp-dir"], "归档程序临时目录");
        public Option<int> Recovery { get; } = new(["--recovery"], () => 3, "RAR 恢复记录百分比：0 到 100");
        public Option<string> Existing { get; } = new(
            ["--existing"],
            () => "overwrite",
            "已有文件策略：skip、update、overwrite");
        public Option<bool> SkipProcessed { get; } = new(
            ["--skip-processed"],
            () => true,
            "跳过已处理项目（默认开启）");
        public Option<bool> NoSkipProcessed { get; } = new(["--no-skip-processed"], "不跳过已处理项目");
        public Option<bool> DeleteSource { get; } = new(["--delete-source"], "成功后删除源文件");
        public Option<bool> MoveSource { get; } = new(["--move-source"], "成功后移动源文件");
        public Option<double> MaxSize { get; } = new(["--max-size", "--max-size-gb"], () => 666, "最大处理总量（GB），0 表示不限");
        public Option<bool> Shutdown { get; } = new(["--shutdown"], "全部完成后请求关机");
        public Option<bool> AddEnclosures { get; } = new(["--add-enclosures"], () => true, "添加附件目录（默认开启）");
        public Option<bool> NoAddEnclosures { get; } = new(["--no-add-enclosures"], "关闭附件目录功能");
        public Option<string?> EnclosureList { get; } = new(["--enclosure-list"], "旧版兼容：换行分隔的附件目录");
        public Option<string[]> Enclosures { get; } = new(
            ["--enclosure"],
            () => [],
            "附件目录，可重复指定");
        public Option<string?> LogFile { get; } = new(["--log-file"], "日志文件路径");
        public Option<bool> Verbose { get; } = new(["--verbose"], "逐项输出详细进度");
        public Option<bool> Quiet { get; } = new(["--quiet", "-q"], "仅向 stderr 输出错误");
        public Option<bool> DryRun { get; } = new(["--dry-run"], "列出将处理的项目，不创建目录或归档");

        public Definition()
        {
            Inputs.AllowMultipleArgumentsPerToken = true;
            Enclosures.AllowMultipleArgumentsPerToken = true;

            Root.AddOption(Compress);
            Root.AddOption(Decompress);
            Root.AddOption(Gui);
            Root.AddOption(Source);
            Root.AddOption(Inputs);
            Root.AddOption(Output);
            Root.AddOption(TextFile);
            Root.AddOption(Extension);
            Root.AddOption(RandomPassword);
            Root.AddOption(NoRandomPassword);
            Root.AddOption(Password);
            Root.AddOption(PasswordFile);
            Root.AddOption(PasswordStdin);
            Root.AddOption(Level);
            Root.AddOption(Solid);
            Root.AddOption(NoSolid);
            Root.AddOption(VolumeSize);
            Root.AddOption(VolumeUnit);
            Root.AddOption(QuickOpen);
            Root.AddOption(Test);
            Root.AddOption(Comment);
            Root.AddOption(TempDir);
            Root.AddOption(Recovery);
            Root.AddOption(Existing);
            Root.AddOption(SkipProcessed);
            Root.AddOption(NoSkipProcessed);
            Root.AddOption(DeleteSource);
            Root.AddOption(MoveSource);
            Root.AddOption(MaxSize);
            Root.AddOption(Shutdown);
            Root.AddOption(AddEnclosures);
            Root.AddOption(NoAddEnclosures);
            Root.AddOption(EnclosureList);
            Root.AddOption(Enclosures);
            Root.AddOption(LogFile);
            Root.AddOption(Verbose);
            Root.AddOption(Quiet);
            Root.AddOption(DryRun);
        }
    }

    public static RootCommand BuildRootCommand() => new Definition().Root;

    public static CommandLineParseOutcome ParseArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var effectiveArgs = NormalizeVerb(args);
        var definition = new Definition();
        var parseResult = definition.Root.Parse(effectiveArgs);
        var errors = parseResult.Errors.Select(error => error.Message).ToList();

        if (errors.Count > 0)
        {
            return new CommandLineParseOutcome { Errors = errors };
        }

        var options = new CommandLineOptions
        {
            Compress = parseResult.GetValueForOption(definition.Compress),
            Decompress = parseResult.GetValueForOption(definition.Decompress),
            SourcePath = parseResult.GetValueForOption(definition.Source),
            InputPaths = parseResult.GetValueForOption(definition.Inputs) ?? [],
            OutputPath = parseResult.GetValueForOption(definition.Output),
            TextFile = parseResult.GetValueForOption(definition.TextFile),
            Extension = NormalizeFormat(parseResult.GetValueForOption(definition.Extension)),
            Password = parseResult.GetValueForOption(definition.Password),
            PasswordFile = parseResult.GetValueForOption(definition.PasswordFile),
            ReadPasswordFromStandardInput = parseResult.GetValueForOption(definition.PasswordStdin),
            CompressionLevel = parseResult.GetValueForOption(definition.Level),
            Solid = parseResult.GetValueForOption(definition.Solid) && !parseResult.GetValueForOption(definition.NoSolid),
            VolumeSize = parseResult.GetValueForOption(definition.VolumeSize),
            VolumeUnit = NormalizeVolumeUnit(parseResult.GetValueForOption(definition.VolumeUnit)),
            QuickOpen = parseResult.GetValueForOption(definition.QuickOpen),
            TestArchive = parseResult.GetValueForOption(definition.Test),
            CommentFile = parseResult.GetValueForOption(definition.Comment),
            TempDir = parseResult.GetValueForOption(definition.TempDir),
            RecoveryRecord = parseResult.GetValueForOption(definition.Recovery),
            ExistingFileMode = (parseResult.GetValueForOption(definition.Existing) ?? "overwrite").Trim().ToLowerInvariant(),
            SkipProcessed = parseResult.GetValueForOption(definition.SkipProcessed) && !parseResult.GetValueForOption(definition.NoSkipProcessed),
            DeleteSource = parseResult.GetValueForOption(definition.DeleteSource),
            MoveSource = parseResult.GetValueForOption(definition.MoveSource),
            MaxSizeGB = parseResult.GetValueForOption(definition.MaxSize),
            ShutdownAfter = parseResult.GetValueForOption(definition.Shutdown),
            AddEnclosures = parseResult.GetValueForOption(definition.AddEnclosures) && !parseResult.GetValueForOption(definition.NoAddEnclosures),
            EnclosureList = parseResult.GetValueForOption(definition.EnclosureList),
            EnclosurePaths = parseResult.GetValueForOption(definition.Enclosures) ?? [],
            LogFile = parseResult.GetValueForOption(definition.LogFile),
            Verbose = parseResult.GetValueForOption(definition.Verbose),
            Quiet = parseResult.GetValueForOption(definition.Quiet),
            DryRun = parseResult.GetValueForOption(definition.DryRun)
        };

        var explicitGui = parseResult.GetValueForOption(definition.Gui);
        options.Gui = effectiveArgs.Length == 0 || explicitGui;

        // GPT-5, 2026-08-06：直接密码、密码文件和标准输入都优先于随机密码派生。
        var explicitPasswordSourceCount = new[]
        {
            !string.IsNullOrEmpty(options.Password),
            !string.IsNullOrWhiteSpace(options.PasswordFile),
            options.ReadPasswordFromStandardInput
        }.Count(value => value);
        options.UseRandomPassword = parseResult.GetValueForOption(definition.RandomPassword) &&
                                    !parseResult.GetValueForOption(definition.NoRandomPassword) &&
                                    explicitPasswordSourceCount == 0;

        Validate(options, explicitGui, explicitPasswordSourceCount, effectiveArgs, errors);
        return new CommandLineParseOutcome { Options = options, Errors = errors };
    }

    public static bool IsHelpRequested(string[] args)
    {
        return args.Any(argument => argument is "--help" or "-h" or "-?" or "/?");
    }

    public static bool IsVersionRequested(string[] args)
    {
        return args.Any(argument => argument.Equals("--version", StringComparison.OrdinalIgnoreCase));
    }

    public static void ShowHelp()
    {
        BuildRootCommand().Invoke(["--help"]);
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

        if (options.Extension is not ("rar" or "zip" or "7z"))
        {
            errors.Add("--extension 仅支持 rar、zip、7z。");
        }

        if (options.ExistingFileMode is not ("skip" or "update" or "overwrite"))
        {
            errors.Add("--existing 仅支持 skip、update、overwrite。");
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
