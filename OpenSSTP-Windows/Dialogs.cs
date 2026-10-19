using System.Runtime.InteropServices;

namespace OpenSSTP.Windows;

/// <summary>Dark, fixed-size dialog window that matches the main window (dark title bar on Windows 10/11).</summary>
internal class DialogShell : Form
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected DialogShell(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Pal.BG1;
        ForeColor = Pal.Text;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int on = 1;
            DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
        }
        catch { }
    }
}

/// <summary>Themed replacement for MessageBox: heading, message, one or two buttons.</summary>
internal sealed class MessageDialog : DialogShell
{
    public MessageDialog(string en, string fa, string message, string okText, string? cancelText, bool danger, IconKind glyph, int height)
        : base(en)
    {
        ClientSize = new Size(470, height);
        var root = new BufferedTable { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 18), ColumnCount = 1, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        root.Controls.Add(new SectionLabel(en, fa) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = message, Dock = DockStyle.Fill, AutoSize = false, ForeColor = Pal.Text2, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10f), Margin = new Padding(2, 12, 2, 0)
        }, 0, 1);

        var buttons = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = cancelText is null ? 1 : 2, RowCount = 1 };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var ok = new GradientButton { Text = okText, Glyph = glyph, Danger = danger, Dock = DockStyle.Fill, Margin = new Padding(4, 6, 0, 0), DialogResult = DialogResult.OK };
        if (cancelText is null)
        {
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.Controls.Add(ok, 0, 0);
        }
        else
        {
            var cancel = new GradientButton { Text = cancelText, Glyph = null, Secondary = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 4, 0), DialogResult = DialogResult.Cancel };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            buttons.Controls.Add(cancel, 0, 0);
            buttons.Controls.Add(ok, 1, 0);
            CancelButton = cancel;
        }
        AcceptButton = ok;
        root.Controls.Add(buttons, 0, 2);
    }
}

/// <summary>Add / edit a saved config: name, server, username, password (+ whether to store it).</summary>
internal sealed class ConfigDialog : DialogShell
{
    readonly TextBox nameBox = new() { PlaceholderText = "e.g. Tokyo VPN Gate #1" };
    readonly TextBox hostBox = new() { PlaceholderText = "VPN Gate hostname or IP[:port]" };
    readonly TextBox userBox = new() { PlaceholderText = "Username (VPN Gate: vpn)" };
    readonly TextBox passBox = new() { UseSystemPasswordChar = true };
    readonly ToggleSwitch savePass = new() { Title = "Save password", Subtitle = "ذخیره امن رمز عبور در ویندوز", Margin = Padding.Empty };
    readonly Label error = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Pal.Red, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleLeft };

    public string ConfigName => nameBox.Text.Trim();
    public string Host => hostBox.Text.Trim();
    public string Login => userBox.Text.Trim();
    public string Password => passBox.Text;
    public bool SavePassword => savePass.Checked;

    public ConfigDialog(string en, string fa, string okText, string name, string host, string user, string password, bool savePassword, bool passwordAlreadySaved)
        : base(en)
    {
        nameBox.Text = name; hostBox.Text = host; userBox.Text = user; passBox.Text = password;
        passBox.PlaceholderText = passwordAlreadySaved ? "Leave empty to keep the saved password" : "Password (VPN Gate often blank)";
        savePass.Checked = savePassword;

        ClientSize = new Size(450, 592);
        var root = new BufferedTable { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 18), ColumnCount = 1, RowCount = 8 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in new[] { 40, 84, 84, 84, 84, 58, 26 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new SectionLabel(en, fa) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        root.Controls.Add(new FieldBox("CONFIG NAME", IconKind.Star, nameBox) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        root.Controls.Add(new FieldBox("SERVER / HOST (optionally :port)", IconKind.Server, hostBox) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 2);
        root.Controls.Add(new FieldBox("USERNAME", IconKind.User, userBox) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 3);
        root.Controls.Add(new FieldBox("PASSWORD", IconKind.Lock, passBox) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 4);
        savePass.Dock = DockStyle.Fill;
        root.Controls.Add(savePass, 0, 5);
        root.Controls.Add(error, 0, 6);

        var buttons = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        var cancel = new GradientButton { Text = "Cancel", Glyph = null, Secondary = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 4, 0), DialogResult = DialogResult.Cancel };
        var ok = new GradientButton { Text = okText, Glyph = IconKind.Save, Danger = false, Dock = DockStyle.Fill, Margin = new Padding(4, 6, 0, 0) };
        ok.Click += (_, _) =>
        {
            if (ConfigName.Length == 0) { Fail("Give this config a name.", nameBox); return; }
            if (Host.Length == 0) { Fail("The server / host is required.", hostBox); return; }
            DialogResult = DialogResult.OK;
        };
        buttons.Controls.Add(cancel, 0, 0);
        buttons.Controls.Add(ok, 1, 0);
        root.Controls.Add(buttons, 0, 7);
        AcceptButton = ok;
        CancelButton = cancel;

        Shown += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
    }

    void Fail(string message, TextBox box)
    {
        error.Text = message;
        box.Focus();
    }
}

internal static class ThemedDialog
{
    public static bool Confirm(IWin32Window owner, string en, string fa, string message, string okText, bool danger = false)
    {
        using var d = new MessageDialog(en, fa, message, okText, "Cancel", danger, danger ? IconKind.Trash : IconKind.Check, 250);
        return d.ShowDialog(owner) == DialogResult.OK;
    }

    public static void Info(IWin32Window owner, string en, string fa, string message, int height = 250)
    {
        using var d = new MessageDialog(en, fa, message, "OK", null, false, IconKind.Check, height);
        d.ShowDialog(owner);
    }
}
