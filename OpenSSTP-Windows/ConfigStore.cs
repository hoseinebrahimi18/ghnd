using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace OpenSSTP.Windows;

/// <summary>One saved connection (shown as a card in the side menu).</summary>
internal sealed class VpnProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string UserName { get; set; } = "";
    /// <summary>True when a password for this config is stored in Windows Credential Manager.</summary>
    public bool PasswordSaved { get; set; }
    public int ColorIndex { get; set; }
    public bool Favorite { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? LastUsed { get; set; }
}

/// <summary>Preferences controlled from the side menu.</summary>
internal sealed class AppSettings
{
    public bool AutoConnectOnLaunch { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool RestoreLastConfig { get; set; } = true;
    public string? LastProfileId { get; set; }
}

/// <summary>Shape of configs.json and of exported files.</summary>
internal sealed class ConfigFile
{
    public string Format { get; set; } = "OpenSSTP-configs";
    public int Version { get; set; } = 1;
    public List<VpnProfile> Profiles { get; set; } = new();
}

/// <summary>
/// Saved configs and app settings. Non-secret data lives in JSON files under %LocalAppData%\OpenSSTP.Windows;
/// passwords never touch the JSON, they go to the current user's Windows Credential Manager.
/// </summary>
internal static class ConfigStore
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenSSTP.Windows");
    static string ProfilesPath => Path.Combine(Dir, "configs.json");
    static string SettingsPath => Path.Combine(Dir, "settings.json");
    const string CredPrefix = "OpenSSTP.Windows.Config.";

    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static List<VpnProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesPath)) return new List<VpnProfile>();
            var file = JsonSerializer.Deserialize<ConfigFile>(File.ReadAllText(ProfilesPath, Encoding.UTF8), Opts);
            return file?.Profiles ?? new List<VpnProfile>();
        }
        catch
        {
            // Keep the unreadable file around instead of silently overwriting it on the next save.
            try { File.Copy(ProfilesPath, ProfilesPath + ".bak", true); } catch { }
            return new List<VpnProfile>();
        }
    }

    public static void SaveProfiles(IEnumerable<VpnProfile> profiles)
    {
        var file = new ConfigFile();
        file.Profiles.AddRange(profiles);
        WriteAtomic(ProfilesPath, JsonSerializer.Serialize(file, Opts));
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8), Opts) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings) =>
        WriteAtomic(SettingsPath, JsonSerializer.Serialize(settings, Opts));

    // ── passwords ──
    public static string? ReadPassword(VpnProfile p) => CredentialVault.Read(CredPrefix + p.Id);
    public static void WritePassword(VpnProfile p, string password) => CredentialVault.Write(CredPrefix + p.Id, p.UserName ?? "", password);
    public static void DeletePassword(VpnProfile p) => CredentialVault.Delete(CredPrefix + p.Id);

    // ── backup / restore ──
    /// <summary>Writes the configs to a file the user can keep or move to another PC. Passwords are never exported.</summary>
    public static void Export(string path, IEnumerable<VpnProfile> profiles)
    {
        var file = new ConfigFile();
        foreach (var p in profiles)
        {
            file.Profiles.Add(new VpnProfile
            {
                Id = p.Id, Name = p.Name, Host = p.Host, UserName = p.UserName, PasswordSaved = false,
                ColorIndex = p.ColorIndex, Favorite = p.Favorite, Created = p.Created, LastUsed = p.LastUsed
            });
        }
        File.WriteAllText(path, JsonSerializer.Serialize(file, Opts), new UTF8Encoding(false));
    }

    /// <summary>Adds configs from a file into <paramref name="target"/>; entries with the same server + user are skipped. Returns how many were added.</summary>
    public static int Import(string path, List<VpnProfile> target)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        List<VpnProfile>? incoming;
        using (var doc = JsonDocument.Parse(text))
        {
            incoming = doc.RootElement.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<VpnProfile>>(text, Opts)
                : JsonSerializer.Deserialize<ConfigFile>(text, Opts)?.Profiles;
        }
        if (incoming is null) throw new InvalidDataException("This file does not contain any OpenSSTP configs.");

        int added = 0;
        foreach (var p in incoming)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.Host)) continue;
            string h = p.Host.Trim(), u = (p.UserName ?? "").Trim();
            bool duplicate = target.Any(t =>
                string.Equals((t.Host ?? "").Trim(), h, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((t.UserName ?? "").Trim(), u, StringComparison.OrdinalIgnoreCase));
            if (duplicate) continue;
            p.Id = Guid.NewGuid().ToString("N");
            p.Host = h; p.UserName = u;
            p.PasswordSaved = false;
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = h;
            target.Add(p);
            added++;
        }
        return added;
    }

    static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }
}

/// <summary>Thin wrapper around the Windows Credential Manager (generic credentials, current user).</summary>
internal static class CredentialVault
{
    public static string? Read(string target)
    {
        if (!CredRead(target, 1, 0, out var pointer)) return null;
        try
        {
            var c = Marshal.PtrToStructure<ReadCredential>(pointer);
            if (c.CredentialBlobSize == 0 || c.CredentialBlob == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUni(c.CredentialBlob, checked((int)(c.CredentialBlobSize / 2)));
        }
        finally { CredFree(pointer); }
    }

    public static void Write(string target, string userName, string password)
    {
        password ??= string.Empty;
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new NativeCredential
            {
                Type = 1, // CRED_TYPE_GENERIC
                TargetName = target,
                CredentialBlobSize = checked((uint)(password.Length * 2)),
                CredentialBlob = blob,
                Persist = 2, // CRED_PERSIST_LOCAL_MACHINE (stays inside this Windows user profile)
                UserName = string.IsNullOrEmpty(userName) ? "OpenSSTP" : userName
            };
            if (!CredWrite(ref credential, 0))
                throw new InvalidOperationException("Windows Credential Manager rejected the saved password (error " + Marshal.GetLastWin32Error() + ").");
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }

    public static void Delete(string target)
    {
        try { CredDelete(target, 1, 0); } catch { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReadCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
}
