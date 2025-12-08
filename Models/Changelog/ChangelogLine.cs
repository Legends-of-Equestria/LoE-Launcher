namespace LoE_Launcher.Models;

public record ChangelogLine(
    ChangelogLineType Type,
    string Text)
{
    public bool IsHeader => Type == ChangelogLineType.Header;
    public bool IsBullet => Type == ChangelogLineType.Bullet;
    public bool IsEmpty => Type == ChangelogLineType.Empty;
}