using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LARS.Configuration;

namespace LARS.UI.Controls;

public enum ViewerType { BOM, PartList, DailyPlan }

public partial class ViewerSettingsForm : Form
{
    private ViewerType _type;
    private TextBox txtFolderName;
    private Label lblPreview;
    private Button btnSave;

    public ViewerSettingsForm(ViewerType type)
    {
        _type = type;
        InitializeComponent();
        LoadSetting();
    }

    private void InitializeComponent()
    {
        this.Text = $"{_type} Settings";
        this.Size = new Size(400, 250);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var lblExplain = new Label
        {
            Text = "개별 저장 폴더 이름 (Sub-folder Name):",
            Location = new Point(20, 20),
            AutoSize = true
        };

        txtFolderName = new TextBox
        {
            Location = new Point(20, 45),
            Width = 340
        };
        txtFolderName.TextChanged += (s, e) => UpdatePreview();

        var lblPreviewTitle = new Label
        {
            Text = "저장 경로 미리보기 (Preview):",
            Location = new Point(20, 85),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        lblPreview = new Label
        {
            Location = new Point(20, 110),
            AutoSize = true,
            Font = new Font("Consolas", 9),
            ForeColor = Color.DarkSlateGray
        };

        btnSave = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(130, 150),
            Size = new Size(120, 40),
            BackColor = LARS.UI.Themes.ColorPalette.ActionProcess,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.Click += BtnSave_Click;

        this.Controls.Add(lblExplain);
        this.Controls.Add(txtFolderName);
        this.Controls.Add(lblPreviewTitle);
        this.Controls.Add(lblPreview);
        this.Controls.Add(btnSave);
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
        UpdatePreview();
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
        ConfigManager.Save();
        this.Close();
    }
}
