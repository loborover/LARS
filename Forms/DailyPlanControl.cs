using System.Data;
using LARS.ENGINE.Documents.DailyPlan;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class DailyPlanControl : UserControl
{
    private DataGridView dataGridView;
    private Button btnRefresh;
    private Button btnProcess;
    private Panel topPanel;
    private readonly DailyPlanProcessor _processor;

    public DailyPlanControl()
    {
        InitializeComponent();
        _processor = new DailyPlanProcessor(DirectoryHelper.SourcePath);
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

        btnProcess = new Button
        {
            Text = "선택 항목 처리 (Process)",
            Location = new Point(180, 15),
            Size = new Size(180, 30),
            BackColor = Color.DarkBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnProcess.Click += BtnProcess_Click;

        topPanel.Controls.Add(btnRefresh);
        topPanel.Controls.Add(btnProcess);

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
            var list = _processor.LoadDailyPlans();
            dataGridView.DataSource = list;

            if(dataGridView.Columns["FilePath"] != null)
                dataGridView.Columns["FilePath"].Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"로드 에러: {ex.Message}", "에러");
        }
    }

    private void BtnProcess_Click(object? sender, EventArgs e)
    {
        if (dataGridView.SelectedRows.Count == 0)
        {
            MessageBox.Show("처리할 항목을 선택해주세요.", "알림");
            return;
        }

        try
        {
            int successCount = 0;
            foreach (DataGridViewRow row in dataGridView.SelectedRows)
            {
                if (row.DataBoundItem is DailyPlanItem item)
                {
                    _processor.ProcessSingle(item.FilePath);
                    successCount++;
                }
            }
            MessageBox.Show($"{successCount}개 파일 처리 완료!", "성공");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"처리 중 에러 발생: {ex.Message}", "에러");
        }
    }
}
