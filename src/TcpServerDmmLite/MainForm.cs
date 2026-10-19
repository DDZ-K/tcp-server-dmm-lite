namespace TcpServerDmmLite;

internal sealed class MainForm : Form
{
    static readonly Color Bg = Color.FromArgb(246, 248, 251);
    static readonly Color Surface = Color.White;
    static readonly Color Ink = Color.FromArgb(15, 23, 42);
    static readonly Color Muted = Color.FromArgb(100, 116, 139);
    static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);
    static readonly Color On = Color.FromArgb(22, 163, 74);
    static readonly Color Off = Color.FromArgb(226, 232, 240);

    readonly StationConfig _cfg;
    readonly MesService _mes = new MesService();
    readonly CommandServer _server;
    readonly Label _clock;
    readonly Label _listen;
    readonly Label _card;
    readonly TextBox _log;
    readonly Panel[] _di = new Panel[16];
    readonly CheckBox[] _dout = new CheckBox[16];
    readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
    bool _busyDo;

    public MainForm()
    {
        _cfg = StationConfig.Load();
        _cfg.ApplyTo(_mes);
        IoCommands.CardIndex = _cfg.CardIndex;
        _mes.Log = WriteLog;
        _server = new CommandServer(_mes) { Log = WriteLog };

        Text = "TCP Server";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 720);
        BackColor = Bg;
        ForeColor = Ink;
        Font = new Font("Microsoft YaHei UI", 10f);

        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = HeaderBg };
        var config = new AntdUI.Button
        {
            Text = "配置",
            Type = AntdUI.TTypeMini.Default,
            Size = new Size(88, 36),
            Location = new Point(16, 14),
            WaveSize = 0
        };
        config.Click += (_, _) => EditConfig();
        var brand = new Label
        {
            Text = "BOSCH",
            ForeColor = Color.White,
            BackColor = HeaderBg,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(120, 16)
        };
        _clock = new Label
        {
            Dock = DockStyle.Right,
            Width = 280,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.White,
            BackColor = HeaderBg,
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            Padding = new Padding(0, 0, 16, 0),
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        header.Controls.Add(_clock);
        header.Controls.Add(brand);
        header.Controls.Add(config);

        var status = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Surface, Padding = new Padding(16, 8, 16, 8) };
        _listen = new Label { AutoSize = true, Location = new Point(16, 10), ForeColor = Ink };
        _card = new Label { AutoSize = true, Location = new Point(280, 10), ForeColor = Muted };
        status.Controls.Add(_listen);
        status.Controls.Add(_card);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
            BackColor = Bg
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        body.Controls.Add(MakeIoPanel(), 0, 0);
        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10f),
            BackColor = Surface
        };
        body.Controls.Add(_log, 1, 0);

        Controls.Add(body);
        Controls.Add(status);
        Controls.Add(header);

        Load += (_, _) => Boot();
        FormClosing += (_, _) =>
        {
            _timer.Stop();
            _server.Stop();
            N1616Card.Close();
        };
        _timer.Interval = 300;
        _timer.Tick += (_, _) => RefreshIo();
        var clock = new System.Windows.Forms.Timer { Interval = 1000 };
        clock.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        clock.Start();
    }

    Control MakeIoPanel()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 5,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        for (int i = 0; i < 8; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        grid.Controls.Add(new Label { Text = "输入", Dock = DockStyle.Fill, ForeColor = Muted }, 0, 0);
        grid.SetColumnSpan(grid.Controls[grid.Controls.Count - 1], 8);

        for (int i = 0; i < 16; i++)
        {
            var lamp = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                BackColor = Off
            };
            var cap = new Label
            {
                Text = (i + 1).ToString(),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                ForeColor = Ink
            };
            lamp.Controls.Add(cap);
            _di[i] = lamp;
            grid.Controls.Add(lamp, i % 8, i < 8 ? 1 : 2);
        }

        var doTitle = new Label { Text = "输出", Dock = DockStyle.Fill, ForeColor = Muted };
        grid.Controls.Add(doTitle, 0, 3);
        grid.SetColumnSpan(doTitle, 8);
        for (int i = 0; i < 16; i++)
        {
            int channel = i;
            var box = new CheckBox
            {
                Text = (i + 1).ToString(),
                Dock = DockStyle.Fill,
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat
            };
            box.CheckedChanged += (_, _) => ToggleOut(channel, box.Checked);
            _dout[i] = box;
            grid.Controls.Add(box, i % 8, 4);
        }
        return grid;
    }

    void Boot()
    {
        var opened = N1616Card.EnsureOpen(_cfg.CardIndex);
        _card.Text = opened ? "IO 板卡已打开" : "IO 板卡未打开";
        _card.ForeColor = opened ? On : Color.FromArgb(220, 38, 38);
        if (!opened && N1616Card.LastError.Length > 0)
            WriteLog(N1616Card.LastError);
        try
        {
            _server.Start(_cfg.ListenPort);
            _listen.Text = "TCP " + _cfg.ListenPort;
        }
        catch (Exception ex)
        {
            _listen.Text = "TCP 启动失败";
            WriteLog(ex.Message);
        }
        _timer.Start();
    }

    void RefreshIo()
    {
        if (!N1616Card.IsOpen)
            return;
        try
        {
            for (int i = 0; i < 16; i++)
            {
                int bit = N1616Card.ReadInBit(N1616Card.Handle, i);
                _di[i].BackColor = bit == 1 ? On : Off;
                int dout = N1616Card.ReadOutBit(N1616Card.Handle, i);
                if (dout == 0 || dout == 1)
                {
                    _busyDo = true;
                    _dout[i].Checked = dout == 1;
                    _busyDo = false;
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("读 IO 失败: " + ex.Message);
            _timer.Stop();
        }
    }

    void ToggleOut(int channel, bool on)
    {
        if (_busyDo || !N1616Card.IsOpen)
            return;
        int code = N1616Card.WriteOutBit(N1616Card.Handle, channel, on ? 1 : 0);
        WriteLog("DO" + (channel + 1) + (on ? " 开" : " 关") + " -> " + code);
    }

    void EditConfig()
    {
        using var dlg = new ConfigForm(_cfg);
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        _cfg.Save();
        _cfg.ApplyTo(_mes);
        IoCommands.CardIndex = _cfg.CardIndex;
        _server.Stop();
        N1616Card.Close();
        Boot();
    }

    void WriteLog(string message)
    {
        AppLog.Write(message);
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(WriteLog), message);
            return;
        }
        _log.AppendText(DateTime.Now.ToString("HH:mm:ss ") + message + Environment.NewLine);
        if (_log.TextLength > 20000)
            _log.Text = _log.Text[^8000..];
    }
}

internal sealed class ConfigForm : Form
{
    readonly TextBox _server;
    readonly TextBox _mesPort;
    readonly TextBox _listen;
    readonly TextBox _index;

    public ConfigForm(StationConfig cfg)
    {
        Text = "配置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 230);
        Font = new Font("Microsoft YaHei UI", 10f);

        _server = AddRow("MES 地址", cfg.MesServer, 20);
        _mesPort = AddRow("MES 端口", cfg.MesPort.ToString(), 64);
        _listen = AddRow("TCP 端口", cfg.ListenPort.ToString(), 108);
        _index = AddRow("板卡号", cfg.CardIndex.ToString(), 152);

        var ok = new Button { Text = "保存", DialogResult = DialogResult.OK, Location = new Point(210, 190), Width = 88 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(308, 190), Width = 88 };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            cfg.MesServer = _server.Text.Trim();
            cfg.MesPort = int.Parse(_mesPort.Text.Trim());
            cfg.ListenPort = int.Parse(_listen.Text.Trim());
            cfg.CardIndex = int.Parse(_index.Text.Trim());
        };
    }

    TextBox AddRow(string label, string value, int top)
    {
        Controls.Add(new Label { Text = label, Location = new Point(16, top + 4), AutoSize = true });
        var box = new TextBox { Text = value, Location = new Point(140, top), Width = 250 };
        Controls.Add(box);
        return box;
    }
}
