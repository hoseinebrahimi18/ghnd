using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OpenSSTP.Windows;

public sealed class MainForm : Form
{
    TextBox host = new() { PlaceholderText = "VPN Gate hostname or IP[:port]", Width = 280 };
    TextBox user = new() { PlaceholderText = "Username (VPN Gate: vpn)", Width = 280 };
    TextBox pass = new() { PlaceholderText = "Password (VPN Gate often blank)", Width = 280, UseSystemPasswordChar = true };
    ToggleSwitch rememberMe = new() { Margin = Padding.Empty };
    StatusPill state = new() { Text = "NOT CONNECTED", ForeColor = Pal.Idle };
    SpeedCell rx = new(IconKind.ArrowDown, Pal.Green, "DOWNLOAD");
    SpeedCell tx = new(IconKind.ArrowUp, Pal.AccentSoft, "UPLOAD");
    PowerOrb orb = new();
    Label heroHint = new() { AutoSize = false, Height = 22, ForeColor = Pal.Muted, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9) };
    Label note = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Pal.Muted, BackColor = Color.Transparent, TextAlign = ContentAlignment.TopLeft, Font = new Font("Segoe UI", 9), Margin = new Padding(2, 12, 2, 0) };
    TrafficChart chart = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    GradientButton connectButton = new() { Text = "Connect", Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
    SoftEtherClientEngine? engine;
    bool connected;
    bool connecting;
    bool polling;
    DateTime lastStatusPoll = DateTime.MinValue;
    string? lastAdapterId;
    long lastIn, lastOut;
    readonly NotifyIcon trayIcon = new();
    readonly ContextMenuStrip trayMenu = new();
    readonly ToolStripMenuItem trayStatusItem = new("Open-SSTP — Not connected") { Enabled = false };
    readonly ToolStripMenuItem trayConnectionItem = new("Connect VPN");
    readonly ToolStripMenuItem trayAutoStartItem = new("Start with Windows");
    Icon? trayIconIdle, trayIconBusy, trayIconOn;
    EventWaitHandle? showEvent;
    RegisteredWaitHandle? showEventWait;
    bool startHidden;
    bool exitRequested;
    bool exiting;
    bool trayReady;
    bool trayHintShown;

    const string AutoStartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AutoStartValue = "OpenSSTP";

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr handle);

    public MainForm(bool startInTray = false)
    {
        startHidden = startInTray;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = "Open-SSTP for Windows"; MinimumSize = new Size(940, 740); Size = new Size(1040, 800);
        BackColor = Pal.BG0; ForeColor = Pal.Text; Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;

        var root = new BufferedTable { Dock = DockStyle.Fill, Padding = new Padding(28, 18, 28, 24), ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        root.Controls.Add(new HeaderBar { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) }, 0, 0);

        var body = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        // ── Left column: hero orb, live speed, traffic graph ──
        var left = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 10, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 310)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 104)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(left, 0, 0);

        var hero = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), Fill = Pal.A(Pal.CardColor, 175), Radius = 26f };
        hero.Controls.Add(orb); hero.Controls.Add(state); hero.Controls.Add(heroHint);
        hero.Resize += (_, _) => LayoutHero(hero);
        state.SizeChanged += (_, _) => LayoutHero(hero);
        orb.Click += async (_, _) => await ToggleConnectionAsync();
        heroHint.Text = "Click the power button to connect";
        left.Controls.Add(hero, 0, 0);

        var speedCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(8, 10, 8, 10) };
        var speedRow = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1)); speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        speedRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rx.Dock = DockStyle.Fill; tx.Dock = DockStyle.Fill; rx.Margin = Padding.Empty; tx.Margin = Padding.Empty;
        speedRow.Controls.Add(rx, 0, 0);
        speedRow.Controls.Add(new Panel { BackColor = Pal.Stroke, Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 14) }, 1, 0);
        speedRow.Controls.Add(tx, 2, 0);
        speedCard.Controls.Add(speedRow);
        left.Controls.Add(speedCard, 0, 1);

        left.Controls.Add(chart, 0, 2);

        // ── Right column: connection form ──
        var formCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0), Padding = new Padding(24, 22, 24, 22), Radius = 26f };
        var form = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in new[] { 40, 84, 84, 84, 58, 66 }) form.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.Controls.Add(new SectionLabel("Connection details", "اطلاعات اتصال") { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        form.Controls.Add(MakeField("SERVER / HOST (optionally :port)", host, IconKind.Server), 0, 1);
        form.Controls.Add(MakeField("USERNAME", user, IconKind.User), 0, 2);
        form.Controls.Add(MakeField("PASSWORD", pass, IconKind.Lock), 0, 3);
        rememberMe.Dock = DockStyle.Fill; form.Controls.Add(rememberMe, 0, 4);
        connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        form.Controls.Add(connectButton, 0, 5);
        note.Text = "Built-in SoftEther engine. The first connection may ask for Administrator permission to register the engine service and VPN adapter. The graph measures VPN adapter traffic only.";
        form.Controls.Add(note, 0, 6);
        formCard.Controls.Add(form);
        body.Controls.Add(formCard, 1, 0);

        timer.Tick += async (_, _) => await OnTimerTickAsync(); timer.Start();
        LoadRememberedData();
        rememberMe.CheckedChanged += (_, _) => { if (!rememberMe.Checked) RememberMeStore.Clear(); };

        // Notification-area (system tray) icon. Windows may place it under the "^" / "Show hidden icons"
        // flyout; the user can drag it onto the taskbar to keep it always visible.
        BuildTrayIcons();
        trayConnectionItem.Click += async (_, _) => await ToggleConnectionAsync();
        trayAutoStartItem.Click += (_, _) => ToggleAutoStart();
        trayMenu.Items.Add(trayStatusItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(trayConnectionItem);
        trayMenu.Items.Add("Open Open-SSTP", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(trayAutoStartItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, async (_, _) => await ExitApplicationAsync());
        trayMenu.Opening += (_, _) => { RefreshTrayMenu(); trayAutoStartItem.Checked = IsAutoStartEnabled(); };
        trayConnectionItem.Font = new Font(trayConnectionItem.Font, FontStyle.Bold);
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Icon = trayIconIdle ?? SystemIcons.Application;
        trayIcon.Text = "Open-SSTP — Not connected";
        trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RestoreFromTray(); };
        trayIcon.BalloonTipClicked += (_, _) => RestoreFromTray();
        trayIcon.Visible = true;
        trayReady = true;
        RefreshTrayMenu();

        // A second launch of the exe (or Start-menu shortcut) brings this instance back from the tray.
        try
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
            showEventWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) =>
            {
                try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(RestoreFromTray)); } catch { }
            }, null, Timeout.Infinite, false);
        }
        catch { }

        if (startHidden) ShowInTaskbar = false;
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        FormClosing += (_, e) =>
        {
            SaveRememberedData();
            bool systemClosing = e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing or CloseReason.ApplicationExitCall;
            if (!exitRequested && !systemClosing && trayReady)
            {
                e.Cancel = true;
                HideToTray();
                if (!trayHintShown)
                {
                    trayHintShown = true;
                    trayIcon.ShowBalloonTip(3000, "Open-SSTP", "Still running in the system tray. Right-click the icon to connect, disconnect or exit.", ToolTipIcon.Info);
                }
            }
            else
            {
                timer.Stop();
                showEventWait?.Unregister(null);
                showEvent?.Dispose();
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayMenu.Dispose();
                trayIconIdle?.Dispose(); trayIconBusy?.Dispose(); trayIconOn?.Dispose();
            }
        };
    }

    // When started with --tray (e.g. from "Start with Windows") the window stays hidden until the tray icon is used.
    protected override void SetVisibleCore(bool value)
    {
        if (startHidden)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
        trayIcon.Visible = true;
        RefreshTrayMenu();
    }

    void RestoreFromTray()
    {
        startHidden = false;
        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        // Briefly toggling TopMost reliably brings the window forward even when Windows blocks plain Activate().
        TopMost = true; TopMost = false;
        Activate();
    }

    async Task ExitApplicationAsync()
    {
        if (exiting) return;
        exiting = true;
        try
        {
            // Leaving the app should not leave a VPN session running with no UI to stop it.
            if (connected && engine is not null)
            {
                trayIcon.Text = "Open-SSTP — Disconnecting…";
                try { await engine.DisconnectAsync(); } catch { }
                connected = false;
            }
        }
        finally
        {
            exitRequested = true;
            trayReady = false;
            Close();
        }
    }

    void BuildTrayIcons()
    {
        Icon baseIcon = Icon ?? SystemIcons.Application;
        trayIconIdle = MakeStatusIcon(baseIcon, null);
        trayIconBusy = MakeStatusIcon(baseIcon, Pal.Amber);
        trayIconOn = MakeStatusIcon(baseIcon, Pal.Green);
    }

    // Draws the app icon with a small coloured status dot (none = idle, amber = busy, green = connected).
    static Icon MakeStatusIcon(Icon baseIcon, Color? dot)
    {
        if (dot is null) return (Icon)baseIcon.Clone();
        try
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.DrawImage(baseIcon.ToBitmap(), new Rectangle(0, 0, size, size));
                var r = new Rectangle(size - 15, size - 15, 14, 14);
                using var ring = new SolidBrush(Color.White);
                using var fill = new SolidBrush(dot.Value);
                g.FillEllipse(ring, r);
                g.FillEllipse(fill, Rectangle.Inflate(r, -2, -2));
            }
            IntPtr h = bmp.GetHicon();
            try { return (Icon)System.Drawing.Icon.FromHandle(h).Clone(); }
            finally { DestroyIcon(h); }
        }
        catch { return (Icon)baseIcon.Clone(); }
    }

    static bool IsAutoStartEnabled()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(AutoStartKey); return k?.GetValue(AutoStartValue) is string v && v.Length > 0; }
        catch { return false; }
    }

    void ToggleAutoStart()
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(AutoStartKey);
            if (IsAutoStartEnabled()) k.DeleteValue(AutoStartValue, false);
            else k.SetValue(AutoStartValue, "\"" + Application.ExecutablePath + "\" --tray");
            trayAutoStartItem.Checked = IsAutoStartEnabled();
        }
        catch (Exception ex) { MessageBox.Show("Could not change the Start with Windows setting.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void RefreshTrayMenu()
    {
        orb.Mode = connected ? OrbMode.On : connecting ? OrbMode.Busy : OrbMode.Idle;
        heroHint.Text = connected ? "Tunnel active · click to disconnect" : connecting ? "Please wait…" : "Click the power button to connect";
        if (!trayReady) return;
        string status = connected ? "Connected" : connecting ? "Working…" : "Not connected";
        trayStatusItem.Text = "Open-SSTP — " + status;
        trayConnectionItem.Text = connected ? "Disconnect VPN" : connecting ? "Please wait…" : "Connect VPN";
        trayConnectionItem.Enabled = !connecting;
        trayIcon.Text = "Open-SSTP — " + status;
        trayIcon.Icon = connected ? (trayIconOn ?? trayIconIdle ?? SystemIcons.Application)
                      : connecting ? (trayIconBusy ?? trayIconIdle ?? SystemIcons.Application)
                      : (trayIconIdle ?? SystemIcons.Application);
    }

    void NotifyTray(string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        // Balloons are only useful when the window is out of sight.
        if (!Visible) trayIcon.ShowBalloonTip(2500, "Open-SSTP", message, icon);
    }

    async Task ToggleConnectionAsync()
    {
        if (connecting) return;
        if (connected)
        {
            connecting = true; connectButton.Enabled = false; state.Text = "DISCONNECTING..."; RefreshTrayMenu();
            try
            {
                engine ??= new SoftEtherClientEngine();
                await engine.DisconnectAsync();
                connected = false; ResetTraffic();
                state.Text = "NOT CONNECTED"; state.ForeColor = Pal.Idle;
                note.Text = "Disconnected. VPN traffic graph is paused.";
                NotifyTray("Disconnected");
            }
            catch (Exception ex) { MessageBox.Show(DescribeError(ex), "Disconnect failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { connecting = false; connectButton.Enabled = true; connectButton.Text = connected ? "Disconnect" : "Connect"; RefreshTrayMenu(); }
            return;
        }

        if (string.IsNullOrWhiteSpace(host.Text))
        {
            // Nothing to connect to yet (e.g. clicked "Connect" in the tray on first run): ask for the server.
            RestoreFromTray(); host.Focus();
            note.Text = "Enter a VPN Gate server first, then press Connect.";
            return;
        }
        connecting = true; connectButton.Enabled = false; state.Text = "CONNECTING..."; state.ForeColor = Pal.Amber;
        RefreshTrayMenu();
        note.Text = "Preparing the bundled SoftEther engine, then connecting to hub VPNGATE...";
        try
        {
            engine ??= new SoftEtherClientEngine();
            await engine.ConnectAsync(host.Text, user.Text, pass.Text);
            lastStatusPoll = DateTime.MinValue;
            note.Text = "Connection request sent. Waiting for SoftEther to confirm the session...";
            await PollConnectionStateAsync();
        }
        catch (Exception ex)
        {
            connected = false; ResetTraffic(); state.Text = "CONNECTION FAILED"; state.ForeColor = Pal.Red;
            note.Text = "SoftEther connection failed. See the error message.";
            NotifyTray("Connection failed", ToolTipIcon.Error);
            MessageBox.Show(DescribeError(ex), "VPN connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { connecting = false; connectButton.Enabled = true; connectButton.Text = connected ? "Disconnect" : "Connect"; RefreshTrayMenu(); }
    }

    async Task OnTimerTickAsync()
    {
        if (IsDisposed || connecting) return;
        if (DateTime.UtcNow - lastStatusPoll >= TimeSpan.FromSeconds(4)) await PollConnectionStateAsync();
        UpdateVpnTraffic();
    }

    async Task PollConnectionStateAsync()
    {
        if (polling || engine is null) return;
        polling = true; lastStatusPoll = DateTime.UtcNow;
        try
        {
            bool nowConnected = await engine.IsConnectedAsync();
            if (nowConnected != connected)
            {
                connected = nowConnected;
                if (connected)
                {
                    state.Text = "CONNECTED"; state.ForeColor = Pal.Green;
                    note.Text = "Connected through SoftEther. Speed graph reads counters from the VPN adapter.";
                    connectButton.Text = "Disconnect";
                    ResetTraffic();
                    NotifyTray("Connected");
                }
                else
                {
                    state.Text = "NOT CONNECTED"; state.ForeColor = Pal.Idle;
                    note.Text = "SoftEther reports no active session. VPN speed is shown as zero.";
                    connectButton.Text = "Connect";
                    ResetTraffic();
                }
            }
        }
        catch
        {
            // A temporary status-query failure must not fabricate a connected state or traffic.
            connected = false; state.Text = "STATUS UNAVAILABLE"; state.ForeColor = Pal.Idle;
            connectButton.Text = "Connect"; ResetTraffic();
        }
        finally { polling = false; RefreshTrayMenu(); }
    }

    void UpdateVpnTraffic()
    {
        if (!connected) { rx.Text = "0.0 KB/s"; tx.Text = "0.0 KB/s"; chart.Caption = "Live traffic · idle"; chart.Add(0, 0); return; }
        var nic = SoftEtherClientEngine.FindVpnInterface();
        if (nic is null)
        {
            rx.Text = "0.0 KB/s"; tx.Text = "0.0 KB/s"; chart.Caption = "Live traffic · adapter counters unavailable"; chart.Add(0, 0); return;
        }
        try
        {
            var stats = nic.GetIPStatistics();
            if (!string.Equals(lastAdapterId, nic.Id, StringComparison.Ordinal))
            {
                lastAdapterId = nic.Id; lastIn = stats.BytesReceived; lastOut = stats.BytesSent;
                rx.Text = "0.0 KB/s"; tx.Text = "0.0 KB/s"; chart.Add(0, 0); return;
            }
            long inNow = stats.BytesReceived, outNow = stats.BytesSent;
            long down = Math.Max(0, inNow - lastIn), up = Math.Max(0, outNow - lastOut);
            lastIn = inNow; lastOut = outNow;
            rx.Text = Fmt(down); tx.Text = Fmt(up); chart.Caption = "Live traffic · VPN adapter"; chart.Add(down, up);
        }
        catch { rx.Text = "0.0 KB/s"; tx.Text = "0.0 KB/s"; chart.Add(0, 0); }
    }

    void ResetTraffic()
    {
        lastAdapterId = null; lastIn = 0; lastOut = 0; rx.Text = "0.0 KB/s"; tx.Text = "0.0 KB/s"; chart.ClearSamples();
    }

    void LoadRememberedData()
    {
        try
        {
            var saved = RememberMeStore.Load();
            if (saved is null) return;
            host.Text = saved.Host; user.Text = saved.UserName; pass.Text = saved.Password; rememberMe.Checked = true;
        }
        catch (Exception ex) { MessageBox.Show("Saved login information could not be read. You can enter it again.\n\n" + ex.Message, "Remember me", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void SaveRememberedData()
    {
        try { if (rememberMe.Checked) RememberMeStore.Save(host.Text.Trim(), user.Text, pass.Text); else RememberMeStore.Clear(); }
        catch (Exception ex) { MessageBox.Show("Could not save the login information securely. The password was not saved.\n\n" + ex.Message, "Remember me", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    static Control MakeField(string caption, TextBox box, IconKind icon) => new FieldBox(caption, icon, box) { Dock = DockStyle.Fill, Margin = Padding.Empty };

    void LayoutHero(Control hero)
    {
        int w = hero.ClientSize.Width, h = hero.ClientSize.Height;
        if (w <= 0 || h <= 0) return;
        int size = Math.Max(120, Math.Min(220, h - 98));
        orb.Size = new Size(size, size);
        orb.Location = new Point((w - size) / 2, 12);
        state.Location = new Point((w - state.Width) / 2, orb.Bottom + 4);
        heroHint.SetBounds(16, state.Bottom + 8, Math.Max(40, w - 32), 22);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics; var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using (var br = new LinearGradientBrush(r, Pal.BG0, Pal.BG1, 90f)) g.FillRectangle(br, r);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // faint accent glows in the corners
        using (var gp = new GraphicsPath())
        {
            float rad = Math.Max(r.Width, r.Height) * 0.55f;
            gp.AddEllipse(r.Right - rad * 0.8f, r.Top - rad * 0.7f, rad * 1.6f, rad * 1.4f);
            using var pb = new PathGradientBrush(gp) { CenterColor = Pal.A(Pal.Accent2, 34), SurroundColors = new[] { Pal.A(Pal.Accent2, 0) } };
            g.FillPath(pb, gp);
        }
        using (var gp = new GraphicsPath())
        {
            float rad = Math.Max(r.Width, r.Height) * 0.5f;
            gp.AddEllipse(r.Left - rad * 0.8f, r.Bottom - rad * 0.7f, rad * 1.6f, rad * 1.4f);
            using var pb = new PathGradientBrush(gp) { CenterColor = Pal.A(Pal.Accent, 26), SurroundColors = new[] { Pal.A(Pal.Accent, 0) } };
            g.FillPath(pb, gp);
        }
    }

    // Builds a full, never-empty error text (type + message + inner errors + location) and saves it to
    // %LocalAppData%\OpenSSTP\last-error.log so it can be copied or sent for diagnosis.
    static string DescribeError(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            sb.AppendLine(e.GetType().FullName + (string.IsNullOrWhiteSpace(e.Message) ? " (no message)" : ": " + e.Message.Trim()));
            if (e is System.ComponentModel.Win32Exception w) sb.AppendLine("Win32 error code: " + w.NativeErrorCode);
        }
        var stack = (ex.StackTrace ?? string.Empty).Split('\n').Take(6);
        sb.AppendLine().AppendLine("Where:").AppendLine(string.Join("\n", stack).Trim());
        var text = sb.ToString().Trim();
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSSTP");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "last-error.log"), DateTime.Now.ToString("u") + Environment.NewLine + text);
            text += "\n\n(Saved to: " + Path.Combine(dir, "last-error.log") + ")";
        }
        catch { }
        return text;
    }

    static string Fmt(long bytesPerSecond) => bytesPerSecond >= 1024 * 1024 ? $"{bytesPerSecond / 1024d / 1024:0.00} MB/s" : $"{bytesPerSecond / 1024d:0.0} KB/s";
}
