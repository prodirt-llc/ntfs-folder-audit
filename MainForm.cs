using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

namespace NTFSReport;

public sealed partial class MainForm : Form
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly PermissionScanner _scanner        = new();
    private readonly ComparisonService _compareService = new();
    private readonly LicenseManager    _license        = new();

    private ScanResult?       _lastScanResult;
    private ScanResult?       _lastLeftResult;
    private ScanResult?       _lastRightResult;
    private ComparisonResult? _lastCompareResult;
    private CancellationTokenSource? _cts;
    private bool _compareSyncing;

    // Flat row list backing the folder grid
    private readonly List<FolderRow> _folderRows = new();
    private FolderRow? _selectedRow;

    // -----------------------------------------------------------------------
    // Controls — shared
    // -----------------------------------------------------------------------
    private MenuStrip            menuStrip1   = null!;
    private Panel                pnlHeader    = null!;
    private TabControl           tabMain      = null!;
    private TabPage              tabAnalyze   = null!;
    private TabPage              tabCompare   = null!;
    private StatusStrip          statusStrip1 = null!;
    private ToolStripStatusLabel statusMain   = null!;
    private ToolStripStatusLabel statusVer    = null!;

    // Analyze options
    private Panel         pnlAnalyzeOpts   = null!;
    private Label         lblPath          = null!;
    private TextBox       txtScanPath      = null!;
    private Button        btnBrowseScan    = null!;
    private Label         lblOutput        = null!;
    private TextBox       txtOutputPath    = null!;
    private Button        btnBrowseOutput  = null!;
    private Button        btnDesktopOutput = null!;
    private CheckBox      chkAutoOpen      = null!;
    private Label         lblDepth         = null!;
    private NumericUpDown nudDepth         = null!;
    private CheckBox      chkExcludeSystem = null!;
    private Button        btnScan          = null!;
    private Button        btnCancelScan    = null!;
    private Button        btnExportHtml    = null!;
    private Button        btnExportCsv     = null!;
    private Button        btnBrokenFilter  = null!;
    private Button        btnResetFilter   = null!;
    private TextBox       txtSearch        = null!;
    private TrackBar      trkThreads       = null!;
    private Label         lblThreads       = null!;
    private ProgressBar   progressScan     = null!;
    private Label         lblScanStatus    = null!;

    // Analyze results — grid-based folder navigator
    private SplitContainer splitResults  = null!;
    private DataGridView   folderGrid    = null!;
    private Panel          pnlPermHeader = null!;
    private Label          lblFolderPath = null!;
    private DataGridView   gridPerms     = null!;

    // Compare tab
    private Panel         pnlCompareOpts       = null!;
    private Label         lblPath1             = null!;
    private TextBox       txtPath1             = null!;
    private Button        btnBrowse1           = null!;
    private Label         lblPath2             = null!;
    private TextBox       txtPath2             = null!;
    private Button        btnBrowse2           = null!;
    private Label         lblDepthC            = null!;
    private NumericUpDown nudDepthC            = null!;
    private CheckBox      chkExcludeSystemC    = null!;
    private Button        btnCompare           = null!;
    private Button        btnCancelCompare     = null!;
    private Button        btnExportCompareHtml = null!;
    private Button        btnExportCompareCsv  = null!;
    private ProgressBar   progressCompare      = null!;
    private Label         lblCompareStatus     = null!;

    private SplitContainer splitCompare    = null!;
    private Panel          pnlLeftHeader   = null!;
    private Label          lblLeftTitle    = null!;
    private SplitContainer splitLeft       = null!;
    private TreeView       treeLeft        = null!;
    private Panel          pnlLeftPermHdr  = null!;
    private Label          lblLeftFolder   = null!;
    private DataGridView   gridLeft        = null!;
    private Panel          pnlRightHeader  = null!;
    private Label          lblRightTitle   = null!;
    private SplitContainer splitRight      = null!;
    private TreeView       treeRight       = null!;
    private Panel          pnlRightPermHdr = null!;
    private Label          lblRightFolder  = null!;
    private DataGridView   gridRight       = null!;

    // -----------------------------------------------------------------------
    // FolderRow — one visible row in the folder grid
    // -----------------------------------------------------------------------
    private sealed class FolderRow
    {
        public FolderNode Folder   { get; init; } = null!;
        public bool       Expanded { get; set; }
        public bool       Visible  { get; set; } = true;
    }

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public MainForm()
    {
        InitializeComponent();
        ApplyTheme();
        SetDefaultOutputPath();
    }

    // -----------------------------------------------------------------------
    // InitializeComponent
    // -----------------------------------------------------------------------
    private void InitializeComponent()
    {
        SuspendLayout();

        Text          = "NTFS Permissions Reporter — ProDirt";
        Size          = new Size(1300, 820);
        MinimumSize   = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState   = FormWindowState.Maximized;
        Font          = new Font("Segoe UI", 9.5f);
        BackColor     = Color.FromArgb(245, 245, 245);
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NTFSReport.app.ico");
            if (stream != null) Icon = new Icon(stream);
        }
        catch { }

        // ================================================================
        // MENU
        // ================================================================
        menuStrip1 = new MenuStrip { Font = new Font("Segoe UI", 9.5f) };
        var mnuFile  = new ToolStripMenuItem("File");
        var mnuTools = new ToolStripMenuItem("Tools");
        var mnuLic   = new ToolStripMenuItem("License");
        var mnuHelp  = new ToolStripMenuItem("Help");

        mnuFile.DropDownItems.Add("New Scan", null, (s, e) => { tabMain.SelectedIndex = 0; txtScanPath.Focus(); });
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add("Open HTML Report…", null, MnuOpenReport_Click);
        mnuFile.DropDownItems.Add(new ToolStripSeparator());
        mnuFile.DropDownItems.Add("Exit", null, (s, e) => Close());

        mnuTools.DropDownItems.Add("Compare Two Paths…", null, (s, e) => tabMain.SelectedIndex = 1);
        mnuTools.DropDownItems.Add(new ToolStripSeparator());
        mnuTools.DropDownItems.Add("Copy Path to Clipboard", null, (s, e) =>
        {
            if (!string.IsNullOrEmpty(txtScanPath.Text)) Clipboard.SetText(txtScanPath.Text);
        });

        mnuLic.DropDownItems.Add("View License Info",    null, MnuLicenseInfo_Click);
        mnuLic.DropDownItems.Add("Change License Key…", null, MnuChangeLicense_Click);
        mnuHelp.DropDownItems.Add("About NTFS Permissions Reporter", null, MnuAbout_Click);
        mnuHelp.DropDownItems.Add("ProDirt Website", null, (s, e) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://prodirt-llc.github.io") { UseShellExecute = true }));

        menuStrip1.Items.AddRange([mnuFile, mnuTools, mnuLic, mnuHelp]);

        // ================================================================
        // STATUS STRIP
        // ================================================================
        statusStrip1 = new StatusStrip();
        statusMain   = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusVer    = new ToolStripStatusLabel("ProDirt NTFS Reporter v1.0") { ForeColor = Color.FromArgb(107, 114, 128) };
        statusStrip1.Items.AddRange([statusMain, statusVer]);

        // ================================================================
        // TAB CONTROL
        // ================================================================
        tabMain    = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
        tabAnalyze = new TabPage("  Analyze  ");
        tabCompare = new TabPage("  Compare  ");
        tabMain.TabPages.Add(tabAnalyze);
        tabMain.TabPages.Add(tabCompare);

        // ================================================================
        // ANALYZE TAB — Options panel (2 rows)
        // ================================================================
        pnlAnalyzeOpts = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.White, Padding = new Padding(8, 4, 8, 4) };
        pnlAnalyzeOpts.Paint += PnlOptions_Paint;

        int lx = 10, lh = 26, r1y = 6, r2y = 38;

        lblPath     = MakeLabel("Scan:", lx, r1y + 3, 40);
        txtScanPath = new TextBox { Location = new Point(lx + 44, r1y), Size = new Size(280, lh), Font = new Font("Segoe UI", 9.5f), PlaceholderText = @"C:\Share  or  \\server\share", Anchor = AnchorStyles.Top | AnchorStyles.Left };
        btnBrowseScan = MakeSmallButton("Browse", lx + 328, r1y, 58);
        btnBrowseScan.Click += BtnBrowseScan_Click;

        lblOutput     = MakeLabel("Output:", lx + 396, r1y + 3, 54);
        txtOutputPath = new TextBox { Location = new Point(lx + 454, r1y), Size = new Size(10, lh), Font = new Font("Segoe UI", 9f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        btnBrowseOutput  = MakeSmallButton("Browse",  0, r1y, 60);
        btnDesktopOutput = MakeSmallButton("Desktop", 0, r1y, 60);
        chkAutoOpen = new CheckBox { Text = "Auto-open", Size = new Size(90, lh), Checked = true, Font = new Font("Segoe UI", 8.5f), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnBrowseOutput.Click  += BtnBrowseOutput_Click;
        btnDesktopOutput.Click += BtnDesktopOutput_Click;
        AnchorR(btnBrowseOutput); AnchorR(btnDesktopOutput); AnchorR(chkAutoOpen);

        btnScan = new Button { Text = "▶ Scan", Location = new Point(lx, r2y), Size = new Size(74, 28), BackColor = Color.FromArgb(102, 126, 234), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnScan.FlatAppearance.BorderSize = 0;
        btnScan.Click += BtnScan_Click;

        btnCancelScan = new Button { Text = "Cancel", Location = new Point(lx + 80, r2y), Size = new Size(60, 28), BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnCancelScan.FlatAppearance.BorderSize = 0;
        btnCancelScan.Click += (s, e) => _cts?.Cancel();

        btnExportHtml = new Button { Text = "Save HTML", Location = new Point(lx + 148, r2y), Size = new Size(80, 28), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnExportHtml.FlatAppearance.BorderSize = 0;
        btnExportHtml.Click += BtnExportHtml_Click;

        btnExportCsv = new Button { Text = "CSV", Location = new Point(lx + 234, r2y), Size = new Size(44, 28), BackColor = Color.FromArgb(108, 117, 125), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnExportCsv.FlatAppearance.BorderSize = 0;
        btnExportCsv.Click += BtnExportCsv_Click;

        btnBrokenFilter = new Button { Text = "⚠ Broken Inheritance", Location = new Point(lx + 286, r2y), Size = new Size(154, 28), BackColor = Color.FromArgb(243, 156, 18), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnBrokenFilter.FlatAppearance.BorderSize = 0;
        btnBrokenFilter.Click += BtnBrokenInheritance_Click;

        btnResetFilter = new Button { Text = "✕ Reset", Location = new Point(lx + 446, r2y), Size = new Size(60, 28), BackColor = Color.FromArgb(108, 117, 125), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnResetFilter.FlatAppearance.BorderSize = 0;
        btnResetFilter.Click += (s, e) => { if (_lastScanResult != null) PopulateAnalyzeGrid(_lastScanResult); btnResetFilter.Visible = false; };

        lblDepth = MakeLabel("Depth:", lx + 518, r2y + 2, 46);
        nudDepth = new NumericUpDown { Location = new Point(lx + 568, r2y), Size = new Size(50, 26), Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        chkExcludeSystem = new CheckBox { Text = "Exclude system folders", Location = new Point(lx + 624, r2y + 3), Size = new Size(168, 22), Font = new Font("Segoe UI", 8.5f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

        trkThreads = new TrackBar { TickFrequency = 8, SmallChange = 1, LargeChange = 8, Width = 120, Height = 28, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, TickStyle = TickStyle.None, AutoSize = false };
        trkThreads.Minimum = 0; trkThreads.Maximum = 64; trkThreads.Value = 0;
        trkThreads.Scroll += (s, e) => lblThreads.Text = trkThreads.Value == 0 ? "Threads: Auto" : $"Threads: {trkThreads.Value}";
        AnchorR(trkThreads);

        lblThreads = new Label { Text = "Threads: Auto", Size = new Size(90, 18), ForeColor = Color.FromArgb(55, 65, 81), Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        AnchorR(lblThreads);

        progressScan  = new ProgressBar { Size = new Size(140, 12), Style = ProgressBarStyle.Marquee, Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        lblScanStatus = new Label { Text = "", Size = new Size(10, 16), ForeColor = Color.FromArgb(75, 85, 99), Font = new Font("Segoe UI", 8f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        pnlAnalyzeOpts.Controls.AddRange([
            lblPath, txtScanPath, btnBrowseScan,
            lblOutput, txtOutputPath, btnBrowseOutput, btnDesktopOutput, chkAutoOpen,
            btnScan, btnCancelScan, btnExportHtml, btnExportCsv,
            btnBrokenFilter, btnResetFilter,
            lblDepth, nudDepth, chkExcludeSystem,
            trkThreads, lblThreads, progressScan, lblScanStatus
        ]);

        // ================================================================
        // ANALYZE TAB — Search bar
        // ================================================================
        var pnlSearch = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.White, Padding = new Padding(6, 4, 6, 4) };
        txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f), PlaceholderText = "Search folders, paths, or identities…" };
        txtSearch.TextChanged += TxtSearch_TextChanged;
        pnlSearch.Controls.Add(txtSearch);

        // ================================================================
        // ANALYZE TAB — SplitContainer: folder grid left, perms right
        // ================================================================
        splitResults = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        // --- Folder grid (left panel) ---
        folderGrid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible     = false,
            BackgroundColor       = Color.White,
            BorderStyle           = BorderStyle.None,
            CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor             = Color.FromArgb(230, 234, 240),
            Font                  = new Font("Segoe UI", 9.5f),
            RowTemplate           = { Height = 22 },
            MultiSelect           = false,
            ShowCellToolTips      = true,
            ScrollBars            = ScrollBars.Both
        };
        folderGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 236, 239);
        folderGrid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        folderGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(73, 80, 87);
        folderGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        folderGrid.ColumnHeadersHeight = 26;
        folderGrid.DefaultCellStyle.Padding = new Padding(2, 1, 2, 1);

        // Single Folder column — indent + arrow baked into the text
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Folder", HeaderText = "Folder",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.False, Padding = new Padding(4, 0, 0, 0) }
        });
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Perms", HeaderText = "Perms", Width = 52,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(102, 126, 234) }
        });
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Flags", HeaderText = "", Width = 28, MinimumWidth = 28,
            Resizable = DataGridViewTriState.False,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        folderGrid.CellMouseDown        += FolderGrid_CellMouseDown;
        folderGrid.SelectionChanged      += FolderGrid_SelectionChanged;
        folderGrid.CellToolTipTextNeeded += FolderGrid_ToolTip;

        splitResults.Panel1.Controls.Add(folderGrid);

        // --- Perms grid (right panel) ---
        pnlPermHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(240, 244, 255), Padding = new Padding(8, 4, 0, 0) };
        lblFolderPath = new Label { Dock = DockStyle.Fill, Text = "Select a folder to view its permissions", ForeColor = Color.FromArgb(102, 126, 234), Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
        pnlPermHeader.Controls.Add(lblFolderPath);

        gridPerms = MakePermGrid();
        splitResults.Panel2.Controls.Add(gridPerms);
        splitResults.Panel2.Controls.Add(pnlPermHeader);

        tabAnalyze.Controls.Add(splitResults);
        tabAnalyze.Controls.Add(pnlSearch);
        tabAnalyze.Controls.Add(pnlAnalyzeOpts);
        tabAnalyze.Padding = new Padding(6);

        // ================================================================
        // COMPARE TAB
        // ================================================================
        pnlCompareOpts = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.White, Padding = new Padding(8, 4, 8, 4) };
        pnlCompareOpts.Paint += PnlOptions_Paint;

        int cx = 10, cr1y = 6, cr2y = 38;

        lblPath1 = MakeLabel("Path A:", cx, cr1y + 3, 52);
        txtPath1 = new TextBox { Location = new Point(cx + 56, cr1y), Size = new Size(10, lh), Font = new Font("Segoe UI", 9.5f), PlaceholderText = @"C:\Shares\Client1", Anchor = AnchorStyles.Top | AnchorStyles.Left };
        btnBrowse1 = MakeSmallButton("Browse", 0, cr1y, 58);
        btnBrowse1.Click += (s, e) => BrowseFolder(txtPath1);
        AnchorR(btnBrowse1);

        lblPath2 = MakeLabel("Path B:", 0, cr1y + 3, 52);
        txtPath2 = new TextBox { Location = new Point(0, cr1y), Size = new Size(10, lh), Font = new Font("Segoe UI", 9.5f), PlaceholderText = @"C:\Shares\Client2", Anchor = AnchorStyles.Top | AnchorStyles.Left };
        btnBrowse2 = MakeSmallButton("Browse", 0, cr1y, 58);
        btnBrowse2.Click += (s, e) => BrowseFolder(txtPath2);

        btnCompare = new Button { Text = "▶ Compare", Location = new Point(cx, cr2y), Size = new Size(90, 28), BackColor = Color.FromArgb(102, 126, 234), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnCompare.FlatAppearance.BorderSize = 0;
        btnCompare.Click += BtnCompare_Click;

        btnCancelCompare = new Button { Text = "Cancel", Location = new Point(cx + 96, cr2y), Size = new Size(60, 28), BackColor = Color.FromArgb(220, 53, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnCancelCompare.FlatAppearance.BorderSize = 0;
        btnCancelCompare.Click += (s, e) => _cts?.Cancel();

        btnExportCompareHtml = new Button { Text = "Save HTML", Location = new Point(cx + 164, cr2y), Size = new Size(82, 28), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnExportCompareHtml.FlatAppearance.BorderSize = 0;
        btnExportCompareHtml.Click += BtnExportCompareHtml_Click;

        btnExportCompareCsv = new Button { Text = "CSV", Location = new Point(cx + 252, cr2y), Size = new Size(44, 28), BackColor = Color.FromArgb(108, 117, 125), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnExportCompareCsv.FlatAppearance.BorderSize = 0;
        btnExportCompareCsv.Click += BtnExportCompareCsv_Click;

        lblDepthC = MakeLabel("Depth:", cx + 310, cr2y + 2, 46);
        nudDepthC = new NumericUpDown { Location = new Point(cx + 360, cr2y), Size = new Size(50, 26), Minimum = 1, Maximum = 50, Value = 5, Font = new Font("Segoe UI", 9f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        chkExcludeSystemC = new CheckBox { Text = "Exclude system folders", Location = new Point(cx + 416, cr2y + 3), Size = new Size(168, 22), Font = new Font("Segoe UI", 8.5f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };

        progressCompare  = new ProgressBar { Size = new Size(140, 12), Style = ProgressBarStyle.Marquee, Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        lblCompareStatus = new Label { Text = "", Location = new Point(cx, cr2y + 30), Size = new Size(10, 16), ForeColor = Color.FromArgb(75, 85, 99), Font = new Font("Segoe UI", 8f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

        pnlCompareOpts.Controls.AddRange([lblPath1, txtPath1, btnBrowse1, lblPath2, txtPath2, btnBrowse2, btnCompare, btnCancelCompare, btnExportCompareHtml, btnExportCompareCsv, lblDepthC, nudDepthC, chkExcludeSystemC, progressCompare, lblCompareStatus]);

        splitCompare = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(227, 242, 253) };
        lblLeftTitle  = new Label { Dock = DockStyle.Fill, Text = "PATH A", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(21, 101, 192), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        pnlLeftHeader.Controls.Add(lblLeftTitle);

        splitLeft = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        treeLeft  = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true, ShowLines = true, Font = new Font("Segoe UI", 9f), BackColor = Color.White, BorderStyle = BorderStyle.None };
        treeLeft.AfterSelect   += TreeLeft_AfterSelect;
        treeLeft.AfterExpand   += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeRight, f.RelativePath, true);  _compareSyncing = false; } };
        treeLeft.AfterCollapse += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeRight, f.RelativePath, false); _compareSyncing = false; } };

        pnlLeftPermHdr = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.FromArgb(240, 244, 255) };
        lblLeftFolder  = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(102, 126, 234), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
        pnlLeftPermHdr.Controls.Add(lblLeftFolder);
        gridLeft = MakePermGrid();
        splitLeft.Panel1.Controls.Add(treeLeft);
        splitLeft.Panel2.Controls.Add(gridLeft);
        splitLeft.Panel2.Controls.Add(pnlLeftPermHdr);
        splitCompare.Panel1.Controls.Add(splitLeft);
        splitCompare.Panel1.Controls.Add(pnlLeftHeader);

        pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(243, 229, 245) };
        lblRightTitle  = new Label { Dock = DockStyle.Fill, Text = "PATH B", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(106, 27, 154), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        pnlRightHeader.Controls.Add(lblRightTitle);

        splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        treeRight  = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true, ShowLines = true, Font = new Font("Segoe UI", 9f), BackColor = Color.White, BorderStyle = BorderStyle.None };
        treeRight.AfterSelect   += TreeRight_AfterSelect;
        treeRight.AfterExpand   += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeLeft, f.RelativePath, true);  _compareSyncing = false; } };
        treeRight.AfterCollapse += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeLeft, f.RelativePath, false); _compareSyncing = false; } };

        pnlRightPermHdr = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.FromArgb(240, 244, 255) };
        lblRightFolder  = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(102, 126, 234), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
        pnlRightPermHdr.Controls.Add(lblRightFolder);
        gridRight = MakePermGrid();
        splitRight.Panel1.Controls.Add(treeRight);
        splitRight.Panel2.Controls.Add(gridRight);
        splitRight.Panel2.Controls.Add(pnlRightPermHdr);
        splitCompare.Panel2.Controls.Add(splitRight);
        splitCompare.Panel2.Controls.Add(pnlRightHeader);

        var pnlLegend = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(250, 250, 250) };
        var lblLegend = new Label { Dock = DockStyle.Fill, Text = "Legend:  🟢 Same   🟠 Permissions differ   🔵 Left path only   🟣 Right path only   🟡 Broken inheritance", Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(75, 85, 99), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        pnlLegend.Controls.Add(lblLegend);

        tabCompare.Controls.Add(splitCompare);
        tabCompare.Controls.Add(pnlLegend);
        tabCompare.Controls.Add(pnlCompareOpts);
        tabCompare.Padding = new Padding(6);

        // ================================================================
        // ASSEMBLE
        // ================================================================
        Controls.Add(tabMain);
        Controls.Add(statusStrip1);
        Controls.Add(menuStrip1);
        MainMenuStrip = menuStrip1;

        Shown += MainForm_Shown;
        ResumeLayout(false);
        PerformLayout();

        pnlAnalyzeOpts.SizeChanged += (s, e) => LayoutAnalyzeOptions();
        pnlCompareOpts.SizeChanged += (s, e) => LayoutCompareOptions();
        LayoutAnalyzeOptions();
        LayoutCompareOptions();
    }

    // -----------------------------------------------------------------------
    // Layout helpers
    // -----------------------------------------------------------------------
    private void LayoutAnalyzeOptions()
    {
        int w = pnlAnalyzeOpts.ClientSize.Width - 16, lx = 10;
        chkAutoOpen.Location      = new Point(w - 92, 9);
        btnDesktopOutput.Location = new Point(w - 92 - 62, 6);
        btnBrowseOutput.Location  = new Point(w - 92 - 62 - 62, 6);
        txtOutputPath.Location    = new Point(lx + 454, 6);
        txtOutputPath.Width       = btnBrowseOutput.Left - (lx + 454) - 4;
        progressScan.Location     = new Point(w - 144, 46);
        progressScan.Width        = 140;
        trkThreads.Location       = new Point(w - 144 - 124, 36);
        trkThreads.Width          = 120;
        lblThreads.Location       = new Point(w - 144 - 124 - 94, 43);
        lblThreads.Width          = 90;
    }

    private void LayoutCompareOptions()
    {
        int w = pnlCompareOpts.ClientSize.Width - 16, lx = 10, mid = w / 2;
        txtPath1.Location   = new Point(lx + 56, 6);
        txtPath1.Width      = mid - 66 - (lx + 56);
        btnBrowse1.Location = new Point(mid - 64, 6);
        lblPath2.Location   = new Point(mid + 4, 9);
        txtPath2.Location   = new Point(mid + 60, 6);
        txtPath2.Width      = w - 64 - (mid + 60);
        btnBrowse2.Location = new Point(w - 62, 6);
        progressCompare.Location = new Point(w - 144, 46);
        progressCompare.Width    = 140;
    }

    // -----------------------------------------------------------------------
    // Paint
    // -----------------------------------------------------------------------
    private void PnlHeader_Paint(object? sender, PaintEventArgs e) { }

    private static void PnlOptions_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel p) return;
        e.Graphics.DrawLine(new Pen(Color.FromArgb(226, 232, 240), 1), 0, p.Height - 1, p.Width, p.Height - 1);
    }

    // -----------------------------------------------------------------------
    // Startup
    // -----------------------------------------------------------------------
    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        splitResults.Panel1MinSize = 200; splitResults.Panel2MinSize = 300;
        splitCompare.Panel1MinSize = 200; splitCompare.Panel2MinSize = 200;
        splitLeft.Panel1MinSize    = 100; splitLeft.Panel2MinSize    = 80;
        splitRight.Panel1MinSize   = 100; splitRight.Panel2MinSize   = 80;
        try { splitResults.SplitterDistance = splitResults.Width  * 40 / 100; } catch { }
        try { splitCompare.SplitterDistance = splitCompare.Width  * 50 / 100; } catch { }
        try { splitLeft.SplitterDistance    = splitLeft.Height    * 65 / 100; } catch { }
        try { splitRight.SplitterDistance   = splitRight.Height   * 65 / 100; } catch { }

        if (!_license.CheckStoredLicense())
        {
            using var lf = new LicenseForm(_license);
            if (lf.ShowDialog(this) != DialogResult.OK) { Close(); return; }
        }
        statusVer.Text = "ProDirt NTFS Reporter v1.0  |  Licensed";
    }

    // -----------------------------------------------------------------------
    // Scan
    // -----------------------------------------------------------------------
    private async void BtnScan_Click(object? sender, EventArgs e)
    {
        var path = txtScanPath.Text.Trim();
        if (string.IsNullOrEmpty(path)) { MessageBox.Show("Please enter a folder path to scan.", "No Path", MessageBoxButtons.OK, MessageBoxIcon.Warning); txtScanPath.Focus(); return; }
        if (!Directory.Exists(path))   { MessageBox.Show($"Path not found:\n{path}", "Path Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        SetScanUI(true);
        _folderRows.Clear();
        folderGrid.RowCount = 0;
        gridPerms.Rows.Clear();
        lblFolderPath.Text = "Select a folder to view its permissions";
        _lastScanResult = null;

        _cts = new CancellationTokenSource();
        var options = new ScanOptions { RootPath = path, MaxDepth = (int)nudDepth.Value, ExcludeSystemFolders = chkExcludeSystem.Checked, MaxThreads = trkThreads.Value };
        var prog    = new Progress<ScanProgress>(p => statusMain.Text = $"Scanning… {p.FolderCount:N0} folders | {p.ErrorCount} errors — {TruncatePath(p.CurrentPath, 80)}");

        try
        {
            _lastScanResult = await _scanner.ScanAsync(options, prog, _cts.Token);
            PopulateAnalyzeGrid(_lastScanResult);

            var r = _lastScanResult;
            progressScan.Visible = false;
            statusMain.Text      = $"Complete — {r.TotalFolders:N0} folders, {r.TotalPermissions:N0} perms, {r.Elapsed.TotalSeconds:F1}s";
            btnExportHtml.Enabled   = true;
            btnExportCsv.Enabled    = true;
            btnBrokenFilter.Enabled = r.BrokenInheritanceCount > 0;
            btnBrokenFilter.Text    = r.BrokenInheritanceCount > 0
                ? $"⚠ Broken Inheritance ({r.BrokenInheritanceCount})"
                : "⚠ Broken Inheritance";
        }
        catch (OperationCanceledException) { progressScan.Visible = false; statusMain.Text = "Scan cancelled"; }
        catch (Exception ex)               { MessageBox.Show($"Scan error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally                            { _cts?.Dispose(); _cts = null; SetScanUI(false); }
    }

    private void SetScanUI(bool scanning)
    {
        btnScan.Visible       = !scanning;
        btnCancelScan.Visible = scanning;
        progressScan.Visible  = scanning;
        progressScan.Style    = ProgressBarStyle.Marquee;
        btnExportHtml.Enabled = !scanning && _lastScanResult != null;
        btnExportCsv.Enabled  = !scanning && _lastScanResult != null;
        trkThreads.Enabled    = !scanning;
        Cursor                = scanning ? Cursors.WaitCursor : Cursors.Default;
    }

    // -----------------------------------------------------------------------
    // Folder grid population
    // -----------------------------------------------------------------------
    private void PopulateAnalyzeGrid(ScanResult result)
    {
        _folderRows.Clear();
        _selectedRow = null;
        gridPerms.Rows.Clear();
        lblFolderPath.Text = "Select a folder to view its permissions";

        if (result.Root != null)
            BuildRows(result.Root, visibleParent: true);

        RefreshGrid();

        if (folderGrid.Rows.Count > 0)
            folderGrid.Rows[0].Selected = true;
    }

    private void BuildRows(FolderNode folder, bool visibleParent)
    {
        var row = new FolderRow { Folder = folder, Expanded = folder.Depth == 0, Visible = visibleParent };
        _folderRows.Add(row);
        bool childrenVisible = visibleParent && row.Expanded;
        foreach (var child in folder.Children)
            BuildRows(child, childrenVisible);
    }

    private void RefreshGrid()
    {
        folderGrid.SuspendLayout();
        folderGrid.RowCount = 0;
        folderGrid.RowCount = _folderRows.Count(r => r.Visible);

        int gi = 0;
        foreach (var fr in _folderRows)
        {
            if (!fr.Visible) continue;
            var    f      = fr.Folder;
            // Build folder cell text: spaces for indent, arrow, then name
            string indent = new string(' ', f.Depth * 3);
            string arrow  = f.Children.Count > 0 ? (fr.Expanded ? "▼ " : "▶ ") : "   ";
            string flags  = f.InheritanceBroken ? "⚠" : f.AccessDenied ? "🔒" : "";
            int    perms  = f.Permissions.Count;

            var gridRow = folderGrid.Rows[gi];
            gridRow.Tag                   = fr;
            gridRow.Cells["Folder"].Value = indent + arrow + f.Name;
            gridRow.Cells["Perms"].Value  = perms > 0 ? perms.ToString() : "";
            gridRow.Cells["Flags"].Value  = flags;

            if (f.InheritanceBroken)
            {
                gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 224);
                gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(180, 100, 0);
            }
            else if (f.AccessDenied)
            {
                gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
                gridRow.DefaultCellStyle.ForeColor = Color.Firebrick;
            }
            else
            {
                gridRow.DefaultCellStyle.BackColor = gi % 2 == 0 ? Color.White : Color.FromArgb(250, 251, 255);
                gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            }

            gi++;
        }

        folderGrid.ResumeLayout();
    }

    // -----------------------------------------------------------------------
    // Folder grid events
    // -----------------------------------------------------------------------
    private void FolderGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (e.Button != MouseButtons.Left) return;
        if (folderGrid.Rows[e.RowIndex].Tag is not FolderRow fr) return;
        if (e.ColumnIndex != folderGrid.Columns["Folder"].Index) return;
        if (fr.Folder.Children.Count == 0) return;

        fr.Expanded = !fr.Expanded;
        SetChildVisibility(fr.Folder, fr.Expanded);

        // Preserve selection and scroll position across refresh
        string? selectedPath = _selectedRow?.Folder.Path;
        int scrollPos = folderGrid.FirstDisplayedScrollingRowIndex;

        RefreshGrid();

        // Restore scroll
        try { if (scrollPos >= 0 && scrollPos < folderGrid.RowCount) folderGrid.FirstDisplayedScrollingRowIndex = scrollPos; } catch { }

        // Restore selection — find the row that was selected and re-select it
        if (selectedPath != null)
        {
            foreach (DataGridViewRow row in folderGrid.Rows)
            {
                if (row.Tag is FolderRow r && r.Folder.Path == selectedPath)
                {
                    folderGrid.ClearSelection();
                    row.Selected = true;
                    break;
                }
            }
        }
    }

    private void SetChildVisibility(FolderNode parent, bool show)
    {
        foreach (var fr in _folderRows)
        {
            if (fr.Folder.ParentPath != parent.Path) continue;
            fr.Visible = show;
            if (!show)
            {
                fr.Expanded = false;
                SetChildVisibility(fr.Folder, false);
            }
        }
    }

    private void FolderGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (folderGrid.SelectedRows.Count == 0) return;
        if (folderGrid.SelectedRows[0].Tag is not FolderRow fr) return;
        if (fr == _selectedRow) return;
        _selectedRow = fr;
        lblFolderPath.Text = fr.Folder.Path + (fr.Folder.InheritanceBroken ? "  ⚠ INHERITANCE BROKEN" : "");
        PopulatePermGrid(gridPerms, fr.Folder);
    }

    private void FolderGrid_ToolTip(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (folderGrid.Rows[e.RowIndex].Tag is not FolderRow fr) return;
        e.ToolTipText = fr.Folder.Path;
    }

    // -----------------------------------------------------------------------
    // Search
    // -----------------------------------------------------------------------
    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        var term = txtSearch.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(term))
        {
            if (_lastScanResult != null) PopulateAnalyzeGrid(_lastScanResult);
            return;
        }

        var matchPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fr in _folderRows)
        {
            var f = fr.Folder;
            if (f.Path.ToLowerInvariant().Contains(term) ||
                f.Name.ToLowerInvariant().Contains(term) ||
                f.Permissions.Any(p => p.Identity.ToLowerInvariant().Contains(term)))
            {
                matchPaths.Add(f.Path);
                // Add all ancestors
                var cur = f;
                while (!string.IsNullOrEmpty(cur.ParentPath))
                {
                    matchPaths.Add(cur.ParentPath);
                    var parent = _folderRows.FirstOrDefault(r => r.Folder.Path == cur.ParentPath);
                    if (parent == null) break;
                    cur = parent.Folder;
                }
            }
        }

        foreach (var fr in _folderRows)
        {
            fr.Visible  = matchPaths.Contains(fr.Folder.Path);
            fr.Expanded = fr.Visible && fr.Folder.Children.Count > 0;
        }
        RefreshGrid();
    }

    // -----------------------------------------------------------------------
    // Broken inheritance filter
    // -----------------------------------------------------------------------
    private void BtnBrokenInheritance_Click(object? sender, EventArgs e)
    {
        if (_lastScanResult == null) return;
        btnBrokenFilter.Enabled = false;
        btnBrokenFilter.Text    = "⚠ Filtering…";
        statusMain.Text         = "Filtering broken inheritance folders…";
        Application.DoEvents();

        try
        {
            var broken = _lastScanResult.AllFolders.Where(f => f.InheritanceBroken).ToHashSet();
            if (broken.Count == 0)
            {
                MessageBox.Show("No broken inheritance folders found.", "Broken Inheritance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var fr in _folderRows)
                fr.Visible = broken.Contains(fr.Folder);

            RefreshGrid();
            btnResetFilter.Visible = true;
            statusMain.Text = $"Showing {broken.Count} folders with broken inheritance — click ✕ Reset to restore";
        }
        finally
        {
            btnBrokenFilter.Enabled = true;
            btnBrokenFilter.Text    = $"⚠ Broken Inheritance ({_lastScanResult.BrokenInheritanceCount})";
        }
    }

    // -----------------------------------------------------------------------
    // Compare tab
    // -----------------------------------------------------------------------
    private async void BtnCompare_Click(object? sender, EventArgs e)
    {
        var path1 = txtPath1.Text.Trim();
        var path2 = txtPath2.Text.Trim();
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2)) { MessageBox.Show("Please enter both paths.", "Missing Paths", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!Directory.Exists(path1)) { MessageBox.Show($"Path A not found:\n{path1}", "Path Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        if (!Directory.Exists(path2)) { MessageBox.Show($"Path B not found:\n{path2}", "Path Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        SetCompareUI(true);
        treeLeft.Nodes.Clear(); treeRight.Nodes.Clear();
        gridLeft.Rows.Clear();  gridRight.Rows.Clear();
        _lastLeftResult = _lastRightResult = null; _lastCompareResult = null;

        _cts = new CancellationTokenSource();
        var opts1 = new ScanOptions { RootPath = path1, MaxDepth = (int)nudDepthC.Value, ExcludeSystemFolders = chkExcludeSystemC.Checked };
        var opts2 = new ScanOptions { RootPath = path2, MaxDepth = (int)nudDepthC.Value, ExcludeSystemFolders = chkExcludeSystemC.Checked };

        try
        {
            lblCompareStatus.Text = "Scanning Path A…";
            _lastLeftResult  = await _scanner.ScanAsync(opts1, new Progress<ScanProgress>(p => lblCompareStatus.Text = $"Path A: {p.FolderCount:N0} folders…"), _cts.Token);
            lblCompareStatus.Text = "Scanning Path B…";
            _lastRightResult = await _scanner.ScanAsync(opts2, new Progress<ScanProgress>(p => lblCompareStatus.Text = $"Path B: {p.FolderCount:N0} folders…"), _cts.Token);

            lblCompareStatus.Text = "Comparing…";
            _lastCompareResult    = _compareService.Compare(_lastLeftResult, _lastRightResult);

            lblLeftTitle.Text  = $"PATH A: {path1}";
            lblRightTitle.Text = $"PATH B: {path2}";
            PopulateCompareTree(treeLeft,  _lastLeftResult,  _lastCompareResult, isLeft: true);
            PopulateCompareTree(treeRight, _lastRightResult, _lastCompareResult, isLeft: false);

            var c = _lastCompareResult;
            lblCompareStatus.Text        = $"Done — Same: {c.SameCount:N0}  Changed: {c.ChangedCount}  Left-only: {c.LeftOnlyCount}  Right-only: {c.RightOnlyCount}";
            btnExportCompareHtml.Enabled = true;
            btnExportCompareCsv.Enabled  = true;
        }
        catch (OperationCanceledException) { lblCompareStatus.Text = "Cancelled."; }
        catch (Exception ex)               { MessageBox.Show($"Compare error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally                            { _cts?.Dispose(); _cts = null; SetCompareUI(false); }
    }

    private void SetCompareUI(bool running)
    {
        btnCompare.Visible       = !running;
        btnCancelCompare.Visible = running;
        progressCompare.Visible  = running;
        progressCompare.Style    = ProgressBarStyle.Marquee;
        // Don't disable export buttons here — they get enabled explicitly after compare completes
        if (running)
        {
            btnExportCompareHtml.Enabled = false;
            btnExportCompareCsv.Enabled  = false;
        }
        Cursor = running ? Cursors.WaitCursor : Cursors.Default;
    }

    // -----------------------------------------------------------------------
    // Compare tree
    // -----------------------------------------------------------------------
    private void PopulateCompareTree(TreeView tree, ScanResult result, ComparisonResult comparison, bool isLeft)
    {
        var diffMap = comparison.Diffs.ToDictionary(d => d.RelativePath, d => d, StringComparer.OrdinalIgnoreCase);
        tree.BeginUpdate();
        tree.Nodes.Clear();
        if (result.Root != null) AddCompareNode(tree.Nodes, result.Root, diffMap, isLeft);
        tree.EndUpdate();
        if (tree.Nodes.Count > 0) tree.Nodes[0].Expand();
    }

    private static void AddCompareNode(TreeNodeCollection nodes, FolderNode folder, Dictionary<string, FolderDiff> diffMap, bool isLeft)
    {
        string prefix = folder.InheritanceBroken ? "⚠ " : "";
        var node = new TreeNode(prefix + folder.Name) { Tag = folder };

        if (diffMap.TryGetValue(folder.RelativePath, out var diff))
        {
            (node.ForeColor, node.BackColor) = diff.Status switch
            {
                DiffStatus.Changed   => (Color.FromArgb(180, 70, 0),   Color.FromArgb(255, 235, 180)),
                DiffStatus.LeftOnly  => (Color.FromArgb(21, 101, 192), Color.FromArgb(227, 242, 253)),
                DiffStatus.RightOnly => (Color.FromArgb(106, 27, 154), Color.FromArgb(243, 229, 245)),
                DiffStatus.Same      => (Color.FromArgb(30, 120, 50),  Color.FromArgb(232, 248, 236)),
                _                    => (Color.Empty, Color.Empty)
            };
            if (diff.Status == DiffStatus.Changed)   node.Text += "  ≠ Changed";
            else if (diff.Status != DiffStatus.Same) node.Text += "  (only here)";
        }

        // Don't override diff colors with broken inheritance in compare view —
        // the diff status is more important. Just prefix the name with ⚠ (already done above).
        nodes.Add(node);
        foreach (var child in folder.Children)
            AddCompareNode(node.Nodes, child, diffMap, isLeft);
    }

    // -----------------------------------------------------------------------
    // Compare tree selection
    // -----------------------------------------------------------------------
    private void TreeLeft_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not FolderNode folder) return;
        lblLeftFolder.Text = folder.Path;
        PopulatePermGrid(gridLeft, folder);
        if (!_compareSyncing) { _compareSyncing = true; SyncCompareTree(treeRight, folder.RelativePath); _compareSyncing = false; }
    }

    private void TreeRight_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not FolderNode folder) return;
        lblRightFolder.Text = folder.Path;
        PopulatePermGrid(gridRight, folder);
        if (!_compareSyncing) { _compareSyncing = true; SyncCompareTree(treeLeft, folder.RelativePath); _compareSyncing = false; }
    }

    private static void SyncCompareTree(TreeView tree, string relPath)
    {
        var node = FindNodeByRelPath(tree.Nodes, relPath);
        if (node == null) return;
        tree.SelectedNode = node;
        node.EnsureVisible();
    }

    private static void SyncExpand(TreeView tree, string relPath, bool expand)
    {
        var node = FindNodeByRelPath(tree.Nodes, relPath);
        if (node == null) return;
        if (expand) node.Expand(); else node.Collapse();
    }

    private static TreeNode? FindNodeByRelPath(TreeNodeCollection nodes, string relPath)
    {
        foreach (TreeNode n in nodes)
        {
            if (n.Tag is FolderNode f && string.Equals(f.RelativePath, relPath, StringComparison.OrdinalIgnoreCase)) return n;
            var found = FindNodeByRelPath(n.Nodes, relPath);
            if (found != null) return found;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Permissions grid
    // -----------------------------------------------------------------------
    private static void PopulatePermGrid(DataGridView grid, FolderNode folder)
    {
        grid.Rows.Clear();
        foreach (var p in folder.Permissions)
        {
            var rights = (!string.IsNullOrEmpty(p.RightsDecoded) && p.RightsDecoded != p.Rights) ? p.RightsDecoded : p.Rights;
            int idx = grid.Rows.Add(p.Identity, p.AccessType, rights, p.IsInherited ? "Yes" : "No (Explicit)", p.InheritanceFlags);
            if (p.AccessType == "Deny")
                grid.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
            else if (!p.IsInherited)
                grid.Rows[idx].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        if (folder.AccessDenied)
            grid.Rows.Add("", "", "⛔ Access Denied — insufficient privileges to read ACL", "", "");
    }

    // -----------------------------------------------------------------------
    // Export
    // -----------------------------------------------------------------------
    private void BtnExportHtml_Click(object? sender, EventArgs e) { if (_lastScanResult == null) { NoDataMsg(); return; } ExportAndOpenHtml(_lastScanResult); }

    private async void ExportAndOpenHtml(ScanResult result)
    {
        var output = ResolveOutputPath(txtOutputPath.Text, result.Options.RootPath);
        if (string.IsNullOrEmpty(output)) return;
        try
        {
            btnExportHtml.Enabled = false;
            Cursor = Cursors.WaitCursor;
            statusMain.Text = "Generating report…";

            var html = await Task.Run(() => HtmlGenerator.BuildReport(result));
            await File.WriteAllTextAsync(output, html, System.Text.Encoding.UTF8);

            statusMain.Text = $"Report saved: {output}  ({new FileInfo(output).Length / 1024:N0} KB)";
            if (chkAutoOpen.Checked) OpenFile(output);
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            btnExportHtml.Enabled = _lastScanResult != null;
            Cursor = Cursors.Default;
        }
    }

    private void BtnExportCsv_Click(object? sender, EventArgs e)
    {
        if (_lastScanResult == null) { NoDataMsg(); return; }
        using var dlg = new SaveFileDialog { Title = "Export Permissions as CSV", Filter = "CSV Files|*.csv", DefaultExt = "csv", FileName = $"NTFS_Permissions_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { CsvExporter.Export(_lastScanResult, dlg.FileName); statusMain.Text = $"CSV saved: {dlg.FileName}"; }
        catch (Exception ex) { MessageBox.Show($"CSV error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async void BtnExportCompareHtml_Click(object? sender, EventArgs e)
    {
        if (_lastCompareResult == null) { MessageBox.Show("No comparison data. Run a compare first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        // Save directly to desktop — no SaveFileDialog to avoid UI thread deadlock
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var outPath = Path.Combine(desktop, $"NTFS_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.html");

        try
        {
            btnExportCompareHtml.Enabled = false;
            Cursor = Cursors.WaitCursor;
            statusMain.Text = "Generating comparison report…";

            var result = _lastCompareResult;
            var html   = await Task.Run(() => HtmlGenerator.BuildComparisonReport(result));
            await File.WriteAllTextAsync(outPath, html, System.Text.Encoding.UTF8);

            var kb = new FileInfo(outPath).Length / 1024;
            statusMain.Text = $"Comparison report saved: {outPath}  ({kb:N0} KB)";
            OpenFile(outPath);
        }
        catch (Exception ex) { MessageBox.Show($"Export error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            btnExportCompareHtml.Enabled = _lastCompareResult != null;
            Cursor = Cursors.Default;
        }
    }

    private void BtnExportCompareCsv_Click(object? sender, EventArgs e)
    {
        if (_lastCompareResult == null) { NoDataMsg(); return; }
        using var dlg = new SaveFileDialog { Title = "Export Comparison as CSV", Filter = "CSV Files|*.csv", DefaultExt = "csv", FileName = $"NTFS_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { CsvExporter.ExportComparison(_lastCompareResult, dlg.FileName); statusMain.Text = $"CSV saved: {dlg.FileName}"; }
        catch (Exception ex) { MessageBox.Show($"CSV error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // -----------------------------------------------------------------------
    // Browse helpers
    // -----------------------------------------------------------------------
    private void BtnBrowseScan_Click(object? sender, EventArgs e) => BrowseFolder(txtScanPath);

    private void BtnBrowseOutput_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Title = "Save HTML Report As", Filter = "HTML Files|*.html", DefaultExt = "html", FileName = $"NTFS_Permissions_{DateTime.Now:yyyyMMdd_HHmmss}.html", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
        if (dlg.ShowDialog(this) == DialogResult.OK) txtOutputPath.Text = dlg.FileName;
    }

    private void BtnDesktopOutput_Click(object? sender, EventArgs e) => SetDefaultOutputPath();

    private void SetDefaultOutputPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop)) desktop = Path.GetTempPath();
        txtOutputPath.Text = Path.Combine(desktop, $"NTFS_Permissions_{DateTime.Now:yyyyMMdd_HHmmss}.html");
    }

    private void BrowseFolder(TextBox target)
    {
        var tcs = new TaskCompletionSource<string?>();
        var t = new Thread(() =>
        {
            try
            {
                using var dlg = new FolderBrowserDialog { Description = "Select a folder to scan", UseDescriptionForTitle = true, ShowNewFolderButton = false };
                var cur = target.Text.Trim();
                if (Directory.Exists(cur)) dlg.InitialDirectory = cur;
                tcs.SetResult(dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : null);
            }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        while (!tcs.Task.IsCompleted) Application.DoEvents();
        if (tcs.Task.Result is string s) target.Text = s;
    }

    // -----------------------------------------------------------------------
    // Menu handlers
    // -----------------------------------------------------------------------
    private void MnuOpenReport_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Title = "Open HTML Report", Filter = "HTML Files|*.html|All Files|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
    }

    private void MnuLicenseInfo_Click(object? sender, EventArgs e)
    {
        var msg = _license.IsActivated ? $"Status: ✓ Activated\nLicensed to: {_license.LicensedTo ?? "N/A"}" : "Status: Not activated";
        MessageBox.Show(msg, "License Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MnuChangeLicense_Click(object? sender, EventArgs e)
    {
        _license.Deactivate();
        using var lf = new LicenseForm(_license);
        if (lf.ShowDialog(this) == DialogResult.OK) statusVer.Text = "ProDirt NTFS Reporter v1.0  |  Licensed";
    }

    private void MnuAbout_Click(object? sender, EventArgs e) =>
        MessageBox.Show("NTFS Permissions Reporter v1.0\n© 2025 ProDirt\n\nProfessional NTFS permissions auditing for MSPs.\nScan local and UNC paths, export interactive HTML reports,\ncompare two paths side-by-side, and detect broken inheritance.\n\nBuilt on .NET 8 — no installation required.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // -----------------------------------------------------------------------
    // Control factory helpers
    // -----------------------------------------------------------------------
    private static Label MakeLabel(string text, int x, int y, int w) =>
        new() { Text = text, Location = new Point(x, y), Size = new Size(w, 20), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(55, 65, 81) };

    private static Button MakeSmallButton(string text, int x, int y, int w) =>
        new() { Text = text, Location = new Point(x, y), Size = new Size(w, 26), BackColor = Color.FromArgb(102, 126, 234), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand };

    private static Button MakeButton(string text, int x, int y, int w) =>
        new() { Text = text, Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(102, 126, 234), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand };

    private static DataGridView MakePermGrid()
    {
        var dg = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            Font = new Font("Segoe UI", 9f), AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            GridColor = Color.FromArgb(230, 234, 240)
        };
        dg.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 236, 239);
        dg.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        dg.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(73, 80, 87);
        dg.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dg.ColumnHeadersHeight = 28;
        dg.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Identity",  HeaderText = "Identity",           AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 38, DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Access",    HeaderText = "Access",             Width = 60 });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rights",    HeaderText = "Rights",             AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 42, DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Inherited", HeaderText = "Inherited",          Width = 100 });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flags",     HeaderText = "Inheritance Flags",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });

        dg.RowsAdded += (s, e) =>
        {
            for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
            {
                if (i >= dg.Rows.Count) break;
                if (dg.Rows[i].Cells["Access"].Value as string == "Deny")
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
            }
        };
        return dg;
    }

    // -----------------------------------------------------------------------
    // Theme / Utility
    // -----------------------------------------------------------------------
    private void ApplyTheme()
    {
        foreach (TabPage tp in tabMain.TabPages)
            tp.BackColor = Color.FromArgb(248, 249, 252);
    }

    private static void AnchorLR(Control c) => c.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
    private static void AnchorR(Control c)  => c.Anchor = AnchorStyles.Top | AnchorStyles.Right;

    private static string ResolveOutputPath(string userPath, string scanRoot)
    {
        userPath = userPath.Trim();
        if (string.IsNullOrEmpty(userPath))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"NTFS_Permissions_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        if (Directory.Exists(userPath))
            return Path.Combine(userPath, $"NTFS_Permissions_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        if (!userPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) userPath += ".html";
        return userPath;
    }

    private static string TruncatePath(string path, int maxLen) =>
        path.Length <= maxLen ? path : "…" + path[^(maxLen - 1)..];

    private static void OpenFile(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }

    private void NoDataMsg() =>
        MessageBox.Show("No scan results available. Run a scan first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
