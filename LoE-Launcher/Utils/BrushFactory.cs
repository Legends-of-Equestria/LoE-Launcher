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

    public static LinearGradientBrush CreateVerticalGradient(string topColor, string bottomColor) =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse(topColor), 0),
                new GradientStop(Color.Parse(bottomColor), 1)
            ]
        };

    public static LinearGradientBrush CreateHorizontalGradient(string leftColor, string rightColor) =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse(leftColor), 0),
                new GradientStop(Color.Parse(rightColor), 1)
            ]
        };
}
