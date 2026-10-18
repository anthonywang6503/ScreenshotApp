using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotApp.Export;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// PNG 存檔：預設檔名、寫出可解碼且像素相同的 PNG、完整覆寫既有檔案、唯讀目標與不存在的資料夾。
/// 檔案只寫入每個測試專用的暫存目錄；編碼與解碼和實際使用時一樣在 STA 執行緒進行。
/// </summary>
public sealed class PngFileServiceTests : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(2026, 10, 18, 9, 5, 7, 42, "Screenshot_20261018_090507_042.png")]
    [InlineData(2027, 1, 2, 21, 59, 59, 999, "Screenshot_20270102_215959_999.png")] // 24 小時制
    [InlineData(2026, 12, 31, 0, 0, 0, 0, "Screenshot_20261231_000000_000.png")]
    public void DefaultFileName_UsesZeroPaddedTimestampWithMilliseconds(
        int year, int month, int day, int hour, int minute, int second, int millisecond, string expected)
    {
        Assert.Equal(expected, PngFileService.DefaultFileName(new DateTime(year, month, day, hour, minute, second, millisecond)));
    }

    [Theory]
    [InlineData("Bgr32")]
    [InlineData("Pbgra32")] // 合成器輸出的格式
    public void Save_WritesDecodablePngWithSamePixelSizeAndPixels(string format)
    {
        StaRunner.Run(() =>
        {
            var image = CreateImage(37, 23, format == "Pbgra32" ? PixelFormats.Pbgra32 : PixelFormats.Bgr32);
            var path = _temp.Combine("shot.png");

            PngFileService.Save(image, path);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(PngSignature, bytes[..PngSignature.Length]);
            var decoded = Decode(bytes);
            Assert.Equal(37, decoded.PixelWidth);
            Assert.Equal(23, decoded.PixelHeight);
            Assert.Equal(ToPbgra32(image), ToPbgra32(decoded));
            Assert.Equal([path], Directory.GetFiles(_temp.FullPath));
        });
    }

    [Fact]
    public void Save_ExistingFile_IsReplacedCompletely()
    {
        StaRunner.Run(() =>
        {
            var path = _temp.Combine("shot.png");

            // 舊檔遠大於新圖：若只覆寫開頭而沒有截斷，檔案尾端會殘留舊內容。
            var oldContent = new byte[256 * 1024];
            Array.Fill(oldContent, (byte)0xAB);
            File.WriteAllBytes(path, oldContent);
            var first = CreateImage(20, 16, PixelFormats.Pbgra32);
            var second = CreateImage(31, 9, PixelFormats.Pbgra32);

            PngFileService.Save(first, path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length < oldContent.Length, $"新檔 {bytes.Length} 位元組，應小於舊檔的 {oldContent.Length} 位元組。");
            Assert.Equal(ToPbgra32(first), ToPbgra32(Decode(bytes)));

            PngFileService.Save(second, path);

            var replaced = Decode(File.ReadAllBytes(path));
            Assert.Equal(31, replaced.PixelWidth);
            Assert.Equal(9, replaced.PixelHeight);
            Assert.Equal(ToPbgra32(second), ToPbgra32(replaced));
            Assert.Equal([path], Directory.GetFiles(_temp.FullPath));
        });
    }

    [Fact]
    public void Save_ReadOnlyExistingTarget_ThrowsKeepsOriginalAndLeavesNoTemporaryFile()
    {
        StaRunner.Run(() =>
        {
            var path = _temp.Combine("locked.png");
            byte[] original = [0x6F, 0x6C, 0x64, 0x0A];
            File.WriteAllBytes(path, original);
            File.SetAttributes(path, FileAttributes.ReadOnly);

            var exception = Record.Exception(() => PngFileService.Save(CreateImage(8, 8, PixelFormats.Pbgra32), path));

            Assert.True(
                exception is UnauthorizedAccessException or IOException,
                $"預期 UnauthorizedAccessException 或 IOException，實際為 {exception?.GetType().Name ?? "沒有例外"}。");
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal([path], Directory.GetFiles(_temp.FullPath));
        });
    }

    [Fact]
    public void Save_MissingDirectory_ThrowsDirectoryNotFoundAndCreatesNothing()
    {
        StaRunner.Run(() =>
        {
            var path = _temp.Combine("missing", "shot.png");

            Assert.Throws<DirectoryNotFoundException>(() => PngFileService.Save(CreateImage(8, 8, PixelFormats.Pbgra32), path));
            Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.FullPath));
        });
    }

    // 從記憶體解碼並立即載入，不會留下開啟中的檔案；使用 PNG 解碼器，內容不是 PNG 時會拋出例外。
    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    // 轉成同一種格式再比對：PNG 解碼後的格式（例如 Bgr24、Bgra32）不一定與來源相同。
    private static byte[] ToPbgra32(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    // 不透明的圖樣，每個像素的 (B, G, R) = (x, y, x ^ y) 都不同。
    private static BitmapSource CreateImage(int width, int height, PixelFormat format)
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

        var image = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        image.Freeze();
        return image;
    }
}
