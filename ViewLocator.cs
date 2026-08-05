using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using BatchCompress.Avalonia.ViewModels;

namespace BatchCompress.Avalonia;

/// <summary>
/// 根据视图模型返回对应的视图（如果可能）。
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
// GPT-5, 2026-08-05: Resolves conventional ViewModel-to-View names at runtime for Avalonia templates.
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = "未找到视图: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
