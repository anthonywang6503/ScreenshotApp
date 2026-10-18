namespace ScreenshotApp.Tests.TestSupport;

/// <summary>每個測試專用的暫存目錄；結束時連同唯讀檔一併刪除，測試不觸碰真實的使用者資料夾。</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "ScreenshotApp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    public string FullPath { get; }

    public string Combine(params string[] parts) => Path.Combine([FullPath, .. parts]);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(FullPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            // 暫存目錄殘留不影響測試結果。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
        }
    }
}
