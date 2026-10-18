using System.IO;
using System.Security;
using Microsoft.Win32;

namespace ScreenshotApp.Platform;

/// <summary>以目前使用者的 Run 登錄值控制「登入 Windows 時啟動」，不需要系統管理員權限。</summary>
internal sealed class StartupService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "ScreenshotApp";

    private readonly RegistryKey _root;
    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly string _command;

    public StartupService(string executablePath)
        : this(Registry.CurrentUser, RunKeyPath, DefaultValueName, executablePath)
    {
    }

    public StartupService(RegistryKey root, string keyPath, string valueName, string executablePath)
    {
        _root = root;
        _keyPath = keyPath;
        _valueName = valueName;

        // 路徑加上引號，避免含空白的路徑被 Windows 拆成多個參數。
        _command = $"\"{executablePath}\"";
    }

    public bool IsEnabled()
    {
        using var key = _root.OpenSubKey(_keyPath);
        return string.Equals(key?.GetValue(_valueName) as string, _command, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 讓登錄值符合偏好：啟用時寫入目前執行檔路徑（可攜版移動後會更新），停用時移除。失敗時回傳原因。
    /// </summary>
    public bool TryApply(bool enabled, out string? errorMessage)
    {
        try
        {
            using var key = _root.CreateSubKey(_keyPath, writable: true);
            if (!enabled)
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            }
            else if (!string.Equals(key.GetValue(_valueName) as string, _command, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(_valueName, _command, RegistryValueKind.String);
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
