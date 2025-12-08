using Avalonia.Controls;

namespace LoE_Launcher.Services;

public interface IWindowProvider
{
    Window? GetMainWindow();
}
