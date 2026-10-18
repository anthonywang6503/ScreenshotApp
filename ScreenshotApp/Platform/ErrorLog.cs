using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace ScreenshotApp.Platform;

/// <summary>
/// 本機故障日誌。只寫入呼叫端提供的情境說明與例外的訊息、堆疊，不接受也不寫入任何圖像或使用者輸入的文字。
/// 單檔超過上限時輪替為前一份（共保留兩份），超過保留期間的檔案於啟動時刪除。寫入失敗時靜默略過，不影響程式運作。
/// </summary>
internal sealed class ErrorLog
{
    public const long DefaultMaxBytes = 256 * 1024;
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly long _maxBytes;
    private readonly TimeSpan _retention;

    public ErrorLog(string logDirectory, long maxBytes = DefaultMaxBytes, TimeSpan? retention = null)
    {
        LogDirectory = logDirectory;
        _maxBytes = maxBytes;
        _retention = retention ?? DefaultRetention;
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotApp", "logs");

    public string LogDirectory { get; }

    public string CurrentFilePath => Path.Combine(LogDirectory, "error.log");

    public string PreviousFilePath => Path.Combine(LogDirectory, "error.1.log");

    public void Write(string context, Exception? exception = null)
    {
        var entry = FormatEntry(context, exception);
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(LogDirectory);
                var current = new FileInfo(CurrentFilePath);
                if (current.Exists && current.Length >= _maxBytes)
                {
                    File.Move(CurrentFilePath, PreviousFilePath, overwrite: true);
                }

                File.AppendAllText(CurrentFilePath, entry, Utf8WithoutBom);
            }
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            // 日誌本身失敗時沒有更安全的回報管道，略過以免影響主要功能。
        }
    }

    public void DeleteExpiredFiles()
    {
        var cutoff = DateTime.UtcNow - _retention;
        foreach (var path in new[] { CurrentFilePath, PreviousFilePath })
        {
            try
            {
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (IsFileSystemFailure(ex))
            {
                // 下次啟動會再嘗試。
            }
        }
    }

    private string FormatEntry(string context, Exception? exception)
    {
        var builder = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(' ')
            .AppendLine(context);
        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        builder.AppendLine();

        // 單筆記錄限制在檔案上限的四分之一個字元內，即使全是多位元組字元，輪替後的檔案大小仍受控。
        var maxChars = (int)(_maxBytes / 4);
        return builder.Length <= maxChars
            ? builder.ToString()
            : builder.ToString(0, maxChars) + Environment.NewLine + "…" + Environment.NewLine + Environment.NewLine;
    }

    private static bool IsFileSystemFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;
}
