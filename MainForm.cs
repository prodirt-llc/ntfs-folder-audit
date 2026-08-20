using System.Drawing.Drawing2D;

namespace NTFSReport;

public sealed partial class MainForm : Form
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly PermissionScanner _scanner        = new();
    private readonly ComparisonService _compareService = new();

    private ScanResult?       _lastScanResult;
    private ScanResult?       _lastLeftResult;
    private ScanResult?       _lastRightResult;
    private ComparisonResult? _lastCompareResult;
    private CancellationTokenSource? _cts;
    private bool _compareSyncing;

    // Flat row list backing the folder grid
    private readonly List<FolderRow> _folderRows = new();
    private FolderRow? _selectedRow;

    // Ribbon + workspace state
    private static readonly string[] RibbonTabNames = { "Analyze", "Compare", "Export" };
    private readonly List<Rectangle> _ribbonTabRects = new();
    private Rectangle _aboutRect;
    private int  _activeRibbonTab;
    private int  _mode;              // 0 = Analyze, 1 = Compare
    private bool _eventLogExpanded;
    private bool _compareLaidOut;

    // -----------------------------------------------------------------------
    // Palette — one ramp. Slate is primary; amber is reserved for broken
    // inheritance and red for destructive/cancel. Nothing else gets a colour.
    // -----------------------------------------------------------------------
    private static readonly Color ClrSlate       = Color.FromArgb(30, 41, 59);
    private static readonly Color ClrSlateDark   = Color.FromArgb(15, 23, 42);
    private static readonly Color ClrSlateText   = Color.FromArgb(203, 213, 225);
    private static readonly Color ClrGreen       = Color.FromArgb(21, 128, 61);
    private static readonly Color ClrAmber       = Color.FromArgb(180, 83, 9);
    private static readonly Color ClrRed         = Color.FromArgb(185, 28, 28);
    private static readonly Color ClrChrome      = Color.FromArgb(244, 244, 245);
    private static readonly Color ClrChromeBrdr  = Color.FromArgb(212, 212, 216);
    private static readonly Color ClrMuted       = Color.FromArgb(113, 113, 122);
    private static readonly Color ClrBody        = Color.FromArgb(250, 250, 250);
    private static readonly Color ClrInk         = Color.FromArgb(51, 65, 85);
    private static readonly Color ClrRule        = Color.FromArgb(226, 232, 240);
    private static readonly Color ClrHdrBg       = Color.FromArgb(241, 245, 249);

    // Three sizes, one family.
    private static readonly Font FntSmall   = new("Segoe UI", 8.25f);
    private static readonly Font FntBody    = new("Segoe UI", 9f);
    private static readonly Font FntBodyB   = new("Segoe UI", 9f, FontStyle.Bold);
    private static readonly Font FntTab     = new("Segoe UI", 9.75f);

    // Ribbon geometry
    private const int TabStripH  = 30;
    private const int RibbonBodyH = 88;
    private const int EventBarH  = 26;

    // -----------------------------------------------------------------------
    // Controls — chrome
    // -----------------------------------------------------------------------
    private Panel                pnlRibbon      = null!;
    private Panel                pnlRibbonTabs  = null!;
    private Panel                pnlRibbonBody  = null!;
    private FlowLayoutPanel      flpAnalyzeRib  = null!;
    private FlowLayoutPanel      flpCompareRib  = null!;
    private FlowLayoutPanel      flpExportRib   = null!;
    private Panel                pnlWorkspace   = null!;
    private Panel                pnlAnalyzeWork = null!;
    private Panel                pnlCompareWork = null!;
    private StatusStrip          statusStrip1   = null!;
    private ToolStripStatusLabel statusMain     = null!;
    private ToolStripStatusLabel statusVer      = null!;

    // Event log
    private Panel   pnlEventLog   = null!;
    private Panel   pnlEventHdr   = null!;
    private Label   lblEventSum   = null!;
    private Button  btnEventToggle = null!;
    private ListBox lstEvents     = null!;

    // Analyze ribbon
    private TextBox       txtScanPath      = null!;
    private Button        btnBrowseScan    = null!;
    private Button        btnScan          = null!;
    private Button        btnCancelScan    = null!;
    private ProgressBar   progressScan     = null!;
    private NumericUpDown nudDepth         = null!;
    private CheckBox      chkExcludeSystem = null!;
    private TrackBar      trkThreads       = null!;
    private Label         lblThreads       = null!;
    private Button        btnBrokenFilter  = null!;
    private Button        btnResetFilter   = null!;

    // Compare ribbon
    private TextBox       txtPath1          = null!;
    private TextBox       txtPath2          = null!;
    private Button        btnBrowse1        = null!;
    private Button        btnBrowse2        = null!;
    private Button        btnCompare        = null!;
    private Button        btnCancelCompare  = null!;
    private ProgressBar   progressCompare   = null!;
    private NumericUpDown nudDepthC         = null!;
    private CheckBox      chkExcludeSystemC = null!;
    private Button        btnChangesOnly    = null!;
    private Button        btnResetCompare   = null!;

    // Export ribbon
    private Button   btnExportHtml    = null!;
    private Button   btnExportCsv     = null!;
    private Button   btnDesktopOutput = null!;
    private Button   btnBrowseOutput  = null!;
    private CheckBox chkAutoOpen      = null!;
    private Button   btnOpenReport    = null!;
    private Label    lblExportTarget  = null!;
    private TextBox  txtOutputPath    = null!;

    // Analyze workspace
    private TextBox        txtSearch     = null!;
    private SplitContainer splitResults  = null!;
    private DataGridView   folderGrid    = null!;
    private DataGridView   gridPerms     = null!;
    private Panel          pnlDetails    = null!;
    private Label          lblDetPath    = null!;
    private Label          lblDetOwner   = null!;
    private Label          lblDetMod     = null!;
    private Label          lblDetInherit = null!;
    private Label          lblFolderPath = null!;
    private Label          lblScanStatus = null!;

    // Compare workspace
    private SplitContainer splitCompare     = null!;
    private SplitContainer splitLeft        = null!;
    private SplitContainer splitRight       = null!;
    private TreeView       treeLeft         = null!;
    private TreeView       treeRight        = null!;
    private DataGridView   gridLeft         = null!;
    private DataGridView   gridRight        = null!;
    private Label          lblLeftTitle     = null!;
    private Label          lblRightTitle    = null!;
    private Label          lblLeftFolder    = null!;
    private Label          lblRightFolder   = null!;
    private Label          lblCompareStatus = null!;

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
    // RibbonGroup — fixed-size panel with a caption along the bottom and a
    // hairline divider on its right edge. This is what makes the command bar
    // read as a ribbon rather than a toolbar.
    // -----------------------------------------------------------------------
    private sealed class RibbonGroup : Panel
    {
        private readonly string _caption;

        public RibbonGroup(string caption, int width)
        {
            _caption     = caption;
            Size         = new Size(width, RibbonBodyH);
            Margin       = new Padding(0);
            BackColor    = ClrBody;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics, _caption, FntSmall,
                new Rectangle(0, Height - 18, Width - 1, 16), ClrMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var pen = new Pen(ClrRule, 1);
            e.Graphics.DrawLine(pen, Width - 1, 8, Width - 1, Height - 22);
        }
    }

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public MainForm()
    {
        InitializeComponent();
        SetDefaultOutputPath();
        SelectRibbonTab(0);
    }

    // -----------------------------------------------------------------------
    // InitializeComponent
    // -----------------------------------------------------------------------
    private void InitializeComponent()
    {
        SuspendLayout();

        Text          = "NTFS Folder Audit";
        Size          = new Size(1300, 820);
        MinimumSize   = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState   = FormWindowState.Maximized;
        Font          = FntBody;
        BackColor     = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;

        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("NTFSReport.app.ico");
            if (stream != null) Icon = new Icon(stream);
        }
        catch { }

        BuildRibbon();
        BuildAnalyzeWorkspace();
        BuildCompareWorkspace();
        BuildEventLog();

        pnlWorkspace = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlWorkspace.Controls.Add(pnlAnalyzeWork);
        pnlWorkspace.Controls.Add(pnlCompareWork);

        statusStrip1 = new StatusStrip { BackColor = ClrBody };
        statusMain   = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusVer    = new ToolStripStatusLabel("NTFS Folder Audit 1.0") { ForeColor = ClrMuted };
        statusStrip1.Items.AddRange([statusMain, statusVer]);

        // Docking is resolved from the highest child index down, so the Fill
        // control is added first and the outermost band last.
        Controls.Add(pnlWorkspace);
        Controls.Add(pnlEventLog);
        Controls.Add(statusStrip1);
        Controls.Add(pnlRibbon);

        Shown += MainForm_Shown;
        ResumeLayout(false);
        PerformLayout();
    }

    // =======================================================================
    // RIBBON
    // =======================================================================
    private void BuildRibbon()
    {
        pnlRibbon = new Panel { Dock = DockStyle.Top, Height = TabStripH + RibbonBodyH, BackColor = ClrBody };

        pnlRibbonTabs = new Panel { Dock = DockStyle.Top, Height = TabStripH, BackColor = ClrSlate };
        pnlRibbonTabs.Paint     += RibbonTabs_Paint;
        pnlRibbonTabs.MouseDown += RibbonTabs_MouseDown;
        pnlRibbonTabs.MouseMove += (s, e) => pnlRibbonTabs.Cursor =
            _aboutRect.Contains(e.Location) || _ribbonTabRects.Any(r => r.Contains(e.Location))
                ? Cursors.Hand : Cursors.Default;

        pnlRibbonBody = new Panel { Dock = DockStyle.Fill, BackColor = ClrBody };
        pnlRibbonBody.Paint += (s, e) =>
        {
            using var pen = new Pen(ClrRule, 1);
            e.Graphics.DrawLine(pen, 0, pnlRibbonBody.Height - 1, pnlRibbonBody.Width, pnlRibbonBody.Height - 1);
        };

        flpAnalyzeRib = MakeRibbonStrip();
        flpCompareRib = MakeRibbonStrip();
        flpExportRib  = MakeRibbonStrip();

        BuildAnalyzeRibbon();
        BuildCompareRibbon();
        BuildExportRibbon();

        pnlRibbonBody.Controls.Add(flpAnalyzeRib);
        pnlRibbonBody.Controls.Add(flpCompareRib);
        pnlRibbonBody.Controls.Add(flpExportRib);

        pnlRibbon.Controls.Add(pnlRibbonBody);
        pnlRibbon.Controls.Add(pnlRibbonTabs);
    }

    private static FlowLayoutPanel MakeRibbonStrip() => new()
    {
        Dock          = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents  = false,
        AutoScroll    = true,
        BackColor     = ClrBody,
        Padding       = new Padding(6, 0, 0, 0),
        Visible       = false
    };

    private void BuildAnalyzeRibbon()
    {
        var gScan = new RibbonGroup("Scan", 268);
        txtScanPath = new TextBox { Location = new Point(10, 12), Size = new Size(246, 24), Font = FntBody, PlaceholderText = @"C:\Share or \\server\share" };
        btnBrowseScan = MakeChromeButton("Browse folder…", 10, 42, 130, 26);
        btnBrowseScan.Click += BtnBrowseScan_Click;
        gScan.Controls.AddRange([txtScanPath, btnBrowseScan]);

        var gRun = new RibbonGroup("Run", 106);
        btnScan = MakeSlateButton("▶\nRun scan", 12, 8, 82, 46);
        btnScan.Font = FntBodyB;
        btnScan.Click += BtnScan_Click;
        btnCancelScan = MakeDangerButton("Cancel", 12, 8, 82, 46);
        btnCancelScan.Visible = false;
        btnCancelScan.Click += (s, e) => _cts?.Cancel();
        progressScan = new ProgressBar { Location = new Point(12, 58), Size = new Size(82, 6), Style = ProgressBarStyle.Marquee, Visible = false };
        gRun.Controls.AddRange([btnScan, btnCancelScan, progressScan]);

        var gOpts = new RibbonGroup("Options", 236);
        nudDepth = new NumericUpDown { Location = new Point(52, 10), Size = new Size(52, 24), Minimum = 1, Maximum = 50, Value = 5, Font = FntBody };
        chkExcludeSystem = new CheckBox { Text = "System folders", Location = new Point(112, 12), Size = new Size(116, 20), Font = FntSmall, ForeColor = ClrInk };
        lblThreads = new Label { Text = "Threads: Auto", Location = new Point(10, 42), Size = new Size(92, 18), Font = FntSmall, ForeColor = ClrMuted };
        trkThreads = new TrackBar { Location = new Point(102, 38), Size = new Size(126, 26), Minimum = 0, Maximum = 64, Value = 0, TickStyle = TickStyle.None, AutoSize = false };
        trkThreads.Scroll += (s, e) => lblThreads.Text = trkThreads.Value == 0 ? "Threads: Auto" : $"Threads: {trkThreads.Value}";
        gOpts.Controls.AddRange([MakeLabel("Depth", 10, 13, 40), nudDepth, chkExcludeSystem, lblThreads, trkThreads]);

        var gFilter = new RibbonGroup("Filter", 176);
        btnBrokenFilter = MakeAmberButton("⚠  Broken only", 10, 10, 156, 26);
        btnBrokenFilter.Enabled = false;
        btnBrokenFilter.Click += BtnBrokenInheritance_Click;
        btnResetFilter = MakeChromeButton("✕  Reset filter", 10, 40, 156, 24);
        btnResetFilter.Visible = false;
        btnResetFilter.Click += (s, e) =>
        {
            if (_lastScanResult != null) PopulateAnalyzeGrid(_lastScanResult);
            btnResetFilter.Visible = false;
        };
        gFilter.Controls.AddRange([btnBrokenFilter, btnResetFilter]);

        flpAnalyzeRib.Controls.AddRange([gScan, gRun, gOpts, gFilter]);
    }

    private void BuildCompareRibbon()
    {
        var gPaths = new RibbonGroup("Paths", 336);
        txtPath1 = new TextBox { Location = new Point(30, 10), Size = new Size(220, 24), Font = FntBody, PlaceholderText = @"C:\Shares\Client1" };
        btnBrowse1 = MakeChromeButton("Browse", 256, 10, 68, 24);
        btnBrowse1.Click += (s, e) => BrowseFolder(txtPath1);
        txtPath2 = new TextBox { Location = new Point(30, 40), Size = new Size(220, 24), Font = FntBody, PlaceholderText = @"C:\Shares\Client2" };
        btnBrowse2 = MakeChromeButton("Browse", 256, 40, 68, 24);
        btnBrowse2.Click += (s, e) => BrowseFolder(txtPath2);
        gPaths.Controls.AddRange([
            MakeLabel("A", 10, 13, 18), txtPath1, btnBrowse1,
            MakeLabel("B", 10, 43, 18), txtPath2, btnBrowse2]);

        var gRun = new RibbonGroup("Run", 106);
        btnCompare = MakeSlateButton("▶\nCompare", 12, 8, 82, 46);
        btnCompare.Font = FntBodyB;
        btnCompare.Click += BtnCompare_Click;
        btnCancelCompare = MakeDangerButton("Cancel", 12, 8, 82, 46);
        btnCancelCompare.Visible = false;
        btnCancelCompare.Click += (s, e) => _cts?.Cancel();
        progressCompare = new ProgressBar { Location = new Point(12, 58), Size = new Size(82, 6), Style = ProgressBarStyle.Marquee, Visible = false };
        gRun.Controls.AddRange([btnCompare, btnCancelCompare, progressCompare]);

        var gOpts = new RibbonGroup("Options", 236);
        nudDepthC = new NumericUpDown { Location = new Point(52, 10), Size = new Size(52, 24), Minimum = 1, Maximum = 50, Value = 5, Font = FntBody };
        chkExcludeSystemC = new CheckBox { Text = "System folders", Location = new Point(112, 12), Size = new Size(116, 20), Font = FntSmall, ForeColor = ClrInk };
        gOpts.Controls.AddRange([MakeLabel("Depth", 10, 13, 40), nudDepthC, chkExcludeSystemC]);

        var gFilter = new RibbonGroup("Filter", 176);
        btnChangesOnly = MakeAmberButton("⚠  Changes only", 10, 10, 156, 26);
        btnChangesOnly.Enabled = false;
        btnChangesOnly.Click += BtnChangesOnly_Click;
        btnResetCompare = MakeChromeButton("✕  Reset filter", 10, 40, 156, 24);
        btnResetCompare.Visible = false;
        btnResetCompare.Click += BtnResetCompareFilter_Click;
        gFilter.Controls.AddRange([btnChangesOnly, btnResetCompare]);

        flpCompareRib.Controls.AddRange([gPaths, gRun, gOpts, gFilter]);
    }

    private void BuildExportRibbon()
    {
        var gReport = new RibbonGroup("Report", 210);
        btnExportHtml = MakeGreenButton("Save HTML report", 10, 10, 186, 26);
        btnExportHtml.Enabled = false;
        btnExportHtml.Click += BtnExportHtml_Click;
        btnExportCsv = MakeChromeButton("Export CSV", 10, 40, 186, 24);
        btnExportCsv.Enabled = false;
        btnExportCsv.Click += BtnExportCsv_Click;
        gReport.Controls.AddRange([btnExportHtml, btnExportCsv]);

        var gOutput = new RibbonGroup("Output", 216);
        btnDesktopOutput = MakeChromeButton("Desktop", 10, 10, 88, 24);
        btnDesktopOutput.Click += BtnDesktopOutput_Click;
        btnBrowseOutput = MakeChromeButton("Choose…", 104, 10, 92, 24);
        btnBrowseOutput.Click += BtnBrowseOutput_Click;
        chkAutoOpen = new CheckBox { Text = "Auto-open report", Location = new Point(10, 40), Size = new Size(186, 20), Checked = true, Font = FntSmall, ForeColor = ClrInk };
        gOutput.Controls.AddRange([btnDesktopOutput, btnBrowseOutput, chkAutoOpen]);

        var gOpen = new RibbonGroup("Open", 196);
        btnOpenReport = MakeChromeButton("Open HTML report…", 10, 10, 176, 26);
        btnOpenReport.Click += MnuOpenReport_Click;
        lblExportTarget = new Label { Location = new Point(10, 42), Size = new Size(176, 18), Font = FntSmall, ForeColor = ClrMuted };
        gOpen.Controls.AddRange([btnOpenReport, lblExportTarget]);

        // Not shown — holds the resolved output path for ResolveOutputPath().
        txtOutputPath = new TextBox { Visible = false };
        gOpen.Controls.Add(txtOutputPath);

        flpExportRib.Controls.AddRange([gReport, gOutput, gOpen]);
    }

    // -----------------------------------------------------------------------
    // Ribbon tab strip — painted and hit-tested by hand
    // -----------------------------------------------------------------------
    private void RibbonTabs_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        _ribbonTabRects.Clear();

        int x = 10;
        for (int i = 0; i < RibbonTabNames.Length; i++)
        {
            string name = RibbonTabNames[i];
            int    w    = TextRenderer.MeasureText(name, FntTab).Width + 30;
            var    rect = new Rectangle(x, 0, w, TabStripH);
            _ribbonTabRects.Add(rect);

            bool active = i == _activeRibbonTab;
            if (active)
            {
                using var bg = new SolidBrush(ClrBody);
                g.FillRectangle(bg, rect);
            }
            TextRenderer.DrawText(g, name, FntTab, rect,
                active ? ClrSlate : ClrSlateText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            x += w;
        }

        _aboutRect = new Rectangle(pnlRibbonTabs.Width - 38, 0, 30, TabStripH);
        TextRenderer.DrawText(g, "?", FntTab, _aboutRect, ClrSlateText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void RibbonTabs_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_aboutRect.Contains(e.Location)) { MnuAbout_Click(null, EventArgs.Empty); return; }
        for (int i = 0; i < _ribbonTabRects.Count; i++)
            if (_ribbonTabRects[i].Contains(e.Location)) { SelectRibbonTab(i); return; }
    }

    /// <summary>
    /// Analyze and Compare switch the workspace as well as the command groups.
    /// Export only swaps the groups — it acts on whichever mode you came from.
    /// </summary>
    private void SelectRibbonTab(int index)
    {
        _activeRibbonTab      = index;
        flpAnalyzeRib.Visible = index == 0;
        flpCompareRib.Visible = index == 1;
        flpExportRib.Visible  = index == 2;

        if (index is 0 or 1)
        {
            _mode = index;
            pnlAnalyzeWork.Visible = index == 0;
            pnlCompareWork.Visible = index == 1;
            if (index == 1) LayoutCompareSplitters();
        }

        UpdateExportState();
        pnlRibbonTabs.Invalidate();
    }

    private void UpdateExportState()
    {
        bool ready = _mode == 0 ? _lastScanResult != null : _lastCompareResult != null;
        btnExportHtml.Enabled = ready;
        btnExportCsv.Enabled  = ready;
        lblExportTarget.Text  = _mode == 0
            ? (ready ? "Acts on the current scan" : "Run a scan first")
            : (ready ? "Acts on the current comparison" : "Run a comparison first");
    }

    // =======================================================================
    // ANALYZE WORKSPACE
    // =======================================================================
    private void BuildAnalyzeWorkspace()
    {
        pnlAnalyzeWork = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        var pnlSearch = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = ClrBody, Padding = new Padding(8, 5, 8, 5) };
        txtSearch = new TextBox { Dock = DockStyle.Fill, Font = FntBody, PlaceholderText = "Search folders, paths, or identities…" };
        txtSearch.TextChanged += TxtSearch_TextChanged;
        pnlSearch.Controls.Add(txtSearch);

        splitResults = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        folderGrid = MakeFolderGrid();
        folderGrid.CellMouseDown         += FolderGrid_CellMouseDown;
        folderGrid.SelectionChanged      += FolderGrid_SelectionChanged;
        folderGrid.CellToolTipTextNeeded += FolderGrid_ToolTip;
        splitResults.Panel1.Controls.Add(folderGrid);

        // Folder details block, then the section header, then the grid.
        pnlDetails = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = ClrHdrBg };
        pnlDetails.Paint += (s, e) =>
        {
            using var pen = new Pen(ClrRule, 1);
            e.Graphics.DrawLine(pen, 0, pnlDetails.Height - 1, pnlDetails.Width, pnlDetails.Height - 1);
        };
        lblDetPath    = MakeDetailValue(66, 6,  520);
        lblDetOwner   = MakeDetailValue(66, 24, 240);
        lblDetMod     = MakeDetailValue(390, 24, 190);
        lblDetInherit = MakeDetailValue(66, 42, 520);
        pnlDetails.Controls.AddRange([
            MakeDetailKey("Path",     10, 6),
            MakeDetailKey("Owner",    10, 24),
            MakeDetailKey("Modified", 330, 24),
            MakeDetailKey("Inherit",  10, 42),
            lblDetPath, lblDetOwner, lblDetMod, lblDetInherit]);

        var pnlPermHdr = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = ClrHdrBg };
        lblFolderPath = new Label { Dock = DockStyle.Fill, Text = "Permissions", ForeColor = ClrInk, Font = FntSmall, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        pnlPermHdr.Controls.Add(lblFolderPath);

        gridPerms = MakePermGrid();
        splitResults.Panel2.Controls.Add(gridPerms);
        splitResults.Panel2.Controls.Add(pnlPermHdr);
        splitResults.Panel2.Controls.Add(pnlDetails);

        lblScanStatus = new Label { Visible = false };

        pnlAnalyzeWork.Controls.Add(splitResults);
        pnlAnalyzeWork.Controls.Add(pnlSearch);
    }

    private static Label MakeDetailKey(string text, int x, int y) => new()
    {
        Text = text, Location = new Point(x, y), Size = new Size(56, 17),
        Font = FntSmall, ForeColor = ClrMuted, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label MakeDetailValue(int x, int y, int w) => new()
    {
        Text = "—", Location = new Point(x, y), Size = new Size(w, 17),
        Font = FntSmall, ForeColor = ClrInk, TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true
    };

    /// <summary>Fills the detail block for the folder selected in the grid.</summary>
    private void ShowFolderDetails(FolderNode? f)
    {
        if (f == null)
        {
            lblDetPath.Text = lblDetOwner.Text = lblDetMod.Text = lblDetInherit.Text = "—";
            return;
        }
        lblDetPath.Text  = f.Path;
        lblDetOwner.Text = string.IsNullOrEmpty(f.Owner) ? "—" : f.Owner;
        lblDetMod.Text   = f.Modified?.ToString("yyyy-MM-dd HH:mm") ?? "—";

        if (f.AccessDenied)
        {
            lblDetInherit.Text      = "Access denied — ACL could not be read";
            lblDetInherit.ForeColor = ClrRed;
        }
        else if (f.InheritanceBroken)
        {
            lblDetInherit.Text      = $"BROKEN — {f.Permissions.Count} explicit entries, not inherited from parent";
            lblDetInherit.ForeColor = ClrAmber;
        }
        else
        {
            lblDetInherit.Text      = $"Inherits from parent — {f.Permissions.Count} entries";
            lblDetInherit.ForeColor = ClrInk;
        }
    }

    // =======================================================================
    // COMPARE WORKSPACE
    // =======================================================================
    private void BuildCompareWorkspace()
    {
        pnlCompareWork = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };

        splitCompare = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        (splitLeft,  treeLeft,  gridLeft,  lblLeftTitle,  lblLeftFolder)  = BuildComparePane("PATH A");
        (splitRight, treeRight, gridRight, lblRightTitle, lblRightFolder) = BuildComparePane("PATH B");

        treeLeft.AfterSelect   += TreeLeft_AfterSelect;
        treeLeft.AfterExpand   += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeRight, f.RelativePath, true);  _compareSyncing = false; } };
        treeLeft.AfterCollapse += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeRight, f.RelativePath, false); _compareSyncing = false; } };
        treeRight.AfterSelect   += TreeRight_AfterSelect;
        treeRight.AfterExpand   += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeLeft, f.RelativePath, true);  _compareSyncing = false; } };
        treeRight.AfterCollapse += (s, e) => { if (!_compareSyncing && e.Node?.Tag is FolderNode f) { _compareSyncing = true; SyncExpand(treeLeft, f.RelativePath, false); _compareSyncing = false; } };

        splitCompare.Panel1.Controls.Add(splitLeft.Parent!);
        splitCompare.Panel2.Controls.Add(splitRight.Parent!);

        var pnlLegend = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = ClrBody };
        lblCompareStatus = new Label { Dock = DockStyle.Fill, Text = "Legend:  ● Same   ● Permissions differ   ● One side only   ⚠ Broken inheritance", Font = FntSmall, ForeColor = ClrMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        pnlLegend.Controls.Add(lblCompareStatus);

        pnlCompareWork.Controls.Add(splitCompare);
        pnlCompareWork.Controls.Add(pnlLegend);
    }

    private static (SplitContainer, TreeView, DataGridView, Label, Label) BuildComparePane(string title)
    {
        var host = new Panel { Dock = DockStyle.Fill };

        var hdr = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = ClrHdrBg };
        var lblTitle = new Label { Dock = DockStyle.Fill, Text = title, Font = FntBodyB, ForeColor = ClrSlate, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        hdr.Controls.Add(lblTitle);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        var tree  = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true, ShowLines = true, Font = FntBody, BackColor = Color.White, BorderStyle = BorderStyle.None };

        var permHdr   = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = ClrHdrBg };
        var lblFolder = new Label { Dock = DockStyle.Fill, Font = FntSmall, ForeColor = ClrInk, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0), AutoEllipsis = true };
        permHdr.Controls.Add(lblFolder);

        var grid = MakePermGrid();
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(grid);
        split.Panel2.Controls.Add(permHdr);

        host.Controls.Add(split);
        host.Controls.Add(hdr);
        return (split, tree, grid, lblTitle, lblFolder);
    }

    // -----------------------------------------------------------------------
    // Compare filter — show only folders whose permissions actually differ
    // -----------------------------------------------------------------------
    private void BtnChangesOnly_Click(object? sender, EventArgs e)
    {
        if (_lastCompareResult == null || _lastLeftResult == null || _lastRightResult == null) return;
        PopulateCompareTree(treeLeft,  _lastLeftResult,  _lastCompareResult, isLeft: true,  changedOnly: true);
        PopulateCompareTree(treeRight, _lastRightResult, _lastCompareResult, isLeft: false, changedOnly: true);
        btnResetCompare.Visible = true;
        statusMain.Text = $"Showing {_lastCompareResult.ChangedCount + _lastCompareResult.LeftOnlyCount + _lastCompareResult.RightOnlyCount} differing folders — click ✕ Reset to restore";
    }

    private void BtnResetCompareFilter_Click(object? sender, EventArgs e)
    {
        if (_lastCompareResult == null || _lastLeftResult == null || _lastRightResult == null) return;
        PopulateCompareTree(treeLeft,  _lastLeftResult,  _lastCompareResult, isLeft: true);
        PopulateCompareTree(treeRight, _lastRightResult, _lastCompareResult, isLeft: false);
        btnResetCompare.Visible = false;
        statusMain.Text = "Filter cleared";
    }

    // =======================================================================
    // EVENT LOG
    // =======================================================================
    private void BuildEventLog()
    {
        pnlEventLog = new Panel { Dock = DockStyle.Bottom, Height = EventBarH, BackColor = ClrBody };

        pnlEventHdr = new Panel { Dock = DockStyle.Top, Height = EventBarH, BackColor = ClrBody };
        pnlEventHdr.Paint += (s, e) =>
        {
            using var pen = new Pen(ClrRule, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlEventHdr.Width, 0);
        };
        lblEventSum = new Label { Dock = DockStyle.Fill, Text = "EVENT LOG — no scan yet", Font = FntSmall, ForeColor = ClrMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        btnEventToggle = new Button { Dock = DockStyle.Right, Width = 34, Text = "▲", FlatStyle = FlatStyle.Flat, Font = FntSmall, ForeColor = ClrMuted, BackColor = ClrBody, Cursor = Cursors.Hand };
        btnEventToggle.FlatAppearance.BorderSize = 0;
        btnEventToggle.Click += (s, e) => ToggleEventLog();
        lblEventSum.Click += (s, e) => ToggleEventLog();
        pnlEventHdr.Controls.Add(lblEventSum);
        pnlEventHdr.Controls.Add(btnEventToggle);

        lstEvents = new ListBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = FntSmall,
            BackColor = Color.White, ForeColor = ClrInk, IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 18
        };
        lstEvents.DrawItem += LstEvents_DrawItem;

        pnlEventLog.Controls.Add(lstEvents);
        pnlEventLog.Controls.Add(pnlEventHdr);
    }

    private void ToggleEventLog()
    {
        _eventLogExpanded  = !_eventLogExpanded;
        pnlEventLog.Height = _eventLogExpanded ? EventBarH + 140 : EventBarH;
        btnEventToggle.Text = _eventLogExpanded ? "▼" : "▲";
    }

    private void LstEvents_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        string text  = lstEvents.Items[e.Index]?.ToString() ?? "";
        Color  color = text.StartsWith("ERROR") ? ClrRed
                     : text.StartsWith("WARN")  ? ClrAmber
                     : ClrInk;
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) color = Color.White;
        TextRenderer.DrawText(e.Graphics, text, FntSmall,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
            color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    /// <summary>
    /// Turns the per-folder AccessDenied flags the scan already collects into a
    /// readable list. Previously these were only ever surfaced as a count.
    /// </summary>
    private void PopulateEventLog(ScanResult result)
    {
        lstEvents.BeginUpdate();
        lstEvents.Items.Clear();

        var denied = result.AllFolders.Where(f => f.AccessDenied).ToList();
        foreach (var f in denied)
            lstEvents.Items.Add($"WARN   Access denied — ACL unreadable:  {f.Path}");

        int broken = result.BrokenInheritanceCount;
        if (broken > 0)
            lstEvents.Items.Add($"INFO   {broken} folder(s) have inheritance disabled — see the Broken only filter");

        if (lstEvents.Items.Count == 0)
            lstEvents.Items.Add("INFO   Scan completed with no warnings");

        lstEvents.EndUpdate();

        int warn = denied.Count;
        lblEventSum.Text = warn > 0
            ? $"⚠  EVENT LOG — {lstEvents.Items.Count} total ({warn} warning{(warn == 1 ? "" : "s")}, 0 errors)"
            : $"EVENT LOG — {lstEvents.Items.Count} total (0 warnings, 0 errors)";
        lblEventSum.ForeColor = warn > 0 ? ClrAmber : ClrMuted;
    }

    // -----------------------------------------------------------------------
    // Startup
    // -----------------------------------------------------------------------
    private void MainForm_Shown(object? sender, EventArgs e) => LayoutAnalyzeSplitters();

    /// <summary>
    /// A SplitContainer throws if a minimum size won't fit inside its current
    /// extent — which is exactly the case for a workspace that hasn't been shown
    /// yet and is still at its default size. Configure only when it will fit.
    /// </summary>
    private static void SafeSplit(SplitContainer sc, int min1, int min2, int percent, bool horizontal = false)
    {
        try
        {
            int extent = horizontal ? sc.Height : sc.Width;
            if (extent < min1 + min2 + sc.SplitterWidth + 8) return;

            sc.Panel1MinSize    = min1;
            sc.Panel2MinSize    = min2;
            sc.SplitterDistance = Math.Clamp(extent * percent / 100, min1, extent - min2 - sc.SplitterWidth);
        }
        catch { }
    }

    private void LayoutAnalyzeSplitters() => SafeSplit(splitResults, 220, 360, 38);

    private void LayoutCompareSplitters()
    {
        if (_compareLaidOut || splitCompare.Width < 420) return;
        SafeSplit(splitCompare, 200, 200, 50);
        SafeSplit(splitLeft,  110, 90, 62, horizontal: true);
        SafeSplit(splitRight, 110, 90, 62, horizontal: true);
        _compareLaidOut = true;
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
        lblFolderPath.Text = "Permissions";
        ShowFolderDetails(null);
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
            PopulateEventLog(r);
            UpdateExportState();
            btnBrokenFilter.Enabled = r.BrokenInheritanceCount > 0;
            btnBrokenFilter.Text    = r.BrokenInheritanceCount > 0
                ? $"⚠  Broken only  ·  {r.BrokenInheritanceCount}"
                : "⚠  Broken only";
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
        trkThreads.Enabled    = !scanning;
        if (scanning) { btnExportHtml.Enabled = false; btnExportCsv.Enabled = false; }
        else UpdateExportState();
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
        lblFolderPath.Text = "Permissions";
        ShowFolderDetails(null);

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
                gridRow.DefaultCellStyle.BackColor = gi % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);
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
        lblFolderPath.Text = "Permissions";
        ShowFolderDetails(fr.Folder);
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
            btnBrokenFilter.Text    = $"⚠  Broken only  ·  {_lastScanResult.BrokenInheritanceCount}";
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

            lblLeftTitle.Text  = $"PATH A  ·  {path1}";
            lblRightTitle.Text = $"PATH B  ·  {path2}";
            PopulateCompareTree(treeLeft,  _lastLeftResult,  _lastCompareResult, isLeft: true);
            PopulateCompareTree(treeRight, _lastRightResult, _lastCompareResult, isLeft: false);

            var c = _lastCompareResult;
            lblCompareStatus.Text        = $"Done — Same: {c.SameCount:N0}  Changed: {c.ChangedCount}  Left-only: {c.LeftOnlyCount}  Right-only: {c.RightOnlyCount}";
            UpdateExportState();
            btnChangesOnly.Enabled = c.ChangedCount + c.LeftOnlyCount + c.RightOnlyCount > 0;
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
        if (running) { btnExportHtml.Enabled = false; btnExportCsv.Enabled = false; }
        else UpdateExportState();
        Cursor = running ? Cursors.WaitCursor : Cursors.Default;
    }

    // -----------------------------------------------------------------------
    // Compare tree
    // -----------------------------------------------------------------------
    private void PopulateCompareTree(TreeView tree, ScanResult result, ComparisonResult comparison, bool isLeft, bool changedOnly = false)
    {
        var diffMap = comparison.Diffs.ToDictionary(d => d.RelativePath, d => d, StringComparer.OrdinalIgnoreCase);
        tree.BeginUpdate();
        tree.Nodes.Clear();
        if (result.Root != null) AddCompareNode(tree.Nodes, result.Root, diffMap, isLeft, changedOnly);
        tree.EndUpdate();
        if (changedOnly) tree.ExpandAll();
        else if (tree.Nodes.Count > 0) tree.Nodes[0].Expand();
    }

    /// <summary>True if this folder, or anything beneath it, differs between the two paths.</summary>
    private static bool HasDifference(FolderNode folder, Dictionary<string, FolderDiff> diffMap)
    {
        if (diffMap.TryGetValue(folder.RelativePath, out var d) && d.Status != DiffStatus.Same) return true;
        return folder.Children.Any(c => HasDifference(c, diffMap));
    }

    private static void AddCompareNode(TreeNodeCollection nodes, FolderNode folder, Dictionary<string, FolderDiff> diffMap, bool isLeft, bool changedOnly = false)
    {
        // Keep a folder when it differs itself or still has a differing descendant,
        // so surviving nodes stay connected back to the root.
        if (changedOnly && !HasDifference(folder, diffMap)) return;

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
            AddCompareNode(node.Nodes, child, diffMap, isLeft, changedOnly);
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
    // The Export ribbon tab acts on whichever workspace you came from.
    private void BtnExportHtml_Click(object? sender, EventArgs e)
    {
        if (_mode == 1) { ExportCompareHtml(); return; }
        if (_lastScanResult == null) { NoDataMsg(); return; }
        ExportAndOpenHtml(_lastScanResult);
    }

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
            UpdateExportState();
            Cursor = Cursors.Default;
        }
    }

    private void BtnExportCsv_Click(object? sender, EventArgs e)
    {
        if (_mode == 1) { BtnExportCompareCsv_Click(sender, e); return; }
        if (_lastScanResult == null) { NoDataMsg(); return; }
        using var dlg = new SaveFileDialog { Title = "Export Permissions as CSV", Filter = "CSV Files|*.csv", DefaultExt = "csv", FileName = $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { CsvExporter.Export(_lastScanResult, dlg.FileName); statusMain.Text = $"CSV saved: {dlg.FileName}"; }
        catch (Exception ex) { MessageBox.Show($"CSV error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async void ExportCompareHtml()
    {
        if (_lastCompareResult == null) { MessageBox.Show("No comparison data. Run a compare first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        // Save directly to desktop — no SaveFileDialog to avoid UI thread deadlock
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var outPath = Path.Combine(desktop, $"NTFSFolderAudit_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.html");

        try
        {
            btnExportHtml.Enabled = false;
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
            UpdateExportState();
            Cursor = Cursors.Default;
        }
    }

    private void BtnExportCompareCsv_Click(object? sender, EventArgs e)
    {
        if (_lastCompareResult == null) { NoDataMsg(); return; }
        using var dlg = new SaveFileDialog { Title = "Export Comparison as CSV", Filter = "CSV Files|*.csv", DefaultExt = "csv", FileName = $"NTFSFolderAudit_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
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
        using var dlg = new SaveFileDialog { Title = "Save HTML Report As", Filter = "HTML Files|*.html", DefaultExt = "html", FileName = $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.html", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
        if (dlg.ShowDialog(this) == DialogResult.OK) txtOutputPath.Text = dlg.FileName;
    }

    private void BtnDesktopOutput_Click(object? sender, EventArgs e) => SetDefaultOutputPath();

    private void SetDefaultOutputPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktop)) desktop = Path.GetTempPath();
        txtOutputPath.Text = Path.Combine(desktop, $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.html");
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

    private void MnuAbout_Click(object? sender, EventArgs e) =>
        MessageBox.Show("NTFS Folder Audit v1.0\n© 2025 ProDirt\n\nProfessional NTFS permissions auditing for MSPs.\nScan local and UNC paths, export interactive HTML reports,\ncompare two paths side-by-side, and detect broken inheritance.\n\nBuilt on .NET 8 — no installation required.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // -----------------------------------------------------------------------
    // Control factory helpers
    // -----------------------------------------------------------------------
    private static Label MakeLabel(string text, int x, int y, int w) => new()
    {
        Text = text, Location = new Point(x, y), Size = new Size(w, 18),
        Font = FntSmall, ForeColor = ClrInk, TextAlign = ContentAlignment.MiddleLeft
    };

    private static readonly Color ClrDisabledBg = Color.FromArgb(234, 234, 237);
    private static readonly Color ClrDisabledFg = Color.FromArgb(163, 163, 172);

    private static Button MakeFlatButton(string text, int x, int y, int w, int h, Color back, Color fore)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat,
            Font = FntSmall, Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;

        // A flat button keeps its BackColor when disabled, so an unavailable
        // action still reads as available. Mute it explicitly instead.
        b.EnabledChanged += (s, e) =>
        {
            b.BackColor = b.Enabled ? back : ClrDisabledBg;
            b.ForeColor = b.Enabled ? fore : ClrDisabledFg;
            b.Cursor    = b.Enabled ? Cursors.Hand : Cursors.Default;
        };
        return b;
    }

    private static Button MakeSlateButton(string t, int x, int y, int w, int h)
    {
        var b = MakeFlatButton(t, x, y, w, h, ClrSlate, Color.White);
        b.FlatAppearance.MouseOverBackColor = ClrSlateDark;
        return b;
    }

    private static Button MakeGreenButton(string t, int x, int y, int w, int h)
    {
        var b = MakeFlatButton(t, x, y, w, h, ClrGreen, Color.White);
        b.Font = FntBodyB;
        return b;
    }

    private static Button MakeAmberButton(string t, int x, int y, int w, int h)
    {
        var b = MakeFlatButton(t, x, y, w, h, ClrAmber, Color.White);
        b.Font = FntBodyB;
        return b;
    }

    private static Button MakeDangerButton(string t, int x, int y, int w, int h) =>
        MakeFlatButton(t, x, y, w, h, ClrRed, Color.White);

    private static Button MakeChromeButton(string t, int x, int y, int w, int h)
    {
        var b = MakeFlatButton(t, x, y, w, h, ClrChrome, ClrMuted);
        b.FlatAppearance.BorderColor = ClrChromeBrdr;
        b.FlatAppearance.BorderSize  = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(228, 228, 231);
        return b;
    }

    // -----------------------------------------------------------------------
    // Grids
    // -----------------------------------------------------------------------
    private static void StyleGrid(DataGridView dg)
    {
        dg.ColumnHeadersDefaultCellStyle.BackColor = ClrHdrBg;
        dg.ColumnHeadersDefaultCellStyle.Font      = FntSmall;
        dg.ColumnHeadersDefaultCellStyle.ForeColor = ClrMuted;
        dg.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dg.ColumnHeadersHeight         = 26;
        dg.EnableHeadersVisualStyles   = false;
        // Without this the header above the current cell paints in selection blue.
        dg.ColumnHeadersDefaultCellStyle.SelectionBackColor = ClrHdrBg;
        dg.ColumnHeadersDefaultCellStyle.SelectionForeColor = ClrMuted;
        dg.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 224, 244);
        dg.DefaultCellStyle.SelectionForeColor = ClrSlate;
    }

    private static DataGridView MakeFolderGrid()
    {
        var dg = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible     = false,
            BackgroundColor       = Color.White,
            BorderStyle           = BorderStyle.None,
            CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor             = ClrRule,
            Font                  = FntBody,
            RowTemplate           = { Height = 22 },
            MultiSelect           = false,
            ShowCellToolTips      = true,
            ScrollBars            = ScrollBars.Both
        };
        StyleGrid(dg);
        dg.DefaultCellStyle.Padding = new Padding(2, 1, 2, 1);

        dg.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Folder", HeaderText = "Folder",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.False, Padding = new Padding(4, 0, 0, 0) }
        });
        dg.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Perms", HeaderText = "Perms", Width = 52,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = ClrSlate }
        });
        dg.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Flags", HeaderText = "", Width = 28, MinimumWidth = 28,
            Resizable = DataGridViewTriState.False,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        return dg;
    }

    private static DataGridView MakePermGrid()
    {
        var dg = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            Font = FntBody, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            GridColor = ClrRule
        };
        StyleGrid(dg);
        dg.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);

        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Identity",  HeaderText = "Identity",          AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 38, DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Access",    HeaderText = "Access",            Width = 60 });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rights",    HeaderText = "Rights",            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 42, DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Inherited", HeaderText = "Inherited",         Width = 100 });
        dg.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flags",     HeaderText = "Inheritance Flags", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });

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
    // Utility
    // -----------------------------------------------------------------------
    private static string ResolveOutputPath(string userPath, string scanRoot)
    {
        userPath = userPath.Trim();
        if (string.IsNullOrEmpty(userPath))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        if (Directory.Exists(userPath))
            return Path.Combine(userPath, $"NTFSFolderAudit_{DateTime.Now:yyyyMMdd_HHmmss}.html");
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
