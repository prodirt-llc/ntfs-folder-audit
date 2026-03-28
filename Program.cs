using System.Security.Principal;

namespace NTFSReport;

static class Program
{
    [STAThread]
    static async Task<int> Main(string[] args)
    {
        // CLI mode
        if (args.Length > 0 &&
            (args.Contains("--path", StringComparer.OrdinalIgnoreCase) ||
             args.Contains("-p",     StringComparer.OrdinalIgnoreCase) ||
             args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
             args.Contains("-h",     StringComparer.OrdinalIgnoreCase)))
        {
            return await CliRunner.RunAsync(args);
        }

        // GUI mode
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Prompt for elevation if not already admin
        if (!IsRunningAsAdmin())
        {
            var result = MessageBox.Show(
                "NTFS Permissions Reporter works best with Administrator privileges.\n\n" +
                "Without elevation, access-denied errors will appear on restricted folders " +
                "such as System Volume Information, $Recycle.Bin, and other protected paths.\n\n" +
                "Relaunch as Administrator now?",
                "Administrator Privileges Recommended",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    var exe = Environment.ProcessPath
                        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        Verb            = "runas"
                    });
                }
                catch { /* User cancelled UAC */ }
                return 0;
            }
            // User said No — continue without elevation
        }

        Application.Run(new MainForm());
        return 0;
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
