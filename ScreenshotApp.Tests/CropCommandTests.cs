using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 裁切命令：連續裁切與調整（可再放大回原圖）、逐步復原與重做、1×1 的極小裁切、輸出尺寸永遠等於裁切範圍、
/// 無效範圍被拒絕且不留下紀錄，以及裁切不改寫標註座標。
/// </summary>
public class CropCommandTests
{
    private const int ImageWidth = 64;
    private const int ImageHeight = 48;

    private static readonly PixelRect FullImage = new(0, 0, ImageWidth, ImageHeight);

    // 依序套用：縮小、再縮小、調整到前一次範圍之外（裁切不破壞原圖，可再放大）、縮到 1×1，最後回到整張圖。
    private static readonly PixelRect[] ConsecutiveCrops =
    [
        new(4, 4, 50, 40),
        new(10, 8, 20, 20),
        new(30, 20, 34, 28),
        new(63, 47, 1, 1),
        new(0, 0, ImageWidth, ImageHeight),
    ];

    [Fact]
    public void Execute_ConsecutiveCrops_EachBecomesTheCurrentCrop()
    {
        var document = CreateDocument();

        foreach (var crop in ConsecutiveCrops)
        {
            document.History.Execute(new SetCropCommand(document, document.CropRect, crop));

            Assert.Equal(crop, document.CropRect);
        }

        Assert.Equal(ConsecutiveCrops.Length, document.History.UndoCount);
    }

    [Fact]
    public void UndoRedo_AfterConsecutiveCrops_RestoreEachPreviousCropInOrder()
    {
        var document = CreateDocument();
        PixelRect[] states = [FullImage, .. ConsecutiveCrops];
        foreach (var crop in ConsecutiveCrops)
        {
            document.History.Execute(new SetCropCommand(document, document.CropRect, crop));
        }

        for (var i = states.Length - 2; i >= 0; i--)
        {
            Assert.True(document.History.Undo());
            Assert.Equal(states[i], document.CropRect);
        }

        Assert.False(document.History.Undo());
        Assert.Equal(FullImage, document.CropRect);

        for (var i = 1; i < states.Length; i++)
        {
            Assert.True(document.History.Redo());
            Assert.Equal(states[i], document.CropRect);
        }

        Assert.False(document.History.Redo());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(63, 47)]
    [InlineData(63, 0)]
    [InlineData(17, 29)]
    public void Render_OneByOneCrop_IsSinglePixelEqualToSourcePixel(int x, int y)
    {
        StaRunner.Run(() =>
        {
            var document = CreateDocument();
            document.History.Execute(new SetCropCommand(document, document.CropRect, new PixelRect(x, y, 1, 1)));

            var image = ImageRenderer.Render(document);

            Assert.Equal(1, image.PixelWidth);
            Assert.Equal(1, image.PixelHeight);
            var pixel = new byte[4];
            image.CopyPixels(pixel, 4, 0);
            Assert.Equal(SourcePixel(x, y), pixel);
        });
    }

    [Fact]
    public void Render_AfterEachCropUndoAndRedo_OutputMatchesCurrentCrop()
    {
        StaRunner.Run(() =>
        {
            var document = CreateDocument();
            AssertRenderMatchesCrop(document);

            foreach (var crop in ConsecutiveCrops)
            {
                document.History.Execute(new SetCropCommand(document, document.CropRect, crop));
                AssertRenderMatchesCrop(document);
            }

            while (document.History.Undo())
            {
                AssertRenderMatchesCrop(document);
            }

            while (document.History.Redo())
            {
                AssertRenderMatchesCrop(document);
            }
        });
    }

    [Theory]
    [InlineData(0, 0, 0, 10)] // 寬為 0
    [InlineData(5, 5, 10, 0)] // 高為 0
    [InlineData(60, 0, 10, 10)] // 超出右邊界
    [InlineData(0, 40, 10, 10)] // 超出下邊界
    [InlineData(0, 0, 65, 48)] // 比原圖寬
    [InlineData(0, 0, 64, 49)] // 比原圖高
    [InlineData(-1, 0, 10, 10)] // 原點 X 為負
    [InlineData(0, -1, 10, 10)] // 原點 Y 為負
    [InlineData(64, 48, 1, 1)] // 完全在原圖之外
    public void Execute_InvalidCrop_ThrowsAndLeavesCropAndHistoryUnchanged(int x, int y, int width, int height)
    {
        var document = CreateDocument();
        var valid = new PixelRect(4, 4, 30, 30);
        document.History.Execute(new SetCropCommand(document, document.CropRect, valid));

        // 留下一筆可重做的紀錄，驗證失敗的命令不會清除它。
        document.History.Execute(new SetCropCommand(document, document.CropRect, new PixelRect(8, 8, 10, 10)));
        document.History.Undo();
        var invalid = new PixelRect(x, y, width, height);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => document.History.Execute(new SetCropCommand(document, document.CropRect, invalid)));
        Assert.Equal(valid, document.CropRect);
        Assert.Equal(1, document.History.UndoCount);
        Assert.Equal(1, document.History.RedoCount);
    }

    [Fact]
    public void CropExecuteUndoAndRedo_NeverChangeAnnotationCoordinates()
    {
        var document = CreateDocument();
        Annotation[] annotations =
        [
            new RectangleAnnotation { Color = Colors.Red, Rect = new PixelRect(5, 5, 20, 10), StrokeWidth = 2 },
            new EllipseAnnotation { Color = Colors.Blue, Rect = new PixelRect(30, 20, 20, 15), StrokeWidth = 3 },
            new ArrowAnnotation { Color = Colors.Green, Start = new PixelPoint(2, 40), End = new PixelPoint(60, 10), StrokeWidth = 3 },
            new LineAnnotation { Color = Colors.Black, Start = new PixelPoint(50, 2), End = new PixelPoint(10, 45), StrokeWidth = 1 },
            new StepNumberAnnotation { Color = Colors.Orange, Center = new PixelPoint(32, 24), Radius = 10, Number = 1 },
        ];
        foreach (var annotation in annotations)
        {
            document.History.Execute(new AddAnnotationCommand(document, annotation));
        }

        foreach (var crop in ConsecutiveCrops)
        {
            document.History.Execute(new SetCropCommand(document, document.CropRect, crop));
            Assert.Equal(annotations, document.Annotations);
        }

        while (document.History.UndoCount > annotations.Length)
        {
            document.History.Undo();
            Assert.Equal(annotations, document.Annotations);
        }

        while (document.History.Redo())
        {
            Assert.Equal(annotations, document.Annotations);
        }
    }

    // 輸出尺寸等於目前的裁切範圍，四個角的像素等於原圖對應位置的像素。
    private static void AssertRenderMatchesCrop(ImageDocument document)
    {
        var crop = document.CropRect;
        var image = ImageRenderer.Render(document);

        Assert.Equal(crop.Width, image.PixelWidth);
        Assert.Equal(crop.Height, image.PixelHeight);
        var stride = crop.Width * 4;
        var pixels = new byte[stride * crop.Height];
        image.CopyPixels(pixels, stride, 0);
        foreach (var (x, y) in new[] { (0, 0), (crop.Width - 1, 0), (0, crop.Height - 1), (crop.Width - 1, crop.Height - 1) })
        {
            var i = y * stride + x * 4;
            Assert.Equal(SourcePixel(crop.X + x, crop.Y + y), pixels[i..(i + 4)]);
        }
    }

    private static ImageDocument CreateDocument() => new(CreatePatternImage());

    // 圖樣中 (x, y) 像素的 BGRA 位元組，與 CreatePatternImage 的規則相同。
    private static byte[] SourcePixel(int x, int y) => [(byte)x, (byte)y, (byte)(x ^ y), 0xFF];

    // 每個像素的 (B, G, R) = (x, y, x ^ y)：寬高都小於 256，任兩個像素都不同，輸出若有位移即可察覺。
    private static BitmapSource CreatePatternImage()
    {
        const int stride = ImageWidth * 4;
        var pixels = new byte[stride * ImageHeight];
        for (var y = 0; y < ImageHeight; y++)
        {
            for (var x = 0; x < ImageWidth; x++)
            {
                SourcePixel(x, y).CopyTo(pixels, y * stride + x * 4);
            }
        }

        var image = BitmapSource.Create(ImageWidth, ImageHeight, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
        image.Freeze();
        return image;
    }
}
