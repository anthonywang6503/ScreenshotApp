using ScreenshotApp.Platform;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>三種 GDI SafeHandle 各自以正確的函式釋放，釋放後處理程序的 GDI 物件數回到原值。</summary>
[Collection(GdiObjectCountCollection.Name)]
public class SafeHandleTests
{
    [Fact]
    public void ScreenDcHandle_Dispose_ReleasesTheDc()
    {
        var before = NativeTestHelpers.GdiObjectCount();

        using (var screen = NativeMethods.GetDC(0))
        {
            Assert.False(screen.IsInvalid);
        }

        Assert.Equal(before, NativeTestHelpers.GdiObjectCount());
    }

    [Fact]
    public void MemoryDcHandle_Dispose_DeletesTheDc()
    {
        using var screen = NativeMethods.GetDC(0);
        var before = NativeTestHelpers.GdiObjectCount();

        var memory = NativeMethods.CreateCompatibleDC(screen);
        Assert.False(memory.IsInvalid);
        Assert.Equal(before + 1, NativeTestHelpers.GdiObjectCount());

        memory.Dispose();
        Assert.Equal(before, NativeTestHelpers.GdiObjectCount());
    }

    [Fact]
    public void GdiObjectHandle_Dispose_DeletesTheBitmap()
    {
        using var screen = NativeMethods.GetDC(0);
        var before = NativeTestHelpers.GdiObjectCount();

        var bitmap = NativeMethods.CreateCompatibleBitmap(screen, 32, 32);
        Assert.False(bitmap.IsInvalid);
        Assert.Equal(before + 1, NativeTestHelpers.GdiObjectCount());

        bitmap.Dispose();
        Assert.Equal(before, NativeTestHelpers.GdiObjectCount());
    }

    [Fact]
    public void Dispose_Twice_IsHarmless()
    {
        using var screen = NativeMethods.GetDC(0);
        var bitmap = NativeMethods.CreateCompatibleBitmap(screen, 8, 8);

        bitmap.Dispose();
        bitmap.Dispose();

        Assert.True(bitmap.IsClosed);
    }
}
