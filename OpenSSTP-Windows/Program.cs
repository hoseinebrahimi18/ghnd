using System;
using System.Threading;
using System.Windows.Forms;

namespace OpenSSTP.Windows;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // This maintenance command must be allowed to run without starting the UI.
        if (args.Any(a => string.Equals(a, "--install-softether-runtime", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                SoftEtherRuntimeBootstrapper.InstallFromBundle();
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("OpenSSTP could not prepare its built-in VPN engine.\n\n" + ex.Message,
                    "OpenSSTP engine setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
            return;
        }

        // Only one UI process may run, preventing duplicate notification-area icons.
        using var singleInstanceMutex = new Mutex(true, @"Local\OpenSSTP.Windows.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Open-SSTP is already running. Look for its icon near the Windows clock (Show hidden icons).",
                "Open-SSTP is already open", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
