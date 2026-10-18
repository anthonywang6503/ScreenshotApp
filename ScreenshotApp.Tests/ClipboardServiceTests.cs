using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Export;
using ScreenshotApp.Resources;
using ScreenshotApp.Tests.TestSupport;
using DataObject = System.Windows.DataObject;

namespace ScreenshotApp.Tests;

/// <summary>
/// 剪貼簿的兩種格式（24 位元 DIB 與 PNG）、經由 OLE 取得資料的方式、寫入失敗時的結果與訊息。
/// 測試不寫入真實的剪貼簿，以免覆蓋使用者的內容；實際複製與占用情境由實機煙霧驗證涵蓋。
/// </summary>
public sealed partial class ClipboardServiceTests : IDisposable
{
    private const short CF_DIB = 8;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();
    [Fact]
    public void CreateDib_WritesHeaderOf24BitBottomUpImageAt96Dpi()
    {
        using var dib = ClipboardService.CreateDib(CreateImage(3, 2));
        using var reader = new BinaryReader(dib, System.Text.Encoding.ASCII, leaveOpen: true);

        Assert.Equal(40, reader.ReadInt32());
        Assert.Equal(3, reader.ReadInt32());
        Assert.Equal(2, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt16());
        Assert.Equal(24, reader.ReadInt16());
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(24, reader.ReadInt32());
        Assert.Equal(3780, reader.ReadInt32());
        Assert.Equal(3780, reader.ReadInt32());
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(0, reader.ReadInt32());
        Assert.Equal(40 + 24, dib.Length);
    }

    [Fact]
    public void CreateDib_StoresRowsBottomUpPaddedToFourBytes()
    {
        using var dib = ClipboardService.CreateDib(CreateImage(3, 2));

        Assert.Equal(
            new byte[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 0, 0 },
            dib.ToArray()[40..]);
    }

    [Fact]
    public async Task CreatePayload_OnAThreadPoolThread_PngDecodesToTheSamePixels()
    {
        var image = CreateImage(7, 5);

        var payload = await Task.Run(() => ClipboardPayload.Create(image));

        var decoded = BitmapDecoder.Create(new MemoryStream(payload.Png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(Bgr(image), Bgr(decoded));
    }

    [Fact]
    public void CreatePayload_PngIsByteForByteTheSameAsTheSavedFile()
    {
        var image = CreateImage(9, 4);
        var path = _temp.Combine("same.png");

        var payload = ClipboardPayload.Create(image);
        PngFileService.Save(image, path);

        Assert.Equal(File.ReadAllBytes(path), payload.Png);
    }

    [Fact]
    public void CreatePayload_DibIsTheTwentyFourBitDib()
    {
        var image = CreateImage(3, 2);
        using var dib = ClipboardService.CreateDib(image);

        Assert.Equal(dib.ToArray(), ClipboardPayload.Create(image).Dib);
    }

    [Fact]
    public void ToDataObject_BothFormatsRenderToGlobalMemoryThroughOle()
    {
        // 與 OleFlushClipboard 相同的取得方式：要求 HGLOBAL 形式的資料，確認串流內容原封不動。
        var payload = ClipboardPayload.Create(CreateImage(6, 3));
        var pngFormat = unchecked((short)RegisterClipboardFormat(ClipboardPayload.PngFormat));

        var dib = RenderThroughOle(payload.ToDataObject(), CF_DIB);
        var png = RenderThroughOle(payload.ToDataObject(), pngFormat);

        Assert.Equal(payload.Dib, dib[..payload.Dib.Length]);
        Assert.Equal(payload.Png, png[..payload.Png.Length]);
    }

    [Fact]
    public void TrySetImage_Success_HandsOverBothFormats()
    {
        var payload = ClipboardPayload.Create(CreateImage(4, 4));
        DataObject? received = null;

        var succeeded = ClipboardService.TrySetImage(payload, data => received = data, out var errorCode);

        Assert.True(succeeded);
        Assert.Equal(0, errorCode);
        Assert.NotNull(received);
        Assert.True(received.GetDataPresent(DataFormats.Dib));
        Assert.True(received.GetDataPresent(ClipboardPayload.PngFormat));
    }

    [Theory]
    [InlineData(ClipboardService.CLIPBRD_E_CANT_OPEN)]
    [InlineData(unchecked((int)0x80004005))]
    public void TrySetImage_ClipboardFails_ReturnsTheErrorCode(int hresult)
    {
        var payload = ClipboardPayload.Create(CreateImage(4, 4));

        var succeeded = ClipboardService.TrySetImage(payload, _ => throw new COMException("剪貼簿失敗", hresult), out var errorCode);

        Assert.False(succeeded);
        Assert.Equal(hresult, errorCode);
    }

    [Fact]
    public void DescribeError_ClipboardInUse_SaysAnotherProgramIsUsingIt()
    {
        var message = ClipboardService.DescribeError(ClipboardService.CLIPBRD_E_CANT_OPEN);

        Assert.Equal(Strings.Get("Editor_CopyFailedBusy"), message);
        Assert.Contains("其他程式", message);
    }

    [Fact]
    public void DescribeError_OtherFailure_ShowsTheErrorCode()
    {
        var message = ClipboardService.DescribeError(unchecked((int)0x80004005));

        Assert.Contains("0x80004005", message);
        Assert.NotEqual(Strings.Get("Editor_CopyFailedBusy"), message);
    }

    private static byte[] RenderThroughOle(DataObject data, short format)
    {
        var request = new FORMATETC { cfFormat = format, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        ((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref request, out var medium);
        Assert.Equal(TYMED.TYMED_HGLOBAL, medium.tymed);
        Assert.Null(medium.pUnkForRelease);
        try
        {
            var bytes = new byte[(int)GlobalSize(medium.unionmember)];
            var pointer = GlobalLock(medium.unionmember);
            try
            {
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
            }
            finally
            {
                GlobalUnlock(medium.unionmember);
            }

            return bytes;
        }
        finally
        {
            GlobalFree(medium.unionmember);
        }
    }

    // 每個像素的藍、綠、紅值；用來比較不同像素格式的圖片內容。
    private static byte[] Bgr(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
        var stride = image.PixelWidth * 3;
        var pixels = new byte[stride * image.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string format);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint hMem);

    // 像素依序為 (B, G, R) = (1, 2, 3)、(4, 5, 6)……，每個像素的值都不同。
    private static BitmapSource CreateImage(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var value = (byte)1;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = value++;
            pixels[i + 1] = value++;
            pixels[i + 2] = value++;
            pixels[i + 3] = 0xFF;
        }

        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }
}
