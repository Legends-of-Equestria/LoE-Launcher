using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LoE_Launcher.Utils;

public static class BrushFactory
{
    public static IBrush FromResource(string key) =>
        Application.Current!.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : throw new KeyNotFoundException($"Palette brush '{key}' not found");
}
