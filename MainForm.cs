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
        ClientSize = new Size(480, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
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
        int y = 16;
        int labelX = 16, fieldX = 16, fieldW = 448;

        var lblTitle = new Label { Text = "目标窗口标题（包含匹配，如：微信 / Chrome / Notepad）", Location = new Point(labelX, y), AutoSize = true };
        Controls.Add(lblTitle);
        y += 24;
        _txtTargetTitle = new TextBox { Location = new Point(fieldX, y), Width = fieldW };
        Controls.Add(_txtTargetTitle);
        y += 36;

        var lblXY = new Label
        {
            Text = "X / Y：跟随模式 = 相对目标窗口左上角；固定模式 = 屏幕绝对坐标",
            Location = new Point(labelX, y), AutoSize = true
        };
        Controls.Add(lblXY);
        y += 24;
        AddLabeledNumber("X", 16, y, out _numX, -10000, 10000);
        AddLabeledNumber("Y", 140, y, out _numY, -10000, 10000);
        AddLabeledNumber("宽", 264, y, out _numWidth, 10, 8000);
        AddLabeledNumber("高", 388, y, out _numHeight, 10, 8000);
        y += 40;

        var lblColor = new Label { Text = "遮挡颜色：", Location = new Point(labelX, y + 4), AutoSize = true };
        Controls.Add(lblColor);
        _cmbColor = new ComboBox
        {
            Location = new Point(92, y), Width = 140,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var (name, _) in PresetColors)
        {
            _cmbColor.Items.Add(name);
        }
        _cmbColor.Items.Add("自定义…");
        _cmbColor.SelectedIndexChanged += OnColorSelected;
        Controls.Add(_cmbColor);

        var btnPickColor = new Button { Text = "选颜色…", Location = new Point(244, y - 1), Width = 88 };
        btnPickColor.Click += (_, _) => PickCustomColor();
        Controls.Add(btnPickColor);

        _pnlColorPreview = new Panel
        {
            Location = new Point(344, y), Size = new Size(120, 24),
            BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black
        };
        Controls.Add(_pnlColorPreview);
        y += 44;

        var grpMode = new GroupBox { Text = "模式", Location = new Point(fieldX, y), Size = new Size(fieldW, 56) };
        _rdoFollow = new RadioButton { Text = "跟随窗口", Location = new Point(16, 22), AutoSize = true, Checked = true };
        _rdoFixed = new RadioButton { Text = "固定屏幕位置", Location = new Point(150, 22), AutoSize = true };
        grpMode.Controls.Add(_rdoFollow);
        grpMode.Controls.Add(_rdoFixed);
        Controls.Add(grpMode);
        y += 68;

        _chkClickThrough = new CheckBox
        {
            Text = "穿透点击（开启后鼠标点击会穿过遮挡块，落到下层窗口）",
            Location = new Point(fieldX, y), AutoSize = true
        };
        _chkClickThrough.CheckedChanged += (_, _) => ApplyClickThrough();
        Controls.Add(_chkClickThrough);
        y += 32;

        _btnStart = new Button { Text = "开始遮挡", Location = new Point(fieldX, y), Size = new Size(140, 36) };
        _btnStart.Click += (_, _) => StartCover();
        Controls.Add(_btnStart);

        _btnStop = new Button { Text = "停止", Location = new Point(172, y), Size = new Size(140, 36), Enabled = false };
        _btnStop.Click += (_, _) => StopCover();
        Controls.Add(_btnStop);

        var btnExit = new Button { Text = "退出程序", Location = new Point(328, y), Size = new Size(136, 36) };
        btnExit.Click += (_, _) => ExitApp();
        Controls.Add(btnExit);
        y += 52;

        _lblStatus = new Label { Text = "状态：未开始", Location = new Point(labelX, y), AutoSize = true };
        Controls.Add(_lblStatus);
        y += 24;
        _lblTargetPos = new Label { Text = "目标窗口：-", Location = new Point(labelX, y), AutoSize = true };
        Controls.Add(_lblTargetPos);
        y += 28;

        var lblHint = new Label
        {
            Text = "开始后主窗自动藏到托盘。右键托盘图标可显示主窗 / 退出。\n配置自动保存到 exe 同目录的 cover-settings.json，下次启动自动加载。",
            Location = new Point(labelX, y), Size = new Size(fieldW, 40)
        };
        Controls.Add(lblHint);
    }

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
