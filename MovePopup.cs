using System.Drawing.Drawing2D;

namespace QuickMove;

// Popup các thư mục đích. Kéo file vào một ô thì ô đó tách đôi: thả bên trái = Copy, bên phải = Move.
class MovePopup : Form
{
    readonly History _history;
    readonly Label _title = new() { Text = "Copy / Move to", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White };

    public MovePopup(History history)
    {
        _history = history;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 32);
        AllowDrop = true; // để cả nền popup cũng nhận kéo (hiện con trỏ "cấm")
        DragOver += (_, e) => e.Effect = DragDropEffects.None;
        Controls.Add(_title);
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
        foreach (var r in Controls.OfType<SlotRow>().ToList()) r.Dispose();

        float s = DeviceDpi / 96f;
        int W = (int)(400 * s), pad = (int)(14 * s), rowH = (int)(68 * s), gap = (int)(8 * s);
        _title.Font = new Font("Segoe UI", 16 * s, FontStyle.Bold, GraphicsUnit.Pixel);
        _title.SetBounds(pad, (int)(8 * s), W - 2 * pad, (int)(32 * s));

        int y = (int)(46 * s);
        foreach (var slot in _history.Slots())
        {
            var r = new SlotRow(slot, s);
            r.SetBounds(pad, y, W - 2 * pad, rowH);
            r.FilesDropped += OnFilesDropped;
            Controls.Add(r);
            y += rowH + gap;
        }
        ClientSize = new Size(W, y - gap + pad);
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

    void OnFilesDropped(Slot slot, string[] files, bool copy)
    {
        Hide();
        // Làm sau khi Explorer kết thúc vòng kéo-thả, tránh treo con trỏ của Explorer.
        BeginInvoke(() =>
        {
            var dest = slot.Path;
            if (dest == null)
            {
                using var dlg = new FolderBrowserDialog { Description = $"{(copy ? "Copy" : "Move")} tới thư mục nào?", UseDescriptionForTitle = true };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                dest = dlg.SelectedPath;
            }
            if (!Directory.Exists(dest))
            {
                MessageBox.Show($"Thư mục không còn tồn tại:\n{dest}", "Quick Move");
                return;
            }
            if (Mover.Run(files, dest, copy) > 0) _history.Record(dest);
        });
    }
}

// Một ô thư mục. Bình thường: icon + tên + đường dẫn. Khi kéo file đè lên: 2 nửa Copy | Move.
class SlotRow : Control
{
    static readonly Color RowBg = Color.FromArgb(56, 56, 56), Dim = Color.FromArgb(175, 175, 175), Accent = Color.FromArgb(96, 205, 255);
    static readonly Color CopyHot = Color.FromArgb(16, 124, 65), CopyIdle = Color.FromArgb(38, 66, 50);
    static readonly Color MoveHot = Color.FromArgb(0, 103, 192), MoveIdle = Color.FromArgb(34, 56, 80);
    const TextFormatFlags OneLine = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    readonly Slot _slot;
    readonly float _s;
    readonly string _name;
    readonly Font _big, _small, _icon;
    bool? _copy; // null = không có file đè lên; true = nửa Copy; false = nửa Move

    public event Action<Slot, string[], bool>? FilesDropped;

    public SlotRow(Slot slot, float scale)
    {
        _slot = slot; _s = scale;
        _name = slot.Path == null ? slot.Label : History.ShortName(slot.Path);
        _big = new Font("Segoe UI", 16 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        _small = new Font("Segoe UI", 12 * scale, GraphicsUnit.Pixel);
        _icon = new Font("Segoe MDL2 Assets", 24 * scale, GraphicsUnit.Pixel);
        DoubleBuffered = true;
        AllowDrop = true;
        AccessibleName = $"{slot.Label} {slot.Path}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _big.Dispose(); _small.Dispose(); _icon.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnDragEnter(DragEventArgs e) => Track(e);
    protected override void OnDragOver(DragEventArgs e) => Track(e);
    protected override void OnDragLeave(EventArgs e) => SetZone(null);

    void Track(DragEventArgs e)
    {
        bool? copy = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? PointToClient(new Point(e.X, e.Y)).X < Width / 2
            : null;
        e.Effect = copy switch { true => DragDropEffects.Copy, false => DragDropEffects.Move, null => DragDropEffects.None } & e.AllowedEffect;
        SetZone(e.Effect == DragDropEffects.None ? null : copy);
    }

    void SetZone(bool? copy)
    {
        if (_copy == copy) return;
        _copy = copy;
        Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        Track(e); // tính lại theo đúng điểm thả
        var copy = _copy;
        SetZone(null);
        // QUAN TRỌNG: tự mình copy/move, rồi báo cho Explorer là "không làm gì" (None).
        // Nếu trả về Move, Explorer có thể tự xoá file gốc → dễ mất dữ liệu.
        e.Effect = DragDropEffects.None;
        if (copy != null && e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            FilesDropped?.Invoke(_slot, files, copy.Value);
    }

    int S(int v) => (int)(v * _s);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? RowBg);
        if (_copy is bool copy)
        {
            int w = (Width - S(6)) / 2;
            DrawZone(g, new Rectangle(0, 0, w, Height), "\uE8C8", "Copy to", copy ? CopyHot : CopyIdle, copy);
            DrawZone(g, new Rectangle(Width - w, 0, w, Height), "\uE8DE", "Move to", copy ? MoveIdle : MoveHot, !copy);
            return;
        }

        Fill(g, ClientRectangle, RowBg);
        int x = S(60), right = Width - S(12), badge = 0;
        TextRenderer.DrawText(g, _slot.Path == null ? "\uE838" : "\uE8B7", _icon, new Rectangle(0, 0, x, Height),
            Color.FromArgb(255, 200, 80), OneLine | TextFormatFlags.HorizontalCenter);
        if (_slot.Path != null)
        {
            badge = TextRenderer.MeasureText(_slot.Label, _small).Width;
            TextRenderer.DrawText(g, _slot.Label, _small, new Rectangle(right - badge, S(11), badge, S(24)), Accent, OneLine | TextFormatFlags.Right);
        }
        TextRenderer.DrawText(g, _name, _big, new Rectangle(x, S(11), right - x - badge, S(24)), Color.White, OneLine | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, _slot.Path ?? "Thả vào rồi chọn thư mục", _small, new Rectangle(x, S(36), right - x, S(20)), Dim, OneLine | TextFormatFlags.PathEllipsis);
    }

    void DrawZone(Graphics g, Rectangle r, string icon, string text, Color bg, bool hot)
    {
        Fill(g, r, bg);
        var fg = hot ? Color.White : Dim;
        int x = r.X + S(56);
        TextRenderer.DrawText(g, icon, _icon, new Rectangle(r.X, 0, S(56), Height), fg, OneLine | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, text, _big, new Rectangle(x, S(11), r.Right - x - S(8), S(24)), fg, OneLine);
        TextRenderer.DrawText(g, _name, _small, new Rectangle(x, S(36), r.Right - x - S(8), S(20)), fg, OneLine | TextFormatFlags.EndEllipsis);
    }

    void Fill(Graphics g, Rectangle r, Color c)
    {
        int d = S(20);
        using var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(c);
        g.FillPath(brush, path);
    }
}
