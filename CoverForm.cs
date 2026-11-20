namespace WindowCover;

/// <summary>
/// 遮挡窗：无边框、置顶、不显示在任务栏、不抢焦点。
/// 位置与大小由主窗的 Timer 每 100ms 更新。
/// </summary>
public sealed class CoverForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private bool _clickThrough;

    public CoverForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Size = new Size(300, 80);
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (_clickThrough == value)
            {
                return;
            }
            _clickThrough = value;
            // 扩展样式变了需要重建句柄才生效
            if (IsHandleCreated)
            {
                RecreateHandle();
            }
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (_clickThrough)
            {
                cp.ExStyle |= WS_EX_TRANSPARENT;
            }
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // 点击也不激活本窗口，焦点留在目标软件上
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    public void SetCoverColor(Color color)
    {
        BackColor = color;
    }

    public void ApplyBounds(int x, int y, int width, int height)
    {
        if (Location.X != x || Location.Y != y || Width != width || Height != height)
        {
            SetBounds(x, y, width, height);
        }
    }
}
