using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using LARS.UI;
using LARS.Models;

namespace LARS.UI.Controls;

public class BaseViewerControl : UserControl
{
    // Layout
    protected Panel TopMenuPanel = null!;
    protected SplitContainer MainSplitter = null!;
    protected SplitContainer RightSplitter = null!;

    // Controls
    protected Button BtnRefresh = null!;
    protected Button BtnDelete = null!;
    protected Button BtnProcess = null!;
    protected Button BtnSettings = null!;
    
    protected Label LblListTitle = null!;
    protected CheckBox ChkSelectAll = null!;
    protected CheckedListBox LstRawFiles = null!;

    protected PropertyGrid MetaPropertyGrid = null!;
    protected DataGridView PreviewGrid = null!;
    
    // Labels for Titles
    protected Label LblMetaTitle = null!;
    protected Label LblPreviewTitle = null!;

    public BaseViewerControl()
    {
        InitializeBaseComponent();
        WireUpLayoutManager();
    }

    private void InitializeBaseComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.White;

        // --- 1. Top Menu ---
        TopMenuPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = LayoutManager.TopMenuHeight,
            BackColor = Color.FromArgb(240, 240, 240),
            Padding = new Padding(10)
        };

        BtnRefresh = CreateMenuButton("Scan / Refresh", Color.SteelBlue);
        BtnDelete = CreateMenuButton("Delete Selected", Color.IndianRed);
        BtnProcess = CreateMenuButton("Process Selected", LARS.UI.Themes.ColorPalette.ActionProcess);
        BtnProcess.Width = 160;
        BtnSettings = CreateMenuButton("Settings", Color.Gray);

        // Add to Panel (Reverse order for Dock.Left)
        TopMenuPanel.Controls.Add(BtnSettings);
        TopMenuPanel.Controls.Add(BtnProcess);
        TopMenuPanel.Controls.Add(new Panel { Width = 20, Dock = DockStyle.Left }); // Spacer
        TopMenuPanel.Controls.Add(BtnDelete);
        TopMenuPanel.Controls.Add(new Panel { Width = 10, Dock = DockStyle.Left }); // Spacer
        TopMenuPanel.Controls.Add(BtnRefresh);

        // --- 2. Main Layout ---
        MainSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = LayoutManager.LeftPanelMinSize,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = LayoutManager.LeftPanelMinSize
        };

        // Left Panel Header
        var leftHeader = new Panel 
        { 
            Dock = DockStyle.Top, 
            Height = LayoutManager.HeaderPanelHeight, 
            Padding = LayoutManager.HeaderPadding 
        };

        LblListTitle = new Label
        {
            Text = "Files (0)",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        ChkSelectAll = new CheckBox
        {
            Text = "Select All",
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 0, 0)
        };
        ChkSelectAll.CheckedChanged += (s, e) => { for(int i=0; i<LstRawFiles.Items.Count; i++) LstRawFiles.SetItemChecked(i, ChkSelectAll.Checked); };

        leftHeader.Controls.Add(LblListTitle);
        leftHeader.Controls.Add(ChkSelectAll);

        LstRawFiles = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 25
        };
        LstRawFiles.DrawItem += LstRawFiles_DrawItem;

        MainSplitter.Panel1.Padding = new Padding(5);
        MainSplitter.Panel1.Controls.Add(LstRawFiles);
        MainSplitter.Panel1.Controls.Add(leftHeader);

        // --- 3. Right Layout ---
        RightSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = LayoutManager.MetaPanelMinHeight,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = LayoutManager.MetaPanelMinHeight
        };

        // Meta
        LblMetaTitle = new Label 
        { 
            Text = "File Metadata", 
            Dock = DockStyle.Top, 
            Height = 25, 
            BackColor = Color.WhiteSmoke, 
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(5, 0, 0, 0) // Consistent Padding
        };
        MetaPropertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            ToolbarVisible = false,
            HelpVisible = false
        };

        RightSplitter.Panel1.Controls.Add(MetaPropertyGrid);
        RightSplitter.Panel1.Controls.Add(LblMetaTitle);

        // Preview
        LblPreviewTitle = new Label 
        { 
            Text = "Preview Content", 
            Dock = DockStyle.Top, 
            Height = 25, 
            BackColor = Color.WhiteSmoke, 
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(5, 0, 0, 0) // Consistent Padding
        };
        PreviewGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells
        };

        RightSplitter.Panel2.Controls.Add(PreviewGrid);
        RightSplitter.Panel2.Controls.Add(LblPreviewTitle);

        MainSplitter.Panel2.Controls.Add(RightSplitter);

        this.Controls.Add(MainSplitter);
        this.Controls.Add(TopMenuPanel);
    }

    private void WireUpLayoutManager()
    {
        // Initial Sync on Load
        this.Load += (s, e) => {
             if (LayoutManager.CurrentMainSplitter > 0) MainSplitter.SplitterDistance = LayoutManager.CurrentMainSplitter;
             if (LayoutManager.CurrentMetaSplitter > 0) RightSplitter.SplitterDistance = LayoutManager.CurrentMetaSplitter;
        };

        // Events
        MainSplitter.SplitterMoved += (s, e) => LayoutManager.NotifyMainSplitter(MainSplitter.SplitterDistance);
        LayoutManager.MainSplitterDistanceChanged += (s, dist) => 
        {
            if (this.IsHandleCreated && !this.Disposing && MainSplitter.SplitterDistance != dist)
                MainSplitter.SplitterDistance = dist;
        };

        RightSplitter.SplitterMoved += (s, e) => LayoutManager.NotifyMetaSplitter(RightSplitter.SplitterDistance);
        LayoutManager.MetaSplitterDistanceChanged += (s, dist) => 
        {
            if (this.IsHandleCreated && !this.Disposing && RightSplitter.SplitterDistance != dist)
                RightSplitter.SplitterDistance = dist;
        };
    }

    protected virtual void LstRawFiles_DrawItem(object? sender, DrawItemEventArgs e)
    {
         if (e.Index < 0) return;
        e.DrawBackground();
        var item = LstRawFiles.Items[e.Index];
        var g = e.Graphics;
        using (var pen = new Pen(Color.LightGray)) { g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1); }
        
        var checkState = LstRawFiles.GetItemCheckState(e.Index);
        var checkSize = CheckBoxRenderer.GetGlyphSize(g, CheckBoxState.UncheckedNormal);
        var checkPos = new Point(e.Bounds.Left + 2, e.Bounds.Top + (e.Bounds.Height - checkSize.Height) / 2);
        
        var state = checkState switch {
            CheckState.Checked => CheckBoxState.CheckedNormal,
            CheckState.Indeterminate => CheckBoxState.MixedNormal,
            _ => CheckBoxState.UncheckedNormal
        };
        CheckBoxRenderer.DrawCheckBox(g, checkPos, state);
        
        var textBounds = new Rectangle(e.Bounds.Left + checkSize.Width + 5, e.Bounds.Top, e.Bounds.Width - checkSize.Width - 5, e.Bounds.Height);
        TextRenderer.DrawText(g, item.ToString(), e.Font, textBounds, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    private Button CreateMenuButton(string text, Color bg)
    {
        return new Button
        {
            Text = text,
            Width = 120,
            Dock = DockStyle.Left,
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
    }
}
