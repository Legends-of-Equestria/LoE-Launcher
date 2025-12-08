using System.Collections.Generic;
using LoE_Launcher.Models;

namespace LoE_Launcher.Services;

public class ChangelogParser
{
    public IEnumerable<ChangelogLine> Parse(string rawText)
    {
        var lines = rawText.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                yield return new ChangelogLine(ChangelogLineType.Empty, string.Empty);
                continue;
            }

            if (trimmed.StartsWith("# "))
            {
                yield return new ChangelogLine(ChangelogLineType.Header, trimmed[2..].Trim());
                continue;
            }

            var contentText = trimmed;
            if (!contentText.StartsWith('•') && !contentText.StartsWith('-') && !contentText.StartsWith('*'))
            {
                contentText = $"• {contentText}";
            }
            else
            {
                // Normalize bullets
                contentText = "• " + contentText[1..].TrimStart();
            }

            yield return new ChangelogLine(ChangelogLineType.Bullet, contentText);
        }
    }
}
