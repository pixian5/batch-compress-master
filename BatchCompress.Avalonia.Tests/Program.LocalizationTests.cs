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
    // 多语言界面、资源扫描与国际化完整性测试


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

}
