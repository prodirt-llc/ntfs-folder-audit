using System.Drawing.Drawing2D;

namespace NTFSReport;

public sealed class LicenseForm : Form
{
    private readonly LicenseManager _license;

    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Label lblInstruction = null!;
    private TextBox txtKey = null!;
    private Button btnActivate = null!;
    private Button btnCancel = null!;
    private Label lblStatus = null!;
    private LinkLabel lnkPurchase = null!;
    private PictureBox spinner = null!;

    public LicenseForm(LicenseManager license)
    {
        _license = license;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "FolderAudit Pro — License Activation";
        Size            = new Size(520, 400);
        MinimumSize     = new Size(520, 400);
        MaximumSize     = new Size(520, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9.5f);

        // --- Gradient header ---
        pnlHeader = new Panel
        {
            Dock   = DockStyle.Top,
            Height = 100
        };
        pnlHeader.Paint += PnlHeader_Paint;

        // --- Content area ---
        lblInstruction = new Label
        {
            Text      = "Enter your Gumroad license key to activate\nFolderAudit Pro:",
            Location  = new Point(36, 120),
            Size      = new Size(440, 44),
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(55, 65, 81)
        };

        txtKey = new TextBox
        {
            Location    = new Point(36, 174),
            Size        = new Size(444, 28),
            Font        = new Font("Consolas", 11f),
            MaxLength   = 64,
            PlaceholderText = "XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX"
        };
        txtKey.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnActivate_Click(s, e); };

        lblStatus = new Label
        {
            Location  = new Point(36, 210),
            Size      = new Size(444, 22),
            ForeColor = Color.Firebrick,
            Font      = new Font("Segoe UI", 9f)
        };

        btnActivate = new Button
        {
            Text      = "Activate License",
            Location  = new Point(36, 242),
            Size      = new Size(160, 36),
            BackColor = Color.FromArgb(102, 126, 234),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btnActivate.FlatAppearance.BorderSize = 0;
        btnActivate.Click += BtnActivate_Click;

        btnCancel = new Button
        {
            Text      = "Exit",
            Location  = new Point(210, 242),
            Size      = new Size(90, 36),
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f),
            Cursor    = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        lnkPurchase = new LinkLabel
        {
            Text      = "Don't have a license? Purchase at prodirt-llc.github.io →",
            Location  = new Point(36, 300),
            Size      = new Size(444, 20),
            Font      = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(102, 126, 234)
        };
        lnkPurchase.LinkClicked += (s, e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = "https://prodirt-llc.github.io",
                UseShellExecute = true
            });

        var lblNote = new Label
        {
            Text      = "This tool is licensed per machine. The key is validated once and cached locally.",
            Location  = new Point(36, 330),
            Size      = new Size(444, 32),
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(107, 114, 128)
        };

        Controls.AddRange([pnlHeader, lblInstruction, txtKey, lblStatus,
                           btnActivate, btnCancel, lnkPurchase, lblNote]);

        AcceptButton = btnActivate;
        CancelButton = btnCancel;
        ResumeLayout(false);
        PerformLayout();
    }

    private void PnlHeader_Paint(object? sender, PaintEventArgs e)
    {
        var g   = e.Graphics;
        var rc  = pnlHeader.ClientRectangle;

        using var brush = new LinearGradientBrush(rc,
            Color.FromArgb(0x66, 0x7e, 0xea),
            Color.FromArgb(0x76, 0x4b, 0xa2),
            LinearGradientMode.Horizontal);
        g.FillRectangle(brush, rc);

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var brandFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        g.DrawString("P R O D I R T", brandFont, new SolidBrush(Color.FromArgb(180, 255, 255, 255)), 28f, 16f);

        using var titleFont = new Font("Segoe UI", 17f, FontStyle.Bold);
        g.DrawString("License Activation", titleFont, Brushes.White, 26f, 36f);
    }

    private async void BtnActivate_Click(object? sender, EventArgs e)
    {
        var key = txtKey.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            lblStatus.Text      = "Please enter your license key.";
            lblStatus.ForeColor = Color.Firebrick;
            return;
        }

        SetBusy(true);
        lblStatus.Text      = "Validating with Gumroad...";
        lblStatus.ForeColor = Color.FromArgb(102, 126, 234);

        var (success, message) = await _license.ActivateAsync(key);

        SetBusy(false);
        if (success)
        {
            lblStatus.Text      = $"✓ {message}";
            lblStatus.ForeColor = Color.FromArgb(22, 101, 52);
            await Task.Delay(900);
            DialogResult = DialogResult.OK;
        }
        else
        {
            lblStatus.Text      = $"✗ {message}";
            lblStatus.ForeColor = Color.Firebrick;
        }
    }

    private void SetBusy(bool busy)
    {
        btnActivate.Enabled = !busy;
        txtKey.Enabled      = !busy;
        Cursor              = busy ? Cursors.WaitCursor : Cursors.Default;
    }
}
