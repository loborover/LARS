using System;
using System.Drawing;
using System.Windows.Forms;
using LARS.Configuration; // using new namespace

namespace LARS.Forms;

public partial class SettingsControl : UserControl
{
    private TabControl tabs = null!;
    private TabPage tabGeneral = null!;
    private TabPage tabDebug = null!;
    private TabPage tabDocuments = null!;

    // General Controls
    private TextBox txtGlobalImport = null!;
    private TextBox txtGlobalExport = null!;
    private Button btnSave = null!;

    // Debug Controls
    private CheckBox chkDebugMode = null!;
    private TextBox txtDebugImport = null!;
    private TextBox txtDebugExport = null!;
    private GroupBox grpDebugPaths = null!;

    // Document Macro Controls
    private ComboBox cmbDocType = null!;
    private TextBox txtMacroEditor = null!;
    private Button btnSaveMacro = null!;
    private string _macroRoot = "";

    public SettingsControl()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.White;

        var lblTitle = new Label
        {
            Text = "환경 설정 (Settings)",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };
        this.Controls.Add(lblTitle);

        tabs = new TabControl
        {
            Location = new Point(20, 60),
            Size = new Size(700, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        InitializeGeneralTab();
        InitializeDebugTab();
        InitializeDocumentsTab();

        tabs.TabPages.Add(tabGeneral);
        tabs.TabPages.Add(tabDebug);
        tabs.TabPages.Add(tabDocuments);

        btnSave = new Button
        {
            Text = "설정 저장 (Save)",
            Location = new Point(20, 480),
            Size = new Size(150, 40),
            BackColor = LARS.UI.Themes.ColorPalette.ActionImport,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.Click += BtnSave_Click;

        this.Controls.Add(tabs);
        this.Controls.Add(btnSave);
    }

    private void InitializeGeneralTab()
    {
        tabGeneral = new TabPage("일반 (General)");
        tabGeneral.Padding = new Padding(20);
        tabGeneral.BackColor = Color.White;

        // Global Import
        var lblImport = new Label { Text = "Global Import Path:", Location = new Point(20, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        txtGlobalImport = new TextBox { Location = new Point(20, 55), Width = 500, ReadOnly = false };
        var btnBrowseImport = new Button { Text = "...", Location = new Point(530, 54), Width = 40 };
        btnBrowseImport.Click += (s, e) => { txtGlobalImport.Text = BrowseFolder(txtGlobalImport.Text); };

        // Global Export
        var lblExport = new Label { Text = "Global Export Path:", Location = new Point(20, 100), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        txtGlobalExport = new TextBox { Location = new Point(20, 125), Width = 500, ReadOnly = false };
        var btnBrowseExport = new Button { Text = "...", Location = new Point(530, 124), Width = 40 };
        btnBrowseExport.Click += (s, e) => { txtGlobalExport.Text = BrowseFolder(txtGlobalExport.Text); };

        tabGeneral.Controls.Add(lblImport);
        tabGeneral.Controls.Add(txtGlobalImport);
        tabGeneral.Controls.Add(btnBrowseImport);
        tabGeneral.Controls.Add(lblExport);
        tabGeneral.Controls.Add(txtGlobalExport);
        tabGeneral.Controls.Add(btnBrowseExport);
    }

    private void InitializeDebugTab()
    {
        tabDebug = new TabPage("디버그 (Debug)");
        tabDebug.Padding = new Padding(20);
        tabDebug.BackColor = Color.White;

        chkDebugMode = new CheckBox
        {
            Text = "디버그 모드 사용 (Debug Mode)",
            Location = new Point(20, 30),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        chkDebugMode.CheckedChanged += (s, e) => grpDebugPaths.Enabled = chkDebugMode.Checked;

        grpDebugPaths = new GroupBox
        {
            Text = "Debug Paths",
            Location = new Point(20, 70),
            Size = new Size(600, 200),
            Enabled = false
        };

        // Debug Import
        var lblDbgImport = new Label { Text = "Debug Import Path:", Location = new Point(20, 30), AutoSize = true };
        txtDebugImport = new TextBox { Location = new Point(20, 55), Width = 500 };
        var btnDbgBrowseImp = new Button { Text = "...", Location = new Point(530, 54), Width = 40 };
        btnDbgBrowseImp.Click += (s, e) => { txtDebugImport.Text = BrowseFolder(txtDebugImport.Text); };

        // Debug Export
        var lblDbgExport = new Label { Text = "Debug Export Path:", Location = new Point(20, 100), AutoSize = true };
        txtDebugExport = new TextBox { Location = new Point(20, 125), Width = 500 };
        var btnDbgBrowseExp = new Button { Text = "...", Location = new Point(530, 124), Width = 40 };
        btnDbgBrowseExp.Click += (s, e) => { txtDebugExport.Text = BrowseFolder(txtDebugExport.Text); };

        grpDebugPaths.Controls.Add(lblDbgImport);
        grpDebugPaths.Controls.Add(txtDebugImport);
        grpDebugPaths.Controls.Add(btnDbgBrowseImp);
        grpDebugPaths.Controls.Add(lblDbgExport);
        grpDebugPaths.Controls.Add(txtDebugExport);
        grpDebugPaths.Controls.Add(btnDbgBrowseExp);

        var btnTempClean = new Button
        {
            Text = "Temp Cleaning (Clear Debug Folders)",
            Location = new Point(20, 290),
            Size = new Size(250, 40),
            BackColor = Color.IndianRed, // Red for caution
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnTempClean.Click += BtnTempClean_Click;

        tabDebug.Controls.Add(chkDebugMode);
        tabDebug.Controls.Add(grpDebugPaths);
        tabDebug.Controls.Add(btnTempClean);
    }

    private void InitializeDocumentsTab()
    {
        tabDocuments = new TabPage("문서 가공 로직 (Documents)");
        tabDocuments.Padding = new Padding(20);
        tabDocuments.BackColor = Color.White;

        _macroRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
        if (!Directory.Exists(_macroRoot)) Directory.CreateDirectory(_macroRoot);

        var lblSelect = new Label { Text = "문서 종류 선택:", Location = new Point(20, 20), AutoSize = true };
        cmbDocType = new ComboBox { Location = new Point(130, 18), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbDocType.Items.AddRange(new string[] { "DailyPlan", "BOM", "PartList" });
        cmbDocType.SelectedIndexChanged += (s, e) => LoadMacroFile();

        txtMacroEditor = new TextBox
        {
            Location = new Point(20, 60),
            Size = new Size(650, 260),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        btnSaveMacro = new Button
        {
            Text = "매크로 저장 (Save Macro)",
            Location = new Point(20, 330),
            Size = new Size(200, 35),
            BackColor = Color.SteelBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnSaveMacro.Click += (s, e) => SaveMacroFile();

        tabDocuments.Controls.Add(lblSelect);
        tabDocuments.Controls.Add(cmbDocType);
        tabDocuments.Controls.Add(txtMacroEditor);
        tabDocuments.Controls.Add(btnSaveMacro);
        
        if (cmbDocType.Items.Count > 0) cmbDocType.SelectedIndex = 0;
    }

    private void LoadMacroFile()
    {
        if (cmbDocType.SelectedItem == null) return;
        string docType = cmbDocType.SelectedItem.ToString()!;
        string fileName = docType switch {
            "DailyPlan" => "DPmacro.md",
            "BOM" => "BOMmacro.md",
            "PartList" => "PartListmacro.md",
            _ => ""
        };

        if (string.IsNullOrEmpty(fileName)) return;

        string path = Path.Combine(_macroRoot, fileName);
        if (File.Exists(path))
        {
            txtMacroEditor.Text = File.ReadAllText(path);
        }
        else
        {
            txtMacroEditor.Text = $"# {docType} 가공 로직\n\n(내용을 입력하세요)";
        }
    }

    private void SaveMacroFile()
    {
        if (cmbDocType.SelectedItem == null) return;
        string docType = cmbDocType.SelectedItem.ToString()!;
        string fileName = docType switch {
            "DailyPlan" => "DPmacro.md",
            "BOM" => "BOMmacro.md",
            "PartList" => "PartListmacro.md",
            _ => ""
        };

        if (string.IsNullOrEmpty(fileName)) return;

        try
        {
            string path = Path.Combine(_macroRoot, fileName);
            File.WriteAllText(path, txtMacroEditor.Text);
            MessageBox.Show($"{docType} 가공 로직(매크로)이 저장되었습니다.", "저장 완료");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 저장 중 오류 발생: {ex.Message}", "오류");
        }
    }

    private void BtnTempClean_Click(object? sender, EventArgs e)
    {
        var cfg = ConfigManager.Instance;
        
        if (MessageBox.Show($"Are you sure you want to delete ALL files in:\n\nImport: {cfg.DebugImportPath}\nExport: {cfg.DebugExportPath}", 
            "Confirm Cleaning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            CleanDirectory(cfg.DebugImportPath);
            CleanDirectory(cfg.DebugExportPath);
            MessageBox.Show("Debug folders cleaned successfully.", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during cleaning: {ex.Message}", "Error");
        }
    }

    private void CleanDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try 
            {
                // Delete the entire directory including root
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                // Retry or fallback might be needed if file is in use, 
                // but usually Delete(true) is what is expected for 'Cleaning'.
                // If it fails, we might try to just clear contents as fallback, 
                // but let's stick to the user's likely intent of a full wipe.
                throw; 
            }
        }
        // Re-create empty
        Directory.CreateDirectory(path);
    }

    private string BrowseFolder(string current)
    {
        using (var fbd = new FolderBrowserDialog())
        {
            if(!string.IsNullOrEmpty(current) && Directory.Exists(current)) fbd.SelectedPath = current;
            if (fbd.ShowDialog() == DialogResult.OK) return fbd.SelectedPath;
        }
        return current;
    }

    private void LoadSettings()
    {
        var cfg = ConfigManager.Instance;
        
        txtGlobalImport.Text = cfg.GlobalImportPath;
        txtGlobalExport.Text = cfg.GlobalExportPath;

        chkDebugMode.Checked = cfg.IsDebugMode;
        txtDebugImport.Text = cfg.DebugImportPath;
        txtDebugExport.Text = cfg.DebugExportPath;
        
        grpDebugPaths.Enabled = chkDebugMode.Checked;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var cfg = ConfigManager.Instance;

        cfg.GlobalImportPath = txtGlobalImport.Text;
        cfg.GlobalExportPath = txtGlobalExport.Text;

        cfg.IsDebugMode = chkDebugMode.Checked;
        cfg.DebugImportPath = txtDebugImport.Text;
        cfg.DebugExportPath = txtDebugExport.Text;

        ConfigManager.Save();
        
        // Ensure directories exist
        try {
            if (!Directory.Exists(cfg.GlobalImportPath)) Directory.CreateDirectory(cfg.GlobalImportPath);
            if (!Directory.Exists(cfg.GlobalExportPath)) Directory.CreateDirectory(cfg.GlobalExportPath);
             // Debug paths only if mode is on? Or always?
            if (cfg.IsDebugMode)
            {
                if (!Directory.Exists(cfg.DebugImportPath)) Directory.CreateDirectory(cfg.DebugImportPath);
                if (!Directory.Exists(cfg.DebugExportPath)) Directory.CreateDirectory(cfg.DebugExportPath);
            }
        } catch { }

        MessageBox.Show("설정이 저장되었습니다.\n(경로가 존재하지 않으면 생성됩니다.)", "저장 완료");
    }
}
