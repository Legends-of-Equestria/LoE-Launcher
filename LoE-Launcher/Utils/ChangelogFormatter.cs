using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LoE_Launcher.Utils;

public static class ChangelogFormatter
{
    public static void SetChangelogContent(StackPanel changelogPanel, string text)
    {
        changelogPanel.Children.Clear();
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#F0FFFFFF")),
            TextWrapping = TextWrapping.Wrap
        };
        changelogPanel.Children.Add(textBlock);
    }

    public static void FormatAndDisplayChangelog(StackPanel changelogPanel, string rawText)
    {
        changelogPanel.Children.Clear();

        var lines = rawText.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                changelogPanel.Children.Add(new Panel { Height = 8 });
                continue;
            }

            // Headers start with # followed by at least one space
            if (trimmed.StartsWith("# "))
            {
                var headerText = trimmed[2..].Trim();
                var headerBlock = new TextBlock
                {
                    Text = headerText,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 4)
                };
                changelogPanel.Children.Add(headerBlock);
                continue;
            }

            var contentText = trimmed;

            if (!contentText.StartsWith('•') && !contentText.StartsWith('-') && !contentText.StartsWith('*'))
            {
                contentText = $"• {contentText}";
            }
            else
            {
                contentText = contentText.Replace("- ", "• ").Replace("* ", "• ");
            }

            var contentBlock = new TextBlock
            {
                Text = contentText,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#F0FFFFFF")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1)
            };
            changelogPanel.Children.Add(contentBlock);
        }
    }
}
