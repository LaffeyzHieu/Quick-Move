namespace QuickMove;

// Theo dõi chuột: bắt đầu kéo file trong Explorer, rồi giữ yên ~HoldMs → gọi HoldDetected.
// Không có "event hover/drag" nào Explorer phát ra cho app khác, nên ta tự hỏi trạng thái chuột mỗi 30ms.
class DragWatcher : IDisposable
{
    public int HoldMs { get; set; } = 2000;
    const int StillRadius = 12; // lắc tay nhẹ trong bán kính này vẫn tính là "giữ yên"

    readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };
    bool _down, _eligible, _dragging, _fired;
    Point _start, _anchor;
    long _anchorTick;

    public event Action<Point>? HoldDetected;
    public event Action? Released;

    public DragWatcher()
    {
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    void Tick()
    {
        bool now = Native.LeftButtonDown;
        var p = Native.CursorPos();

        if (now && !_down)
        {
            _down = true; _dragging = false; _fired = false;
            _start = p;
            _eligible = Native.IsExplorerAt(p);
            return;
        }
        if (!now && _down)
        {
            _down = false;
            if (_fired) Released?.Invoke();
            return;
        }
        if (!_down || !_eligible || _fired) return;

        if (!_dragging)
        {
            if (Dist(p, _start) > Native.DragThreshold)
            {
                _dragging = true; _anchor = p; _anchorTick = Environment.TickCount64;
            }
            return;
        }

        if (Dist(p, _anchor) > StillRadius)
        {
            _anchor = p; _anchorTick = Environment.TickCount64;
            return;
        }

        if (Environment.TickCount64 - _anchorTick >= HoldMs && Native.IsOleDragActive())
        {
            _fired = true;
            HoldDetected?.Invoke(p);
        }
    }

    // Popup bị đóng giữa chừng (chuột đi xa) → cho phép giữ yên 2s lần nữa để mở lại.
    public void Rearm()
    {
        if (!_down) return;
        _fired = false;
        _anchor = Native.CursorPos();
        _anchorTick = Environment.TickCount64;
    }

    static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (double)(a.X - b.X) + (a.Y - b.Y) * (double)(a.Y - b.Y));

    public void Dispose() => _timer.Dispose();
}
