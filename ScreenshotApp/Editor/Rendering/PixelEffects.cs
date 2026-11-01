namespace ScreenshotApp.Editor.Rendering;

/// <summary>緊密排列的四位元組 BGR 像素運算；忽略輸入 alpha，不修改輸入，輸出 alpha 一律為 255。</summary>
internal static class PixelEffects
{
    public static byte[] Mosaic(byte[] source, int width, int height, int blockSize, int offsetX = 0, int offsetY = 0)
    {
        Validate(source, width, height, blockSize, Model.MosaicAnnotation.MinimumStrength, Model.MosaicAnnotation.MaximumStrength);
        ArgumentOutOfRangeException.ThrowIfNegative(offsetX);
        ArgumentOutOfRangeException.ThrowIfNegative(offsetY);
        var output = new byte[source.Length];
        for (var top = 0; top < height;)
        {
            var bottom = Math.Min(height, top + blockSize - (top + offsetY) % blockSize);
            for (var left = 0; left < width;)
            {
                var right = Math.Min(width, left + blockSize - (left + offsetX) % blockSize);
                var count = (right - left) * (bottom - top);
                var blue = 0;
                var green = 0;
                var red = 0;
                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        var i = (y * width + x) * 4;
                        blue += source[i];
                        green += source[i + 1];
                        red += source[i + 2];
                    }
                }

                var averageBlue = Average(blue, count);
                var averageGreen = Average(green, count);
                var averageRed = Average(red, count);
                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        var i = (y * width + x) * 4;
                        output[i] = averageBlue;
                        output[i + 1] = averageGreen;
                        output[i + 2] = averageRed;
                        output[i + 3] = 255;
                    }
                }

                left = right;
            }

            top = bottom;
        }

        return output;
    }

    public static byte[] Blur(byte[] source, int width, int height, int radius)
    {
        Validate(source, width, height, radius, Model.BlurAnnotation.MinimumStrength, Model.BlurAnnotation.MaximumStrength);
        var horizontal = new byte[source.Length];
        var output = new byte[source.Length];
        for (var pass = 0; pass < 3; pass++)
        {
            HorizontalPass(pass == 0 ? source : output, horizontal, width, height, radius);
            VerticalPass(horizontal, output, width, height, radius);
        }

        return output;
    }

    private static void HorizontalPass(byte[] source, byte[] output, int length, int lines, int radius)
    {
        const int step = 4;
        for (var line = 0; line < lines; line++)
        {
            var origin = line * length * 4;
            var blue = 0;
            var green = 0;
            var red = 0;
            var first = 0;
            var last = -1;
            for (var position = 0; position < length; position++)
            {
                var end = Math.Min(length - 1, position + radius);
                while (last < end)
                {
                    var i = origin + ++last * step;
                    blue += source[i];
                    green += source[i + 1];
                    red += source[i + 2];
                }

                var start = Math.Max(0, position - radius);
                while (first < start)
                {
                    var i = origin + first++ * step;
                    blue -= source[i];
                    green -= source[i + 1];
                    red -= source[i + 2];
                }

                var target = origin + position * step;
                var count = last - first + 1;
                output[target] = Average(blue, count);
                output[target + 1] = Average(green, count);
                output[target + 2] = Average(red, count);
                output[target + 3] = 255;
            }
        }
    }

    // 按列維護所有欄的總和，讓讀寫連續，避免大圖逐欄跨越整張影像的記憶體存取。
    private static void VerticalPass(byte[] source, byte[] output, int width, int height, int radius)
    {
        var sums = new int[width * 4];
        var stride = width * 4;
        var first = 0;
        var last = -1;
        for (var y = 0; y < height; y++)
        {
            var end = Math.Min(height - 1, y + radius);
            while (last < end)
            {
                var row = ++last * stride;
                for (var x = 0; x < stride; x++)
                {
                    sums[x] += source[row + x];
                }
            }

            var start = Math.Max(0, y - radius);
            while (first < start)
            {
                var row = first++ * stride;
                for (var x = 0; x < stride; x++)
                {
                    sums[x] -= source[row + x];
                }
            }

            var count = last - first + 1;
            var target = y * stride;
            for (var x = 0; x < stride; x += 4)
            {
                output[target + x] = Average(sums[x], count);
                output[target + x + 1] = Average(sums[x + 1], count);
                output[target + x + 2] = Average(sums[x + 2], count);
                output[target + x + 3] = 255;
            }
        }
    }

    private static byte Average(int sum, int count) => (byte)((sum + count / 2) / count);

    private static void Validate(byte[] source, int width, int height, int strength, int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(strength, minimum);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(strength, maximum);
        if (source.Length != checked(width * height * 4))
        {
            throw new ArgumentException("像素陣列大小與寬高不符。", nameof(source));
        }
    }
}
