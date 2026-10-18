using Microsoft.Win32;
using ScreenshotApp.Platform;

namespace ScreenshotApp.Tests;

/// <summary>以測試專用的登錄子機碼驗證，不觸碰真實的 Run 機碼。</summary>
public sealed class StartupServiceTests : IDisposable
{
    private const string TestRoot = @"Software\ScreenshotApp.Tests";
    private const string ValueName = "ScreenshotApp";
    private const string ExecutablePath = @"C:\Program Files\Screenshot App\ScreenshotApp.exe";

    private readonly string _keyPath = $@"{TestRoot}\{Guid.NewGuid():N}\Run";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(Path.GetDirectoryName(_keyPath)!, throwOnMissingSubKey: false);
        using var root = Registry.CurrentUser.OpenSubKey(TestRoot);
        if (root is not null && root.SubKeyCount == 0 && root.ValueCount == 0)
        {
            root.Dispose();
            Registry.CurrentUser.DeleteSubKey(TestRoot, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void RunKeyPath_IsTheCurrentUserRunKey()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", StartupService.RunKeyPath);
    }

    [Fact]
    public void IsEnabled_WhenKeyDoesNotExist_ReturnsFalse()
    {
        Assert.False(Create(ExecutablePath).IsEnabled());
    }

    [Fact]
    public void Enable_WritesTheQuotedExecutablePath()
    {
        var service = Create(ExecutablePath);

        Assert.True(service.TryApply(true, out var error));

        Assert.Null(error);
        Assert.True(service.IsEnabled());
        Assert.Equal($"\"{ExecutablePath}\"", ReadValue());
    }

    [Fact]
    public void Disable_RemovesTheValue()
    {
        var service = Create(ExecutablePath);
        Assert.True(service.TryApply(true, out _));

        Assert.True(service.TryApply(false, out var error));

        Assert.Null(error);
        Assert.False(service.IsEnabled());
        Assert.Null(ReadValue());
    }

    [Fact]
    public void Disable_WhenValueDoesNotExist_Succeeds()
    {
        Assert.True(Create(ExecutablePath).TryApply(false, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Enable_AfterExecutableMoved_UpdatesThePath()
    {
        Assert.True(Create(ExecutablePath).TryApply(true, out _));
        var moved = Create(@"E:\Portable\ScreenshotApp.exe");
        Assert.False(moved.IsEnabled());

        Assert.True(moved.TryApply(true, out _));

        Assert.True(moved.IsEnabled());
        Assert.Equal("\"E:\\Portable\\ScreenshotApp.exe\"", ReadValue());
    }

    private StartupService Create(string executablePath) =>
        new(Registry.CurrentUser, _keyPath, ValueName, executablePath);

    private string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(ValueName) as string;
    }
}
