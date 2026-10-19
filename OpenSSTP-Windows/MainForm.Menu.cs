using System.Diagnostics;

namespace OpenSSTP.Windows;

// Hamburger menu: slide-in side panel with saved configs, import/export, preferences and app actions.
public sealed partial class MainForm
{
    readonly List<VpnProfile> profiles = new();
    AppSettings settings = new();
    string? activeProfileId;

    HeaderBar header = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
    SideDrawer? drawer;
    Scrim? scrim;
    readonly System.Windows.Forms.Timer drawerTimer = new() { Interval = 15 };
    bool drawerOpen;
    float drawerT;
    bool autoConnectDone;
    readonly ToolStripMenuItem trayProfilesItem = new("Saved configs");

    // ───────────────────────── setup ─────────────────────────

    void InitMenu()
    {
        settings = ConfigStore.LoadSettings();
        profiles.Clear();
        profiles.AddRange(ConfigStore.LoadProfiles());

        var d = new SideDrawer { Visible = false };
        var s = new Scrim { Visible = false };
        drawer = d; scrim = s;
        d.CloseRequested += (_, _) => CloseDrawer();
        s.Click += (_, _) => CloseDrawer();
        Controls.Add(s);
        Controls.Add(d);

        header.Burger.Click += (_, _) => { if (drawerOpen) CloseDrawer(); else OpenDrawer(); };
        drawerTimer.Tick += (_, _) => DrawerTick();
        Resize += (_, _) =>
        {
            if (drawerOpen) CloseDrawerInstant();
            if (drawer is not null) drawer.Height = ClientSize.Height;
        };

        // Tray icon: quick-connect submenu with the saved configs.
        int at = trayMenu.Items.IndexOf(trayConnectionItem);
        trayMenu.Items.Insert(at < 0 ? 0 : at + 1, trayProfilesItem);
        trayMenu.Opening += (_, _) => RefreshTrayProfiles();

        RestoreLastProfile();

        // Optional: connect automatically when the app starts (also when it starts hidden in the tray).
        void ScheduleAutoConnect()
        {
            if (autoConnectDone) return;
            autoConnectDone = true;
            if (!settings.AutoConnectOnLaunch) return;
            BeginInvoke(new Action(async () =>
            {
                await Task.Delay(1500);
                if (!IsDisposed && !connected && !connecting && !string.IsNullOrWhiteSpace(host.Text))
                    await ToggleConnectionAsync();
            }));
        }
        if (IsHandleCreated) ScheduleAutoConnect(); else HandleCreated += (_, _) => ScheduleAutoConnect();
    }

    void RestoreLastProfile()
    {
        var last = profiles.FirstOrDefault(x => x.Id == settings.LastProfileId);
        if (last is null) return;
        bool same = string.Equals(host.Text.Trim(), (last.Host ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                 && string.Equals(user.Text.Trim(), (last.UserName ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        if (same) { activeProfileId = last.Id; header.ProfileName = last.Name; return; }
        if (settings.RestoreLastConfig && string.IsNullOrWhiteSpace(host.Text)) UseProfile(last, false);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && drawerOpen) { CloseDrawer(); return true; }
        if (keyData == (Keys.Control | Keys.B)) { if (drawerOpen) CloseDrawer(); else OpenDrawer(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ───────────────────────── drawer open / close ─────────────────────────

    Bitmap? CaptureClient()
    {
        try
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return null;
            var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, ClientSize);
            return bmp;
        }
        catch { return null; }
    }

    void OpenDrawer()
    {
        if (drawerOpen || drawer is null || scrim is null) return;
        if (!Visible || WindowState == FormWindowState.Minimized) return;

        RebuildDrawer();
        drawer.ResetScroll();

        var shot = CaptureClient();
        scrim.SetSnapshot(shot);
        scrim.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
        scrim.Alpha = 0f;
        scrim.Visible = shot is not null; // without a snapshot there is nothing to dim: only the panel slides in

        drawerT = 0f;
        drawer.SetBounds(-SideDrawer.DrawerWidth, 0, SideDrawer.DrawerWidth, ClientSize.Height);
        drawer.Visible = true;
        scrim.BringToFront();
        drawer.BringToFront();

        drawerOpen = true;
        drawerTimer.Start();
        drawer.Focus();
    }

    void CloseDrawer()
    {
        if (!drawerOpen) return;
        drawerOpen = false;
        drawerTimer.Start();
    }

    void CloseDrawerInstant()
    {
        drawerTimer.Stop();
        drawerOpen = false;
        drawerT = 0f;
        FinishClose();
    }

    void FinishClose()
    {
        if (drawer is not null) drawer.Visible = false;
        if (scrim is not null) { scrim.Visible = false; scrim.SetSnapshot(null); }
    }

    void DrawerTick()
    {
        if (drawer is null || scrim is null) { drawerTimer.Stop(); return; }
        float target = drawerOpen ? 1f : 0f;
        drawerT += (target - drawerT) * 0.30f;
        if (Math.Abs(target - drawerT) < 0.01f) { drawerT = target; drawerTimer.Stop(); }
        drawer.Left = (int)Math.Round(-SideDrawer.DrawerWidth * (1f - drawerT));
        scrim.Alpha = drawerT;
        if (!drawerOpen && drawerT <= 0f) FinishClose();
    }

    // ───────────────────────── building the menu ─────────────────────────

    void RebuildDrawer()
    {
        if (drawer is null) return;
        var items = new List<Control>();

        items.Add(new DrawerSection("SAVED CONFIGS", "کانفیگ‌های ذخیره‌شده"));
        if (profiles.Count == 0)
            items.Add(new DrawerEmpty("No saved configs yet", "هنوز کانفیگی ذخیره نشده"));

        foreach (var p in profiles
                     .OrderByDescending(x => x.Favorite)
                     .ThenByDescending(x => x.LastUsed ?? x.Created)
                     .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var prof = p;
            var row = new ConfigRow(prof) { Active = prof.Id == activeProfileId };
            row.UseClicked += (_, _) => UseProfile(prof, true);
            row.ConnectClicked += (_, _) => { CloseDrawer(); _ = ConnectProfileAsync(prof); };
            row.EditClicked += (_, _) => BeginInvoke(new Action(() => EditProfile(prof)));
            row.DeleteClicked += (_, _) => BeginInvoke(new Action(() => DeleteProfile(prof)));
            row.FavoriteClicked += (_, _) =>
            {
                prof.Favorite = !prof.Favorite;
                PersistProfiles();
                BeginInvoke(new Action(RebuildDrawer));
            };
            items.Add(row);
        }

        var save = new GradientButton { Text = "Save current as config", Glyph = IconKind.Save, Danger = false, Height = 50, Margin = new Padding(0, 6, 0, 2) };
        save.Click += (_, _) => BeginInvoke(new Action(SaveCurrentAsProfile));
        items.Add(save);

        items.Add(new DrawerSection("BACKUP", "پشتیبان‌گیری"));
        items.Add(MenuRow(IconKind.Import, "Import configs", "وارد کردن کانفیگ‌ها از فایل", Pal.Green, ImportConfigs));
        items.Add(MenuRow(IconKind.Export, "Export configs", "ذخیره کانفیگ‌ها در یک فایل", Pal.AccentSoft, ExportConfigs, profiles.Count.ToString()));

        items.Add(new DrawerSection("PREFERENCES", "تنظیمات"));
        var tStart = Toggle("Start with Windows", "اجرا همراه ویندوز", IsAutoStartEnabled());
        tStart.CheckedChanged += (_, _) =>
        {
            if (IsAutoStartEnabled() != tStart.Checked) ToggleAutoStart();
            if (tStart.Checked != IsAutoStartEnabled()) tStart.Checked = IsAutoStartEnabled();
        };
        items.Add(tStart);

        var tAuto = Toggle("Connect on launch", "اتصال خودکار هنگام اجرا", settings.AutoConnectOnLaunch);
        tAuto.CheckedChanged += (_, _) => { settings.AutoConnectOnLaunch = tAuto.Checked; SaveSettings(); };
        items.Add(tAuto);

        var tRestore = Toggle("Restore last config", "بازیابی آخرین کانفیگ هنگام اجرا", settings.RestoreLastConfig);
        tRestore.CheckedChanged += (_, _) => { settings.RestoreLastConfig = tRestore.Checked; SaveSettings(); };
        items.Add(tRestore);

        var tTray = Toggle("Close to tray", "بستن پنجره به سینی سیستم", settings.CloseToTray);
        tTray.CheckedChanged += (_, _) => { settings.CloseToTray = tTray.Checked; SaveSettings(); };
        items.Add(tTray);

        var tNotify = Toggle("Notifications", "اعلان‌های سینی سیستم", settings.Notifications);
        tNotify.CheckedChanged += (_, _) => { settings.Notifications = tNotify.Checked; SaveSettings(); };
        items.Add(tNotify);

        items.Add(new DrawerSection("MORE", "بیشتر"));
        items.Add(MenuRow(IconKind.Folder, "Open data folder", "پوشه ذخیره کانفیگ‌ها و تنظیمات", Pal.Amber, OpenDataFolder));
        items.Add(MenuRow(IconKind.Doc, "View last error log", "آخرین گزارش خطا", Pal.Cyan, ViewErrorLog));
        items.Add(MenuRow(IconKind.Info, "About", "درباره برنامه", Pal.AccentSoft, ShowAbout));
        items.Add(MenuRow(IconKind.Exit, "Exit Open-SSTP", "قطع اتصال و خروج کامل", Pal.Red, () => { CloseDrawer(); _ = ExitApplicationAsync(); }, "", false));

        drawer.SetItems(items);
    }

    static DrawerMenuRow MenuRow(IconKind icon, string title, string fa, Color tint, Action action, string badge = "", bool chevron = true)
    {
        var r = new DrawerMenuRow { Icon = icon, Title = title, Fa = fa, Tint = tint, Badge = badge, Chevron = chevron && badge.Length == 0 };
        // Run after the click has finished so a rebuilt menu never disposes the row that is still handling the event.
        r.Click += (_, _) => r.BeginInvoke(action);
        return r;
    }

    static ToggleSwitch Toggle(string en, string fa, bool value)
    {
        var t = new ToggleSwitch { Title = en, Subtitle = fa, Height = 58, Margin = new Padding(8, 0, 4, 2) };
        t.Checked = value;
        return t;
    }

    void SaveSettings()
    {
        try { ConfigStore.SaveSettings(settings); }
        catch (Exception ex) { MessageBox.Show("Could not save the preferences.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ───────────────────────── saved configs ─────────────────────────

    void PersistProfiles(bool quiet = false)
    {
        try { ConfigStore.SaveProfiles(profiles); }
        catch (Exception ex)
        {
            if (!quiet) MessageBox.Show("Could not save your configs.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void SetActiveProfile(VpnProfile? p)
    {
        activeProfileId = p?.Id;
        settings.LastProfileId = p?.Id;
        SaveSettings();
        header.ProfileName = p?.Name;
    }

    /// <summary>Copies a saved config into the connection form.</summary>
    void UseProfile(VpnProfile p, bool closeAfter)
    {
        host.Text = p.Host ?? "";
        user.Text = p.UserName ?? "";
        string pw = "";
        if (p.PasswordSaved)
        {
            try { pw = ConfigStore.ReadPassword(p) ?? ""; } catch { }
        }
        pass.Text = pw;
        SetActiveProfile(p);

        if (connected) note.Text = "Config '" + p.Name + "' loaded. It will be used the next time you connect.";
        else if (p.PasswordSaved) note.Text = "Config '" + p.Name + "' loaded. Press Connect.";
        else note.Text = "Config '" + p.Name + "' loaded. Enter the password if the server needs one, then press Connect.";

        if (closeAfter) CloseDrawer();
    }

    async Task ConnectProfileAsync(VpnProfile p)
    {
        if (connecting) return;
        if (connected)
        {
            await ToggleConnectionAsync(); // disconnect the current session first
            if (connected) return;
        }
        UseProfile(p, false);
        await ToggleConnectionAsync();
    }

    /// <summary>Remember when a config was last used (called after a connection request succeeds).</summary>
    void TouchActiveProfile()
    {
        var p = profiles.FirstOrDefault(x => x.Id == activeProfileId);
        if (p is null) return;
        p.LastUsed = DateTime.Now;
        PersistProfiles(true);
    }

    void ApplyDialog(VpnProfile p, ConfigDialog d)
    {
        p.Name = d.ConfigName;
        p.Host = d.Host;
        p.UserName = d.Login;
        try
        {
            if (d.SavePassword)
            {
                if (d.Password.Length > 0) { ConfigStore.WritePassword(p, d.Password); p.PasswordSaved = true; }
            }
            else
            {
                ConfigStore.DeletePassword(p);
                p.PasswordSaved = false;
            }
        }
        catch (Exception ex)
        {
            p.PasswordSaved = false;
            MessageBox.Show("The config was saved, but the password could not be stored securely.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void SaveCurrentAsProfile()
    {
        var active = profiles.FirstOrDefault(x => x.Id == activeProfileId);
        string suggested = active?.Name ?? host.Text.Trim();
        using var dlg = new ConfigDialog("Save config", "ذخیره کانفیگ", "Save", suggested, host.Text.Trim(), user.Text, pass.Text, true, false);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        // Saving under an existing name updates that config instead of creating a duplicate.
        var target = profiles.FirstOrDefault(x => string.Equals(x.Name, dlg.ConfigName, StringComparison.CurrentCultureIgnoreCase));
        bool isNew = target is null;
        target ??= new VpnProfile { ColorIndex = Random.Shared.Next(Pal.Avatars.Length) };
        ApplyDialog(target, dlg);
        if (isNew) profiles.Add(target);
        PersistProfiles();
        SetActiveProfile(target);
        note.Text = (isNew ? "Saved new config '" : "Updated config '") + target.Name + "'.";
        RebuildDrawer();
    }

    void EditProfile(VpnProfile p)
    {
        using var dlg = new ConfigDialog("Edit config", "ویرایش کانفیگ", "Save changes", p.Name, p.Host, p.UserName, "", p.PasswordSaved, p.PasswordSaved);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        ApplyDialog(p, dlg);
        PersistProfiles();
        if (p.Id == activeProfileId)
        {
            header.ProfileName = p.Name;
            if (!connected)
            {
                host.Text = p.Host; user.Text = p.UserName;
                if (dlg.Password.Length > 0) pass.Text = dlg.Password;
            }
        }
        RebuildDrawer();
    }

    void DeleteProfile(VpnProfile p)
    {
        if (!ThemedDialog.Confirm(this, "Delete config", "حذف کانفیگ",
                "Delete '" + p.Name + "'?\n\nThe password saved for this config is removed from Windows as well.", "Delete", true)) return;
        try { ConfigStore.DeletePassword(p); } catch { }
        profiles.Remove(p);
        if (p.Id == activeProfileId) SetActiveProfile(null);
        PersistProfiles();
        RebuildDrawer();
    }

    void ExportConfigs()
    {
        if (profiles.Count == 0)
        {
            ThemedDialog.Info(this, "Export configs", "خروجی کانفیگ‌ها", "There are no saved configs to export yet.\n\nSave one first with 'Save current as config'.");
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Title = "Export OpenSSTP configs",
            Filter = "OpenSSTP configs (*.json)|*.json",
            FileName = "OpenSSTP-configs.json",
            DefaultExt = "json",
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ConfigStore.Export(dlg.FileName, profiles);
            ThemedDialog.Info(this, "Export configs", "خروجی کانفیگ‌ها",
                "Exported " + profiles.Count + " config(s).\n\nPasswords are never written to the file. After importing on another PC, enter them once and save again.", 280);
        }
        catch (Exception ex) { MessageBox.Show("Could not export the configs.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void ImportConfigs()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import OpenSSTP configs",
            Filter = "OpenSSTP configs (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int added = ConfigStore.Import(dlg.FileName, profiles);
            if (added > 0) PersistProfiles();
            RebuildDrawer();
            ThemedDialog.Info(this, "Import configs", "وارد کردن کانفیگ‌ها",
                added > 0 ? "Imported " + added + " new config(s)." : "Nothing new to import. Configs that are already saved (same server and user) are skipped.");
        }
        catch (Exception ex) { MessageBox.Show("Could not import this file.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ───────────────────────── more ─────────────────────────

    void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.Dir);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ConfigStore.Dir + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show("Could not open the folder.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void ViewErrorLog()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSSTP", "last-error.log");
        if (!File.Exists(path))
        {
            ThemedDialog.Info(this, "Error log", "گزارش خطا", "No error has been logged yet. That's good news.");
            return;
        }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Could not open the log.\n\n" + ex.Message, "Open-SSTP", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void ShowAbout()
    {
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        ThemedDialog.Info(this, "About Open-SSTP", "درباره برنامه",
            "Open-SSTP for Windows  ·  v" + version + "\n\nA SoftEther-based client for VPN Gate servers.\n\nSaved configs are stored in:\n" + ConfigStore.Dir +
            "\n\nShortcuts:  Ctrl+B opens the menu,  Esc closes it.", 330);
    }

    void RefreshTrayProfiles()
    {
        trayProfilesItem.DropDownItems.Clear();
        if (profiles.Count == 0)
        {
            trayProfilesItem.DropDownItems.Add(new ToolStripMenuItem("(no saved configs)") { Enabled = false });
            return;
        }
        foreach (var p in profiles.OrderByDescending(x => x.Favorite).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var prof = p;
            var item = new ToolStripMenuItem((prof.Favorite ? "★ " : "") + prof.Name) { Checked = prof.Id == activeProfileId };
            item.Click += async (_, _) => await ConnectProfileAsync(prof);
            trayProfilesItem.DropDownItems.Add(item);
        }
        trayProfilesItem.Enabled = !connecting;
    }
}
