using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor;
using ScreenshotApp.Editor.Commands;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Editor.Rendering;
using ScreenshotApp.Export;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

public class EffectRenderingTests
{
    [Theory]
    [InlineData(false, 3, 2, 9, 7)]
    [InlineData(true, 3, 2, 9, 7)]
    [InlineData(false, -2, -1, 9, 7)]
    [InlineData(true, -2, -1, 9, 7)]
    [InlineData(false, 15, 11, 8, 6)]
    [InlineData(true, 15, 11, 8, 6)]
    [InlineData(false, 17, 12, 1, 1)]
    [InlineData(true, 17, 12, 1, 1)]
    public void AT13_Render_MatchesReferenceInsideAndOriginalOutside(bool blur, int x, int y, int width, int height)
    {
        StaRunner.Run(() =>
        {
            var source = CreateSource();
            var before = Pixels(source);
            var document = new ImageDocument(source);
            var effect = EffectAnnotationTests.Create(blur, new PixelRect(x, y, width, height));
            Add(document, effect);
            var rect = effect.Rect.Intersect(document.Bounds);
            var input = new byte[rect.Width * rect.Height * 4];
            source.CopyPixels(new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height), input, rect.Width * 4, 0);
            var expected = blur ? PixelEffectsTests.ReferenceBlur(input, rect.Width, rect.Height, 3) :
                PixelEffectsTests.ReferenceMosaic(input, rect.Width, rect.Height, 3, rect.X - x, rect.Y - y);
            var actual = Pixels(ImageRenderer.Render(document));
            for (var yy = 0; yy < document.PixelHeight; yy++)
            {
                for (var xx = 0; xx < document.PixelWidth; xx++)
                {
                    var i = (yy * document.PixelWidth + xx) * 4;
                    var inside = rect.Contains(new PixelPoint(xx, yy));
                    for (var c = 0; c < 3; c++)
                    {
                        Assert.Equal(inside ? expected[((yy - rect.Y) * rect.Width + xx - rect.X) * 4 + c] : before[i + c], actual[i + c]);
                    }

                    Assert.Equal(255, actual[i + 3]);
                }
            }

            Assert.Equal(before, Pixels(source));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT13_LayersCropAndExport_KeepOnlyComposedPixels(bool blur)
    {
        StaRunner.Run(() =>
        {
            var source = CreateSource();
            var document = new ImageDocument(source);
            var rect = new PixelRect(2, 2, 12, 9);
            var effect = EffectAnnotationTests.Create(blur, rect);
            var underneath = new RectangleAnnotation { Color = Colors.Red, Rect = rect, StrokeWidth = 20 };
            Add(document, underneath);
            Add(document, effect);
            var plainEffect = new ImageDocument(source);
            Add(plainEffect, effect);
            Assert.Equal(Pixels(ImageRenderer.Render(plainEffect)), Pixels(ImageRenderer.Render(document)));
            Add(document, new RectangleAnnotation { Color = Colors.Lime, Rect = new PixelRect(4, 4, 2, 2), StrokeWidth = 2 });
            var full = ImageRenderer.Render(document);
            Assert.Equal(new byte[] { 0, 255, 0, 255 }, Pixels(full).Skip((4 * 18 + 4) * 4).Take(4));
            var crop = new PixelRect(5, 3, 10, 8);
            document.History.Execute(new SetCropCommand(document, document.Bounds, crop));
            var cropped = ImageRenderer.Render(document);
            var expected = new byte[crop.Width * crop.Height * 4];
            full.CopyPixels(new Int32Rect(crop.X, crop.Y, crop.Width, crop.Height), expected, crop.Width * 4, 0);
            Assert.Equal(expected, Pixels(cropped));

            using var temp = new TempDirectory();
            var path = temp.Combine("effect.png");
            PngFileService.Save(cropped, path);
            var payload = ClipboardPayload.Create(cropped);
            Assert.Equal(File.ReadAllBytes(path), payload.Png);
            using var stream = new MemoryStream(payload.Png);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = Assert.Single(decoder.Frames);
            Assert.Equal(expected, Pixels(new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0)));
            Assert.Equal(new[] { System.Windows.DataFormats.Dib, ClipboardPayload.PngFormat }.Order(), payload.ToDataObject().GetFormats(false).Order());
            var stride = (crop.Width * 3 + 3) & ~3;
            Assert.Equal(40 + stride * crop.Height, payload.Dib.Length);
            for (var yy = 0; yy < crop.Height; yy++)
            {
                for (var xx = 0; xx < crop.Width; xx++)
                {
                    for (var c = 0; c < 3; c++)
                    {
                        Assert.Equal(expected[(yy * crop.Width + xx) * 4 + c], payload.Dib[40 + (crop.Height - yy - 1) * stride + xx * 3 + c]);
                    }
                }
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT13_Cache_KeysIncludeSourceGeometryKindStrengthAndEvictOldEntries(bool blur)
    {
        StaRunner.Run(() =>
        {
            var source = CreateSource();
            var effect = EffectAnnotationTests.Create(blur, new PixelRect(1, 1, 10, 8));
            var first = EffectBitmapCache.Get(source, effect)!;
            Assert.True(first.IsFrozen);
            Assert.Equal(PixelFormats.Bgr32, first.Format);
            Assert.Same(first, EffectBitmapCache.Get(source, effect with { Id = Guid.NewGuid(), Color = Colors.Blue }));
            Assert.NotSame(first, EffectBitmapCache.Get(CreateSource(), effect));
            Assert.NotSame(first, EffectBitmapCache.Get(source, EffectAnnotationTests.Create(!blur, effect.Rect)));
            Assert.NotSame(first, EffectBitmapCache.Get(source, effect with { Rect = effect.Rect.Offset(1, 0) }));
            Assert.NotSame(first, EffectBitmapCache.Get(source, effect with { Rect = new PixelRect(1, 1, 9, 8) }));
            for (var strength = 4; strength <= 12; strength++)
            {
                Assert.NotSame(first, EffectBitmapCache.Get(source, effect with { Strength = strength }));
            }

            var regenerated = EffectBitmapCache.Get(source, effect)!;
            Assert.NotSame(first, regenerated);
            Assert.Equal(Pixels(first), Pixels(regenerated));
            Assert.Null(EffectBitmapCache.Get(source, effect with { Rect = new PixelRect(100, 100, 10, 10) }));
        });
    }

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(false, 2.0)]
    [InlineData(true, 1.25)]
    [InlineData(true, 4.0)]
    public void AT07_CanvasRenderAtOtherZooms_DoesNotChangeExport(bool blur, double zoom)
    {
        StaRunner.Run(() =>
        {
            var document = new ImageDocument(CreateSource());
            Add(document, EffectAnnotationTests.Create(blur, document.Bounds));
            var expected = Pixels(ImageRenderer.Render(document));
            var layer = new AnnotationLayer
            {
                Session = new EditorSession(document, new AnnotationStyle(Colors.Red, 2, "Segoe UI", 16)),
                Width = 18, Height = 13,
            };
            layer.Measure(new Size(18, 13));
            layer.Arrange(new Rect(0, 0, 18, 13));
            var actualSize = new RenderTargetBitmap(18, 13, 96, 96, PixelFormats.Pbgra32);
            actualSize.Render(layer);
            Assert.Equal(expected, Pixels(actualSize));
            layer.RenderTransform = new ScaleTransform(zoom, zoom);
            var scaled = new RenderTargetBitmap((int)Math.Ceiling(18 * zoom), (int)Math.Ceiling(13 * zoom), 96, 96, PixelFormats.Pbgra32);
            scaled.Render(layer);
            Assert.Equal(expected, Pixels(ImageRenderer.Render(document)));
        });
    }

    [Fact]
    public void AT13_TransparentSource_StillProducesOpaqueEffect()
    {
        StaRunner.Run(() =>
        {
            var source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 12, 34, 56, 0 }, 4);
            source.Freeze();
            var document = new ImageDocument(source);
            Add(document, EffectAnnotationTests.Create(false, document.Bounds));
            Assert.Equal(new byte[] { 12, 34, 56, 255 }, Pixels(ImageRenderer.Render(document)));
        });
    }

    internal static BitmapSource CreateSource(int width = 18, int height = 13)
    {
        var image = BitmapSource.Create(width, height, 144, 144, PixelFormats.Bgr32, null, PixelEffectsTests.Pattern(width, height), width * 4);
        image.Freeze();
        return image;
    }

    internal static byte[] Pixels(BitmapSource source)
    {
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }

    private static void Add(ImageDocument document, Annotation annotation) =>
        document.History.Execute(new AddAnnotationCommand(document, annotation));
}
