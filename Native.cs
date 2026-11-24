using System.Runtime.InteropServices;
using System.Text;

namespace QuickMove;

// Các hàm Win32 cần dùng (giống JNI bên Android: gọi thẳng vào API của hệ điều hành).
static class Native
{
    public const int VK_LBUTTON = 0x01;
    const uint GA_ROOT = 2;
    const int SM_CXDRAG = 68;
    const int IDC_ARROW = 32512;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT pt; }

    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int idx);
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO ci);
    [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr hInst, int id);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static bool LeftButtonDown => (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

    public static Point CursorPos()
    {
        GetCursorPos(out var p);
        return new Point(p.X, p.Y);
    }

    public static int DragThreshold => Math.Max(4, GetSystemMetrics(SM_CXDRAG));

    // Cửa sổ dưới con trỏ có phải File Explorer hoặc Desktop không.
    public static bool IsExplorerAt(Point pt)
    {
        var root = GetAncestor(WindowFromPoint(new POINT { X = pt.X, Y = pt.Y }), GA_ROOT);
        var sb = new StringBuilder(64);
        GetClassName(root, sb, sb.Capacity);
        return sb.ToString() is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW";
    }

    // Đang kéo-thả thật (OLE drag) chứ không phải kéo khung chọn nhiều file.
    // Explorer hiện ảnh "bóng ma" của file bằng cửa sổ lớp SysDragImage; khung chọn thì không.
    // Dự phòng: lúc kéo-thả, con trỏ không còn là mũi tên thường.
    public static bool IsOleDragActive()
    {
        var img = FindWindow("SysDragImage", null);
        if (img != IntPtr.Zero && IsWindowVisible(img)) return true;
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        return GetCursorInfo(ref ci) && ci.hCursor != IntPtr.Zero && ci.hCursor != LoadCursor(IntPtr.Zero, IDC_ARROW);
    }

    // Bo góc cửa sổ kiểu Windows 11.
    public static void RoundCorners(IntPtr hwnd)
    {
        int round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
        int blue = 0xD47800; // màu viền (BGR), để popup tối không chìm vào Explorer nền tối
        DwmSetWindowAttribute(hwnd, 34, ref blue, sizeof(int));
    }
}
