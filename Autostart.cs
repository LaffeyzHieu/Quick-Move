using Microsoft.Win32;

namespace QuickMove;

// Bật/tắt chạy cùng Windows qua khoá Run của user hiện tại (không cần quyền admin).
static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "QuickMove";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Không xác định được đường dẫn app");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
