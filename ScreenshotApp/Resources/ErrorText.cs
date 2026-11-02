using System.IO;
using System.Security;

namespace ScreenshotApp.Resources;

/// <summary>
/// 檔案與登錄操作的常見失敗依介面語言說明；其他情況沿用例外訊息。
/// 未列出的 Win32 錯誤由 Windows 依系統語言提供說明。
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
