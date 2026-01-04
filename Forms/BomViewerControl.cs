using System.Data;
using LARS.ENGINE.Documents.BOM;
using LARS.Models;
using LARS.Utils;

namespace LARS.Forms;

public partial class BomViewerControl : UserControl
{
    private DataGridView dataGridView;
    private Button btnLoad;
    private Button btnExport;
    private Panel topPanel;
    
    // Store loaded items or file path
    private List<BomItem> _loadedItems;
    private string _currentFilePath;
    private readonly BOMProcessor _processor;

    public BomViewerControl()
    {
        InitializeComponent();
        _processor = new BOMProcessor();
        _loadedItems = new List<BomItem>();
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
        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Filter = "Excel Files|*.xlsx;*.xls";
            ofd.Title = "Select BOM File";
            
            // For testing convenience, if TestSet exists, point there?
            // if (Directory.Exists(@"d:\Workshop\LARS\TestSet")) ofd.InitialDirectory = @"d:\Workshop\LARS\TestSet";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _currentFilePath = ofd.FileName;
                    _loadedItems = _processor.LoadBOM(_currentFilePath);
                    
                    dataGridView.DataSource = _loadedItems;
                    MessageBox.Show($"BOM Loaded: {_loadedItems.Count} items.", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading BOM: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("Please load a BOM file first.", "Warning");
            return;
        }

        using (SaveFileDialog sfd = new SaveFileDialog())
        {
            sfd.Filter = "Excel Files|*.xlsx";
            sfd.FileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_Processed.xlsx";
            
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _processor.ProcessSingle(_currentFilePath, sfd.FileName);
                    MessageBox.Show("BOM processed and saved successfully!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving BOM: {ex.Message}", "Error");
                }
            }
        }
    }
}
