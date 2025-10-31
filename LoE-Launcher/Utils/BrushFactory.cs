using Avalonia;
using Avalonia.Media;

namespace LoE_Launcher.Utils;

public static class BrushFactory
{
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
