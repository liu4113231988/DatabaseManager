using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.AppCore.Common;

namespace DatabaseManager.Avalonia;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        // AppCore 的 ViewModel 对应的 View 位于 DatabaseManager.Avalonia.Views
        var vmType = param.GetType();
        var name = vmType.FullName!
            .Replace("DatabaseManager.AppCore.ViewModels", "DatabaseManager.Avalonia.Views")
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return null;
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
