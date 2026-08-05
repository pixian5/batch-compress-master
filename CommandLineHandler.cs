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
    // Cached option references for efficient parsing
    private static Option<bool>? _compressOption;
    private static Option<bool>? _decompressOption;
    private static Option<bool>? _guiOption;
    private static Option<string?>? _sourcePathOption;
    private static Option<string?>? _outputPathOption;
    private static Option<string?>? _textFileOption;
    private static Option<string>? _extensionOption;
    private static Option<bool>? _useRandomPasswordOption;
    private static Option<string?>? _passwordOption;
    private static Option<int>? _compressionLevelOption;
    private static Option<bool>? _solidOption;
    private static Option<string?>? _volumeSizeOption;
    private static Option<string>? _volumeUnitOption;
    private static Option<bool>? _quickOpenOption;
    private static Option<bool>? _testArchiveOption;
    private static Option<string?>? _commentFileOption;
    private static Option<string?>? _tempDirOption;
    private static Option<int>? _recoveryRecordOption;
    private static Option<string>? _existingFileModeOption;
    private static Option<bool>? _skipProcessedOption;
    private static Option<bool>? _deleteSourceOption;
    private static Option<bool>? _moveSourceOption;
    private static Option<double>? _maxSizeOption;
    private static Option<bool>? _shutdownOption;
    private static Option<bool>? _addEnclosuresOption;
    private static Option<string?>? _enclosureListOption;
    private static Option<string?>? _logFileOption;
    private static Option<bool>? _verboseOption;

    /// <summary>
    /// Build the root command with all options
    /// </summary>
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("BatchCompress - Batch compression and decompression tool");

        // Mode options
        _compressOption = new Option<bool>(
            aliases: new[] { "--compress", "-c" },
            description: "Run in compress mode (headless)");

        _decompressOption = new Option<bool>(
            aliases: new[] { "--decompress", "-d" },
            description: "Run in decompress mode (headless)");

        _guiOption = new Option<bool>(
            aliases: new[] { "--gui", "-g" },
            getDefaultValue: () => true,
            description: "Run with graphical user interface (default: true)");

        // Source options
        _sourcePathOption = new Option<string?>(
            aliases: new[] { "--source", "-s" },
            description: "Source folder path for compression/decompression");

        _outputPathOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output folder path");

        _textFileOption = new Option<string?>(
            aliases: new[] { "--text-file", "-t" },
            description: "Text file containing file list with passwords");

        _extensionOption = new Option<string>(
            aliases: new[] { "--extension", "-e" },
            getDefaultValue: () => "rar",
            description: "Archive extension (rar, zip; 7z is extract-only)");

        // Password options
        _useRandomPasswordOption = new Option<bool>(
            aliases: new[] { "--random-password", "-r" },
            getDefaultValue: () => true,
            description: "Use random password based on filename");

        _passwordOption = new Option<string?>(
            aliases: new[] { "--password", "-p" },
            description: "Custom password for archive (disables random password)");

        // Compression options
        _compressionLevelOption = new Option<int>(
            aliases: new[] { "--level", "-l" },
            getDefaultValue: () => 3,
            description: "Compression level (0=store, 1=fastest, 2=fast, 3=normal, 4=good, 5=best)");

        _solidOption = new Option<bool>(
            aliases: new[] { "--solid" },
            getDefaultValue: () => true,
            description: "Create solid archive");

        _volumeSizeOption = new Option<string?>(
            aliases: new[] { "--volume-size", "-v" },
            description: "Volume size for split archives (e.g., '20')");

        _volumeUnitOption = new Option<string>(
            aliases: new[] { "--volume-unit" },
            getDefaultValue: () => "g",
            description: "Volume size unit (g=GB, m=MB, k=KB)");

        _quickOpenOption = new Option<bool>(
            aliases: new[] { "--quick-open" },
            description: "Enable quick open for archive");

        _testArchiveOption = new Option<bool>(
            aliases: new[] { "--test" },
            description: "Test archive after creation");

        _commentFileOption = new Option<string?>(
            aliases: new[] { "--comment" },
            description: "Path to comment file");

        _tempDirOption = new Option<string?>(
            aliases: new[] { "--temp-dir" },
            description: "Temporary directory for operations");

        _recoveryRecordOption = new Option<int>(
            aliases: new[] { "--recovery" },
            getDefaultValue: () => 3,
            description: "Recovery record percentage (0-10)");

        // File handling options
        _existingFileModeOption = new Option<string>(
            aliases: new[] { "--existing" },
            getDefaultValue: () => "overwrite",
            description: "How to handle existing files (skip, update, overwrite)");

        _skipProcessedOption = new Option<bool>(
            aliases: new[] { "--skip-processed" },
            getDefaultValue: () => true,
            description: "Skip already processed files");

        _deleteSourceOption = new Option<bool>(
            aliases: new[] { "--delete-source" },
            description: "Delete source after successful operation");

        _moveSourceOption = new Option<bool>(
            aliases: new[] { "--move-source" },
            description: "Move source to processed folder after operation");

        _maxSizeOption = new Option<double>(
            aliases: new[] { "--max-size" },
            getDefaultValue: () => 666,
            description: "Maximum total size in GB before stopping");

        _shutdownOption = new Option<bool>(
            aliases: new[] { "--shutdown" },
            description: "Shutdown computer after completion");

        // Enclosure options
        _addEnclosuresOption = new Option<bool>(
            aliases: new[] { "--add-enclosures" },
            getDefaultValue: () => true,
            description: "Add enclosure directories to archives");

        _enclosureListOption = new Option<string?>(
            aliases: new[] { "--enclosure-list" },
            description: "Newline-separated list of enclosure directory names");

        // Logging options
        _logFileOption = new Option<string?>(
            aliases: new[] { "--log-file" },
            description: "Path to log file (default: logs/batchcompress_timestamp.log)");

        _verboseOption = new Option<bool>(
            aliases: new[] { "--verbose" },
            description: "Enable verbose logging");

        // Add all options to the root command
        rootCommand.AddOption(_compressOption);
        rootCommand.AddOption(_decompressOption);
        rootCommand.AddOption(_guiOption);
        rootCommand.AddOption(_sourcePathOption);
        rootCommand.AddOption(_outputPathOption);
        rootCommand.AddOption(_textFileOption);
        rootCommand.AddOption(_extensionOption);
        rootCommand.AddOption(_useRandomPasswordOption);
        rootCommand.AddOption(_passwordOption);
        rootCommand.AddOption(_compressionLevelOption);
        rootCommand.AddOption(_solidOption);
        rootCommand.AddOption(_volumeSizeOption);
        rootCommand.AddOption(_volumeUnitOption);
        rootCommand.AddOption(_quickOpenOption);
        rootCommand.AddOption(_testArchiveOption);
        rootCommand.AddOption(_commentFileOption);
        rootCommand.AddOption(_tempDirOption);
        rootCommand.AddOption(_recoveryRecordOption);
        rootCommand.AddOption(_existingFileModeOption);
        rootCommand.AddOption(_skipProcessedOption);
        rootCommand.AddOption(_deleteSourceOption);
        rootCommand.AddOption(_moveSourceOption);
        rootCommand.AddOption(_maxSizeOption);
        rootCommand.AddOption(_shutdownOption);
        rootCommand.AddOption(_addEnclosuresOption);
        rootCommand.AddOption(_enclosureListOption);
        rootCommand.AddOption(_logFileOption);
        rootCommand.AddOption(_verboseOption);

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

            // Use cached options for efficient parsing
            options.Compress = parseResult.GetValueForOption(_compressOption!);
            options.Decompress = parseResult.GetValueForOption(_decompressOption!);
            options.Gui = parseResult.GetValueForOption(_guiOption!);
            options.SourcePath = parseResult.GetValueForOption(_sourcePathOption!);
            options.OutputPath = parseResult.GetValueForOption(_outputPathOption!);
            options.TextFile = parseResult.GetValueForOption(_textFileOption!);
            options.Extension = parseResult.GetValueForOption(_extensionOption!) ?? "rar";
            options.UseRandomPassword = parseResult.GetValueForOption(_useRandomPasswordOption!);
            options.Password = parseResult.GetValueForOption(_passwordOption!);
            options.CompressionLevel = parseResult.GetValueForOption(_compressionLevelOption!);
            options.Solid = parseResult.GetValueForOption(_solidOption!);
            options.VolumeSize = parseResult.GetValueForOption(_volumeSizeOption!);
            options.VolumeUnit = parseResult.GetValueForOption(_volumeUnitOption!) ?? "g";
            options.QuickOpen = parseResult.GetValueForOption(_quickOpenOption!);
            options.TestArchive = parseResult.GetValueForOption(_testArchiveOption!);
            options.CommentFile = parseResult.GetValueForOption(_commentFileOption!);
            options.TempDir = parseResult.GetValueForOption(_tempDirOption!);
            options.RecoveryRecord = parseResult.GetValueForOption(_recoveryRecordOption!);
            options.ExistingFileMode = parseResult.GetValueForOption(_existingFileModeOption!) ?? "overwrite";
            options.SkipProcessed = parseResult.GetValueForOption(_skipProcessedOption!);
            options.DeleteSource = parseResult.GetValueForOption(_deleteSourceOption!);
            options.MoveSource = parseResult.GetValueForOption(_moveSourceOption!);
            options.MaxSizeGB = parseResult.GetValueForOption(_maxSizeOption!);
            options.ShutdownAfter = parseResult.GetValueForOption(_shutdownOption!);
            options.AddEnclosures = parseResult.GetValueForOption(_addEnclosuresOption!);
            options.EnclosureList = parseResult.GetValueForOption(_enclosureListOption!);
            options.LogFile = parseResult.GetValueForOption(_logFileOption!);
            options.Verbose = parseResult.GetValueForOption(_verboseOption!);
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
