namespace WindowCover;

/// <summary>
/// 主配置窗：填目标标题、位置/尺寸、颜色、模式，开始/停止遮挡。
/// 开始后主窗自动藏到托盘，托盘右键可显示主窗或退出。
/// 界面全部用代码搭建（无 Designer 文件），方便在 Linux 下手写/编译。
/// </summary>
public sealed class MainForm : Form
{
    private readonly CoverSettings _settings = CoverSettings.Load();
    private readonly System.Windows.Forms.Timer _followTimer = new() { Interval = 100 };
    private CoverForm? _cover;
    private bool _running;
    private bool _reallyExit;
    private bool _trayTipShown;

    private TextBox _txtTargetTitle = null!;
    private NumericUpDown _numX = null!;
    private NumericUpDown _numY = null!;
    private NumericUpDown _numWidth = null!;
    private NumericUpDown _numHeight = null!;
    private ComboBox _cmbColor = null!;
    private Panel _pnlColorPreview = null!;
    private RadioButton _rdoFollow = null!;
    private RadioButton _rdoFixed = null!;
    private CheckBox _chkClickThrough = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Label _lblStatus = null!;
    private Label _lblTargetPos = null!;
    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;

    private static readonly (string Name, Color Color)[] PresetColors =
    {
        ("黑色", Color.Black),
        ("白色", Color.White),
        ("红色", Color.Red),
        ("黄色", Color.Yellow),
        ("绿色", Color.Lime),
        ("蓝色", Color.Blue),
    };

    public MainForm()
    {
        Text = "WindowCover - 跟随遮挡";
        AutoScaleMode = AutoScaleMode.Dpi;
        // 之前 480 宽是根因：“高”那组控件右边缘在 498，本身就超出窗口，
        // 长标签在 125% 缩放下还会更宽。改宽、可拉伸，布局用面板自动换行。
        ClientSize = new Size(620, 640);
        MinimumSize = new Size(560, 600);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        BuildTray();
        LoadSettingsToUi();

        _followTimer.Tick += OnFollowTick;
        FormClosing += OnFormClosing;
    }

    // ---------- 界面搭建 ----------

    private void BuildUi()
    {
        // 主布局：单列 TableLayoutPanel，Dock.Fill，随窗口拉伸；长标签设 MaximumSize 自动换行。
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(16),
            AutoSize = false,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(layout);

        Label WrapLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(588, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };

        void AddRow(Control c, int height = -1)
        {
            layout.RowStyles.Add(new RowStyle(height > 0 ? SizeType.Absolute : SizeType.AutoSize, height > 0 ? height : 0));
            layout.Controls.Add(c);
        }

        AddRow(WrapLabel("目标窗口标题（包含匹配，如：微信 / Chrome / Notepad）"));
        _txtTargetTitle = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        AddRow(_txtTargetTitle, 28);

        AddRow(WrapLabel("X / Y：跟随模式 = 相对目标窗口左上角；固定模式 = 屏幕绝对坐标"));

        // X/Y/宽/高分两行，每行两组，再也不会 4 组挤在一行超出窗口。
        var grid = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Fill, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        AddRow(grid, 62);
        _numX = MakeNumber(-10000, 10000);
        _numY = MakeNumber(-10000, 10000);
        _numWidth = MakeNumber(10, 8000);
        _numHeight = MakeNumber(10, 8000);
        grid.Controls.Add(new Label { Text = "X", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        grid.Controls.Add(_numX, 1, 0);
        grid.Controls.Add(new Label { Text = "Y", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        grid.Controls.Add(_numY, 3, 0);
        grid.Controls.Add(new Label { Text = "宽", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(_numWidth, 1, 1);
        grid.Controls.Add(new Label { Text = "高", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
        grid.Controls.Add(_numHeight, 3, 1);

        var colorRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        AddRow(colorRow, 32);
        colorRow.Controls.Add(new Label { Text = "遮挡颜色：", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
        _cmbColor = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (name, _) in PresetColors)
        {
            _cmbColor.Items.Add(name);
        }
        _cmbColor.Items.Add("自定义…");
        _cmbColor.SelectedIndexChanged += OnColorSelected;
        colorRow.Controls.Add(_cmbColor);
        var btnPickColor = new Button { Text = "选颜色…", Width = 88 };
        btnPickColor.Click += (_, _) => PickCustomColor();
        colorRow.Controls.Add(btnPickColor);
        _pnlColorPreview = new Panel { Size = new Size(120, 24), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
        colorRow.Controls.Add(_pnlColorPreview);

        var grpMode = new GroupBox { Text = "模式", Dock = DockStyle.Fill };
        _rdoFollow = new RadioButton { Text = "跟随窗口", Location = new Point(16, 22), AutoSize = true, Checked = true };
        _rdoFixed = new RadioButton { Text = "固定屏幕位置", Location = new Point(170, 22), AutoSize = true };
        grpMode.Controls.Add(_rdoFollow);
        grpMode.Controls.Add(_rdoFixed);
        AddRow(grpMode, 60);

        _chkClickThrough = new CheckBox { Text = "穿透点击", AutoSize = true };
        _chkClickThrough.CheckedChanged += (_, _) => ApplyClickThrough();
        AddRow(_chkClickThrough);
        AddRow(WrapLabel("开启穿透点击后，鼠标点击会穿过遮挡块，落到下层窗口。"));

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        AddRow(btnRow, 44);
        _btnStart = new Button { Text = "开始遮挡", Size = new Size(160, 36) };
        _btnStart.Click += (_, _) => StartCover();
        btnRow.Controls.Add(_btnStart);
        _btnStop = new Button { Text = "停止", Size = new Size(160, 36), Enabled = false };
        _btnStop.Click += (_, _) => StopCover();
        btnRow.Controls.Add(_btnStop);
        var btnExit = new Button { Text = "退出程序", Size = new Size(160, 36) };
        btnExit.Click += (_, _) => ExitApp();
        btnRow.Controls.Add(btnExit);

        _lblStatus = new Label { Text = "状态：未开始", AutoSize = true, MaximumSize = new Size(588, 0) };
        AddRow(_lblStatus);
        _lblTargetPos = new Label { Text = "目标窗口：-", AutoSize = true, MaximumSize = new Size(588, 0) };
        AddRow(_lblTargetPos);

        AddRow(WrapLabel("开始后主窗自动藏到托盘。右键托盘图标可显示主窗 / 退出。配置自动保存到 exe 同目录的 cover-settings.json，下次启动自动加载。"));
    }

    private static NumericUpDown MakeNumber(int min, int max) => new()
    {
        Minimum = min, Maximum = max, Dock = DockStyle.Fill,
    };

    private void AddLabeledNumber(string label, int x, int y, out NumericUpDown num, int min, int max)
    {
        var lbl = new Label { Text = label, Location = new Point(x, y + 4), AutoSize = true };
        Controls.Add(lbl);
        num = new NumericUpDown
        {
            Location = new Point(x + 22, y), Width = 88,
            Minimum = min, Maximum = max
        };
        Controls.Add(num);
    }

    private void BuildTray()
    {
        _trayMenu = new ContextMenuStrip();
        var miShow = new ToolStripMenuItem("显示主窗口");
        miShow.Click += (_, _) => ShowMainWindow();
        var miExit = new ToolStripMenuItem("退出");
        miExit.Click += (_, _) => ExitApp();
        _trayMenu.Items.Add(miShow);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(miExit);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WindowCover - 跟随遮挡",
            ContextMenuStrip = _trayMenu,
            Visible = false
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    // ---------- 配置 ----------

    private void LoadSettingsToUi()
    {
        _txtTargetTitle.Text = _settings.TargetTitle;
        _numX.Value = _settings.X;
        _numY.Value = _settings.Y;
        _numWidth.Value = _settings.Width;
        _numHeight.Value = _settings.Height;
        _rdoFollow.Checked = _settings.Mode == CoverMode.Follow;
        _rdoFixed.Checked = _settings.Mode == CoverMode.Fixed;
        _chkClickThrough.Checked = _settings.ClickThrough;
        ApplyColorToUi();
    }

    private void ReadSettingsFromUi()
    {
        _settings.TargetTitle = _txtTargetTitle.Text.Trim();
        _settings.X = (int)_numX.Value;
        _settings.Y = (int)_numY.Value;
        _settings.Width = Math.Max(10, (int)_numWidth.Value);
        _settings.Height = Math.Max(10, (int)_numHeight.Value);
        _settings.Mode = _rdoFollow.Checked ? CoverMode.Follow : CoverMode.Fixed;
        _settings.ClickThrough = _chkClickThrough.Checked;
        // 颜色由颜色选择事件维护 ColorArgb / ColorName
    }

    private void ApplyColorToUi()
    {
        Color color = Color.FromArgb(_settings.ColorArgb);
        _pnlColorPreview.BackColor = color;
        int idx = Array.FindIndex(PresetColors, p => p.Name == _settings.ColorName);
        _cmbColor.SelectedIndex = idx >= 0 ? idx : PresetColors.Length; // 自定义…
    }

    private void OnColorSelected(object? sender, EventArgs e)
    {
        if (_cmbColor.SelectedIndex < 0)
        {
            return;
        }
        if (_cmbColor.SelectedIndex < PresetColors.Length)
        {
            var (name, color) = PresetColors[_cmbColor.SelectedIndex];
            _settings.ColorName = name;
            _settings.ColorArgb = color.ToArgb();
            _pnlColorPreview.BackColor = color;
            ApplyCoverAppearance();
        }
        else
        {
            PickCustomColor();
        }
    }

    private void PickCustomColor()
    {
        using var dlg = new ColorDialog { Color = Color.FromArgb(_settings.ColorArgb), FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings.ColorName = "自定义";
            _settings.ColorArgb = dlg.Color.ToArgb();
            ApplyColorToUi();
            ApplyCoverAppearance();
        }
        else
        {
            ApplyColorToUi();
        }
    }

    private void ApplyCoverAppearance()
    {
        if (_cover is not null)
        {
            _cover.SetCoverColor(Color.FromArgb(_settings.ColorArgb));
        }
    }

    private void ApplyClickThrough()
    {
        if (_cover is not null)
        {
            _cover.ClickThrough = _chkClickThrough.Checked;
        }
    }

    // ---------- 开始 / 停止 ----------

    private void StartCover()
    {
        ReadSettingsFromUi();
        if (string.IsNullOrWhiteSpace(_settings.TargetTitle) && _settings.Mode == CoverMode.Follow)
        {
            MessageBox.Show(this, "跟随模式需要填写目标窗口标题。", "WindowCover",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Save();
        _cover ??= new CoverForm();
        _cover.SetCoverColor(Color.FromArgb(_settings.ColorArgb));
        _cover.ClickThrough = _settings.ClickThrough;
        _cover.Size = new Size(_settings.Width, _settings.Height);

        _running = true;
        _btnStart.Enabled = false;
        _btnStop.Enabled = true;
        _lblStatus.Text = "状态：运行中…";
        _followTimer.Start();

        // 立刻跟随一次，不等第一个 100ms 周期
        OnFollowTick(this, EventArgs.Empty);

        // 藏到托盘
        _trayIcon.Visible = true;
        Hide();
        if (!_trayTipShown)
        {
            _trayTipShown = true;
            _trayIcon.ShowBalloonTip(3000, "WindowCover",
                "遮挡运行中。右键托盘图标可显示主窗 / 退出。", ToolTipIcon.Info);
        }
    }

    private void StopCover()
    {
        _running = false;
        _followTimer.Stop();
        _cover?.Hide();
        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        _lblStatus.Text = "状态：已停止";
        _lblTargetPos.Text = "目标窗口：-";
        _trayIcon.Visible = false;
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _reallyExit = true;
        ReadSettingsFromUi();
        _settings.Save();
        _followTimer.Stop();
        _trayIcon.Visible = false;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyExit && _running)
        {
            // 运行中点右上角关闭：不是退出，只是藏到托盘
            e.Cancel = true;
            _trayIcon.Visible = true;
            Hide();
            return;
        }
        ReadSettingsFromUi();
        _settings.Save();
        _trayIcon.Visible = false;
    }

    // ---------- 跟随逻辑（每 100ms） ----------

    private void OnFollowTick(object? sender, EventArgs e)
    {
        if (!_running || _cover is null)
        {
            return;
        }

        // 颜色 / 穿透开关在运行中改了也要立即生效
        ReadSettingsFromUi();
        _cover.SetCoverColor(Color.FromArgb(_settings.ColorArgb));
        _cover.ClickThrough = _settings.ClickThrough;

        if (_settings.Mode == CoverMode.Fixed)
        {
            _cover.ApplyBounds(_settings.X, _settings.Y, _settings.Width, _settings.Height);
            if (!_cover.Visible)
            {
                _cover.Show();
            }
            _lblStatus.Text = "状态：运行中（固定屏幕位置）";
            _lblTargetPos.Text = $"遮挡位置：屏幕 ({_settings.X}, {_settings.Y})  {_settings.Width}×{_settings.Height}";
            return;
        }

        // 跟随模式
        var exclude = new HashSet<IntPtr> { Handle, _cover.Handle };
        IntPtr target = NativeMethods.FindWindowByTitleContains(_settings.TargetTitle, exclude);
        if (target == IntPtr.Zero)
        {
            _cover.Hide();
            _lblStatus.Text = "状态：运行中，未找到目标窗口";
            _lblTargetPos.Text = $"目标窗口：未找到 “{_settings.TargetTitle}”";
            return;
        }
        if (NativeMethods.IsMinimized(target))
        {
            _cover.Hide();
            _lblStatus.Text = "状态：运行中，目标窗口已最小化";
            _lblTargetPos.Text = "目标窗口：已最小化";
            return;
        }
        if (!NativeMethods.TryGetWindowRect(target, out var rect))
        {
            _cover.Hide();
            _lblStatus.Text = "状态：运行中，取目标窗口位置失败";
            return;
        }

        int x = rect.Left + _settings.X;
        int y = rect.Top + _settings.Y;
        _cover.ApplyBounds(x, y, _settings.Width, _settings.Height);
        if (!_cover.Visible)
        {
            _cover.Show();
        }
        _lblStatus.Text = "状态：运行中，遮挡中";
        _lblTargetPos.Text = $"目标窗口：({rect.Left}, {rect.Top})  {rect.Width}×{rect.Height}  → 遮挡 ({x}, {y})";
    }
}
