using System.Security;
using ScreenshotApp.Resources;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>
/// 檔案與登錄操作失敗時給使用者看的原因：.NET 以英文描述的常見情況（拒絕存取、找不到路徑、路徑太長、檔案被占用、磁碟已滿）
/// 改為繁體中文，其他情況沿用例外訊息。以實際觸發的檔案錯誤確認 .NET 對這些情況拋出的例外型別與 HResult。
/// </summary>
public class ErrorTextTests
{
    public static TheoryData<Exception, string> KnownExceptions => new()
    {
        { new UnauthorizedAccessException("Access to the path 'C:\\x.png' is denied."), "Error_AccessDenied" },
        { new SecurityException("Requested registry access is not allowed."), "Error_AccessDenied" },
        { new FileNotFoundException("Could not find file 'C:\\x.png'."), "Error_PathNotFound" },
        { new DirectoryNotFoundException("Could not find a part of the path 'C:\\missing\\x.png'."), "Error_PathNotFound" },
        { new PathTooLongException("The path is too long."), "Error_PathTooLong" },
        { new IOException("sharing violation", unchecked((int)0x80070020)), "Error_FileInUse" },
        { new IOException("lock violation", unchecked((int)0x80070021)), "Error_FileInUse" },
        { new IOException("disk full", unchecked((int)0x80070070)), "Error_DiskFull" },
        { new IOException("handle disk full", unchecked((int)0x80070027)), "Error_DiskFull" },
    };

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public void Describe_CommonFailure_UsesTraditionalChineseReason(Exception exception, string key)
    {
        Assert.Equal(Strings.Get(key), ErrorText.Describe(exception));
    }

    [Fact]
    public void Describe_OtherFailure_KeepsExceptionMessage()
    {
        // 其他 Win32 錯誤的訊息由 Windows 依系統語言提供，原樣顯示。
        Assert.Equal("網路名稱已無法使用。", ErrorText.Describe(new IOException("網路名稱已無法使用。", unchecked((int)0x80070040))));
        Assert.Equal("Invalid data.", ErrorText.Describe(new InvalidDataException("Invalid data.")));
    }

    [Fact]
    public void Describe_ReplacingReadOnlyFile_IsAccessDenied()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("new.png");
        var target = temp.Combine("existing.png");
        File.WriteAllText(source, "new");
        File.WriteAllText(target, "old");
        File.SetAttributes(target, FileAttributes.ReadOnly);

        var failure = Record.Exception(() => File.Move(source, target, overwrite: true));

        Assert.Equal(Strings.Get("Error_AccessDenied"), ErrorText.Describe(Assert.IsAssignableFrom<Exception>(failure)));
    }

    [Fact]
    public void Describe_WritingIntoMissingFolder_IsPathNotFound()
    {
        using var temp = new TempDirectory();

        var failure = Record.Exception(() => File.WriteAllText(temp.Combine("missing", "x.png"), "x"));

        Assert.Equal(Strings.Get("Error_PathNotFound"), ErrorText.Describe(Assert.IsAssignableFrom<Exception>(failure)));
    }

    [Fact]
    public void Describe_FileOpenedExclusivelyByAnotherHandle_IsFileInUse()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("locked.png");
        File.WriteAllText(path, "x");
        using var locker = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var failure = Record.Exception(() => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Dispose());

        Assert.Equal(Strings.Get("Error_FileInUse"), ErrorText.Describe(Assert.IsAssignableFrom<Exception>(failure)));
    }
}
