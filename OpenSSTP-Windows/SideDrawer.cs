using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace OpenSSTP.Windows;

/// <summary>Mouse-wheel helpers: owner-drawn controls don't scroll their parent by themselves.</summary>
internal static class ScrollHelper
{
    public static void Scroll(ScrollableControl sc, int delta)
    {
        var p = sc.AutoScrollPosition; // negative while scrolled
        sc.AutoScrollPosition = new Point(-p.X, -p.Y - (int)(delta * 0.75));
    }

    /// <summary>Passes a wheel event to the nearest parent that currently shows a vertical scrollbar.</summary>
    public static void Forward(Control from, MouseEventArgs e)
    {
        for (Control? c = from.Parent; c is not null; c = c.Parent)
        {
            if (c is ScrollableControl sc && sc.VerticalScroll.Visible) { Scroll(sc, e.Delta); return; }
        }
    }
}

/// <summary>The "☰" button in the header. Soft hover glow, same look as the cards.</summary>
public sealed class HamburgerButton : Control
{
    bool hover, down;
    float glow;
    readonly System.Windows.Forms.Timer anim = new() { Interval = 15 };

    public HamburgerButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        anim.Tick += (_, _) =>
        {
            float target = hover ? 1f : 0f;
            glow += (target - glow) * 0.3f;
            if (Math.Abs(target - glow) < 0.02f) { glow = target; anim.Stop(); }
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; anim.Start(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; down = false; anim.Start(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Pal.Round(rect, 14))
        using (var fill = new SolidBrush(Pal.Lerp(Pal.A(Pal.CardColor, 200), Pal.CardHi, glow)))
        using (var pen = new Pen(Pal.Lerp(Pal.Stroke, Pal.A(Pal.Accent, 200), glow), 1.2f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
            if (down) { using var lo = new SolidBrush(Color.FromArgb(40, 0, 0, 0)); g.FillPath(lo, path); }
        }
        float ic = 22f;
        Icons.Draw(g, IconKind.Menu, new RectangleF((Width - ic) / 2f, (Height - ic) / 2f, ic, ic), Pal.Lerp(Pal.Text2, Pal.Text, glow), 2.2f);
    }
}

/// <summary>Dims the main window while the side menu is open (draws a snapshot of the window + a dark veil).</summary>
internal sealed class Scrim : Control
{
    Bitmap? snapshot;
    float alpha;

    public Scrim()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        BackColor = Pal.BG0;
    }

    public float Alpha
    {
        get => alpha;
        set { alpha = Math.Clamp(value, 0f, 1f); Invalidate(); }
    }

    public void SetSnapshot(Bitmap? bmp) { snapshot?.Dispose(); snapshot = bmp; }

    protected override void Dispose(bool disposing) { if (disposing) { snapshot?.Dispose(); snapshot = null; } base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (snapshot is not null)
            g.DrawImage(snapshot, new Rectangle(0, 0, Width, Height), 0, 0, snapshot.Width, snapshot.Height, GraphicsUnit.Pixel);
        else
            g.Clear(Pal.BG0);
        using var veil = new SolidBrush(Color.FromArgb((int)(alpha * 175), 3, 6, 12));
        g.FillRectangle(veil, 0, 0, Width, Height);
    }
}

/// <summary>Base for every row inside the side menu: transparent, hover tracking, wheel scrolls the menu.</summary>
internal abstract class DrawerItem : Control
{
    protected bool Hover;

    protected DrawerItem()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 4);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover = false; Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ScrollHelper.Forward(this, e); }
}

/// <summary>Small caps heading (English left, Persian right) that splits the menu into groups.</summary>
internal sealed class DrawerSection : DrawerItem
{
    readonly string en, fa;
    readonly Font f1 = new("Segoe UI", 8f, FontStyle.Bold);
    readonly Font f2 = new("Segoe UI", 8.5f);

    public DrawerSection(string en, string fa)
    {
        this.en = en; this.fa = fa;
        Height = 34;
        Margin = new Padding(0, 10, 0, 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        Pal.DrawText(g, en, f1, Pal.Muted, new RectangleF(4, 4, Width - 8, Height - 4), v: StringAlignment.Center);
        Pal.DrawFa(g, fa, f2, Pal.Muted, new RectangleF(0, 4, Width - 4, Height - 4), toRight: true);
    }
}

/// <summary>Shown instead of the config list when nothing has been saved yet.</summary>
internal sealed class DrawerEmpty : DrawerItem
{
    readonly string en, fa;
    readonly Font f1 = new("Segoe UI Semibold", 9.5f);
    readonly Font f2 = new("Segoe UI", 8.5f);

    public DrawerEmpty(string en, string fa)
    {
        this.en = en; this.fa = fa;
        Height = 78;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Pal.Round(rect, 16))
        using (var pen = new Pen(Pal.A(Pal.Stroke, 230), 1.2f) { DashStyle = DashStyle.Dash })
            g.DrawPath(pen, path);
        Pal.DrawText(g, en, f1, Pal.Text2, new RectangleF(0, 14, Width, 24), StringAlignment.Center, StringAlignment.Center);
        Pal.DrawText(g, fa, f2, Pal.Muted, new RectangleF(0, 40, Width, 22), StringAlignment.Center, StringAlignment.Center);
    }
}

/// <summary>A clickable menu entry: tinted icon badge, English title, Persian caption, optional badge or chevron.</summary>
internal sealed class DrawerMenuRow : DrawerItem
{
    public IconKind Icon { get; set; } = IconKind.Info;
    public string Title { get; set; } = "";
    public string Fa { get; set; } = "";
    public string Badge { get; set; } = "";
    public bool Chevron { get; set; } = true;
    public Color Tint { get; set; } = Pal.AccentSoft;

    readonly Font f1 = new("Segoe UI Semibold", 10.5f);
    readonly Font f2 = new("Segoe UI", 8.5f);
    readonly Font fb = new("Segoe UI Semibold", 8.5f);
    bool down;

    public DrawerMenuRow()
    {
        Height = 56;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { down = false; base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (Hover)
        {
            using var path = Pal.Round(rect, 14);
            using var fill = new SolidBrush(Pal.A(Pal.CardHi, down ? 255 : 210));
            g.FillPath(fill, path);
        }

        var badge = new RectangleF(8, (Height - 38f) / 2f, 38, 38);
        using (var path = Pal.Round(badge, 12))
        using (var fill = new SolidBrush(Pal.A(Tint, 32)))
        using (var pen = new Pen(Pal.A(Tint, 70), 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        Icons.Draw(g, Icon, new RectangleF(badge.X + 9, badge.Y + 9, 20, 20), Tint, 2f);

        float tx = badge.Right + 12;
        float right = Width - 14;
        if (Badge.Length > 0)
        {
            float bw = Math.Max(26f, g.MeasureString(Badge, fb).Width + 10f);
            var pill = new RectangleF(Width - bw - 12, (Height - 22f) / 2f, bw, 22);
            using (var path = Pal.Round(pill, 11))
            using (var fill = new SolidBrush(Pal.A(Tint, 36)))
            {
                g.FillPath(fill, path);
            }
            Pal.DrawText(g, Badge, fb, Tint, pill, StringAlignment.Center, StringAlignment.Center);
            right = pill.X - 8;
        }
        else if (Chevron)
        {
            Icons.Draw(g, IconKind.Chevron, new RectangleF(Width - 30, (Height - 16f) / 2f, 16, 16), Hover ? Pal.Text2 : Pal.Muted, 1.8f);
            right = Width - 34;
        }

        float tw = Math.Max(20, right - tx);
        if (Fa.Length == 0)
        {
            Pal.DrawText(g, Title, f1, Pal.Text, new RectangleF(tx, 0, tw, Height), v: StringAlignment.Center, ellipsis: true);
        }
        else
        {
            Pal.DrawText(g, Title, f1, Pal.Text, new RectangleF(tx, 8, tw, 22), v: StringAlignment.Center, ellipsis: true);
            Pal.DrawFa(g, Fa, f2, Pal.Muted, new RectangleF(tx, 29, tw, 18));
        }
    }
}

/// <summary>A saved config: gradient avatar, name, server · user. Hover reveals favourite / connect / edit / delete.</summary>
internal sealed class ConfigRow : DrawerItem
{
    public VpnProfile Profile { get; }
    public bool Active { get; set; }

    public event EventHandler? UseClicked;
    public event EventHandler? FavoriteClicked;
    public event EventHandler? ConnectClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? DeleteClicked;

    static readonly string[] ActionTips = { "Favourite", "Connect now", "Edit", "Delete" };
    readonly Font initialFont = new("Segoe UI Semibold", 13f);
    readonly Font nameFont = new("Segoe UI Semibold", 10.5f);
    readonly Font subFont = new("Segoe UI", 8.5f);
    readonly ToolTip tip = new();
    int hot = -1;

    const int Cell = 26;
    const int ActionCount = 4;

    public ConfigRow(VpnProfile profile)
    {
        Profile = profile;
        Height = 64;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 0, 6);
    }

    protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }

    RectangleF ActionRect(int i) => new(Width - 10 - Cell * (ActionCount - i), (Height - Cell) / 2f, Cell, Cell);

    int ActionAt(Point p)
    {
        if (!Hover) return -1;
        for (int i = 0; i < ActionCount; i++) if (ActionRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = ActionAt(e.Location);
        if (h != hot)
        {
            hot = h;
            try { tip.SetToolTip(this, h >= 0 ? ActionTips[h] : string.Empty); } catch { }
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hot = -1;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        switch (ActionAt(e.Location))
        {
            case 0: FavoriteClicked?.Invoke(this, EventArgs.Empty); break;
            case 1: ConnectClicked?.Invoke(this, EventArgs.Empty); break;
            case 2: EditClicked?.Invoke(this, EventArgs.Empty); break;
            case 3: DeleteClicked?.Invoke(this, EventArgs.Empty); break;
            default: UseClicked?.Invoke(this, EventArgs.Empty); break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color fillColor = Active ? Pal.Lerp(Pal.CardColor, Pal.Accent, 0.16f) : Hover ? Pal.CardHi : Pal.CardColor;
        Color lineColor = Active ? Pal.A(Pal.Accent, 175) : Hover ? Pal.A(Pal.Accent, 90) : Pal.Stroke;
        using (var path = Pal.Round(rect, 16))
        using (var fill = new SolidBrush(fillColor))
        using (var pen = new Pen(lineColor, 1.2f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        // avatar
        var av = new RectangleF(12, (Height - 40f) / 2f, 40, 40);
        var pair = Pal.Avatars[Math.Abs(Profile.ColorIndex) % Pal.Avatars.Length];
        using (var br = new LinearGradientBrush(av, pair[0], pair[1], 45f))
            g.FillEllipse(br, av);
        string name = (Profile.Name ?? "").Trim();
        string initial = name.Length == 0 ? "?" : char.ToUpperInvariant(name[0]).ToString();
        Pal.DrawText(g, initial, initialFont, Color.White, av, StringAlignment.Center, StringAlignment.Center);

        // texts
        float tx = av.Right + 12;
        float reserve = (Profile.Favorite || Active) ? 46f : 0f;
        if (Hover) reserve = Cell * ActionCount + 14f;
        float tw = Math.Max(20f, Width - tx - 12f - reserve);
        string host = (Profile.Host ?? "").Trim();
        string usr = (Profile.UserName ?? "").Trim();
        string sub = usr.Length == 0 ? host : host + "  ·  " + usr;
        Pal.DrawText(g, name.Length == 0 ? host : name, nameFont, Pal.Text, new RectangleF(tx, 10, tw, 22), v: StringAlignment.Center, ellipsis: true);
        Pal.DrawText(g, sub, subFont, Pal.Muted, new RectangleF(tx, 33, tw, 18), v: StringAlignment.Center, ellipsis: true);

        if (Hover)
        {
            for (int i = 0; i < ActionCount; i++)
            {
                var cell = ActionRect(i);
                bool isHot = i == hot;
                Color tint = i switch
                {
                    0 => Pal.Amber,
                    1 => Pal.Green,
                    2 => Pal.AccentSoft,
                    _ => Pal.Red
                };
                if (isHot)
                {
                    using var path = Pal.Round(cell, 8);
                    using var fill = new SolidBrush(Pal.A(tint, 38));
                    g.FillPath(fill, path);
                }
                IconKind kind = i switch
                {
                    0 => Profile.Favorite ? IconKind.StarFill : IconKind.Star,
                    1 => IconKind.Bolt,
                    2 => IconKind.Edit,
                    _ => IconKind.Trash
                };
                Color c = isHot ? tint : (i == 0 && Profile.Favorite ? Pal.Amber : Pal.Text2);
                Icons.Draw(g, kind, new RectangleF(cell.X + 5, cell.Y + 5, Cell - 10, Cell - 10), c, 1.8f);
            }
        }
        else
        {
            float x = Width - 16;
            if (Active)
            {
                using var dot = new SolidBrush(Pal.Green);
                g.FillEllipse(dot, x - 8, Height / 2f - 4f, 8, 8);
                x -= 22;
            }
            if (Profile.Favorite)
                Icons.Draw(g, IconKind.StarFill, new RectangleF(x - 16, Height / 2f - 8f, 16, 16), Pal.Amber, 1.4f);
        }
    }
}

/// <summary>Scrolling column that holds the menu rows. Double buffered, dark scrollbar where Windows supports it.</summary>
internal sealed class DrawerBody : FlowLayoutPanel
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

    public DrawerBody()
    {
        DoubleBuffered = true;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        BackColor = Pal.Drawer;
        Padding = new Padding(14, 4, 14, 22);
        Dock = DockStyle.Fill;
        HandleCreated += (_, _) =>
        {
            try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
        };
    }
}

/// <summary>The slide-in menu panel: header with close button + a scrolling body of rows.</summary>
internal sealed class SideDrawer : Control
{
    public const int DrawerWidth = 352;
    const int HeaderHeight = 76;

    readonly DrawerBody body = new();
    readonly Font titleFont = new("Segoe UI Semibold", 15f);
    readonly Font subFont = new("Segoe UI", 9f);
    bool hoverClose;

    public event EventHandler? CloseRequested;

    public SideDrawer()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Pal.Drawer;
        Padding = new Padding(0, HeaderHeight, 1, 0);
        Controls.Add(body);
        body.Resize += (_, _) => FitItems();
    }

    RectangleF CloseRect => new(Width - 58, (HeaderHeight - 36f) / 2f, 36, 36);

    /// <summary>Replaces all rows (the old ones are disposed). The scroll position is kept when possible.</summary>
    public void SetItems(IList<Control> items)
    {
        int keep = -body.AutoScrollPosition.Y;
        body.SuspendLayout();
        var old = new List<Control>();
        foreach (Control c in body.Controls) old.Add(c);
        body.Controls.Clear();
        foreach (var c in old) c.Dispose();
        foreach (var c in items) body.Controls.Add(c);
        FitItems();
        body.ResumeLayout(true);
        if (keep > 0)
        {
            body.PerformLayout();
            body.AutoScrollPosition = new Point(0, keep);
        }
    }

    public void ResetScroll() { body.AutoScrollPosition = new Point(0, 0); }

    void FitItems()
    {
        int w = body.ClientSize.Width - body.Padding.Horizontal;
        foreach (Control c in body.Controls)
        {
            int cw = Math.Max(40, w - c.Margin.Horizontal);
            if (c.Width != cw) c.Width = cw;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool h = CloseRect.Contains(e.Location);
        if (h != hoverClose) { hoverClose = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverClose) { hoverClose = false; Cursor = Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && CloseRect.Contains(e.Location)) CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollHelper.Scroll(body, e.Delta);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Pal.Smooth(g);
        using (var bg = new SolidBrush(Pal.Drawer)) g.FillRectangle(bg, 0, 0, Width, Height);

        // right edge
        using (var edge = new Pen(Pal.Stroke, 1f)) g.DrawLine(edge, Width - 1, 0, Width - 1, Height);

        // header
        var logo = new RectangleF(18, (HeaderHeight - 40f) / 2f, 40, 40);
        using (var path = Pal.Round(logo, 13))
        using (var br = new LinearGradientBrush(logo, Pal.Accent, Pal.Accent2, 45f))
            g.FillPath(br, path);
        Icons.Draw(g, IconKind.Gear, new RectangleF(logo.X + 10, logo.Y + 10, 20, 20), Color.White, 2f);
        Pal.DrawText(g, "Menu", titleFont, Pal.Text, new RectangleF(70, 12, Width - 140, 28), v: StringAlignment.Center);
        Pal.DrawText(g, "منو و تنظیمات برنامه", subFont, Pal.Muted, new RectangleF(71, 40, Width - 140, 20), v: StringAlignment.Center);

        var cr = CloseRect;
        if (hoverClose)
        {
            using var path = Pal.Round(cr, 12);
            using var fill = new SolidBrush(Pal.CardHi);
            g.FillPath(fill, path);
        }
        Icons.Draw(g, IconKind.Close, new RectangleF(cr.X + 9, cr.Y + 9, 18, 18), hoverClose ? Pal.Text : Pal.Text2, 2f);

        using (var line = new Pen(Pal.A(Pal.Stroke, 230), 1f))
            g.DrawLine(line, 16, HeaderHeight - 1, Width - 17, HeaderHeight - 1);
    }
}
