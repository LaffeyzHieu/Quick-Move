using System.Drawing.Drawing2D;

namespace QuickMove;

// Popup "Move to": mỗi ô là 1 drop target. Thả file vào ô nào → move tới thư mục đó.
class MovePopup : Form
{
    static readonly Color Bg = Color.FromArgb(232, 232, 232);
    static readonly Color Pill = Color.FromArgb(150, 150, 150);
    static readonly Color PillHot = Color.FromArgb(0, 120, 212);
    static readonly Color PillOther = Color.FromArgb(185, 185, 185);

    readonly History _history;
    readonly Label _title = new() { Text = "Move to", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Black };
    readonly Panel _line = new() { BackColor = Color.Black };
    readonly List<SlotButton> _buttons = new();

    // Báo cho app biết vừa thả xong (để không tự ẩn sai lúc).
    public event Action? Dropped;

    public MovePopup(History history)
    {
        _history = history;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Bg;
        Font = new Font("Segoe UI", 10.5f);
        AllowDrop = true; // để cả nền popup cũng nhận kéo (hiện con trỏ "cấm")
        DragOver += (_, e) => e.Effect = DragDropEffects.None;
        Controls.Add(_title);
        Controls.Add(_line);
    }

    // Không cướp focus của Explorer khi hiện lên.
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x00000080 /*TOOLWINDOW*/ | 0x00000008 /*TOPMOST*/;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle);
    }

    public void Rebuild()
    {
        foreach (var b in _buttons) { Controls.Remove(b); b.Dispose(); }
        _buttons.Clear();

        float s = DeviceDpi / 96f;
        int W = (int)(240 * s), pad = (int)(16 * s), bh = (int)(32 * s), gap = (int)(10 * s);
        _title.SetBounds(0, (int)(8 * s), W, (int)(26 * s));
        _line.SetBounds(pad, (int)(38 * s), W - 2 * pad, Math.Max(2, (int)(3 * s)));

        int y = (int)(52 * s);
        foreach (var slot in _history.Slots())
        {
            var b = new SlotButton(slot, slot.Path == null ? PillOther : Pill, PillHot);
            b.SetBounds(pad, y, W - 2 * pad, bh);
            b.FilesDropped += OnFilesDropped;
            Controls.Add(b);
            _buttons.Add(b);
            y += bh + gap;
        }
        ClientSize = new Size(W, y + pad - gap);
    }

    public void ShowNear(Point cursor)
    {
        Rebuild();
        var area = Screen.FromPoint(cursor).WorkingArea;
        int off = (int)(36 * DeviceDpi / 96f);
        int x = cursor.X + off, y = cursor.Y + off / 2;
        if (x + Width > area.Right) x = cursor.X - off - Width;
        if (y + Height > area.Bottom) y = area.Bottom - Height;
        Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
        if (!Visible) Show();
    }

    void OnFilesDropped(Slot slot, string[] files)
    {
        Hide();
        Dropped?.Invoke();
        // Làm sau khi Explorer kết thúc vòng kéo-thả, tránh treo con trỏ của Explorer.
        BeginInvoke(() =>
        {
            var dest = slot.Path;
            if (dest == null)
            {
                using var dlg = new FolderBrowserDialog { Description = "Move tới thư mục nào?", UseDescriptionForTitle = true };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                dest = dlg.SelectedPath;
            }
            if (!Directory.Exists(dest))
            {
                MessageBox.Show($"Thư mục không còn tồn tại:\n{dest}", "Quick Move");
                return;
            }
            if (Mover.MoveInto(files, dest) > 0) _history.Record(dest);
        });
    }
}

// Ô bo tròn kiểu "viên thuốc", sáng lên khi kéo file đè lên.
class SlotButton : Control
{
    readonly Slot _slot;
    readonly Color _normal, _hot;
    bool _hover;
    public event Action<Slot, string[]>? FilesDropped;

    public SlotButton(Slot slot, Color normal, Color hot)
    {
        _slot = slot; _normal = normal; _hot = hot;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Color.White;
        AllowDrop = true;
        Text = slot.Path == null ? slot.Label : $"{slot.Label}: {History.ShortName(slot.Path)}";
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        bool ok = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = ok ? DragDropEffects.Move : DragDropEffects.None;
        _hover = ok; Invalidate();
    }

    protected override void OnDragOver(DragEventArgs e) =>
        e.Effect = _hover ? DragDropEffects.Move : DragDropEffects.None;

    protected override void OnDragLeave(EventArgs e) { _hover = false; Invalidate(); }

    protected override void OnDragDrop(DragEventArgs e)
    {
        _hover = false; Invalidate();
        // QUAN TRỌNG: tự mình move, rồi báo cho Explorer là "không làm gì" (None).
        // Nếu trả về Move, Explorer có thể tự xoá file gốc → dễ mất dữ liệu.
        e.Effect = DragDropEffects.None;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            FilesDropped?.Invoke(_slot, files);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = new GraphicsPath();
        int d = Height - 1;
        path.AddArc(r.X, r.Y, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        path.CloseFigure();
        using var brush = new SolidBrush(_hover ? _hot : _normal);
        g.FillPath(brush, path);
        TextRenderer.DrawText(g, Text, Font, r, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}
