using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using LoE_Launcher.Models;

namespace LoE_Launcher.Utils;

public class ChangelogTextHelper
{
    public static readonly AttachedProperty<IEnumerable?> SourceProperty =
        AvaloniaProperty.RegisterAttached<ChangelogTextHelper, SelectableTextBlock, IEnumerable?>("Source");

    static ChangelogTextHelper()
    {
        SourceProperty.Changed.AddClassHandler<SelectableTextBlock>(OnSourceChanged);
    }

    public static void SetSource(SelectableTextBlock target, IEnumerable? value) =>
        target.SetValue(SourceProperty, value);

    public static IEnumerable? GetSource(SelectableTextBlock target) =>
        target.GetValue(SourceProperty);

    private static void OnSourceChanged(SelectableTextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += (_, _) => RebuildInlines(textBlock);
        }

        RebuildInlines(textBlock);
    }

    private static void RebuildInlines(SelectableTextBlock textBlock)
    {
        textBlock.Inlines?.Clear();

        var source = GetSource(textBlock);
        if (source == null)
        {
            return;
        }

        var first = true;
        foreach (var item in source)
        {
            if (item is not ChangelogLine line)
            {
                continue;
            }

            if (line.IsEmpty)
            {
                textBlock.Inlines?.Add(new LineBreak());
                first = false;
                continue;
            }

            if (!first)
            {
                textBlock.Inlines?.Add(new LineBreak());
                if (line.IsHeader)
                {
                    textBlock.Inlines?.Add(new LineBreak());
                }
            }

            if (line.IsHeader)
            {
                textBlock.Inlines?.Add(new Run(line.Text)
                {
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                });
            }
            else if (line.IsBullet)
            {
                textBlock.Inlines?.Add(new Run(line.Text)
                {
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.Parse("#F0FFFFFF"))
                });
            }

            first = false;
        }
    }
}
