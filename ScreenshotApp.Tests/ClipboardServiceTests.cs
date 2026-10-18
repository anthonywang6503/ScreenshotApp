using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Export;

namespace ScreenshotApp.Tests;

/// <summary>
/// 剪貼簿圖片的 DIB 格式：24 位元、由下而上、每列補齊到 4 位元組。測試不寫入真實的剪貼簿，
/// 以免覆蓋使用者的內容；實際複製與 GDI 資源由實機煙霧驗證涵蓋。
/// </summary>
public class ClipboardServiceTests
{
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
