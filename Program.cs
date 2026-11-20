namespace QuickMove;

static class Program
{
    [STAThread] // bắt buộc cho kéo-thả (OLE)
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run();
        if (args.Length > 1 && args[0] == "--snapshot") { SelfTest.Snapshot(args[1]); return 0; }
        if (args.Length > 1 && args[0] == "--autostart") { Autostart.Set(args[1] == "on"); return 0; }

        using var mutex = new Mutex(true, "QuickMove.SingleInstance", out bool first);
        if (!first) return 0;

        Application.Run(new TrayApp());
        return 0;
    }
}

class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly DragWatcher _watcher = new();
    readonly MovePopup _popup;
    readonly System.Windows.Forms.Timer _hideTimer = new() { Interval = 400 };
    readonly System.Windows.Forms.Timer _awayTimer = new() { Interval = 50 };
    const int CloseDistance = 120; // px (ở 100% scale) tính từ mép popup

    public TrayApp()
    {
        var history = History.Load(Path.Combine(History.DataDir(), "history.json"));
        _popup = new MovePopup(history);
        _ = _popup.Handle; // tạo cửa sổ trước để lần đầu hiện cho nhanh

        _watcher.HoldDetected += p => { _hideTimer.Stop(); _popup.ShowNear(p); };
        // Nhả chuột ngoài popup → ẩn. Đợi chút để lượt Drop (nếu có) kịp tới trước.
        _watcher.Released += () => { _hideTimer.Stop(); _hideTimer.Start(); };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); _popup.Hide(); };

        // Kéo chuột ra xa popup quá → tự đóng (vẫn kéo-thả bình thường chỗ khác được).
        _awayTimer.Tick += (_, _) =>
        {
            if (!_popup.Visible || !Native.LeftButtonDown) return;
            var p = Native.CursorPos();
            var r = _popup.Bounds;
            int dx = Math.Max(0, Math.Max(r.Left - p.X, p.X - r.Right));
            int dy = Math.Max(0, Math.Max(r.Top - p.Y, p.Y - r.Bottom));
            if (Math.Sqrt(dx * dx + dy * dy) > CloseDistance * _popup.DeviceDpi / 96.0)
            {
                _popup.Hide();
                _watcher.Rearm();
            }
        };
        _awayTimer.Start();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Mở thư mục dữ liệu", null, (_, _) =>
            System.Diagnostics.Process.Start("explorer.exe", History.DataDir()));
        var auto = new ToolStripMenuItem("Chạy cùng Windows") { Checked = Autostart.IsEnabled(), CheckOnClick = true };
        auto.CheckedChanged += (_, _) => Autostart.Set(auto.Checked);
        menu.Items.Add(auto);
        menu.Items.Add("Thoát", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Quick Move – kéo file trong Explorer, giữ yên 2s",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _watcher.Dispose();
        base.ExitThreadCore();
    }
}
