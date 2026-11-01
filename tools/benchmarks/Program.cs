using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Editor.Model;
using ScreenshotApp.Editor.Rendering;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        const int width = 3840;
        const int height = 2160;
        var pixels = new byte[width * height * 4];
        new Random(10).NextBytes(pixels);
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        source.Freeze();
        // 先以小圖暖機，完整 4K 項目仍須重新取樣、配置及計算。
        PixelEffects.Mosaic(new byte[64 * 64 * 4], 64, 64, 12);
        PixelEffects.Blur(new byte[64 * 64 * 4], 64, 64, 6);
        var results = new List<object>();
        foreach (var blur in new[] { false, true })
        {
            EffectAnnotation effect = blur
                ? new BlurAnnotation { Rect = new PixelRect(0, 0, width, height), Strength = 6, Color = Colors.Transparent }
                : new MosaicAnnotation { Rect = new PixelRect(0, 0, width, height), Strength = 12, Color = Colors.Transparent };
            var samples = new List<double>();
            var cached = new List<double>();
            for (var i = 0; i < 5; i++)
            {
                var changed = effect with { Rect = new PixelRect(i, 0, width - i, height) };
                var watch = Stopwatch.StartNew();
                var image = EffectBitmapCache.Get(source, changed)!;
                samples.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                if (!ReferenceEquals(image, EffectBitmapCache.Get(source, changed)))
                {
                    throw new InvalidOperationException("預期快取命中。");
                }

                cached.Add(watch.Elapsed.TotalMilliseconds);
            }

            results.Add(new { effect = blur ? "blur" : "mosaic", strength = effect.Strength, recomputeMs = samples, cacheHitMs = cached });
        }

        var report = new
        {
            width, height, iterations = 5, runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, logicalProcessors = Environment.ProcessorCount,
            processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
            assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PixelEffects).Assembly.Location))),
            scope = "Release；單一 STA 執行緒；含原圖取樣、效果計算、Bitmap 建立／Freeze；每輪移動左邊界 1px；不含 WPF 呈現、桌面輸入與匯出編碼。",
            results,
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n");
        if (args.Length > 0)
        {
            File.WriteAllText(args[0], json + "\n");
        }

        Console.WriteLine(json);
    }
}
