using System.Data;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class PartListControl : UserControl
{
    private DataGridView dataGridView;
    private Button btnRefresh;
    private Panel topPanel;

    public PartListControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.Load += PartListControl_Load;

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
            BackColor = Color.Orange,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.Click += (s, e) => LoadPartLists();

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

    private void PartListControl_Load(object? sender, EventArgs e)
    {
        LoadPartLists();
    }

    private void LoadPartLists()
    {
        try
        {
            var folder = DirectoryHelper.PartListPath;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // 가상의 데이터나 실제 파일 목록 로드
            var files = Directory.GetFiles(folder, "*.xlsx");
            var list = new List<PartListItem>();

            // 파일이 없으면 테스트용 더미 데이터 추가 (UI 확인용)
            if (files.Length == 0)
            {
                list.Add(new PartListItem { FilePath = "Test_20260101.xlsx", Date = "2026-01-01", PrintStatus = "Done", PdfStatus = "Done" });
                list.Add(new PartListItem { FilePath = "Test_20260102.xlsx", Date = "2026-01-02", PrintStatus = "Ready", PdfStatus = "Pending" });
            }
            else
            {
                foreach (var file in files)
                {
                    list.Add(new PartListItem
                    {
                        FilePath = file,
                        Date = File.GetCreationTime(file).ToString("yyyy-MM-dd"),
                        PrintStatus = "Unknown",
                        PdfStatus = "Unknown"
                    });
                }
            }

            dataGridView.DataSource = list;
            
            // FilePath 컬럼 숨기기 (너무 기니까)
            if (dataGridView.Columns["FilePath"] != null)
                dataGridView.Columns["FilePath"].Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"로드 에러: {ex.Message}", "에러");
        }
    }
}
