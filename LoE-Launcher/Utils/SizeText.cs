namespace LoE_Launcher.Utils;

public static class SizeText
{
    public static string Format(long bytes)
    {
        const double kb = 1024, mb = kb * 1024, gb = mb * 1024;
        return bytes switch
        {
            >= (long)gb => $"{bytes / gb:0.0} GB",
            >= (long)mb => $"{bytes / mb:0.0} MB",
            _ => $"{bytes / kb:0.0} KB"
        };
    }
}
