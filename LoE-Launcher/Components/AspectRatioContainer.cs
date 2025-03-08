namespace LoE_Launcher.Components;

public class AspectRatioContainer : ContentView
{
    public static readonly BindableProperty AspectRatioProperty =
        BindableProperty.Create(nameof(AspectRatio), typeof(double), typeof(AspectRatioContainer), 1.0);

    public double AspectRatio
    {
        get => (double)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
    {
        if (widthConstraint > 0 && heightConstraint > 0)
        {
            if (widthConstraint / heightConstraint > AspectRatio)
            {
                widthConstraint = heightConstraint * AspectRatio;
            }
            else
            {
                heightConstraint = widthConstraint / AspectRatio;
            }
        }

        return base.OnMeasure(widthConstraint, heightConstraint);
    }
}