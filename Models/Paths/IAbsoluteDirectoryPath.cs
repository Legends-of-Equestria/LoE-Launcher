namespace Models.Paths;

public interface IAbsoluteDirectoryPath
{
    string Path { get; }
    bool Exists { get; }
    DirectoryInfo DirectoryInfo { get; }
    string DirectoryName { get; }
    IAbsoluteDirectoryPath ParentDirectoryPath { get; }
    IAbsoluteFilePath GetChildFileWithName(string fileName);
}