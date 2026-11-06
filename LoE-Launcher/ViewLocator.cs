using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LoE_Launcher.ViewModels;
using LoE_Launcher.Views;

namespace LoE_Launcher;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data) => data switch
    {
        SettingsViewModel => new SettingsView(),
        TesterAccessViewModel => new TesterAccessView(),
        null => new TextBlock { Text = "data is null" },
        _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
    };

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
