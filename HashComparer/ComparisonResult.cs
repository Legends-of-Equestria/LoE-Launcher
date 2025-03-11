namespace HashComparer;

internal record ComparisonResult(
    string FileName,
    string OriginalPath,
    string ProcessedPath,
    string OriginalMD5,
    string ProcessedMD5,
    long OriginalSize,
    long ProcessedSize,
    bool HashesMatch,
    bool IsBinary,
    bool LineEndingDifference,
    string? Error = null
    );
