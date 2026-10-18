using System;
using System.Windows.Forms;
namespace OpenSSTP.Windows;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
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
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
