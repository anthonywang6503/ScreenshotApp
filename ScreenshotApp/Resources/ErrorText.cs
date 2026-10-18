using System.IO;
using System.Security;

namespace ScreenshotApp.Resources;

/// <summary>
/// 檔案與登錄操作失敗時給使用者看的原因。.NET 對拒絕存取、找不到路徑、路徑太長、檔案被占用等情況的例外訊息是英文，
/// 這些常見情況改用繁體中文說明；其他情況沿用例外訊息（其餘 Win32 錯誤的說明由 Windows 依系統語言提供）。
/// </summary>
internal static class ErrorText
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);
    private const int HandleDiskFull = unchecked((int)0x80070027);
    private const int DiskFull = unchecked((int)0x80070070);

    public static string Describe(Exception exception) => exception switch
    {
        UnauthorizedAccessException or SecurityException => Strings.Get("Error_AccessDenied"),
        FileNotFoundException or DirectoryNotFoundException => Strings.Get("Error_PathNotFound"),
        PathTooLongException => Strings.Get("Error_PathTooLong"),
        IOException { HResult: SharingViolation or LockViolation } => Strings.Get("Error_FileInUse"),
        IOException { HResult: DiskFull or HandleDiskFull } => Strings.Get("Error_DiskFull"),
        _ => exception.Message,
    };
}
