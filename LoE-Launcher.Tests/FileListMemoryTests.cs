using FluentAssertions;
using LoE_Launcher.Core;

namespace LoE_Launcher.Tests;

public class FileListMemoryTests
{
    [Fact]
    public void Forget_ShouldClearTheRememberedList()
    {
        var memory = new FileListMemory { Current = ["a.dll"] };

        memory.Forget();

        memory.Current.Should().BeNull();
    }
}
