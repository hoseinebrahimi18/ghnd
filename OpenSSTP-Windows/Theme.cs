using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace OpenSSTP.Windows;

/// <summary>Colour palette + drawing helpers. Mirrors the Android app's "Ui" object so both apps look alike.</summary>
public static class Pal
{
    static Color H(string hex) => ColorTranslator.FromHtml(hex);

    public static readonly Color BG0 = H("#060A13");
    public static readonly Color BG1 = H("#0C1424");
    public static readonly Color CardColor = H("#111A2C");
    public static readonly Color Field = H("#0E1626");
    public static readonly Color CardHi = H("#16213A");
    public static readonly Color Stroke = H("#1F2B46");
    public static readonly Color Text = H("#F2F5FB");
    public static readonly Color Text2 = H("#93A1BC");
    public static readonly Color Muted = H("#5F6E8A");
    public static readonly Color Idle = H("#6B7FA3");
    public static readonly Color Accent = H("#3B82F6");
    public static readonly Color Accent2 = H("#7C5CFF");
    public static readonly Color AccentSoft = H("#7FB0FF");
    public static readonly Color Green = H("#2EE6A6");
    public static readonly Color Cyan = H("#4DD0FF");
    public static readonly Color Red = H("#FF6B7A");
    public static readonly Color DangerA = H("#E5484D");
    public static readonly Color DangerB = H("#FF7A59");
    public static readonly Color Amber = H("#FFBE5A");
    public static readonly Color CoreTop = H("#1E2B49");
    public static readonly Color CoreBottom = H("#0E1626");

    public static Color A(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r,
        StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Near, bool ellipsis = false)
    {
        using var br = new SolidBrush(c);
        using var sf = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = h,
            LineAlignment = v,
            Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None
        };
        g.DrawString(s, f, br, r, sf);
    }
}

public enum IconKind { Shield, ArrowDown, ArrowUp, Server, User, Lock, Power }

/// <summary>Vector icons drawn with GDI+ (24x24 design grid), same set as the Android app.</summary>
public static class Icons
{
    public static void Draw(Graphics g, IconKind kind, RectangleF r, Color color, float width = 2f)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PointF P(float x, float y) => new(r.X + x / 24f * r.Width, r.Y + y / 24f * r.Height);
        RectangleF R(float x, float y, float w, float h) => new(r.X + x / 24f * r.Width, r.Y + y / 24f * r.Height, w / 24f * r.Width, h / 24f * r.Height);

        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case IconKind.Shield:
            {
                using var path = new GraphicsPath();
                path.AddLine(P(12, 2.5f), P(20, 5.5f));
                path.AddLine(P(20, 5.5f), P(20, 11.5f));
                path.AddBezier(P(20, 11.5f), P(20, 16.5f), P(16.5f, 20), P(12, 21.8f));
                path.AddBezier(P(12, 21.8f), P(7.5f, 20), P(4, 16.5f), P(4, 11.5f));
                path.AddLine(P(4, 11.5f), P(4, 5.5f));
                path.CloseFigure();
                g.DrawPath(pen, path);
                g.DrawLines(pen, new[] { P(8.5f, 12), P(11, 14.5f), P(15.5f, 9.5f) });
                break;
            }
            case IconKind.ArrowDown:
                g.DrawLine(pen, P(12, 5), P(12, 19));
                g.DrawLines(pen, new[] { P(6, 13), P(12, 19), P(18, 13) });
                break;
            case IconKind.ArrowUp:
                g.DrawLine(pen, P(12, 19), P(12, 5));
                g.DrawLines(pen, new[] { P(6, 11), P(12, 5), P(18, 11) });
                break;
            case IconKind.Server:
            {
                using (var p1 = Pal.Round(R(4, 4, 16, 6), 2.5f * r.Width / 24f)) g.DrawPath(pen, p1);
                using (var p2 = Pal.Round(R(4, 14, 16, 6), 2.5f * r.Width / 24f)) g.DrawPath(pen, p2);
                g.FillEllipse(brush, R(6.6f, 6.1f, 1.8f, 1.8f));
                g.FillEllipse(brush, R(6.6f, 16.1f, 1.8f, 1.8f));
                break;
            }
            case IconKind.User:
                g.DrawEllipse(pen, R(8.5f, 4, 7, 7));
                g.DrawArc(pen, R(5, 14, 14, 14), 180, 180);
                break;
            case IconKind.Lock:
            {
                using var body = Pal.Round(R(5, 10.5f, 14, 10), 2.5f * r.Width / 24f);
                g.DrawPath(pen, body);
                using var shackle = new GraphicsPath();
                shackle.AddLine(P(8, 10.5f), P(8, 8));
                shackle.AddArc(R(8, 4, 8, 8), 180, 180);
                shackle.AddLine(P(16, 8), P(16, 10.5f));
                g.DrawPath(pen, shackle);
                break;
            }
            case IconKind.Power:
                g.DrawArc(pen, R(4, 4, 16, 16), 310, 280);
                g.DrawLine(pen, P(12, 3), P(12, 12));
                break;
        }
        g.SmoothingMode = old;
    }
}

public enum OrbMode { Idle, Busy, On }

/// <summary>Rounded translucent panel used for every card.</summary>
public class Card : Panel
{
    public float Radius { get; set; } = 20f;
    public Color Fill { get; set; } = Pal.CardColor;
    public Color Outline { get; set; } = Pal.Stroke;

    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        using var path = Pal.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius);
        using var fill = new SolidBrush(Fill);
        using var pen = new Pen(Outline, 1f);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

/// <summary>TableLayoutPanel that is transparent and double buffered (lets the form gradient show through).</summary>
public sealed class BufferedTable : TableLayoutPanel
{
    public BufferedTable()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
    }
}

/// <summary>Logo + app name at the top of the window.</summary>
public sealed class HeaderBar : Control
{
    readonly Font title = new("Segoe UI Semibold", 21f);
    readonly Font sub = new("Segoe UI", 8f, FontStyle.Bold);

    public HeaderBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        float top = (Height - 48f) / 2f;
        var logo = new RectangleF(0, top, 48, 48);
        using (var path = Pal.Round(logo, 15))
        using (var br = new LinearGradientBrush(logo, Pal.Accent, Pal.Accent2, 45f))
        {
            // soft glow under the logo
            using var glow = new SolidBrush(Pal.A(Pal.Accent, 28));
            g.FillEllipse(glow, logo.X - 6, logo.Y - 2, logo.Width + 12, logo.Height + 12);
            g.FillPath(br, path);
        }
        Icons.Draw(g, IconKind.Shield, new RectangleF(logo.X + 13, logo.Y + 13, 22, 22), Color.White, 2.1f);
        Pal.DrawText(g, "Open-SSTP", title, Pal.Text, new RectangleF(64, top - 5, Width - 64, 34), v: StringAlignment.Center);
        Pal.DrawText(g, "SOFTETHER VPN FOR WINDOWS", sub, Pal.Muted, new RectangleF(66, top + 29, Width - 66, 16), v: StringAlignment.Center);
    }
}

/// <summary>Big round connect button with glow, pulse rings (connected) and spinner (connecting).</summary>
public sealed class PowerOrb : Control
{
    OrbMode mode = OrbMode.Idle;
    float phase;
    bool hover;
    readonly System.Windows.Forms.Timer anim = new() { Interval = 33 };

    public PowerOrb()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        anim.Tick += (_, _) => { phase += 0.033f; if (phase > 1000f) phase = 0f; if (Visible) Invalidate(); };
    }

    public OrbMode Mode
    {
        get => mode;
        set
        {
            if (mode == value) return;
            mode = value;
            if (mode == OrbMode.Idle) anim.Stop(); else anim.Start();
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        float size = Math.Min(Width, Height);
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, outer = size / 2f - 3f;
        Color c = mode switch { OrbMode.On => Pal.Green, OrbMode.Busy => Pal.Amber, _ => Pal.Idle };

        // glow
        using (var gp = new GraphicsPath())
        {
            gp.AddEllipse(cx - outer, cy - outer, outer * 2, outer * 2);
            using var pb = new PathGradientBrush(gp)
            {
                CenterColor = Pal.A(c, mode == OrbMode.Idle ? (hover ? 56 : 30) : (hover ? 100 : 76)),
                SurroundColors = new[] { Pal.A(c, 0) }
            };
            g.FillPath(pb, gp);
        }

        float core = outer * 0.62f, ring = outer * 0.78f;

        if (mode == OrbMode.On)
        {
            for (int k = 0; k < 2; k++)
            {
                float t = (phase * 0.45f + k * 0.5f) % 1f;
                float rr = core + (outer - core) * t;
                using var p = new Pen(Pal.A(c, (int)(110 * (1f - t))), 2f);
                g.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2);
            }
        }

        var ringRect = new RectangleF(cx - ring, cy - ring, ring * 2, ring * 2);
        if (mode == OrbMode.Busy)
        {
            using var faint = new Pen(Pal.A(c, 46), 3f);
            g.DrawEllipse(faint, ringRect);
            using var spin = new Pen(Pal.A(c, 235), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(spin, ringRect, (phase * 330f) % 360f, 105f);
        }
        else
        {
            using var rp = new Pen(Pal.A(c, mode == OrbMode.On ? 210 : (hover ? 170 : 110)), 2.5f);
            g.DrawEllipse(rp, ringRect);
        }

        // core disc
        var coreRect = new RectangleF(cx - core, cy - core, core * 2, core * 2);
        using (var cb = new LinearGradientBrush(coreRect, Pal.CoreTop, Pal.CoreBottom, 90f))
            g.FillEllipse(cb, coreRect);
        using (var cp = new Pen(Pal.A(c, hover ? 210 : 130), 1.6f))
            g.DrawEllipse(cp, coreRect);

        float ic = core * 0.82f;
        Icons.Draw(g, IconKind.Power, new RectangleF(cx - ic / 2f, cy - ic / 2f, ic, ic),
            mode == OrbMode.Idle ? Pal.Text2 : c, Math.Max(2.4f, core * 0.075f));
    }
}

/// <summary>Rounded status chip: coloured dot + text. Colour comes from ForeColor.</summary>
public sealed class StatusPill : Control
{
    readonly Font font = new("Segoe UI Semibold", 10f);

    public StatusPill()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 34; Width = 170;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = TextRenderer.MeasureText(Text, font).Width + 54;
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Pal.Round(rect, rect.Height / 2f))
        using (var fill = new SolidBrush(Pal.A(ForeColor, 30)))
        using (var pen = new Pen(Pal.A(ForeColor, 90), 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        using (var dot = new SolidBrush(ForeColor)) g.FillEllipse(dot, 15, Height / 2f - 4f, 8, 8);
        Pal.DrawText(g, Text, font, ForeColor, new RectangleF(32, 0, Width - 38, Height), v: StringAlignment.Center);
    }
}

/// <summary>Download / upload tile: icon badge + caption + live value (Text).</summary>
public sealed class SpeedCell : Control
{
    readonly IconKind kind;
    readonly Color tint;
    readonly string caption;
    readonly Font capFont = new("Segoe UI", 8f, FontStyle.Bold);
    readonly Font valFont = new("Segoe UI Semibold", 17f);

    public SpeedCell(IconKind kind, Color tint, string caption)
    {
        this.kind = kind; this.tint = tint; this.caption = caption;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Text = "0.0 KB/s";
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        const float bs = 46f;
        float by = (Height - bs) / 2f;
        var badge = new RectangleF(14, by, bs, bs);
        using (var path = Pal.Round(badge, 14))
        using (var fill = new SolidBrush(Pal.A(tint, 34)))
        using (var pen = new Pen(Pal.A(tint, 70), 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        Icons.Draw(g, kind, new RectangleF(badge.X + 12, badge.Y + 12, bs - 24, bs - 24), tint, 2.3f);
        float tx = badge.Right + 12;
        Pal.DrawText(g, caption, capFont, Pal.Muted, new RectangleF(tx, by + 1, Width - tx - 4, 16), ellipsis: true);
        Pal.DrawText(g, Text, valFont, Pal.Text, new RectangleF(tx, by + 16, Width - tx - 4, 30), ellipsis: true);
    }
}

/// <summary>Label + icon + text box on a rounded dark field (replaces the plain TextBox look).</summary>
public sealed class FieldBox : Control
{
    readonly TextBox box;
    readonly string caption;
    readonly IconKind kind;
    readonly Font capFont = new("Segoe UI", 8f, FontStyle.Bold);
    bool focused;

    public FieldBox(string caption, IconKind kind, TextBox tb)
    {
        this.caption = caption; this.kind = kind; box = tb;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = Pal.Field;
        box.ForeColor = Pal.Text;
        box.Font = new Font("Segoe UI", 11f);
        box.Width = 200;
        box.Enter += (_, _) => { focused = true; Invalidate(); };
        box.Leave += (_, _) => { focused = false; Invalidate(); };
        Controls.Add(box);
        Height = 80;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        box.Left = 48;
        box.Width = Math.Max(20, Width - 48 - 16);
        box.Top = 22 + (48 - box.Height) / 2;
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); box.Focus(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        Pal.DrawText(g, caption, capFont, Pal.Muted, new RectangleF(3, 0, Width - 6, 18), ellipsis: true);
        var rect = new RectangleF(0.5f, 22.5f, Width - 1.5f, 47f);
        using (var path = Pal.Round(rect, 14))
        using (var fill = new SolidBrush(Pal.Field))
        using (var pen = new Pen(focused ? Pal.A(Pal.Accent, 230) : Pal.Stroke, focused ? 1.6f : 1.2f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        Icons.Draw(g, kind, new RectangleF(16, 22 + 13.5f, 20, 20), focused ? Pal.AccentSoft : Pal.Muted, 1.8f);
    }
}

/// <summary>Section title: English on the left, Persian on the right, with an accent bar.</summary>
public sealed class SectionLabel : Control
{
    readonly string left, right;
    readonly Font f1 = new("Segoe UI Semibold", 12f);
    readonly Font f2 = new("Segoe UI", 10f);

    public SectionLabel(string left, string right)
    {
        this.left = left; this.right = right;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var bar = new RectangleF(0, Height / 2f - 9, 4, 18);
        using (var path = Pal.Round(bar, 2))
        using (var br = new LinearGradientBrush(bar, Pal.Accent, Pal.Accent2, 90f))
            g.FillPath(br, path);
        Pal.DrawText(g, left, f1, Pal.Text, new RectangleF(14, 0, Width - 14, Height), v: StringAlignment.Center);
        using var br2 = new SolidBrush(Pal.Muted);
        using var sf = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft)
        { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        g.DrawString(right, f2, br2, new RectangleF(0, 0, Width, Height), sf);
    }
}

/// <summary>Whole "Remember me" row: two-line label + animated switch. Click anywhere to toggle.</summary>
public sealed class ToggleSwitch : Control
{
    bool on;
    float pos;
    readonly System.Windows.Forms.Timer t = new() { Interval = 15 };
    readonly Font f1 = new("Segoe UI", 10.5f);
    readonly Font f2 = new("Segoe UI", 8.5f);

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        t.Tick += (_, _) =>
        {
            float target = on ? 1f : 0f;
            pos += (target - pos) * 0.35f;
            if (Math.Abs(target - pos) < 0.02f) { pos = target; t.Stop(); }
            Invalidate();
        };
    }

    public bool Checked
    {
        get => on;
        set
        {
            if (on == value) return;
            on = value;
            if (IsHandleCreated) t.Start(); else pos = on ? 1f : 0f;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) t.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        Pal.DrawText(g, "مرا به خاطر بسپار", f1, Pal.Text, new RectangleF(2, 4, Width - 70, 24), v: StringAlignment.Center);
        Pal.DrawText(g, "Remember me", f2, Pal.Muted, new RectangleF(2, 26, Width - 70, 18), v: StringAlignment.Center);

        const float tw = 48f, th = 28f;
        var track = new RectangleF(Width - tw - 2, (Height - th) / 2f, tw, th);
        using (var path = Pal.Round(track, th / 2f))
        using (var fill = new SolidBrush(Pal.Lerp(Pal.Stroke, Pal.Accent, pos)))
        {
            g.FillPath(fill, path);
            if (Focused) { using var fp = new Pen(Pal.A(Pal.AccentSoft, 160), 1.5f); g.DrawPath(fp, path); }
        }
        float kd = th - 8f;
        float kx = track.X + 4f + (tw - 8f - kd) * pos;
        using var knob = new SolidBrush(Pal.Lerp(Pal.Text2, Color.White, pos));
        g.FillEllipse(knob, kx, track.Y + 4f, kd, kd);
    }
}

/// <summary>Rounded gradient button. Blue/violet normally, red/orange while it says "Disconnect".</summary>
public sealed class GradientButton : Button
{
    bool hover, down;
    readonly Font font = new("Segoe UI Semibold", 12.5f);

    public GradientButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; down = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs mevent) { base.OnMouseDown(mevent); down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs mevent) { base.OnMouseUp(mevent); down = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics; Pal.Smooth(g);
        bool danger = string.Equals(Text, "Disconnect", StringComparison.OrdinalIgnoreCase);
        Color a = danger ? Pal.DangerA : Pal.Accent, b = danger ? Pal.DangerB : Pal.Accent2;
        int alpha = Enabled ? 255 : 120;

        var rect = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
        using var path = Pal.Round(rect, 18);
        if (rect.Width < 4 || rect.Height < 4) return;
        using (var br = new LinearGradientBrush(rect, Pal.A(a, alpha), Pal.A(b, alpha), LinearGradientMode.Horizontal))
            g.FillPath(br, path);
        if (Enabled && hover && !down) { using var hi = new SolidBrush(Color.FromArgb(24, 255, 255, 255)); g.FillPath(hi, path); }
        if (down) { using var lo = new SolidBrush(Color.FromArgb(36, 0, 0, 0)); g.FillPath(lo, path); }

        string label = Text;
        var sz = g.MeasureString(label, font);
        const float iconSize = 20f, gap = 10f;
        float total = iconSize + gap + sz.Width;
        float x0 = (Width - total) / 2f;
        var tint = Color.FromArgb(Enabled ? 255 : 170, 255, 255, 255);
        Icons.Draw(g, IconKind.Power, new RectangleF(x0, (Height - iconSize) / 2f, iconSize, iconSize), tint, 2.2f);
        Pal.DrawText(g, label, font, tint, new RectangleF(x0 + iconSize + gap, 0, sz.Width + 6, Height), v: StringAlignment.Center);
    }
}

/// <summary>Live traffic graph: smooth gradient-filled lines for download and upload.</summary>
public sealed class TrafficChart : Control
{
    readonly Queue<double> incoming = new(), outgoing = new();
    readonly Font capFont = new("Segoe UI", 9f, FontStyle.Bold);
    readonly Font legFont = new("Segoe UI", 8.5f);
    public string Caption { get; set; } = "Live traffic";

    public TrafficChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public void Add(long rx, long tx)
    {
        incoming.Enqueue(rx / 1024d); outgoing.Enqueue(tx / 1024d);
        while (incoming.Count > 90) incoming.Dequeue();
        while (outgoing.Count > 90) outgoing.Dequeue();
        if (Visible) Invalidate();
    }

    public void ClearSamples() { incoming.Clear(); outgoing.Clear(); Add(0, 0); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Pal.Round(card, 20))
        using (var fill = new SolidBrush(Pal.CardColor))
        using (var pen = new Pen(Pal.Stroke, 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        // legend (right)
        float lx = Width - 16;
        foreach (var (label, color) in new[] { ("Upload", Pal.AccentSoft), ("Download", Pal.Green) })
        {
            float w = g.MeasureString(label, legFont).Width - 6;
            lx -= w;
            Pal.DrawText(g, label, legFont, Pal.Text2, new RectangleF(lx, 12, w + 8, 20), v: StringAlignment.Center);
            lx -= 14;
            using var dot = new SolidBrush(color);
            g.FillEllipse(dot, lx, 22 - 4, 8, 8);
            lx -= 14;
        }
        float capWidth = Math.Max(40, lx - 18);
        Pal.DrawText(g, Caption, capFont, Pal.Text2, new RectangleF(18, 12, capWidth, 20), v: StringAlignment.Center, ellipsis: true);

        var plot = new RectangleF(18, 46, Width - 36, Height - 46 - 18);
        if (plot.Width < 20 || plot.Height < 20) return;

        using (var grid = new Pen(Pal.A(Pal.Stroke, 200), 1f) { DashStyle = DashStyle.Dash })
            for (int i = 0; i <= 3; i++)
            {
                float y = plot.Y + plot.Height * i / 3f;
                g.DrawLine(grid, plot.X, y, plot.Right, y);
            }

        double peak = 16;
        if (incoming.Count > 0) peak = Math.Max(peak, incoming.Max());
        if (outgoing.Count > 0) peak = Math.Max(peak, outgoing.Max());
        double max = peak * 1.2;

        DrawSeries(g, incoming, Pal.Green, plot, max);
        DrawSeries(g, outgoing, Pal.AccentSoft, plot, max);
    }

    static void DrawSeries(Graphics g, IEnumerable<double> vals, Color color, RectangleF plot, double max)
    {
        var a = vals.ToArray();
        if (a.Length < 2) return;
        var pts = new PointF[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            float x = plot.X + i * plot.Width / Math.Max(1, a.Length - 1);
            float y = plot.Bottom - (float)(a[i] / max) * plot.Height;
            pts[i] = new PointF(x, Math.Clamp(y, plot.Y, plot.Bottom));
        }
        using (var fill = new GraphicsPath())
        {
            fill.AddCurve(pts, 0.35f);
            fill.AddLine(pts[^1], new PointF(pts[^1].X, plot.Bottom));
            fill.AddLine(new PointF(pts[^1].X, plot.Bottom), new PointF(pts[0].X, plot.Bottom));
            fill.CloseFigure();
            using var br = new LinearGradientBrush(new RectangleF(plot.X, plot.Y, plot.Width, plot.Height + 1), Pal.A(color, 80), Pal.A(color, 0), 90f);
            var old = g.Clip;
            g.SetClip(plot, CombineMode.Intersect);
            g.FillPath(br, fill);
            g.Clip = old;
        }
        using var pen = new Pen(color, 2.4f) { LineJoin = LineJoin.Round };
        g.DrawCurve(pen, pts, 0.35f);
    }
}
