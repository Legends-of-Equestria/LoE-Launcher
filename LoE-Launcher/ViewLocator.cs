using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LoE_Launcher.ViewModels;

namespace LoE_Launcher;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "data is null" };
        }

        var viewModelType = data.GetType();
        var viewName = viewModelType.Name.Replace("ViewModel", "View");
        
        if (viewModelType.Namespace is null)
        {
            return new TextBlock { Text = "Not Found: ViewModel has no namespace" };
        }
        
        var viewNamespace = viewModelType.Namespace.Replace("ViewModels", "Views");
        var viewTypeName = $"{viewNamespace}.{viewName}";
        var type = Type.GetType(viewTypeName);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + viewTypeName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
