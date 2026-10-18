using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public sealed class ErrorLogTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Write_RecordsContextExceptionMessageAndStackTrace()
    {
        var log = new ErrorLog(_temp.Combine("logs"));

        log.Write("讀取設定失敗", CreateThrownException("測試例外訊息"));

        var text = File.ReadAllText(log.CurrentFilePath);
        Assert.Contains("讀取設定失敗", text);
        Assert.Contains("System.InvalidOperationException: 測試例外訊息", text);
        Assert.Contains(nameof(CreateThrownException), text);
    }

    [Fact]
    public void Write_WithoutException_RecordsContextOnly()
    {
        var log = new ErrorLog(_temp.Combine("logs"));

        log.Write("DPI 感知模式異常");

        Assert.Contains("DPI 感知模式異常", File.ReadAllText(log.CurrentFilePath));
    }

    [Fact]
    public void Write_WhenFileReachesLimit_RotatesAndKeepsAtMostTwoFiles()
    {
        const long maxBytes = 2048;
        var log = new ErrorLog(_temp.Combine("logs"), maxBytes);

        for (var i = 0; i < 100; i++)
        {
            log.Write($"第 {i} 筆錯誤", CreateThrownException("內容"));
        }

        var files = Directory.GetFiles(log.LogDirectory);
        Assert.Equal(2, files.Length);
        Assert.True(File.Exists(log.PreviousFilePath));
        Assert.True(new FileInfo(log.CurrentFilePath).Length < maxBytes * 2);
        Assert.True(new FileInfo(log.PreviousFilePath).Length < maxBytes * 2);
        Assert.Contains("第 99 筆錯誤", File.ReadAllText(log.CurrentFilePath));
    }

    [Fact]
    public void Write_OversizedEntry_IsTruncated()
    {
        const long maxBytes = 400;
        var log = new ErrorLog(_temp.Combine("logs"), maxBytes);

        log.Write(new string('錯', 5000));

        Assert.True(new FileInfo(log.CurrentFilePath).Length < maxBytes);
    }

    [Fact]
    public void DeleteExpiredFiles_RemovesOnlyFilesOlderThanRetention()
    {
        var log = new ErrorLog(_temp.Combine("logs"), retention: TimeSpan.FromDays(30));
        log.Write("舊的錯誤");
        File.Copy(log.CurrentFilePath, log.PreviousFilePath);
        File.SetLastWriteTimeUtc(log.PreviousFilePath, DateTime.UtcNow.AddDays(-31));
        File.SetLastWriteTimeUtc(log.CurrentFilePath, DateTime.UtcNow.AddDays(-1));

        log.DeleteExpiredFiles();

        Assert.False(File.Exists(log.PreviousFilePath));
        Assert.True(File.Exists(log.CurrentFilePath));
    }

    [Fact]
    public void WriteAndCleanup_WhenDirectoryCannotBeCreated_DoNotThrow()
    {
        var blocker = _temp.Combine("blocker");
        File.WriteAllText(blocker, "這是檔案，不是資料夾");
        var log = new ErrorLog(Path.Combine(blocker, "logs"));

        log.Write("不應拋出例外", CreateThrownException("內容"));
        log.DeleteExpiredFiles();

        Assert.False(File.Exists(log.CurrentFilePath));
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "ScreenshotApp", "logs"), ErrorLog.DefaultDirectory);
    }

    private static Exception CreateThrownException(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
