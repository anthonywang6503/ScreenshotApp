using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Export;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 匯出合成器：輸出尺寸與格式、底圖像素逐一相同、標註依裁切範圍平移、矩形框的清晰邊緣、各種標註的繪製範圍、
/// 步驟編號的文字對比，以及原始圖片不被修改。WPF 繪製必須在 STA 執行緒進行，因此每個測試都在 <see cref="StaRunner"/> 中執行。
/// </summary>
public class ImageRendererTests
{
    private const int PatternWidth = 64;
    private const int PatternHeight = 48;

    private static readonly Color Red = Color.FromRgb(0xFF, 0x00, 0x00);
    private static readonly Color Black = Color.FromRgb(0x00, 0x00, 0x00);
    private static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color Gray = Color.FromRgb(0x80, 0x80, 0x80);

    [Theory]
    [InlineData(0, 0, 64, 48, 96.0)]
    [InlineData(5, 7, 30, 20, 96.0)]
    [InlineData(63, 47, 1, 1, 96.0)]
    [InlineData(5, 7, 30, 20, 144.0)] // 圖檔記錄的 DPI 不影響輸出
    public void Render_AnyCrop_OutputsFrozenPbgra32BitmapAt96DpiWithCropSize(int x, int y, int width, int height, double sourceDpi)
    {
        StaRunner.Run(() =>
        {
            var document = new ImageDocument(CreatePatternImage(PatternWidth, PatternHeight, sourceDpi));
            SetCrop(document, new PixelRect(x, y, width, height));

            var image = ImageRenderer.Render(document);

            Assert.Equal(width, image.PixelWidth);
            Assert.Equal(height, image.PixelHeight);
            Assert.Equal(PixelFormats.Pbgra32, image.Format);
            Assert.Equal(96, image.DpiX);
            Assert.Equal(96, image.DpiY);
            Assert.True(image.IsFrozen);
        });
    }

    [Theory]
    [InlineData(0, 0, 64, 48, 96.0)]
    [InlineData(5, 7, 30, 20, 96.0)]
    [InlineData(33, 1, 31, 46, 96.0)]
    [InlineData(0, 47, 64, 1, 96.0)]
    [InlineData(5, 7, 30, 20, 144.0)] // 以像素尺寸繪製，不依圖檔記錄的 DPI 縮放
    public void Render_NoAnnotations_CopiesSourcePixelsOfCropExactly(int x, int y, int width, int height, double sourceDpi)
    {
        StaRunner.Run(() =>
        {
            var source = CreatePatternImage(PatternWidth, PatternHeight, sourceDpi);
            var document = new ImageDocument(source);
            var crop = new PixelRect(x, y, width, height);
            SetCrop(document, crop);

            var actual = CopyPixels(ImageRenderer.Render(document));

            AssertPixelsEqual(SourcePixels(source, crop), actual, width);
        });
    }

    [Fact]
    public void Render_AnnotationsEntirelyOutsideCrop_DoNotAppear()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePatternImage(PatternWidth, PatternHeight);
            var crop = new PixelRect(10, 10, 20, 20);
            var plain = new ImageDocument(source);
            SetCrop(plain, crop);
            var annotated = new ImageDocument(source);
            SetCrop(annotated, crop);
            Annotation[] outside =
            [
                new RectangleAnnotation { Color = Red, Rect = new PixelRect(36, 8, 20, 22), StrokeWidth = 3 },
                new ArrowAnnotation { Color = Red, Start = new PixelPoint(2, 40), End = new PixelPoint(60, 44), StrokeWidth = 2 },
                new LineAnnotation { Color = Red, Start = new PixelPoint(3, 2), End = new PixelPoint(4, 45), StrokeWidth = 2 },
                new TextAnnotation { Color = Red, Origin = new PixelPoint(38, 34), Text = "AB", FontFamily = "Segoe UI", FontSize = 10 },
                new StepNumberAnnotation { Color = Red, Center = new PixelPoint(50, 40), Radius = 6, Number = 1 },
            ];

            // 每個標註的繪製範圍與裁切範圍至少相隔 4 像素，反鋸齒的邊緣也不可能落入輸出。
            var guard = new Rect(crop.X - 4, crop.Y - 4, crop.Width + 8, crop.Height + 8);
            foreach (var annotation in outside)
            {
                Assert.False(guard.IntersectsWith(annotation.Bounds), $"測試資料錯誤：{annotation.GetType().Name} 太靠近裁切範圍。");
                Add(annotated, annotation);
            }

            Assert.Equal(CopyPixels(ImageRenderer.Render(plain)), CopyPixels(ImageRenderer.Render(annotated)));
        });
    }

    [Fact]
    public void Render_AnnotationInsideCrop_ChangesPixels()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePatternImage(PatternWidth, PatternHeight);
            var crop = new PixelRect(10, 10, 20, 20);
            var plain = new ImageDocument(source);
            SetCrop(plain, crop);
            var annotated = new ImageDocument(source);
            SetCrop(annotated, crop);
            Add(annotated, new RectangleAnnotation { Color = Red, Rect = new PixelRect(14, 14, 10, 10), StrokeWidth = 2 });

            Assert.NotEqual(CopyPixels(ImageRenderer.Render(plain)), CopyPixels(ImageRenderer.Render(annotated)));
        });
    }

    [Fact]
    public void Render_AnnotationWithOffsetCrop_IsShiftedByCropOrigin()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePatternImage(PatternWidth, PatternHeight);
            var document = new ImageDocument(source);
            var crop = new PixelRect(10, 6, 30, 30);
            SetCrop(document, crop);

            // 寬高不超過兩倍線寬，整塊實心：圖片像素 X 20..23、Y 15..18，輸出中應位於 X 10..13、Y 9..12。
            var box = new PixelRect(20, 15, 4, 4);
            Add(document, new RectangleAnnotation { Color = Red, Rect = box, StrokeWidth = 2 });

            var actual = CopyPixels(ImageRenderer.Render(document));

            var expected = SourcePixels(source, crop);
            for (var y = box.Y; y < box.Bottom; y++)
            {
                for (var x = box.X; x < box.Right; x++)
                {
                    SetPixel(expected, crop.Width, x - crop.X, y - crop.Y, Red);
                }
            }

            AssertPixelsEqual(expected, actual, crop.Width);
        });
    }

    [Fact]
    public void Render_SameDocumentTwice_ProducesIdenticalBytes()
    {
        StaRunner.Run(() =>
        {
            var document = new ImageDocument(CreatePatternImage(PatternWidth, PatternHeight));
            SetCrop(document, new PixelRect(3, 2, 58, 44));
            Add(document, new RectangleAnnotation { Color = Red, Rect = new PixelRect(5, 5, 30, 20), StrokeWidth = 3 });
            Add(document, new EllipseAnnotation { Color = White, Rect = new PixelRect(20, 10, 30, 25), StrokeWidth = 2 });
            Add(document, new ArrowAnnotation { Color = Red, Start = new PixelPoint(4, 40), End = new PixelPoint(55, 8), StrokeWidth = 3 });
            Add(document, new TextAnnotation { Color = White, Origin = new PixelPoint(8, 24), Text = "AB 12", FontFamily = "Segoe UI", FontSize = 14 });
            Add(document, new StepNumberAnnotation { Color = Red, Center = new PixelPoint(48, 36), Radius = 8, Number = 2 });

            var first = CopyPixels(ImageRenderer.Render(document));
            var second = CopyPixels(ImageRenderer.Render(document));

            Assert.Equal(first, second);
        });
    }

    [Fact]
    public void ExecuteCommandsAndRender_SourceBitmapStaysUnchanged()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePatternImage(PatternWidth, PatternHeight);
            var hashBefore = SHA256.HashData(CopyPixels(source));
            var document = new ImageDocument(source);
            var rectangle = new RectangleAnnotation { Color = Red, Rect = new PixelRect(5, 5, 30, 20), StrokeWidth = 3 };
            var arrow = new ArrowAnnotation { Color = White, Start = new PixelPoint(4, 40), End = new PixelPoint(55, 8), StrokeWidth = 3 };

            document.History.Execute(new AddAnnotationCommand(document, rectangle));
            document.History.Execute(new AddAnnotationCommand(document, arrow));
            document.History.Execute(new ReplaceAnnotationCommand(document, rectangle, rectangle.Translate(6, 4)));
            document.History.Execute(new SetCropCommand(document, document.CropRect, new PixelRect(4, 4, 40, 30)));
            ImageRenderer.Render(document);
            document.History.Undo();
            document.History.Undo();
            ImageRenderer.Render(document);
            document.History.Redo();
            ImageRenderer.Render(document);

            Assert.Same(source, document.Source);
            Assert.Equal(hashBefore, SHA256.HashData(CopyPixels(document.Source)));
        });
    }

    [Theory]
    [InlineData(10, 10, 20, 20, 3)] // 筆劃為 10..12 與 27..29，空心為 13..26，第 9 列與第 30 欄維持底圖
    [InlineData(5, 7, 30, 12, 1)]
    [InlineData(20, 15, 25, 30, 2)]
    [InlineData(8, 30, 21, 17, 4)]
    [InlineData(40, 30, 7, 9, 3)] // 寬為兩倍線寬加 1，只剩 1 像素寬的空心
    [InlineData(0, 0, 64, 64, 5)] // 與圖片邊緣重合
    public void Render_HollowRectangle_StrokeBandIsExactColorAndOtherPixelsUntouched(int x, int y, int width, int height, int stroke)
    {
        StaRunner.Run(() =>
        {
            const int size = 64;
            var document = new ImageDocument(CreateSolidImage(size, size, Black));
            Add(document, new RectangleAnnotation { Color = Red, Rect = new PixelRect(x, y, width, height), StrokeWidth = stroke });

            var actual = CopyPixels(ImageRenderer.Render(document));

            // 筆劃畫在範圍內側：距離任一邊不到線寬的像素完全是標註色，空心內側與範圍外的像素維持底圖。
            var expected = new byte[size * size * 4];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var inside = px >= x && px < x + width && py >= y && py < y + height;
                    var onStroke = inside
                        && (px < x + stroke || px >= x + width - stroke || py < y + stroke || py >= y + height - stroke);
                    SetPixel(expected, size, px, py, onStroke ? Red : Black);
                }
            }

            AssertPixelsEqual(expected, actual, size);
        });
    }

    [Theory]
    [InlineData(5, 5, 6, 20, 3)] // 寬恰為兩倍線寬
    [InlineData(20, 40, 30, 4, 3)] // 高小於兩倍線寬
    [InlineData(40, 10, 10, 10, 5)]
    [InlineData(50, 30, 1, 1, 1)]
    public void Render_RectangleNotLargerThanTwiceStroke_IsFilledCompletely(int x, int y, int width, int height, int stroke)
    {
        StaRunner.Run(() =>
        {
            const int size = 64;
            var document = new ImageDocument(CreateSolidImage(size, size, Black));
            Add(document, new RectangleAnnotation { Color = Red, Rect = new PixelRect(x, y, width, height), StrokeWidth = stroke });

            var actual = CopyPixels(ImageRenderer.Render(document));

            var expected = new byte[size * size * 4];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var inside = px >= x && px < x + width && py >= y && py < y + height;
                    SetPixel(expected, size, px, py, inside ? Red : Black);
                }
            }

            AssertPixelsEqual(expected, actual, size);
        });
    }

    [Theory]
    [InlineData("Rectangle")]
    [InlineData("Ellipse")]
    [InlineData("FilledEllipse")]
    [InlineData("Line")]
    [InlineData("Dot")]
    [InlineData("Arrow")]
    [InlineData("ShortArrow")]
    [InlineData("Text")]
    [InlineData("StepNumber")]
    public void Render_EachAnnotationKind_DrawsItsColorInsideBoundsAndNothingBeyondTwoPixels(string kind)
    {
        StaRunner.Run(() =>
        {
            const int width = 160;
            const int height = 120;
            var annotation = CreateAnnotation(kind);
            var document = new ImageDocument(CreateSolidImage(width, height, Black));
            Add(document, annotation);

            var pixels = CopyPixels(ImageRenderer.Render(document));

            // 範圍內：像素中心落在 Bounds 之內；範圍外：整個像素都在 Bounds 往外 2 像素之外。
            var bounds = annotation.Bounds;
            var limit = bounds;
            limit.Inflate(2, 2);
            var colorPixelsInside = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (bounds.Contains(new Point(x + 0.5, y + 0.5)) && PixelIs(pixels, width, x, y, annotation.Color))
                    {
                        colorPixelsInside++;
                    }

                    var beyondLimit = x + 1 <= limit.Left || x >= limit.Right || y + 1 <= limit.Top || y >= limit.Bottom;
                    if (beyondLimit && !PixelIs(pixels, width, x, y, Black))
                    {
                        Assert.Fail($"{kind} 在繪製範圍 {bounds} 往外 2 像素之外的 ({x}, {y}) 畫出 {Describe(pixels, width, x, y)}。");
                    }
                }
            }

            Assert.True(colorPixelsInside > 0, $"{kind} 在繪製範圍 {bounds} 內沒有任何完全是標註色的像素。");
        });
    }

    [Theory]
    [InlineData(0xFF, 0xD4, 0x00, true)] // 黃色：亮色底配黑字
    [InlineData(0xFF, 0x00, 0x00, false)] // 紅色：深色底配白字
    [InlineData(0x00, 0x78, 0xD4, false)] // 藍色：深色底配白字
    public void Render_StepNumber_DigitColorContrastsWithCircleColor(int r, int g, int b, bool expectBlackDigits)
    {
        StaRunner.Run(() =>
        {
            const int size = 100;
            var step = new StepNumberAnnotation
            {
                Color = Color.FromRgb((byte)r, (byte)g, (byte)b),
                Center = new PixelPoint(50, 50),
                Radius = 30,
                Number = 8,
            };

            // 中灰色底圖：圓心附近的區域完全在圓內，其中的純黑或純白像素只可能來自編號文字。
            var document = new ImageDocument(CreateSolidImage(size, size, Gray));
            Add(document, step);

            var pixels = CopyPixels(ImageRenderer.Render(document));

            var black = 0;
            var white = 0;
            for (var y = 35; y <= 65; y++)
            {
                for (var x = 35; x <= 65; x++)
                {
                    black += PixelIs(pixels, size, x, y, Black) ? 1 : 0;
                    white += PixelIs(pixels, size, x, y, White) ? 1 : 0;
                }
            }

            var (digitPixels, oppositePixels) = expectBlackDigits ? (black, white) : (white, black);
            Assert.True(
                digitPixels >= 20,
                $"預期圓心附近有{(expectBlackDigits ? "黑" : "白")}色文字，實際黑色像素 {black} 個、白色像素 {white} 個。");
            Assert.Equal(0, oppositePixels);
        });
    }

    private static Annotation CreateAnnotation(string kind) => kind switch
    {
        "Rectangle" => new RectangleAnnotation { Color = Red, Rect = new PixelRect(12, 10, 50, 30), StrokeWidth = 3 },
        "Ellipse" => new EllipseAnnotation { Color = Red, Rect = new PixelRect(30, 20, 90, 70), StrokeWidth = 4 },
        "FilledEllipse" => new EllipseAnnotation { Color = Red, Rect = new PixelRect(40, 40, 8, 8), StrokeWidth = 4 },
        "Line" => new LineAnnotation { Color = Red, Start = new PixelPoint(15, 100), End = new PixelPoint(140, 20), StrokeWidth = 5 },
        "Dot" => new LineAnnotation { Color = Red, Start = new PixelPoint(60, 60), End = new PixelPoint(60, 60), StrokeWidth = 6 },
        "Arrow" => new ArrowAnnotation { Color = Red, Start = new PixelPoint(20, 30), End = new PixelPoint(130, 95), StrokeWidth = 4 },
        "ShortArrow" => new ArrowAnnotation { Color = Red, Start = new PixelPoint(60, 60), End = new PixelPoint(64, 62), StrokeWidth = 4 },
        "Text" => new TextAnnotation { Color = Red, Origin = new PixelPoint(20, 30), Text = "AB 12", FontFamily = "Segoe UI", FontSize = 40 },
        "StepNumber" => new StepNumberAnnotation { Color = Red, Center = new PixelPoint(80, 60), Radius = 24, Number = 3 },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的標註種類。"),
    };

    private static void SetCrop(ImageDocument document, PixelRect crop) =>
        document.History.Execute(new SetCropCommand(document, document.CropRect, crop));

    private static void Add(ImageDocument document, Annotation annotation) =>
        document.History.Execute(new AddAnnotationCommand(document, annotation));

    // 每個像素的 (B, G, R) = (x, y, x ^ y)：寬高都小於 256，任兩個像素都不同，輸出若有位移即可察覺。
    private static BitmapSource CreatePatternImage(int width, int height, double dpi = 96)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 2] = (byte)(x ^ y);
                pixels[i + 3] = 0xFF;
            }
        }

        return CreateImage(width, height, dpi, pixels);
    }

    private static BitmapSource CreateSolidImage(int width, int height, Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 0xFF;
        }

        return CreateImage(width, height, 96, pixels);
    }

    private static BitmapSource CreateImage(int width, int height, double dpi, byte[] pixels)
    {
        var image = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgr32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    private static byte[] CopyPixels(BitmapSource image)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    // 原圖在裁切範圍內的像素，排成輸出點陣圖的 BGRA 位元組；底圖不透明，Alpha 一律為 255。
    private static byte[] SourcePixels(BitmapSource source, PixelRect crop)
    {
        var stride = crop.Width * 4;
        var pixels = new byte[stride * crop.Height];
        source.CopyPixels(new Int32Rect(crop.X, crop.Y, crop.Width, crop.Height), pixels, stride, 0);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xFF;
        }

        return pixels;
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, Color color)
    {
        var i = (y * width + x) * 4;
        pixels[i] = color.B;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.R;
        pixels[i + 3] = 0xFF;
    }

    private static bool PixelIs(byte[] pixels, int width, int x, int y, Color color)
    {
        var i = (y * width + x) * 4;
        return pixels[i] == color.B && pixels[i + 1] == color.G && pixels[i + 2] == color.R && pixels[i + 3] == 0xFF;
    }

    private static string Describe(byte[] pixels, int width, int x, int y)
    {
        var i = (y * width + x) * 4;
        return $"BGRA({pixels[i]}, {pixels[i + 1]}, {pixels[i + 2]}, {pixels[i + 3]})";
    }

    // 逐像素比對；失敗時指出第一個不同的像素座標、兩邊的值與不同的像素總數，比整個位元組陣列的差異容易判讀。
    private static void AssertPixelsEqual(byte[] expected, byte[] actual, int width)
    {
        Assert.Equal(expected.Length, actual.Length);
        var first = -1;
        var mismatches = 0;
        for (var i = 0; i < expected.Length; i += 4)
        {
            if (!expected.AsSpan(i, 4).SequenceEqual(actual.AsSpan(i, 4)))
            {
                first = first < 0 ? i / 4 : first;
                mismatches++;
            }
        }

        if (first >= 0)
        {
            var (x, y) = (first % width, first / width);
            Assert.Fail(
                $"共 {mismatches} 個像素不同；第一個為 ({x}, {y})，預期 {Describe(expected, width, x, y)}，實際 {Describe(actual, width, x, y)}。");
        }
    }
}
