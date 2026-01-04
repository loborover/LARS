using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using LARS.Configuration;
using System.Drawing.Printing;

namespace LARS.UI.Controls;

public enum ViewerType { BOM, PartList, DailyPlan }

public partial class ViewerSettingsForm : Form
{
    private ViewerType _type;
    private TextBox txtFolderName;
    private NumericUpDown numHeaderRow;
    private Label lblPreview;
    private Button btnSave;
    private ComboBox cmbPrinters;
    private DataGridView dgvHeaders;

    public ViewerSettingsForm(ViewerType type)
    {
        _type = type;
        InitializeComponent();
        LoadSetting();
    }

    private void InitializeComponent()
    {
        this.Text = $"{_type} Settings";
        this.Size = new Size(600, 600); // Increased Size
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;

        var mainTab = new TabControl { Dock = DockStyle.Fill, Padding = new Point(10, 10) };
        var tabGeneral = new TabPage { Text = "General / Path" };
        var tabHeaders = new TabPage { Text = "Header Mapping" };
        
        mainTab.TabPages.Add(tabGeneral);
        mainTab.TabPages.Add(tabHeaders);
        this.Controls.Add(mainTab);

        // --- General Tab ---
        var panelGeneral = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        tabGeneral.Controls.Add(panelGeneral);

        var lblExplain = new Label
        {
            Text = "개별 저장 폴더 이름 (Sub-folder Name):",
            Location = new Point(15, 15),
            AutoSize = true,
            Font = new Font(this.Font, FontStyle.Bold)
        };

        txtFolderName = new TextBox
        {
            Location = new Point(15, 40),
            Width = 530
        };
        txtFolderName.TextChanged += (s, e) => UpdatePreview();

        var lblPreviewTitle = new Label
        {
            Text = "저장 경로 미리보기 (Preview):",
            Location = new Point(15, 80),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        lblPreview = new Label
        {
            Location = new Point(15, 105),
            AutoSize = true,
            Font = new Font("Consolas", 9),
            ForeColor = Color.DarkSlateGray
        };

        // Printer Selection
        var lblPrinter = new Label 
        { 
            Text = "기본 프린터 설정 (Default Printer):", 
            Location = new Point(15, 150), 
            AutoSize = true,
            Font = new Font(this.Font, FontStyle.Bold)
        };
        cmbPrinters = new ComboBox 
        { 
            Location = new Point(15, 175), 
            Width = 530, 
            DropDownStyle = ComboBoxStyle.DropDownList 
        };
        
        // Load Printers
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            cmbPrinters.Items.Add(printer);
        }

        panelGeneral.Controls.Add(lblExplain);
        panelGeneral.Controls.Add(txtFolderName);
        panelGeneral.Controls.Add(lblPreviewTitle);
        panelGeneral.Controls.Add(lblPreview);
        panelGeneral.Controls.Add(lblPrinter);
        panelGeneral.Controls.Add(cmbPrinters);

        // --- Headers Tab ---
        var panelHeader = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        tabHeaders.Controls.Add(panelHeader);

        // Header Row (Moved here)
        var lblHeaderRow = new Label 
        { 
            Text = "Header Row:", 
            Location = new Point(15, 10), 
            AutoSize = true,
            Font = new Font(this.Font, FontStyle.Bold)
        };
        numHeaderRow = new NumericUpDown 
        { 
            Location = new Point(100, 8), // Much closer now
            Width = 60, 
            Minimum = 1, 
            Value = 1 
        };

        var lblHeaderInfo = new Label { 
            Text = "Target Header 매핑: Number 순서대로 재배치되며 Width는 px 단위입니다.", 
            Location = new Point(15, 45), 
            AutoSize = true,
            ForeColor = Color.SteelBlue
        };

        dgvHeaders = new DataGridView
        {
            Location = new Point(15, 75),
            Size = new Size(530, 350),
            BackgroundColor = Color.White,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        
        dgvHeaders.Columns.Add("Order", "No");
        dgvHeaders.Columns["Order"].Width = 35; 
        dgvHeaders.Columns["Order"].Resizable = DataGridViewTriState.False;
        dgvHeaders.Columns.Add("Target", "Target Header");
        dgvHeaders.Columns.Add("UserSet", "User's Set Name");
        dgvHeaders.Columns.Add("Width", "Width");
        dgvHeaders.Columns["Width"].Width = 50;
        
        // Ensure "No" remains 35px if AutoSize kicks in (though it shouldn't for this column)
        dgvHeaders.Columns["Order"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

        // Grid Validation & Behavior Logic
        dgvHeaders.CellValidating += DgvHeaders_CellValidating;
        dgvHeaders.RowsAdded += DgvHeaders_RowsAdded;

        panelHeader.Controls.Add(lblHeaderRow);
        panelHeader.Controls.Add(numHeaderRow);
        panelHeader.Controls.Add(lblHeaderInfo);
        panelHeader.Controls.Add(dgvHeaders);

        // --- Bottom Buttons ---
        var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 60 };
        this.Controls.Add(panelBottom);

        btnSave = new Button
        {
            Text = "Apply Settings",
            Location = new Point(220, 10),
            Size = new Size(160, 40),
            BackColor = LARS.UI.Themes.ColorPalette.ActionProcess,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnSave.Click += BtnSave_Click;
        panelBottom.Controls.Add(btnSave);
    }

    private void LoadSetting()
    {
        var cfg = ConfigManager.Instance;
        switch (_type)
        {
            case ViewerType.BOM: txtFolderName.Text = cfg.BomExportDir; break;
            case ViewerType.PartList: txtFolderName.Text = cfg.PartListExportDir; break;
            case ViewerType.DailyPlan: txtFolderName.Text = cfg.DailyPlanExportDir; break;
        }

        if (!string.IsNullOrEmpty(cfg.DefaultPrinter))
        {
            cmbPrinters.SelectedItem = cfg.DefaultPrinter;
        }
        else if (cmbPrinters.Items.Count > 0)
        {
            cmbPrinters.SelectedIndex = 0;
        }

        // Load Headers
        var headers = GetHeaderConfig();
        numHeaderRow.Value = headers.TargetHeaderRow ?? 1;
        
        dgvHeaders.Rows.Clear();
        foreach (var mapping in headers.Mappings.OrderBy(m => m.Order))
        {
            dgvHeaders.Rows.Add(mapping.Order, mapping.Target, mapping.UserSet, mapping.Width);
        }
        
        UpdatePreview();
    }

    private ViewerHeaderConfig GetHeaderConfig()
    {
        return _type switch
        {
            ViewerType.BOM => ConfigManager.Headers.Bom,
            ViewerType.PartList => ConfigManager.Headers.PartList,
            ViewerType.DailyPlan => ConfigManager.Headers.DailyPlan,
            _ => new ViewerHeaderConfig()
        };
    }

    private void UpdatePreview()
    {
        var root = ConfigManager.Instance.IsDebugMode 
            ? ConfigManager.Instance.DebugExportPath 
            : ConfigManager.Instance.GlobalExportPath;
        
        try
        {
            lblPreview.Text = Path.Combine(root, txtFolderName.Text);
        }
        catch
        {
            lblPreview.Text = "Invalid Path";
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var cfg = ConfigManager.Instance;
        switch (_type)
        {
            case ViewerType.BOM: cfg.BomExportDir = txtFolderName.Text; break;
            case ViewerType.PartList: cfg.PartListExportDir = txtFolderName.Text; break;
            case ViewerType.DailyPlan: cfg.DailyPlanExportDir = txtFolderName.Text; break;
        }
        
        cfg.DefaultPrinter = cmbPrinters.SelectedItem?.ToString() ?? "";
        
        // Save Headers
        var headers = GetHeaderConfig();
        headers.TargetHeaderRow = (int)numHeaderRow.Value;
        
        var newMappings = new List<HeaderMapping>();
        foreach (DataGridViewRow row in dgvHeaders.Rows)
        {
            if (row.IsNewRow) continue;
            string orderStr = row.Cells[0].Value?.ToString() ?? "0";
            string target = row.Cells[1].Value?.ToString() ?? "";
            string userSet = row.Cells[2].Value?.ToString() ?? "";
            string widthStr = row.Cells[3].Value?.ToString() ?? "15";
            
            if (!string.IsNullOrEmpty(target))
            {
                int.TryParse(orderStr, out int order);
                double.TryParse(widthStr, out double width);
                newMappings.Add(new HeaderMapping { Order = order, Target = target, UserSet = userSet, Width = width });
            }
        }
        
        // Sort by Order ONLY at save time
        headers.Mappings = newMappings.OrderBy(m => m.Order).ToList();

        ConfigManager.Save();
        ConfigManager.SaveHeaders();
        this.DialogResult = DialogResult.OK; // Set result
        this.Close();
    }

    private void DgvHeaders_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.ColumnIndex == 0) // No (Order) column
        {
            string newVal = e.FormattedValue?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(newVal)) return;

            if (!int.TryParse(newVal, out int num))
            {
                MessageBox.Show("숫자만 입력 가능합니다.", "입력 오류");
                e.Cancel = true;
                return;
            }

            // Check duplicates in OTHER rows
            foreach (DataGridViewRow row in dgvHeaders.Rows)
            {
                if (row.Index == e.RowIndex || row.IsNewRow) continue;
                if (row.Cells[0].Value?.ToString() == newVal)
                {
                    MessageBox.Show("중복된 순서 번호입니다.", "중복 오류");
                    e.Cancel = true;
                    return;
                }
            }
        }
    }

    private void DgvHeaders_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
    {
        for (int i = 0; i < e.RowCount; i++)
        {
            int rowIndex = e.RowIndex + i;
            var cell = dgvHeaders.Rows[rowIndex].Cells[0];
            if (cell.Value == null || string.IsNullOrWhiteSpace(cell.Value.ToString()))
            {
                int max = 0;
                foreach (DataGridViewRow row in dgvHeaders.Rows)
                {
                    if (row.Index == rowIndex || row.IsNewRow) continue;
                    if (int.TryParse(row.Cells[0].Value?.ToString(), out int val) && val > max) max = val;
                }
                cell.Value = max + 1;
                dgvHeaders.Rows[rowIndex].Cells[3].Value = 100; // Default 100px
            }
        }
    }
}
