using System.Data;
using LARS.Features.BomViewer;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class BomViewerControl : UserControl
{
    private DataGridView dataGridView;
    private Button btnLoad;
    private Button btnExport;
    private Panel topPanel;

    public BomViewerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;

        // 상단 패널 (버튼 영역)
        topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.WhiteSmoke
        };

        // 로드 버튼
        btnLoad = new Button
        {
            Text = "BOM 로드 (Load BOM)",
            Location = new Point(20, 15),
            Size = new Size(150, 30),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnLoad.Click += BtnLoad_Click;

        // 내보내기 버튼
        btnExport = new Button
        {
            Text = "엑셀 저장 (Export)",
            Location = new Point(180, 15),
            Size = new Size(150, 30),
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExport.Click += BtnExport_Click;

        topPanel.Controls.Add(btnLoad);
        topPanel.Controls.Add(btnExport);

        // 그리드 뷰
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

    private void BtnLoad_Click(object? sender, EventArgs e)
    {
        try
        {
            // 테스트용 샘플 파일 생성
            MockDataGenerator.GenerateSampleBomFile();
            
            string sampleFile = Path.Combine(DirectoryHelper.SourcePath, "Excel_Export_Sample_BOM.xlsx");
            
            if (!File.Exists(sampleFile))
            {
                MessageBox.Show("BOM 파일을 찾을 수 없습니다.", "에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 파싱 및 데이터 바인딩
            var parser = new BomParser();
            List<BomItem> items = parser.ParseBomFile(sampleFile);

            dataGridView.DataSource = items;
            MessageBox.Show($"BOM 로드 완료: {items.Count}개 항목", "성공");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"에러 발생: {ex.Message}", "에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (dataGridView.DataSource is not List<BomItem> items || items.Count == 0)
        {
            MessageBox.Show("내보낼 데이터가 없습니다.", "알림");
            return;
        }

        using (SaveFileDialog sfd = new SaveFileDialog())
        {
            sfd.Filter = "Excel Files|*.xlsx";
            sfd.FileName = $"BOM_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExcelHelper.SaveListToExcel(items, sfd.FileName);
                    MessageBox.Show("엑셀 저장 완료!", "성공");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"저장 중 에러 발생: {ex.Message}", "에러");
                }
            }
        }
    }
}
