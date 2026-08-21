using System.Diagnostics;

namespace NTFSReport;

/// <summary>
/// Replaces the old MessageBox About box, which could not render clickable
/// links. Reached from the '?' button at the right of the ribbon tab strip.
/// </summary>
public sealed class AboutForm : Form
{
    private const string SiteUrl     = "https://prodirt-llc.github.io";
    private const string ReleasesUrl = "https://github.com/prodirt-llc/ntfs-folder-audit/releases";
    private const string SupportUrl  = "https://paypal.me/ProDirtLLC";

    public AboutForm()
    {
        Text            = "About NTFS Folder Audit";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = MinimizeBox = false;
        ShowInTaskbar   = false;
        ClientSize      = new Size(462, 348);
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        var ink   = Color.FromArgb(51, 65, 85);
        var muted = Color.FromArgb(113, 113, 122);
        var slate = Color.FromArgb(30, 41, 59);

        // App icon, drawn from the same embedded resource the main window uses.
        var pic = new PictureBox
        {
            Location = new Point(24, 24), Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
        };
        try
        {
            using var s = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NTFSReport.app.ico");
            if (s != null) pic.Image = new Icon(s, 64, 64).ToBitmap();
        }
        catch { }

        var lblName = new Label
        {
            Text = "NTFS Folder Audit", Location = new Point(104, 26), AutoSize = true,
            Font = new Font("Segoe UI", 14f), ForeColor = slate
        };
        var lblVer = new Label
        {
            Text = $"Version {Application.ProductVersion.Split('+')[0]}",
            Location = new Point(106, 56), AutoSize = true,
            Font = new Font("Segoe UI", 8.25f), ForeColor = muted
        };

        var lblDesc = new Label
        {
            Text = "NTFS folder permissions auditing and client reporting for "
                 + "Windows administrators.\r\n\r\n"
                 + "Scan local and UNC paths, export interactive HTML reports, "
                 + "compare two paths side by side, and detect broken inheritance.",
            Location = new Point(24, 104), Size = new Size(414, 92),
            ForeColor = ink
        };

        var lblFree = new Label
        {
            Text = "Free, with no licence key. If it saved you some time, "
                 + "you're welcome to buy me a coffee.",
            Location = new Point(24, 200), Size = new Size(414, 34),
            ForeColor = muted, Font = new Font("Segoe UI", 8.25f)
        };

        var links = new FlowLayoutPanel
        {
            Location = new Point(21, 242), Size = new Size(420, 26),
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        links.Controls.Add(MakeLink("prodirt-llc.github.io", SiteUrl));
        links.Controls.Add(MakeSep(muted));
        links.Controls.Add(MakeLink("Releases on GitHub", ReleasesUrl));
        links.Controls.Add(MakeSep(muted));
        links.Controls.Add(MakeLink("☕ Buy me a coffee", SupportUrl));

        var lblBuilt = new Label
        {
            Text = "Built on .NET 8 · no installation required · © 2026 ProDirt",
            Location = new Point(24, 282), AutoSize = true,
            ForeColor = muted, Font = new Font("Segoe UI", 8.25f)
        };

        var btnClose = new Button
        {
            Text = "Close", Size = new Size(88, 28),
            Location = new Point(350, 304), DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(244, 244, 245),
            ForeColor = ink, UseVisualStyleBackColor = false, Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(212, 212, 216);

        Controls.AddRange([pic, lblName, lblVer, lblDesc, lblFree, links, lblBuilt, btnClose]);
        AcceptButton = CancelButton = btnClose;
    }

    private static LinkLabel MakeLink(string text, string url)
    {
        var l = new LinkLabel
        {
            Text = text, AutoSize = true, Margin = new Padding(3, 4, 3, 0),
            LinkColor = Color.FromArgb(30, 64, 130),
            ActiveLinkColor = Color.FromArgb(180, 83, 9),
            VisitedLinkColor = Color.FromArgb(30, 64, 130),
            LinkBehavior = LinkBehavior.HoverUnderline,
            Font = new Font("Segoe UI", 9f)
        };
        l.LinkClicked += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* no default browser, or the user cancelled */ }
        };
        return l;
    }

    private static Label MakeSep(Color c) => new()
    {
        Text = "·", AutoSize = true, ForeColor = c,
        Margin = new Padding(6, 4, 6, 0)
    };
}
