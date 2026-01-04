using LARS.Features.BomViewer;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class MainForm : Form
{
    // Initialize with null! to suppress CS8618
    private Panel sidebarPanel = null!;
    private Panel contentPanel = null!;
    
    // New consolidated buttons
    private Button btnDataViewer = null!;
    private Button btnItemCounter = null!;
    private Button btnStickerLabel = null!;
    private Button btnSettings = null!;

    public MainForm()
    {
        InitializeComponent();
        InitializeCustomLayout();
        ApplyModernStyle();
    }

    private void InitializeCustomLayout()
    {
        sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // Create Buttons with Dock Style
        btnDataViewer = CreateMenuButton("데이터 뷰어 (Viewers)");
        btnItemCounter = CreateMenuButton("아이템 카운터 (Counter)");
        btnStickerLabel = CreateMenuButton("스티커 라벨 (Label)");
        
        btnSettings = CreateMenuButton("설정 (Settings)");
        btnSettings.Dock = DockStyle.Bottom; // Bottom Align

        // Event Handlers
        btnDataViewer.Click += (s, e) => { HighlightButton(btnDataViewer); ShowContent(new DataViewerControl()); };
        btnItemCounter.Click += (s, e) => { HighlightButton(btnItemCounter); ShowContent(new ItemCounterControl()); };
        btnStickerLabel.Click += (s, e) => { HighlightButton(btnStickerLabel); ShowContent(new LabelPreviewControl()); };
        btnSettings.Click += (s, e) => { HighlightButton(btnSettings); ShowContent(new SettingsControl()); };

        // Add to Sidebar (Reverse Order for Dock=Top)
        sidebarPanel.Controls.Add(btnStickerLabel);
        sidebarPanel.Controls.Add(btnItemCounter);
        sidebarPanel.Controls.Add(btnDataViewer);
        sidebarPanel.Controls.Add(btnSettings); // Added first but docked bottom

        this.Controls.Add(contentPanel);
        this.Controls.Add(sidebarPanel);
        
        // Default View
        HighlightButton(btnDataViewer);
        ShowContent(new DataViewerControl());
    }

    private Button CreateMenuButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void HighlightButton(Button activeBtn)
    {
        // Reset all buttons
        foreach(Control c in sidebarPanel.Controls)
        {
            if(c is Button b)
            {
                b.BackColor = Color.FromArgb(45, 45, 48);
                b.ForeColor = Color.White;
            }
        }

        // Highlight active
        activeBtn.BackColor = Color.White;
        activeBtn.ForeColor = Color.Black;
    }

    private void ShowContent(Control control)
    {
        contentPanel.Controls.Clear();
        control.Dock = DockStyle.Fill;
        contentPanel.Controls.Add(control);
    }

    private void ApplyModernStyle()
    {
        this.Text = "LARS // Logistics Automation Reporting System";
        this.Size = new Size(1280, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
    }
}
