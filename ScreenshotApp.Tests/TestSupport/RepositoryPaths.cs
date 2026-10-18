namespace ScreenshotApp.Tests.TestSupport;

/// <summary>由測試輸出目錄往上尋找儲存庫根目錄，供原始碼掃描測試使用。</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string AppProject => Path.Combine(Root, "ScreenshotApp");

    /// <summary>應用程式專案中指定副檔名的原始檔，排除建置輸出。</summary>
    public static IEnumerable<string> AppSourceFiles(string extension)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(AppProject, "*" + extension, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}"));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScreenshotApp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("找不到儲存庫根目錄。");
    }
}
