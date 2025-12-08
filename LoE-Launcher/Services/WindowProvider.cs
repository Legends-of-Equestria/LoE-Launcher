using Avalonia.Controls;

namespace LoE_Launcher.Services;

public class WindowProvider : IWindowProvider
{
    public Window? MainWindow { get; set; }
    
    public Window? GetMainWindow() => MainWindow;
}
