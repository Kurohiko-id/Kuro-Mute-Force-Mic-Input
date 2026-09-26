using System.Drawing.Drawing2D;

namespace MuteMic;

// Small auto-dismissing OSD, styled like the PowerToys mute-mic overlay.
// One instance is bound to one target monitor (see TrayApp.RebuildOverlays).
internal sealed class OverlayForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int EdgeMargin = 24;
    private static readonly Size BaseSize = new(220, 100);

    private readonly Label _icon;
    private readonly Label _text;
    private readonly System.Windows.Forms.Timer _timer;

    public Screen TargetScreen { get; set; } = Screen.PrimaryScreen!;
    public OverlayPosition Position { get; set; } = OverlayPosition.BottomCenter;
    public double Scale { get; set; } = 1.0;
    public bool PersistWhileMuted { get; set; }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(0x13, 0x14, 0x17);
        Size = BaseSize;
        Opacity = 0.95;

        _icon = new Label
        {
            Font = new Font("Segoe MDL2 Assets", 22),
            ForeColor = Color.FromArgb(0xd7, 0xda, 0xdf),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 60,
        };
        _text = new Label
        {
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(0xd7, 0xda, 0xdf),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };
        Controls.Add(_text);
        Controls.Add(_icon);

        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var path = RoundedRect(ClientRectangle, (int)(14 * Scale));
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void ShowMuteState(bool muted)
    {
        _icon.Text = muted ? "" : ""; // MicOff / Microphone glyphs
        _text.Text = muted ? "Microphone Muted" : "Microphone Unmuted";

        Size = new Size((int)(BaseSize.Width * Scale), (int)(BaseSize.Height * Scale));
        _icon.Font = new Font("Segoe MDL2 Assets", (float)(22 * Scale));
        _icon.Height = (int)(60 * Scale);
        _text.Font = new Font("Segoe UI", (float)(9.5 * Scale));
        Location = ComputeLocation();

        Show();
        Invalidate(); // re-cut the rounded region for the new size

        _timer.Stop();
        if (!(muted && PersistWhileMuted))
            _timer.Start();
    }

    private Point ComputeLocation()
    {
        var area = TargetScreen.WorkingArea;
        int x = Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => area.X + EdgeMargin,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => area.Right - Width - EdgeMargin,
            _ => area.X + (area.Width - Width) / 2,
        };
        int y = Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => area.Y + EdgeMargin,
            _ => area.Bottom - Height - EdgeMargin,
        };
        return new Point(x, y);
    }
}
