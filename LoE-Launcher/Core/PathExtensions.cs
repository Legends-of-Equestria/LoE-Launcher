namespace LoE_Launcher.Core
{
    public static class PathExtensions
    {
        public static IRelativeDirectoryPath ToRelativeDirectoryPathAuto(this string path)
        {
            return new CustomDirectoryPath(path);
        }
        public static IAbsoluteDirectoryPath ToAbsoluteDirectoryPathAuto(this string path)
        {
            return new CustomDirectoryPath(path);
        }
        public static IRelativeFilePath ToRelativeFilePathAuto(this string path)
        {
            return new CustomFilePath(path);
        }
        public static IAbsoluteFilePath ToAbsoluteFilePathAuto(this string path)
        {
            return new CustomFilePath(path);
        }
    }
}