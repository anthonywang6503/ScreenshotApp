using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace ScreenshotApp.Export;

/// <summary>把合成後的圖片存成 PNG 檔。</summary>
internal static class PngFileService
{
    public static string DefaultFileName(DateTime time) =>
        "Screenshot_" + time.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".png";

    /// <summary>
    /// 先寫入同一資料夾的暫存檔，完整寫入後才取代目標檔案：中途失敗不會留下不完整的 PNG，也不會破壞同名的既有檔案。
    /// 失敗時拋出 <see cref="IOException"/>、<see cref="UnauthorizedAccessException"/> 等例外，由呼叫端提示使用者。
    /// </summary>
    public static void Save(BitmapSource image, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(fullPath)!, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
            {
                encoder.Save(stream);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
