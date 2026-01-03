namespace LARS.Forms;

public partial class MainForm : Form
{
    private Panel sidebarPanel;
    private Panel contentPanel;
    private Button btnBomViewer;
    private Button btnPartList;
    private Button btnDailyPlan;
    private Button btnItemCounter;
    private Button btnStickerLabel;

    public MainForm()
    {
        InitializeComponent();
        InitializeCustomLayout();
        ApplyModernStyle();
    }

    private void InitializeCustomLayout()
    {
        // 사이드바 패널
        sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = Color.FromArgb(45, 45, 48) // 어두운 테마
        };

        // 컨텐츠 패널
        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // 버튼 생성
        btnBomViewer = CreateMenuButton("BOM Viewer", 0);
        btnPartList = CreateMenuButton("Part List", 50);
        btnDailyPlan = CreateMenuButton("Daily Plan", 100);
        btnItemCounter = CreateMenuButton("Item Counter", 150);
        btnStickerLabel = CreateMenuButton("Sticker Label", 200);

        // 이벤트 연결
        btnBomViewer.Click += (s, e) => ShowContent(new BomViewerControl());
        btnPartList.Click += (s, e) => ShowContent(new PartListControl());
        btnDailyPlan.Click += (s, e) => ShowContent(new DailyPlanControl());
        btnItemCounter.Click += (s, e) => ShowContent(new ItemCounterControl());
        btnStickerLabel.Click += (s, e) => ShowContent(new LabelPreviewControl());

        // 컨트롤 추가
        sidebarPanel.Controls.Add(btnStickerLabel);
        sidebarPanel.Controls.Add(btnItemCounter);
        sidebarPanel.Controls.Add(btnDailyPlan);
        sidebarPanel.Controls.Add(btnPartList);
        sidebarPanel.Controls.Add(btnBomViewer);

        this.Controls.Add(contentPanel);
        this.Controls.Add(sidebarPanel);
    }

    private Button CreateMenuButton(string text, int top)
    {
        return new Button
        {
            Text = text,
            Top = top,
            Left = 0,
            Width = 200,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void ShowContent(Control control)
    {
        contentPanel.Controls.Clear();
        control.Dock = DockStyle.Fill;
        contentPanel.Controls.Add(control);
    }

    private void ApplyModernStyle()
    {
        this.Text = "LARS Automation";
        this.Size = new Size(1280, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
    }
}
