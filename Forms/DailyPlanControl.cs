using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LARS.ENGINE;
using LARS.ENGINE.Documents.DailyPlan;
using LARS.Models;
using LARS.UI.Controls;

namespace LARS.Forms;

public partial class DailyPlanControl : BaseViewerControl
{
    private readonly DailyPlanProcessor _processor;
    private List<string> _detectedFiles = new();

    private DailyPlanCanvas _canvas;
    
    // Add Print Button reference (assuming it exists in BaseViewer or we add it dynamically)
    private Button _btnPrint;

    public DailyPlanControl()
    {
        _processor = new DailyPlanProcessor(); 
        
        // Setup Canvas (replace or place next to MetaPropertyGrid/PreviewGrid)
        // Adjust layout: SplitContainer (List vs Right Panel). Right Panel: Split (Preview vs Meta).
        // For simplicity, we add Canvas to the DataPreviewPanel (where PreviewGrid usually is).
        
        _canvas = new DailyPlanCanvas { Dock = DockStyle.Fill };
        PreviewGrid.Parent.Controls.Add(_canvas);
        PreviewGrid.Visible = false; // Hide grid, prefer Canvas
        _canvas.BringToFront();

        // Add Print Button dynamically if not present in Designer
        _btnPrint = new Button { Text = "Print / PDF", Size = new Size(100, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _btnPrint.Location = new Point(BtnProcess.Location.X - 110, BtnProcess.Location.Y);
        BtnProcess.Parent.Controls.Add(_btnPrint);
        _btnPrint.Click += BtnPrint_Click;

        this.Load += (s, e) => ScanImportFolder();
        
        BtnRefresh.Click += (s, e) => ScanImportFolder();
        BtnDelete.Click += BtnDelete_Click;
        BtnProcess.Click += BtnProcess_Click;
        BtnSettings.Click += (s, e) => new ViewerSettingsForm(ViewerType.DailyPlan).ShowDialog();

        LstRawFiles.SelectedIndexChanged += LstRawFiles_SelectedIndexChanged;
    }

    private void ScanImportFolder()
    {
        LstRawFiles.Items.Clear();
        _detectedFiles.Clear();
        MetaPropertyGrid.SelectedObject = null;
        // PreviewGrid.DataSource = null; 
        
        _canvas.LoadPlan(new ProcessedDailyPlan()); // Clear canvas

        string path = LARS.Configuration.ConfigManager.GetImportPath();
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        var allFiles = Directory.GetFiles(path, "*.xlsx").Concat(Directory.GetFiles(path, "*.xls"));

        foreach (var file in allFiles)
        {
            if (FileClassifier.Classify(file) == SupportedFileType.DailyPlan)
            {
                _detectedFiles.Add(file);
                LstRawFiles.Items.Add(Path.GetFileName(file));
            }
        }
        LblListTitle.Text = $"Files ({_detectedFiles.Count})";
    }

    private void LstRawFiles_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (LstRawFiles.SelectedIndex == -1) return;
        string fileName = LstRawFiles.Items[LstRawFiles.SelectedIndex].ToString()!;
        string fullPath = _detectedFiles.FirstOrDefault(f => Path.GetFileName(f) == fileName) ?? "";

        if (File.Exists(fullPath))
        {
             var fi = new FileInfo(fullPath);
             MetaPropertyGrid.SelectedObject = new FileMetadata
             {
                Name = fi.Name,
                SizeKB = fi.Length / 1024,
                Created = fi.CreationTime,
                Modified = fi.LastWriteTime,
                Directory = fi.DirectoryName
             };
             
             // GDI+ Preview Load
             try 
             {
                 var plan = _processor.GetProcessedPlan(fullPath);
                 _canvas.LoadPlan(plan);
                 _currentPlan = plan; // Store for printing
             }
             catch (Exception ex)
             {
                 MessageBox.Show($"Preview Error: {ex.Message}");
             }
        }
    }
    
    private ProcessedDailyPlan _currentPlan;

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (_currentPlan == null) 
        {
            MessageBox.Show("Please select a file to print.");
            return;
        }
        
        try 
        {
            var exporter = new DailyPlanPdfExporter(_currentPlan);
            exporter.Print($"DailyPlan_{_currentPlan.DateTitle}");
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Print Error: {ex.Message}");
        }
    }

    private void BtnProcess_Click(object? sender, EventArgs e)
    {
        var checkedItems = LstRawFiles.CheckedItems;
        if (checkedItems.Count == 0)
        {
             if (LstRawFiles.SelectedIndex != -1 && MessageBox.Show("Process currently selected item?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
             {
                 string fileName = LstRawFiles.Items[LstRawFiles.SelectedIndex].ToString()!;
                 ProcessSingleFile(_detectedFiles.First(f => Path.GetFileName(f) == fileName));
                 return;
             }
             return;
        }

        if (checkedItems.Count == 1)
        {
             string fileName = checkedItems[0]!.ToString()!;
             ProcessSingleFile(_detectedFiles.First(f => Path.GetFileName(f) == fileName));
        }
        else
        {
            ProcessMultipleFiles(checkedItems.Cast<string>().ToList());
        }
    }

    private void ProcessSingleFile(string path)
    {
        try {
            string exportDir = LARS.Configuration.ConfigManager.GetExportPath(LARS.Configuration.ConfigManager.Instance.DailyPlanExportDir);
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
            
            // Pass Directory, let Processor decide filename
            _processor.ProcessSingle(path, exportDir); 
            MessageBox.Show($"Processed successfully. Saved to: {exportDir}", "Success");
        } catch(Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void ProcessMultipleFiles(List<string> fileNames)
    {
        var exportDir = LARS.Configuration.ConfigManager.GetExportPath(LARS.Configuration.ConfigManager.Instance.DailyPlanExportDir);
        var files = _detectedFiles.Where(f => fileNames.Contains(Path.GetFileName(f))).ToList();
        var results = new ConcurrentBag<string>();
        Parallel.ForEach(files, (path) => {
             try {
                _processor.ProcessSingle(path, exportDir);
                results.Add("[OK] " + Path.GetFileName(path));
             } catch(Exception ex) { results.Add("[FAIL] " + Path.GetFileName(path) + ": " + ex.Message); }
        });
        MessageBox.Show(string.Join("\n", results), "Batch Report");
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var checkedItems = LstRawFiles.CheckedItems;
        if (checkedItems.Count == 0) return;
        if (MessageBox.Show($"Delete {checkedItems.Count} files?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            foreach (var item in checkedItems)
            {
                string fullPath = _detectedFiles.FirstOrDefault(f => Path.GetFileName(f) == item.ToString()) ?? "";
                if(File.Exists(fullPath)) try{ File.Delete(fullPath); } catch{}
            }
            ScanImportFolder();
        }
    }
}
