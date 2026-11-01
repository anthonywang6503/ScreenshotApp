using System.Windows;
using System.Windows.Media;
using ScreenshotApp.Editor.Model;

namespace ScreenshotApp.Tests;

public class EffectAnnotationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT13_Geometry_HitsInteriorAndMovesWithoutMutatingOriginal(bool blur)
    {
        var effect = Create(blur, new PixelRect(10, 20, 30, 40));
        Assert.True(effect.HitTest(new Point(25, 40), 0));
        Assert.True(effect.HitTest(new Point(9, 20), 1));
        Assert.False(effect.HitTest(new Point(8, 20), 1));
        Assert.Equal(new Rect(10, 20, 30, 40), effect.Bounds);
        Assert.Equal(8, effect.Handles.Count);
        Assert.Equal(8, effect.Handles.Select(h => h.Kind).Distinct().Count());
        var moved = Assert.IsAssignableFrom<EffectAnnotation>(effect.Translate(-12, 9));
        Assert.Equal(new PixelRect(-2, 29, 30, 40), moved.Rect);
        Assert.Equal(effect.Id, moved.Id);
        Assert.Equal(effect.Strength, moved.Strength);
        Assert.Equal(new PixelRect(10, 20, 30, 40), effect.Rect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AT13_AllEightHandles_ResizeAndClampToAtLeastOnePixel(bool blur)
    {
        var effect = Create(blur, new PixelRect(10, 20, 30, 40));
        var expected = new[]
        {
            new PixelRect(12, 23, 28, 37), new PixelRect(10, 23, 30, 37), new PixelRect(10, 23, 32, 37),
            new PixelRect(10, 20, 32, 40), new PixelRect(10, 20, 32, 43), new PixelRect(10, 20, 30, 43),
            new PixelRect(12, 20, 28, 43), new PixelRect(12, 20, 28, 40),
        };
        for (var i = 0; i < effect.Handles.Count; i++)
        {
            Assert.Equal(expected[i], Assert.IsAssignableFrom<EffectAnnotation>(effect.Resize(effect.Handles[i].Kind, 2, 3)).Rect);
        }

        Assert.Equal(new PixelRect(39, 59, 1, 1), Assert.IsAssignableFrom<EffectAnnotation>(effect.Resize(HandleKind.TopLeft, 29, 39)).Rect);
        Assert.Equal(new PixelRect(39, 59, 72, 62), Assert.IsAssignableFrom<EffectAnnotation>(effect.Resize(HandleKind.TopLeft, 100, 100)).Rect);
        Assert.Equal(new PixelRect(10, 20, 1, 1), Assert.IsAssignableFrom<EffectAnnotation>(effect.Resize(HandleKind.BottomRight, -29, -39)).Rect);
    }

    internal static EffectAnnotation Create(bool blur, PixelRect rect, int strength = 3) => blur
        ? new BlurAnnotation { Rect = rect, Strength = strength, Color = Colors.Transparent }
        : new MosaicAnnotation { Rect = rect, Strength = strength, Color = Colors.Transparent };
}
