namespace Models;

public enum SyncState
{
    Unknown,
    CalculatingDiff,
    CopyingExisting,
    DownloadingPatch,
    DownloadingNew,
    ApplyingPatch
}

public class ZsyncProgressData
{
    public ulong BytesProcessed { get; set; }
    public SyncState State { get; set; }

    public static implicit operator ulong(ZsyncProgressData data)
    {
        return data.BytesProcessed;
    }
}