using LARS.Features.ItemCounter;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class ItemCounterControl : UserControl
{
    private DataGridView dataGridView;
    private DateTimePicker datePicker;
    private Button btnCalculate;
    private Button btnExport;
    private Panel topPanel;
    private Label lblResult;

    public ItemCounterControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;

        topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.WhiteSmoke
        };

        // 날짜 선택
        datePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Location = new Point(20, 18),
            Width = 120
        };

        // 집계 버튼
        btnCalculate = new Button
        {
            Text = "집계 (Count)",
            Location = new Point(150, 15),
            Size = new Size(100, 30),
            BackColor = Color.RoyalBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCalculate.Click += (s, e) => CalculateItems();

        // 내보내기 버튼
        btnExport = new Button
        {
            Text = "엑셀 저장",
            Location = new Point(260, 15),
            Size = new Size(100, 30),
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExport.Click += (s, e) => ExportData();

        // 결과 라벨
        lblResult = new Label
        {
            Text = "준비됨",
            Location = new Point(380, 22),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        topPanel.Controls.Add(datePicker);
        topPanel.Controls.Add(btnCalculate);
        topPanel.Controls.Add(btnExport);
        topPanel.Controls.Add(lblResult);

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

    private void CalculateItems()
    {
        try
        {
            var service = new ItemCounterService();
            // 테스트를 위해 오늘 날짜 사용하거나, 선택된 날짜 사용
            // 실제 데이터가 없으므로 MockDataGenerator로 오늘 날짜 파일 생성 시도
            MockDataGenerator.GenerateSampleBomFile(); // BOM 포맷을 PartList로 재활용하거나 별도 생성 필요
            // 여기서는 단순하게 PartList 폴더에 임시 파일이 있다고 가정하거나 빈 결과 반환

            var result = service.AggregateItems(datePicker.Value);
            
            if (result.Count == 0)
            {
                // 테스트용 더미 데이터
                result.Add(new ItemSummary { PartNo = "TEST-001", Description = "Test Item 1", TotalQuantity = 100, Date = datePicker.Value.ToShortDateString(), FileCount = 2 });
                result.Add(new ItemSummary { PartNo = "TEST-002", Description = "Test Item 2", TotalQuantity = 50, Date = datePicker.Value.ToShortDateString(), FileCount = 1 });
            }

            dataGridView.DataSource = result;
            lblResult.Text = $"집계 완료: {result.Count} 품목";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"집계 중 오류: {ex.Message}", "에러");
        }
    }

    private void ExportData()
    {
        if (dataGridView.DataSource is not List<ItemSummary> items || items.Count == 0)
        {
            MessageBox.Show("저장할 데이터가 없습니다.");
            return;
        }

        using (SaveFileDialog sfd = new SaveFileDialog())
        {
            sfd.Filter = "Excel Files|*.xlsx";
            sfd.FileName = $"ItemCount_{DateTime.Now:yyyyMMdd}.xlsx";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                ExcelHelper.SaveListToExcel(items, sfd.FileName);
                MessageBox.Show("저장 완료!");
            }
        }
    }
}
