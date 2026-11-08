using System.Collections.Generic;

namespace LoE_Launcher.Core;

internal sealed class FileListMemory
{
    public IReadOnlyList<string>? Current { get; set; }

    public void Forget() => Current = null;
}
