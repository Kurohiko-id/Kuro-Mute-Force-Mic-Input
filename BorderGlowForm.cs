namespace MuteMic;

// Full-screen, click-through frame drawn around one monitor's edge while its mic is muted.
internal sealed class BorderGlowForm : Form
{
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly Color KeyColor = Color.FromArgb(1, 1, 1);
    private static readonly Color BorderColor = Color.FromArgb(0xe5, 0x48, 0x4d);

    public Screen TargetScreen { get; set; } = Screen.PrimaryScreen!;
    public int Thickness { get; set; } = 6;

    public BorderGlowForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = KeyColor;
        TransparencyKey = KeyColor; // everything painted this exact color becomes see-through
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TRANSPARENT makes the whole window (border included) click-through.
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(BorderColor, Thickness);
        var rect = new Rectangle(Thickness / 2, Thickness / 2, Width - Thickness, Height - Thickness);
        e.Graphics.DrawRectangle(pen, rect);
    }

    public void SetMuted(bool muted)
    {
        if (muted) Show();
        else Hide();
    }
}
