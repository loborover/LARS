using System.Data;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class DailyPlanControl : UserControl
{
    private DataGridView dataGridView;
    private Button btnRefresh;
    private Panel topPanel;

    public DailyPlanControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.Load += DailyPlanControl_Load;

        topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.WhiteSmoke
        };

        btnRefresh = new Button
        {
            Text = "새로고침 (Refresh)",
            Location = new Point(20, 15),
            Size = new Size(150, 30),
            BackColor = Color.Purple,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.Click += (s, e) => LoadDailyPlans();

        topPanel.Controls.Add(btnRefresh);

        dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        this.Controls.Add(dataGridView);
        this.Controls.Add(topPanel);
    }

    private void DailyPlanControl_Load(object? sender, EventArgs e)
    {
        LoadDailyPlans();
    }

    private void LoadDailyPlans()
    {
        try
        {
            var folder = DirectoryHelper.SourcePath;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // "Excel_Export_"로 시작하는 파일 검색 (VBA 로직 참조)
            var files = Directory.GetFiles(folder, "Excel_Export_*.xlsx");
            var list = new List<DailyPlanItem>();

            // 파일이 없으면 테스트용 더미 데이터
            if (files.Length == 0)
            {
                list.Add(new DailyPlanItem { FilePath = "DP_20260101_LineA.xlsx", Date = "2026-01-01", Line = "A", PrintStatus = "Done" });
                list.Add(new DailyPlanItem { FilePath = "DP_20260101_LineB.xlsx", Date = "2026-01-01", Line = "B", PrintStatus = "Ready" });
            }
            else
            {
                foreach (var file in files)
                {
                    list.Add(new DailyPlanItem
                    {
                        FilePath = file,
                        Date = File.GetCreationTime(file).ToString("yyyy-MM-dd"),
                        Line = "Unknown", // 파일명 파싱 로직 필요 시 추가
                        PrintStatus = "Ready"
                    });
                }
            }

            dataGridView.DataSource = list;

            if(dataGridView.Columns["FilePath"] != null)
                dataGridView.Columns["FilePath"].Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"로드 에러: {ex.Message}", "에러");
        }
    }
}
