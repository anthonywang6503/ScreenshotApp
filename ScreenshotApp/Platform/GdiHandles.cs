using Microsoft.Win32.SafeHandles;

namespace ScreenshotApp.Platform;

/// <summary>以 <c>GetDC(NULL)</c> 取得的整個螢幕 DC；只能以 <c>ReleaseDC</c> 歸還，不能刪除。</summary>
internal sealed class SafeScreenDcHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeScreenDcHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.ReleaseDC(0, handle) == 1;
}

/// <summary>以 <c>CreateCompatibleDC</c> 建立的記憶體 DC；以 <c>DeleteDC</c> 刪除，刪除前必須換回原本選入的物件。</summary>
internal sealed class SafeMemoryDcHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeMemoryDcHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DeleteDC(handle);
}

/// <summary>GDI 物件（例如 HBITMAP）；以 <c>DeleteObject</c> 刪除，刪除時不得仍選入任何 DC。</summary>
internal sealed class SafeGdiObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeGdiObjectHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.DeleteObject(handle);
}
