using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Services;

namespace BatchCompress.Avalonia;

/// <summary>
/// Command-line options for batch compression/decompression
/// </summary>
public class CommandLineOptions
{
    // Mode options
    public bool Compress { get; set; }
    public bool Decompress { get; set; }
    public bool Gui { get; set; } = true;

    // Source options
    public string? SourcePath { get; set; }
    public string? OutputPath { get; set; }
    public string? TextFile { get; set; }
    public string Extension { get; set; } = "rar";

    // Password options
    public bool UseRandomPassword { get; set; } = true;
    public string? Password { get; set; }

    // Compression options
    public int CompressionLevel { get; set; } = 3;
    public bool Solid { get; set; } = true;
    public string? VolumeSize { get; set; }
    public string VolumeUnit { get; set; } = "g";
    public bool QuickOpen { get; set; }
    public bool TestArchive { get; set; }
    public string? CommentFile { get; set; }
    public string? TempDir { get; set; }
    public int RecoveryRecord { get; set; } = 3;

    // File handling options
    public string ExistingFileMode { get; set; } = "overwrite";
    public bool SkipProcessed { get; set; } = true;
    public bool DeleteSource { get; set; }
    public bool MoveSource { get; set; }
    public double MaxSizeGB { get; set; } = 666;
    public bool ShutdownAfter { get; set; }

    // Enclosure options
    public bool AddEnclosures { get; set; } = true;
    public string? EnclosureList { get; set; }

    // Logging options
    public string? LogFile { get; set; }
    public bool Verbose { get; set; }
}

/// <summary>
/// Handler for command-line operations
/// </summary>
public static class CommandLineHandler
{
    /// <summary>
    /// Build the root command with all options
    /// </summary>
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("BatchCompress - Batch compression and decompression tool");

        // Mode options
        var compressOption = new Option<bool>(
            aliases: new[] { "--compress", "-c" },
            description: "Run in compress mode (headless)");

        var decompressOption = new Option<bool>(
            aliases: new[] { "--decompress", "-d" },
            description: "Run in decompress mode (headless)");

        var guiOption = new Option<bool>(
            aliases: new[] { "--gui", "-g" },
            getDefaultValue: () => true,
            description: "Run with graphical user interface (default: true)");

        // Source options
        var sourcePathOption = new Option<string?>(
            aliases: new[] { "--source", "-s" },
            description: "Source folder path for compression/decompression");

        var outputPathOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output folder path");

        var textFileOption = new Option<string?>(
            aliases: new[] { "--text-file", "-t" },
            description: "Text file containing file list with passwords");

        var extensionOption = new Option<string>(
            aliases: new[] { "--extension", "-e" },
            getDefaultValue: () => "rar",
            description: "Archive extension (rar, zip, 7z)");

        // Password options
        var useRandomPasswordOption = new Option<bool>(
            aliases: new[] { "--random-password", "-r" },
            getDefaultValue: () => true,
            description: "Use random password based on filename");

        var passwordOption = new Option<string?>(
            aliases: new[] { "--password", "-p" },
            description: "Custom password for archive (disables random password)");

        // Compression options
        var compressionLevelOption = new Option<int>(
            aliases: new[] { "--level", "-l" },
            getDefaultValue: () => 3,
            description: "Compression level (0=store, 1=fastest, 2=fast, 3=normal, 4=good, 5=best)");

        var solidOption = new Option<bool>(
            aliases: new[] { "--solid" },
            getDefaultValue: () => true,
            description: "Create solid archive");

        var volumeSizeOption = new Option<string?>(
            aliases: new[] { "--volume-size", "-v" },
            description: "Volume size for split archives (e.g., '20')");

        var volumeUnitOption = new Option<string>(
            aliases: new[] { "--volume-unit" },
            getDefaultValue: () => "g",
            description: "Volume size unit (g=GB, m=MB, k=KB)");

        var quickOpenOption = new Option<bool>(
            aliases: new[] { "--quick-open" },
            description: "Enable quick open for archive");

        var testArchiveOption = new Option<bool>(
            aliases: new[] { "--test" },
            description: "Test archive after creation");

        var commentFileOption = new Option<string?>(
            aliases: new[] { "--comment" },
            description: "Path to comment file");

        var tempDirOption = new Option<string?>(
            aliases: new[] { "--temp-dir" },
            description: "Temporary directory for operations");

        var recoveryRecordOption = new Option<int>(
            aliases: new[] { "--recovery" },
            getDefaultValue: () => 3,
            description: "Recovery record percentage (0-10)");

        // File handling options
        var existingFileModeOption = new Option<string>(
            aliases: new[] { "--existing" },
            getDefaultValue: () => "overwrite",
            description: "How to handle existing files (skip, update, overwrite)");

        var skipProcessedOption = new Option<bool>(
            aliases: new[] { "--skip-processed" },
            getDefaultValue: () => true,
            description: "Skip already processed files");

        var deleteSourceOption = new Option<bool>(
            aliases: new[] { "--delete-source" },
            description: "Delete source after successful operation");

        var moveSourceOption = new Option<bool>(
            aliases: new[] { "--move-source" },
            description: "Move source to processed folder after operation");

        var maxSizeOption = new Option<double>(
            aliases: new[] { "--max-size" },
            getDefaultValue: () => 666,
            description: "Maximum total size in GB before stopping");

        var shutdownOption = new Option<bool>(
            aliases: new[] { "--shutdown" },
            description: "Shutdown computer after completion");

        // Enclosure options
        var addEnclosuresOption = new Option<bool>(
            aliases: new[] { "--add-enclosures" },
            getDefaultValue: () => true,
            description: "Add enclosure directories to archives");

        var enclosureListOption = new Option<string?>(
            aliases: new[] { "--enclosure-list" },
            description: "Newline-separated list of enclosure directory names");

        // Logging options
        var logFileOption = new Option<string?>(
            aliases: new[] { "--log-file" },
            description: "Path to log file (default: logs/batchcompress_timestamp.log)");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose" },
            description: "Enable verbose logging");

        // Add all options to the root command
        rootCommand.AddOption(compressOption);
        rootCommand.AddOption(decompressOption);
        rootCommand.AddOption(guiOption);
        rootCommand.AddOption(sourcePathOption);
        rootCommand.AddOption(outputPathOption);
        rootCommand.AddOption(textFileOption);
        rootCommand.AddOption(extensionOption);
        rootCommand.AddOption(useRandomPasswordOption);
        rootCommand.AddOption(passwordOption);
        rootCommand.AddOption(compressionLevelOption);
        rootCommand.AddOption(solidOption);
        rootCommand.AddOption(volumeSizeOption);
        rootCommand.AddOption(volumeUnitOption);
        rootCommand.AddOption(quickOpenOption);
        rootCommand.AddOption(testArchiveOption);
        rootCommand.AddOption(commentFileOption);
        rootCommand.AddOption(tempDirOption);
        rootCommand.AddOption(recoveryRecordOption);
        rootCommand.AddOption(existingFileModeOption);
        rootCommand.AddOption(skipProcessedOption);
        rootCommand.AddOption(deleteSourceOption);
        rootCommand.AddOption(moveSourceOption);
        rootCommand.AddOption(maxSizeOption);
        rootCommand.AddOption(shutdownOption);
        rootCommand.AddOption(addEnclosuresOption);
        rootCommand.AddOption(enclosureListOption);
        rootCommand.AddOption(logFileOption);
        rootCommand.AddOption(verboseOption);

        return rootCommand;
    }

    /// <summary>
    /// Parse command-line arguments into options
    /// </summary>
    public static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();
        var rootCommand = BuildRootCommand();

        rootCommand.SetHandler((InvocationContext context) =>
        {
            var parseResult = context.ParseResult;

            options.Compress = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--compress")) as Option<bool>);
            options.Decompress = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--decompress")) as Option<bool>);
            options.Gui = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--gui")) as Option<bool>);
            options.SourcePath = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--source")) as Option<string?>);
            options.OutputPath = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--output")) as Option<string?>);
            options.TextFile = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--text-file")) as Option<string?>);
            options.Extension = parseResult.GetValueForOption<string>(
                rootCommand.Options.First(o => o.HasAlias("--extension")) as Option<string>) ?? "rar";
            options.UseRandomPassword = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--random-password")) as Option<bool>);
            options.Password = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--password")) as Option<string?>);
            options.CompressionLevel = parseResult.GetValueForOption<int>(
                rootCommand.Options.First(o => o.HasAlias("--level")) as Option<int>);
            options.Solid = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--solid")) as Option<bool>);
            options.VolumeSize = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--volume-size")) as Option<string?>);
            options.VolumeUnit = parseResult.GetValueForOption<string>(
                rootCommand.Options.First(o => o.HasAlias("--volume-unit")) as Option<string>) ?? "g";
            options.QuickOpen = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--quick-open")) as Option<bool>);
            options.TestArchive = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--test")) as Option<bool>);
            options.CommentFile = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--comment")) as Option<string?>);
            options.TempDir = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--temp-dir")) as Option<string?>);
            options.RecoveryRecord = parseResult.GetValueForOption<int>(
                rootCommand.Options.First(o => o.HasAlias("--recovery")) as Option<int>);
            options.ExistingFileMode = parseResult.GetValueForOption<string>(
                rootCommand.Options.First(o => o.HasAlias("--existing")) as Option<string>) ?? "overwrite";
            options.SkipProcessed = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--skip-processed")) as Option<bool>);
            options.DeleteSource = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--delete-source")) as Option<bool>);
            options.MoveSource = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--move-source")) as Option<bool>);
            options.MaxSizeGB = parseResult.GetValueForOption<double>(
                rootCommand.Options.First(o => o.HasAlias("--max-size")) as Option<double>);
            options.ShutdownAfter = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--shutdown")) as Option<bool>);
            options.AddEnclosures = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--add-enclosures")) as Option<bool>);
            options.EnclosureList = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--enclosure-list")) as Option<string?>);
            options.LogFile = parseResult.GetValueForOption<string?>(
                rootCommand.Options.First(o => o.HasAlias("--log-file")) as Option<string?>);
            options.Verbose = parseResult.GetValueForOption<bool>(
                rootCommand.Options.First(o => o.HasAlias("--verbose")) as Option<bool>);
        });

        rootCommand.Invoke(args);

        // If password is specified, disable random password
        if (!string.IsNullOrEmpty(options.Password))
        {
            options.UseRandomPassword = false;
        }

        // If compress or decompress is specified, disable GUI
        if (options.Compress || options.Decompress)
        {
            options.Gui = false;
        }

        return options;
    }

    /// <summary>
    /// Check if help is requested
    /// </summary>
    public static bool IsHelpRequested(string[] args)
    {
        return args.Any(a => a == "--help" || a == "-h" || a == "-?" || a == "/?");
    }

    /// <summary>
    /// Show help information
    /// </summary>
    public static void ShowHelp()
    {
        var rootCommand = BuildRootCommand();
        rootCommand.Invoke(new[] { "--help" });
    }
}
