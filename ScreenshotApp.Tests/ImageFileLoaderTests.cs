using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Export;
using ScreenshotApp.Resources;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 開啟既有圖片：PNG（24 位元、含 Alpha、索引色、灰階）、BMP 與 JPEG 轉成凍結、96 DPI 的不透明 Bgr32，透明部分以白色合成；
/// 依內容而非副檔名判斷格式；文字檔、空檔案、截斷的檔案、不支援的格式、找不到或被獨占的檔案與尺寸過大的圖片各自回報對應的訊息而不拋出例外；
/// 讀取後立即釋放檔案。測試圖片以 WPF 編碼器即時產生，來源一律記錄為 144 DPI，只寫入每個測試專用的暫存目錄。
/// 多數測試在 STA 執行緒解碼；編輯器實際在背景執行緒開檔，另有一個測試在執行緒集區載入。
/// </summary>
public sealed class ImageFileLoaderTests : IDisposable
{
    private const double SourceDpi = 144;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Load_OnAThreadPoolThread_ReturnsTheSameFrozenImage()
    {
        // 編輯器在背景執行緒開檔，結果交給 UI 執行緒使用。
        var source = CreatePattern(29, 13);
        var path = Write("background.png", Encode(new PngBitmapEncoder(), source));

        var result = await Task.Run(() => ImageFileLoader.Load(path));

        var image = AssertLoaded(result, 29, 13);
        AssertPixelsEqual(CopyPixels(source), CopyPixels(image), 29);
    }

    [Fact]
    public void Load_Png24Bit_ReturnsIdenticalPixels()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePattern(37, 23);
            var bytes = Encode(new PngBitmapEncoder(), new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0));
            Assert.Equal(PixelFormats.Bgr24, DecodeNative(bytes).Format);

            var image = AssertLoaded(ImageFileLoader.Load(Write("rgb.png", bytes)), 37, 23);

            AssertPixelsEqual(CopyPixels(source), CopyPixels(image), 37);
        });
    }

    [Fact]
    public void Load_Png32BitWithAlpha_CompositesOntoWhiteFromPremultipliedValues()
    {
        StaRunner.Run(() =>
        {
            // 來源 (B, G, R, A) 與預期輸出 (B, G, R)：每個通道為 WIC 預乘後的值 round(c × A / 255) 加上 255 − A。
            (byte[] Source, byte[] Expected)[] cases =
            [
                ([10, 20, 30, 255], [10, 20, 30]), // 完全不透明：與檔案中的值完全相同
                ([200, 100, 50, 255], [200, 100, 50]),
                ([10, 20, 30, 0], [255, 255, 255]), // 完全透明：白色
                ([0, 0, 0, 0], [255, 255, 255]),
                ([200, 100, 50, 128], [227, 177, 152]), // 100 + 127、50 + 127、25 + 127
                ([0, 255, 0, 128], [127, 255, 127]), // 0 + 127、128 + 127
                ([200, 100, 50, 51], [244, 224, 214]), // 40 + 204、20 + 204、10 + 204
                ([0, 0, 0, 1], [254, 254, 254]), // 0 + 254
                ([255, 255, 255, 254], [255, 255, 255]), // 254 + 1
            ];
            var pixels = cases.SelectMany(item => item.Source).ToArray();
            var source = BitmapSource.Create(cases.Length, 1, SourceDpi, SourceDpi, PixelFormats.Bgra32, null, pixels, cases.Length * 4);
            var bytes = Encode(new PngBitmapEncoder(), source);
            Assert.Equal(PixelFormats.Bgra32, DecodeNative(bytes).Format);

            var image = AssertLoaded(ImageFileLoader.Load(Write("alpha.png", bytes)), cases.Length, 1);

            var expected = cases.SelectMany(item => item.Expected.Append((byte)0xFF)).ToArray();
            AssertPixelsEqual(expected, CopyPixels(image), cases.Length);
        });
    }

    [Fact]
    public void Load_IndexedPng_ReturnsPaletteColorsAndWhiteForTransparentEntry()
    {
        StaRunner.Run(() =>
        {
            var palette = new BitmapPalette(
            [
                Color.FromRgb(255, 0, 0), Color.FromRgb(0, 128, 0), Color.FromRgb(10, 20, 30), Color.FromArgb(0, 50, 60, 70),
            ]);
            byte[] indices = [0, 1, 2, 3, 3, 2, 1, 0];
            var source = BitmapSource.Create(4, 2, SourceDpi, SourceDpi, PixelFormats.Indexed8, palette, indices, 4);
            var bytes = Encode(new PngBitmapEncoder(), source);
            Assert.NotNull(DecodeNative(bytes).Palette);

            var image = AssertLoaded(ImageFileLoader.Load(Write("indexed.png", bytes)), 4, 2);

            byte[] red = [0, 0, 255, 255], green = [0, 128, 0, 255], dark = [30, 20, 10, 255], white = [255, 255, 255, 255];
            AssertPixelsEqual([.. red, .. green, .. dark, .. white, .. white, .. dark, .. green, .. red], CopyPixels(image), 4);
        });
    }

    [Fact]
    public void Load_GrayscalePng_ReturnsSameLevelInEveryChannel()
    {
        StaRunner.Run(() =>
        {
            var levels = Enumerable.Range(0, 256).Select(level => (byte)level).ToArray();
            var source = BitmapSource.Create(16, 16, SourceDpi, SourceDpi, PixelFormats.Gray8, null, levels, 16);
            var bytes = Encode(new PngBitmapEncoder(), source);
            Assert.Equal(PixelFormats.Gray8, DecodeNative(bytes).Format);

            var image = AssertLoaded(ImageFileLoader.Load(Write("gray.png", bytes)), 16, 16);

            var expected = levels.SelectMany(level => new[] { level, level, level, (byte)0xFF }).ToArray();
            AssertPixelsEqual(expected, CopyPixels(image), 16);
        });
    }

    [Fact]
    public void Load_Bmp24Bit_ReturnsIdenticalPixels()
    {
        StaRunner.Run(() =>
        {
            // 寬 37 像素的列為 111 位元組，BMP 會補齊到 4 的倍數。
            var source = CreatePattern(37, 23);
            var bytes = Encode(new BmpBitmapEncoder(), new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0));
            Assert.Equal(PixelFormats.Bgr24, DecodeNative(bytes).Format);

            var image = AssertLoaded(ImageFileLoader.Load(Write("rgb.bmp", bytes)), 37, 23);

            AssertPixelsEqual(CopyPixels(source), CopyPixels(image), 37);
        });
    }

    [Fact]
    public void Load_Jpeg_ReturnsSameSizeWithPixelsCloseToSource()
    {
        StaRunner.Run(() =>
        {
            // 平滑漸層的 JPEG 壓縮誤差很小；藍、綠兩個通道沿不同方向變化，通道錯置或透明度處理錯誤都會造成大誤差。
            const int width = 64;
            const int height = 48;
            var source = CreateGradient(width, height);
            var bytes = Encode(new JpegBitmapEncoder { QualityLevel = 90 }, source);

            var image = AssertLoaded(ImageFileLoader.Load(Write("photo.jpg", bytes)), width, height);

            var expected = CopyPixels(source);
            var actual = CopyPixels(image);
            var maxDifference = 0;
            var totalDifference = 0L;
            for (var i = 0; i < expected.Length; i += 4)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    var difference = Math.Abs(expected[i + channel] - actual[i + channel]);
                    maxDifference = Math.Max(maxDifference, difference);
                    totalDifference += difference;
                }
            }

            var meanDifference = (double)totalDifference / (width * height * 3);
            Assert.True(maxDifference <= 16, $"單一通道最大誤差 {maxDifference}，預期不超過 16。");
            Assert.True(meanDifference <= 2, $"平均誤差 {meanDifference:F2}，預期不超過 2。");
        });
    }

    [Fact]
    public void Load_PngContentWithJpgExtension_LoadsByContent()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePattern(20, 10);
            var path = Write("mislabeled.jpg", Encode(new PngBitmapEncoder(), source));

            var image = AssertLoaded(ImageFileLoader.Load(path), 20, 10);

            // 逐像素相同：內容確實以無失真的 PNG 解碼，而不是依副檔名當成 JPEG。
            AssertPixelsEqual(CopyPixels(source), CopyPixels(image), 20);
        });
    }

    [Fact]
    public void Load_TextFileWithPngExtension_ReturnsCorruptError()
    {
        StaRunner.Run(() =>
        {
            var path = Write("notes.png", Encoding.UTF8.GetBytes("這不是圖片，只是一般文字。\n"));

            AssertFailed(ImageFileLoader.Load(path), Strings.Get("ImageLoad_Corrupt"));
        });
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("tiff")]
    [InlineData("wmp")]
    public void Load_DecodableButUnsupportedFormat_ReturnsUnsupportedFormatErrorEvenWithPngExtension(string format)
    {
        StaRunner.Run(() =>
        {
            BitmapEncoder encoder = format switch
            {
                "gif" => new GifBitmapEncoder(),
                "tiff" => new TiffBitmapEncoder(),
                _ => new WmpBitmapEncoder(),
            };
            var path = Write("image.png", Encode(encoder, CreatePattern(16, 16)));

            AssertFailed(ImageFileLoader.Load(path), Strings.Get("ImageLoad_UnsupportedFormat"));
        });
    }

    [Fact]
    public void Load_EmptyFile_ReturnsCorruptError()
    {
        StaRunner.Run(() =>
        {
            AssertFailed(ImageFileLoader.Load(Write("empty.png", [])), Strings.Get("ImageLoad_Corrupt"));
        });
    }

    // WIC 的 PNG 解碼器會把影像資料（IDAT）中途截斷的部分補成黑色而不回報錯誤，因此 PNG 截斷在影像資料之前。
    [Theory]
    [InlineData("png", 8)] // 只有 PNG 簽章
    [InlineData("png", 20)] // 截斷在 IHDR 之中
    [InlineData("png", 33)] // 完整的簽章與 IHDR，沒有影像資料
    [InlineData("bmp", 30)] // BMP 標頭不完整：WIC 拋出 IOException
    [InlineData("bmp", 1300)] // 像素資料只剩一半：WIC 拋出 FileFormatException
    public void Load_TruncatedFile_ReturnsCorruptError(string format, int keptBytes)
    {
        StaRunner.Run(() =>
        {
            var source = CreatePattern(37, 23);
            var bytes = format == "png"
                ? Encode(new PngBitmapEncoder(), source)
                : Encode(new BmpBitmapEncoder(), new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0));
            Assert.True(keptBytes < bytes.Length, $"測試資料錯誤：完整檔案只有 {bytes.Length} 位元組。");

            var result = ImageFileLoader.Load(Write("truncated." + format, bytes[..keptBytes]));

            AssertFailed(result, Strings.Get("ImageLoad_Corrupt"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_MissingFileOrFolder_ReturnsFileNotFoundError(bool missingFolder)
    {
        StaRunner.Run(() =>
        {
            var path = missingFolder ? _temp.Combine("missing", "image.png") : _temp.Combine("missing.png");

            AssertFailed(ImageFileLoader.Load(path), Strings.Get("ImageLoad_FileNotFound"));
        });
    }

    [Fact]
    public void Load_PathThatCannotBeOpenedForReading_ReturnsAccessDeniedError()
    {
        // 以檔案方式開啟資料夾時 Windows 拒絕存取，與沒有讀取權限的檔案是同一種例外。
        AssertFailed(ImageFileLoader.Load(_temp.FullPath), Strings.Get("ImageLoad_AccessDenied"));
    }

    [Fact]
    public void Load_FileLockedExclusivelyByAnotherHandle_ReturnsReadFailedWithFileInUseReason()
    {
        StaRunner.Run(() =>
        {
            var path = Write("locked.png", Encode(new PngBitmapEncoder(), CreatePattern(8, 8)));
            using var locker = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            AssertFailed(ImageFileLoader.Load(path), Strings.Format("ImageLoad_ReadFailed", Strings.Get("Error_FileInUse")));
        });
    }

    [Fact]
    public void Load_FileOpenForWritingByAnotherHandleThatSharesAccess_StillLoads()
    {
        StaRunner.Run(() =>
        {
            var source = CreatePattern(8, 8);
            var path = Write("shared.png", Encode(new PngBitmapEncoder(), source));
            using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var image = AssertLoaded(ImageFileLoader.Load(path), 8, 8);

            AssertPixelsEqual(CopyPixels(source), CopyPixels(image), 8);
        });
    }

    [Fact]
    public void Load_PngDeclaringOversizedDimensions_FailsFastWithSizeErrorWithoutDecodingPixels()
    {
        StaRunner.Run(() =>
        {
            // IHDR 宣告 20000 × 20000（4 億像素），只附兩列像素資料。WIC 不會因影像資料不完整而報錯：
            // 若先完整解碼，會配置超過 1 GB 的記憶體並花上將近一秒，最後得到一張大多是黑色的圖片。
            const int size = 20000;
            var path = Write("huge.png", CreateOversizedPng(size, size));

            // 先載入一張小圖，排除 WIC 首次初始化的時間。
            AssertLoaded(ImageFileLoader.Load(Write("warmup.png", Encode(new PngBitmapEncoder(), CreatePattern(4, 4)))), 4, 4);

            using var process = Process.GetCurrentProcess();
            var peakBefore = process.PeakPagedMemorySize64;
            var stopwatch = Stopwatch.StartNew();
            var result = ImageFileLoader.Load(path);
            stopwatch.Stop();
            process.Refresh();
            var peakGrowth = process.PeakPagedMemorySize64 - peakBefore;

            AssertFailed(result, Strings.Format("ImageLoad_SizeOutOfRange", size, size, ImageFileLoader.MaxPixels));
            Assert.Contains(size.ToString(CultureInfo.CurrentCulture), result.Error);
            Assert.Contains(ImageFileLoader.MaxPixels.ToString("N0", CultureInfo.CurrentCulture), result.Error);
            Assert.NotEqual(Strings.Get("ImageLoad_Corrupt"), result.Error);
            Assert.NotEqual(Strings.Get("ImageLoad_OutOfMemory"), result.Error);
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"耗時 {stopwatch.ElapsedMilliseconds} ms，預期在 1 秒內回傳。");
            Assert.True(
                peakGrowth < 256L * 1024 * 1024,
                $"記憶體使用量峰值增加 {peakGrowth / (1024 * 1024)} MB，表示圖片的像素已被解碼。");
        });
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("unsupported")]
    [InlineData("oversized")]
    [InlineData("corrupt")]
    public void Load_AnyOutcome_ReleasesFileSoItCanBeRenamedAndDeletedImmediately(string kind)
    {
        StaRunner.Run(() =>
        {
            var source = CreatePattern(16, 16);
            var bytes = kind switch
            {
                "valid" => Encode(new PngBitmapEncoder(), source),
                "unsupported" => Encode(new GifBitmapEncoder(), source),
                "oversized" => CreateOversizedPng(20000, 20000),
                _ => Encode(new PngBitmapEncoder(), source)[..33],
            };
            var path = Write("image.png", bytes);

            var result = ImageFileLoader.Load(path);

            // 讀取時允許其他程式刪除，單憑刪除成功無法證明已關閉檔案；以獨占方式開啟，只要還有未關閉的控制代碼就會失敗。
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            var renamed = _temp.Combine("renamed.png");
            File.Move(path, renamed);
            File.Delete(renamed);
            Assert.False(File.Exists(renamed));
            Assert.Equal(kind == "valid", result.Image is not null);
            if (result.Image is not null)
            {
                // 像素在讀取時已全部載入記憶體，與檔案無關。
                AssertPixelsEqual(CopyPixels(source), CopyPixels(result.Image), 16);
            }
        });
    }

    private string Write(string name, byte[] bytes)
    {
        var path = _temp.Combine(name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Encode(BitmapEncoder encoder, BitmapSource source)
    {
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // 以檔案本身的像素格式解碼，確認測試資料確實是要驗證的格式。
    private static BitmapSource DecodeNative(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static BitmapSource AssertLoaded(ImageLoadResult result, int width, int height)
    {
        Assert.Null(result.Error);
        Assert.NotNull(result.Image);
        var image = result.Image;
        Assert.True(image.IsFrozen);
        Assert.Equal(PixelFormats.Bgr32, image.Format);
        Assert.Equal(96, image.DpiX);
        Assert.Equal(96, image.DpiY);
        Assert.Equal(width, image.PixelWidth);
        Assert.Equal(height, image.PixelHeight);
        return image;
    }

    private static void AssertFailed(ImageLoadResult result, string expectedError)
    {
        Assert.Null(result.Image);
        Assert.NotNull(result.Error);

        // 資源鍵遺失時 Strings 會回傳 !Key!，預期值與實際值會同樣是這種形式。
        Assert.DoesNotMatch(@"^![A-Za-z0-9_]+!$", result.Error);
        Assert.Equal(expectedError, result.Error);
    }

    // 不透明的圖樣，每個像素的 (B, G, R) = (x, y, x ^ y) 都不同。
    private static BitmapSource CreatePattern(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 2] = (byte)(x ^ y);
                pixels[i + 3] = 0xFF;
            }
        }

        return CreateBgr32(width, height, pixels);
    }

    // 藍色沿水平方向、綠色沿垂直方向漸變，紅色固定。
    private static BitmapSource CreateGradient(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i] = (byte)(x * 4);
                pixels[i + 1] = (byte)(y * 5);
                pixels[i + 2] = 160;
                pixels[i + 3] = 0xFF;
            }
        }

        return CreateBgr32(width, height, pixels);
    }

    private static BitmapSource CreateBgr32(int width, int height, byte[] pixels)
    {
        var image = BitmapSource.Create(width, height, SourceDpi, SourceDpi, PixelFormats.Bgr32, null, pixels, width * 4);
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

    // 手工組成的 PNG：IHDR 宣告指定的寬高（每色版 8 位元的 RGB），之後只有兩列像素的壓縮資料與 IEND，每個 chunk 的 CRC 都正確。
    // WIC 必須看到 IDAT 才會把內容辨識為 PNG，因此不能只有 IHDR。
    private static byte[] CreateOversizedPng(int width, int height)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // 每個色版 8 位元
        header[9] = 2; // RGB；壓縮、濾波與交錯方式都是 0
        WriteChunk(png, "IHDR", header);

        // 每列是 1 位元組的濾波類型加上寬 × 3 位元組的像素。
        var row = new byte[1 + width * 3];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(row);
            zlib.Write(row);
        }

        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        stream.Write(typeBytes);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32([.. typeBytes, .. data]));
        stream.Write(buffer);
    }

    // PNG 使用的 CRC-32（反射多項式 0xEDB88320），涵蓋 chunk 的類型與資料。
    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    // 逐像素比對；失敗時指出第一個不同的像素座標、兩邊的值與不同的像素總數。
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
                $"共 {mismatches} 個像素不同；第一個為 ({x}, {y})，預期 {Describe(expected, first)}，實際 {Describe(actual, first)}。");
        }
    }

    private static string Describe(byte[] pixels, int index) =>
        $"BGRA({pixels[index * 4]}, {pixels[index * 4 + 1]}, {pixels[index * 4 + 2]}, {pixels[index * 4 + 3]})";
}
