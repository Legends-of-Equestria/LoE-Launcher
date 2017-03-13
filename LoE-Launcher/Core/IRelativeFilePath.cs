namespace LoE_Launcher.Core
{
    public interface IRelativeFilePath
    {
        string Path { get; }
        string FileName { get; }
        IRelativeDirectoryPath ParentDirectoryPath { get; }
        string FileNameWithoutExtension { get; }
        IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath gameInstallFolder);
        IRelativeFilePath GetBrotherFileWithName(string fileName);
    }
}