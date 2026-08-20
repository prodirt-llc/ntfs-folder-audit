using System.Runtime.InteropServices;

namespace NTFSReport;

/// <summary>
/// Headless CLI mode. Used when --path argument is present.
/// </summary>
public static class CliRunner
{
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

    public static void EnsureConsole()
    {
        // Try to attach to parent process console (e.g. cmd.exe / PowerShell)
        if (!AttachConsole(-1))
            AllocConsole();

        // Redirect standard streams after attaching
        var writer = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(writer);
        Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    public static async Task<int> RunAsync(string[] args)
    {
        EnsureConsole();

        WriteBanner();

        var opts = ParseArgs(args);
        if (opts == null) return 1;

        if (!Directory.Exists(opts.ScanOpts.RootPath))
        {
            WriteError($"Path not found: {opts.ScanOpts.RootPath}");
            return 1;
        }

        Console.WriteLine($"Path    : {opts.ScanOpts.RootPath}");
        Console.WriteLine($"Depth   : {opts.ScanOpts.MaxDepth}");
        Console.WriteLine($"Ex-Sys  : {opts.ScanOpts.ExcludeSystemFolders}");
        Console.WriteLine($"Output  : {opts.HtmlOutput}");
        if (!string.IsNullOrEmpty(opts.CsvOutput))
            Console.WriteLine($"CSV     : {opts.CsvOutput}");
        Console.WriteLine();

        var scanner = new PermissionScanner();
        int lastCount = 0;

        var progress = new Progress<ScanProgress>(p =>
        {
            lastCount = p.FolderCount;
            var msg = $"\rScanning: {p.FolderCount,6:N0} folders | {p.ErrorCount} errors | {TruncPath(p.CurrentPath, 60)}";
            Console.Write(msg.PadRight(120));
        });

        ScanResult result;
        try
        {
            result = await scanner.ScanAsync(opts.ScanOpts, progress);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            WriteError($"Scan failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ Scan complete in {result.Elapsed.TotalSeconds:F1}s");
        Console.ResetColor();
        Console.WriteLine($"  Folders       : {result.TotalFolders:N0}");
        Console.WriteLine($"  Permissions   : {result.TotalPermissions:N0}");
        Console.WriteLine($"  Access errors : {result.TotalErrors}");
        Console.WriteLine($"  Broken inheri.: {result.BrokenInheritanceCount}");
        Console.WriteLine();

        // Resolve output path
        var htmlPath = ResolveOutput(opts.HtmlOutput, result.Options.RootPath, ".html");

        Console.Write("Generating HTML report...");
        try
        {
            var html = HtmlGenerator.BuildReport(result);
            File.WriteAllText(htmlPath, html, System.Text.Encoding.UTF8);
            var kb = new FileInfo(htmlPath).Length / 1024;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" saved ({kb:N0} KB)");
            Console.ResetColor();
            Console.WriteLine($"  → {htmlPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            WriteError($"HTML write failed: {ex.Message}");
            return 1;
        }

        if (!string.IsNullOrEmpty(opts.CsvOutput))
        {
            var csvPath = ResolveOutput(opts.CsvOutput, result.Options.RootPath, ".csv");
            Console.Write("Generating CSV...");
            try
            {
                CsvExporter.Export(result, csvPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" saved");
                Console.ResetColor();
                Console.WriteLine($"  → {csvPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                WriteError($"CSV write failed: {ex.Message}");
            }
        }

        if (opts.OpenAfter)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(htmlPath) { UseShellExecute = true });
        }

        Console.WriteLine();
        return 0;
    }

    // -----------------------------------------------------------------------
    // Arg parsing
    // -----------------------------------------------------------------------
    private sealed class CliOptions
    {
        public ScanOptions ScanOpts = new();
        public string HtmlOutput   = "";
        public string CsvOutput    = "";
        public bool   OpenAfter;
    }

    private static CliOptions? ParseArgs(string[] args)
    {
        var opts = new CliOptions();
        opts.ScanOpts.MaxDepth = 5;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--path":
                case "-p":
                    opts.ScanOpts.RootPath = NextArg(args, ref i);
                    break;
                case "--output":
                case "-o":
                    opts.HtmlOutput = NextArg(args, ref i);
                    break;
                case "--csv":
                    opts.CsvOutput = NextArg(args, ref i);
                    break;
                case "--depth":
                case "-d":
                    if (int.TryParse(NextArg(args, ref i), out int d)) opts.ScanOpts.MaxDepth = d;
                    break;
                case "--exclude-system":
                case "--nosys":
                    opts.ScanOpts.ExcludeSystemFolders = true;
                    break;
                case "--open":
                    opts.OpenAfter = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return null;
                default:
                    // Positional: treat as path if not set
                    if (string.IsNullOrEmpty(opts.ScanOpts.RootPath) && !args[i].StartsWith('-'))
                        opts.ScanOpts.RootPath = args[i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(opts.ScanOpts.RootPath))
        {
            WriteError("--path is required.");
            PrintHelp();
            return null;
        }

        if (string.IsNullOrEmpty(opts.HtmlOutput))
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop)) desktop = Path.GetTempPath();
            opts.HtmlOutput = Path.Combine(desktop, $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        }

        return opts;
    }

    private static string NextArg(string[] args, ref int i)
    {
        if (++i < args.Length) return args[i];
        WriteError($"Missing value for argument at position {i}.");
        return "";
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  NTFSFolderAudit.exe --path <path> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --path <path>       Root path to scan (local or UNC)");
        Console.WriteLine("  --output <file>     Output HTML file path (default: Desktop)");
        Console.WriteLine("  --csv <file>        Also export a CSV file");
        Console.WriteLine("  --depth <n>         Max folder depth (default: 5)");
        Console.WriteLine("  --exclude-system    Skip Windows/ProgramData/etc folders");
        Console.WriteLine("  --open              Open the HTML report after generating");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(@"  NTFSFolderAudit.exe --path \\server\share --output report.html --depth 3");
        Console.WriteLine(@"  NTFSFolderAudit.exe --path C:\Shares --exclude-system --csv perms.csv --open");
    }

    private static void WriteBanner()
    {
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine("================================================================");
        Console.WriteLine("  NTFS Folder Audit v1.0  —  ProDirt");
        Console.WriteLine("================================================================");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void WriteError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"ERROR: {msg}");
        Console.ResetColor();
    }

    private static string TruncPath(string path, int max) =>
        path.Length <= max ? path : "…" + path[^(max - 1)..];

    private static string ResolveOutput(string user, string scanRoot, string ext)
    {
        if (string.IsNullOrEmpty(user)) return user;
        if (Directory.Exists(user))
            return Path.Combine(user, $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        if (!user.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            user += ext;
        return user;
    }
}
