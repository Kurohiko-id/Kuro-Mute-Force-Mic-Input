using System.Drawing.Drawing2D;

namespace MuteMic;

// Notification shown when Windows silently changes the default input/output away
// from what the user picked, with a button to force it back.
internal sealed class DeviceChangeToast : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;

    // Same dark card palette and corner radius as OverlayForm, so the two toasts read as
    // one design language instead of two different UI kits bolted together.
    private static readonly Color CardBackground = Color.FromArgb(0x13, 0x14, 0x17);
    private static readonly Color TextColor = Color.FromArgb(0xd7, 0xda, 0xdf);
    private static readonly Color AccentColor = Color.FromArgb(0x7c, 0x96, 0xff);
    private static readonly Color DividerColor = Color.FromArgb(0x24, 0x26, 0x2b);
    private const int CornerRadius = 14;

    private readonly Label _text;
    private readonly Button _button;
    private Action? _onAction;

    public DeviceChangeToast()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = CardBackground;
        Size = new Size(320, 128);
        Opacity = 0.97;

        _text = new Label
        {
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 16, 0),
            Dock = DockStyle.Fill,
        };

        var divider = new Panel { BackColor = DividerColor, Dock = DockStyle.Bottom, Height = 1 };

        // An accent-colored text action instead of a filled button, like a Fluent dialog's
        // link-style action row, so it doesn't clash as a flat WinForms rectangle would.
        _button = new Button
        {
            Text = "OK",
            Dock = DockStyle.Bottom,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBackground,
            ForeColor = AccentColor,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand,
        };
        _button.FlatAppearance.BorderSize = 0;
        _button.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1b, 0x1d, 0x21);
        _button.Click += (_, _) =>
        {
            _onAction?.Invoke();
            Hide();
        };

        Controls.Add(_text);
        Controls.Add(_button);
        Controls.Add(divider);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var path = RoundedRect(ClientRectangle, CornerRadius);
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

    public void ShowMessage(string message, string buttonText, Action onAction)
    {
        _text.Text = message;
        _button.Text = buttonText;
        _onAction = onAction;

        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.X + (screen.Width - Width) / 2, screen.Y + (screen.Height - Height) / 2);

        Show();
    }
}
