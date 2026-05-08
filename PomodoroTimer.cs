using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Media;
using System.Windows.Forms;

public class PomodoroForm : Form
{
    // ── 模式定义 ──
    enum Mode { Work, ShortBreak, LongBreak }

    struct ModeInfo
    {
        public readonly Color Color;
        public readonly string Label;
        public readonly string TabLabel;
        public readonly int DurationSec;

        public ModeInfo(Color color, string label, string tabLabel, int durationSec)
        {
            Color = color; Label = label; TabLabel = tabLabel; DurationSec = durationSec;
        }
    }

    const int SessionsPerLongBreak = 4;

    // ── 配色方案（参照大屏UI设计规范） ──
    static readonly Color Bg           = Color.FromArgb(0, 14, 44);    // #000E2C  窗口背景
    static readonly Color RingBg       = Color.FromArgb(30, 51, 103);  // #1E3367  进度环底色
    static readonly Color WorkRed      = Color.FromArgb(255, 56, 56);  // #FF3838  工作红
    static readonly Color BreakTeal    = Color.FromArgb(42, 238, 187); // #2AEEBB  短休青
    static readonly Color LongBreakCyan = Color.FromArgb(25, 219, 255);// #19DBFF  长休蓝
    static readonly Color AccentBlue   = Color.FromArgb(21, 154, 255); // #159AFF  主色调
    static readonly Color TextWhite    = Color.FromArgb(255, 255, 255); // #FFFFFF  主文字
    static readonly Color TextGray     = Color.FromArgb(208, 222, 238); // #D0DEEE  辅助文字
    static readonly Color TextMuted    = Color.FromArgb(108, 129, 151); // #6C8097  弱化文字
    static readonly Color BtnBg        = Color.FromArgb(30, 51, 103);  // #1E3367  按钮背景
    static readonly Color BtnHover     = Color.FromArgb(21, 154, 255, 80);
    static readonly Color TabActiveBg  = Color.FromArgb(21, 154, 255, 50);
    static readonly Color TabHoverBg   = Color.FromArgb(21, 154, 255, 60);
    static readonly Color TabPanelBg   = Color.FromArgb(21, 154, 255, 25);
    static readonly Color FlashOverlay = Color.FromArgb(21, 154, 255, 40);

    static readonly ModeInfo[] Modes =
    {
        new ModeInfo(WorkRed,       "专注", "专注工作", 25 * 60),
        new ModeInfo(BreakTeal,     "短休", "短休息",   5 * 60),
        new ModeInfo(LongBreakCyan, "长休", "长休息",   15 * 60),
    };

    // ── 字体（DIN 优先，否则回退到微软雅黑）──
    static readonly string FontName = IsFontInstalled("DIN") ? "DIN" : "Microsoft YaHei";
    static readonly Font FontBase     = new Font(FontName, 9f);
    static readonly Font FontTime     = new Font(FontName, 38f, FontStyle.Bold);
    static readonly Font FontMode     = new Font(FontName, 10f);
    static readonly Font FontPlayBtn  = new Font(FontName, 16f);
    static readonly Font FontHint     = new Font(FontName, 8f);
    static readonly Font FontBtn      = new Font(FontName, 12f);
    static readonly Font FontTabBtn   = new Font(FontName, 9f);

    static bool IsFontInstalled(string name)
    {
        return FontFamily.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // ── 运行状态 ──
    Mode _mode = Mode.Work;
    int _timeRemaining;
    int _sessionsCompleted;
    Timer _timer = new Timer { Interval = 1000 };

    // ── UI 控件 ──
    Label _timeLabel, _modeLabel;
    Panel _ringPanel;
    FlowLayoutPanel _tabPanel, _dotPanel;
    Button _btnPlay, _btnReset, _btnSkip;
    Panel[] _dots = new Panel[SessionsPerLongBreak];

    // ── 绘图缓存 ──
    Pen _bgPen = new Pen(RingBg, 8);
    Pen _fgPen;

    // ── 闪烁动画 ──
    Timer _pulseTimer;
    int _pulseCount;
    bool _bright;

    public PomodoroForm()
    {
        DoubleBuffered = true;                     // 减少闪烁
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(380, 560);
        BackColor = Bg;
        ForeColor = TextWhite;
        Font = FontBase;
        Text = "番茄钟";

        _timeRemaining = Modes[(int)_mode].DurationSec;
        _fgPen = new Pen(Modes[(int)_mode].Color, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        _pulseTimer = new Timer { Interval = 400 };
        _pulseTimer.Tick += OnPulseTick;

        BuildUI();
        UpdateDisplay();

        _timer.Tick += delegate { Tick(); };
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_timer != null) _timer.Dispose();
            if (_pulseTimer != null) _pulseTimer.Dispose();
            if (_bgPen != null) _bgPen.Dispose();
            if (_fgPen != null) _fgPen.Dispose();
            FontBase.Dispose();
            FontTime.Dispose();
            FontMode.Dispose();
            FontPlayBtn.Dispose();
            FontHint.Dispose();
            FontBtn.Dispose();
            FontTabBtn.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── UI 构建 ──

    void BuildUI()
    {
        SuspendLayout();

        // 模式切换标签栏
        _tabPanel = new FlowLayoutPanel
        {
            Location = new Point(24, 20), Size = new Size(332, 42),
            BackColor = TabPanelBg, Padding = new Padding(4), AutoSize = false
        };
        for (int i = 0; i < Modes.Length; i++)
            AddTab(Modes[i].TabLabel, (Mode)i);
        HighlightTab(Mode.Work);

        // 圆形进度环面板
        _ringPanel = new Panel
        {
            Location = new Point(40, 80), Size = new Size(300, 300),
            BackColor = Color.Transparent
        };
        _ringPanel.Paint += DrawRing;

        _timeLabel = new Label
        {
            Location = new Point(0, 130), Size = new Size(300, 54),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextWhite,
            Font = FontTime, BackColor = Color.Transparent
        };

        _modeLabel = new Label
        {
            Location = new Point(0, 180), Size = new Size(300, 20),
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextGray,
            Font = FontMode, BackColor = Color.Transparent
        };

        _ringPanel.Controls.Add(_timeLabel);
        _ringPanel.Controls.Add(_modeLabel);

        // 完成圆点指示器（每4个番茄一组）
        _dotPanel = new FlowLayoutPanel
        {
            Location = new Point(130, 392), Size = new Size(120, 18),
            BackColor = Color.Transparent
        };
        for (int i = 0; i < SessionsPerLongBreak; i++)
        {
            _dots[i] = new Panel { Size = new Size(12, 12), Margin = new Padding(3) };
            _dotPanel.Controls.Add(_dots[i]);
        }
        RenderDots();

        // 控制按钮
        int cy = 426;
        _btnReset = MakeButton("⟲", 20, cy, 48, 48, delegate { Reset(); });
        _btnPlay  = MakeButton("▶", 166, cy - 8, 64, 64, delegate { Toggle(); }, FontPlayBtn);
        _btnSkip  = MakeButton("⏭", 292, cy, 48, 48, delegate { Skip(); });

        // 快捷键提示
        var hint = new Label
        {
            Location = new Point(0, 492), Size = new Size(380, 16),
            Text = "空格键 开始/暂停   R 重置   → 跳过",
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = TextMuted, Font = FontHint
        };

        Controls.AddRange(new Control[] { _tabPanel, _ringPanel, _dotPanel, _btnPlay, _btnReset, _btnSkip, hint });
        ResumeLayout(false);
    }

    void AddTab(string text, Mode mode)
    {
        var btn = new Button
        {
            Text = text, Tag = mode, FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = TextGray,
            Size = new Size(100, 34), Font = FontTabBtn,
            UseVisualStyleBackColor = false, Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = TabHoverBg;
        btn.Click += delegate { Stop(); SetMode(mode); };
        _tabPanel.Controls.Add(btn);
    }

    Button MakeButton(string text, int x, int y, int w, int h, EventHandler click, Font font = null)
    {
        var btn = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat, BackColor = BtnBg, ForeColor = TextWhite,
            Font = font ?? FontBtn, UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = BtnHover;
        btn.Click += click;
        return btn;
    }

    // ── 绘制 ──

    void DrawRing(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;

        int cx = 150, cy = 150, r = 112;
        var rect = new Rectangle(cx - r, cy - r, r * 2, r * 2);

        // 底层完整圆环
        g.DrawArc(_bgPen, rect, 0, 360);

        // 前景彩色弧线（从12点钟方向顺时针递减）
        float angle = 360f * _timeRemaining / Modes[(int)_mode].DurationSec;
        if (angle > 0)
            g.DrawArc(_fgPen, rect, -90, angle);
    }

    // ── 核心逻辑 ──

    void SetMode(Mode m)
    {
        _mode = m;
        _timeRemaining = Modes[(int)m].DurationSec;
        _fgPen = new Pen(Modes[(int)m].Color, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        HighlightTab(m);
        _modeLabel.Text = Modes[(int)m].Label;
        RefreshUI();
    }

    void Tick()
    {
        _timeRemaining--;
        RefreshUI();
        if (_timeRemaining <= 0)
            Complete();
    }

    void Complete()
    {
        Stop();
        PlaySound();
        FlashWindow();

        if (_mode == Mode.Work)
        {
            _sessionsCompleted++;
            RenderDots();
            SetMode(_sessionsCompleted % SessionsPerLongBreak == 0 ? Mode.LongBreak : Mode.ShortBreak);
        }
        else
        {
            SetMode(Mode.Work);
        }
    }

    void Toggle() { if (_timer.Enabled) Stop(); else Start(); }
    void Start()  { _timer.Start();  UpdatePlayButton(); }
    void Stop()   { _timer.Stop();   UpdatePlayButton(); }
    void Reset()  { Stop(); SetMode(_mode); }

    void Skip()
    {
        _timeRemaining = 0;
        RefreshUI();
        Complete();
    }

    void HighlightTab(Mode mode)
    {
        foreach (Control c in _tabPanel.Controls)
        {
            Button tb = c as Button;
            if (tb != null && tb.Tag is Mode)
            {
                Mode m = (Mode)tb.Tag;
                tb.BackColor = m == mode ? TabActiveBg : Color.Transparent;
            }
        }
    }

    void RefreshUI()
    {
        _ringPanel.Invalidate();
        UpdateDisplay();
    }

    void UpdatePlayButton()
    {
        _btnPlay.Text = _timer.Enabled ? "⏸" : "▶";
    }

    void UpdateDisplay()
    {
        var ts = TimeSpan.FromSeconds(_timeRemaining);
        _timeLabel.Text = ts.ToString(@"mm\:ss");
        Text = string.Format("{0} - {1} | 番茄钟", _timeLabel.Text, _modeLabel.Text);
    }

    void RenderDots()
    {
        int filled = _sessionsCompleted % SessionsPerLongBreak;
        for (int i = 0; i < SessionsPerLongBreak; i++)
            _dots[i].BackColor = i < filled ? WorkRed : RingBg;
    }

    void PlaySound()
    {
        try { SystemSounds.Beep.Play(); } catch { }
    }

    void FlashWindow()
    {
        _pulseCount = 0;
        _bright = false;
        _pulseTimer.Start();
    }

    void OnPulseTick(object sender, EventArgs e)
    {
        _bright = !_bright;
        BackColor = _bright ? FlashOverlay : Bg;
        _pulseCount++;
        if (_pulseCount >= 6)
        {
            BackColor = Bg;
            _pulseTimer.Stop();
        }
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)  { e.Handled = true; Toggle(); }
        if (e.KeyCode == Keys.R)      { e.Handled = true; Reset(); }
        if (e.KeyCode == Keys.Right)  { e.Handled = true; Skip(); }
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PomodoroForm());
    }
}
