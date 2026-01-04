using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LARS.ENGINE;
using LARS.ENGINE.Documents.BOM;
using LARS.Models;
using LARS.UI.Controls;

namespace LARS.Forms;

public partial class BomViewerControl : BaseViewerControl
{
    // Logic
    private readonly BOMProcessor _processor;
    private List<string> _detectedFiles = new();
    private string? _currentPreviewFile;
    private Dictionary<string, List<string>> _columnFilters = new();
    private List<BomItem> _previewItems = new();
    private BindingSource _bindingSource = new();

    public BomViewerControl()
    {
        // No InitializeComponent needed (handled by Base)
        _processor = new BOMProcessor();
        
        this.Load += (s, e) => ScanImportFolder();

        // Wire up Base Events
        BtnRefresh.Click += (s, e) => ScanImportFolder();
        BtnDelete.Click += BtnDelete_Click;
        BtnProcess.Click += BtnProcess_Click;
        BtnSettings.Click += (s, e) => new ViewerSettingsForm(ViewerType.BOM).ShowDialog();

        // Specific Grid Logic
        LstRawFiles.SelectedIndexChanged += LstRawFiles_SelectedIndexChanged;
        PreviewGrid.ColumnHeaderMouseClick += DataGridView_ColumnHeaderMouseClick;
        PreviewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        
        // Row Numbers
        PreviewGrid.RowPostPaint += (s, e) => {
             var grid = s as DataGridView;
             var idx = (e.RowIndex + 1).ToString();
             var fmt = new StringFormat{Alignment=StringAlignment.Center, LineAlignment=StringAlignment.Center};
             var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid?.RowHeadersWidth ?? 50, e.RowBounds.Height);
             e.Graphics.DrawString(idx, this.Font, SystemBrushes.ControlText, bounds, fmt);
        };
    }

    // --- Logic ---
    private void ScanImportFolder()
    {
        LstRawFiles.Items.Clear();
        _detectedFiles.Clear();
        _previewItems.Clear();
        _bindingSource.DataSource = null;
        MetaPropertyGrid.SelectedObject = null;

        string path = LARS.Configuration.ConfigManager.GetImportPath();
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        var allFiles = Directory.GetFiles(path, "*.xlsx").Concat(Directory.GetFiles(path, "*.xls"));

        foreach (var file in allFiles)
        {
            if (FileClassifier.Classify(file) == SupportedFileType.BOM)
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
            // Update Metadata
            var fi = new FileInfo(fullPath);
            MetaPropertyGrid.SelectedObject = new FileMetadata
            {
                Name = fi.Name,
                SizeKB = fi.Length / 1024,
                Created = fi.CreationTime,
                Modified = fi.LastWriteTime,
                Directory = fi.DirectoryName
            };

            // Update Preview if changed
            if (_currentPreviewFile != fullPath)
            {
                LoadPreview(fullPath);
            }
        }
    }

    private void LoadPreview(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            _currentPreviewFile = path;
            _previewItems = _processor.LoadBOM(path);
            _columnFilters.Clear();

            _bindingSource.DataSource = _previewItems;
            PreviewGrid.DataSource = _bindingSource;
            
            ApplyDefaultLevelFilter();
            PreviewGrid.AutoResizeColumns();
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Preview Error: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BtnProcess_Click(object? sender, EventArgs e)
    {
        var checkedItems = LstRawFiles.CheckedItems;
        if (checkedItems.Count == 0)
        {
             if (LstRawFiles.SelectedIndex != -1 && MessageBox.Show("No items checked. Process currently previewed item?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
             {
                 ProcessSingleFile(_currentPreviewFile!);
                 return;
             }
             MessageBox.Show("Please check files to process.", "Warning");
             return;
        }

        if (checkedItems.Count == 1)
        {
            string fileName = checkedItems[0]!.ToString()!;
            string fullPath = _detectedFiles.First(f => Path.GetFileName(f) == fileName);
            ProcessSingleFile(fullPath);
        }
        else
        {
            ProcessMultipleFiles(checkedItems.Cast<string>().ToList());
        }
    }

    private void ProcessSingleFile(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            IEnumerable<BomItem>? filters = null;

            if (path == _currentPreviewFile)
            {
                filters = _bindingSource.List.Cast<BomItem>();
            }
            else
            {
                var items = _processor.LoadBOM(path);
                filters = items.Where(x => x.Level == "0" || x.Level == ".1" || x.Level.Contains("S"));
            }

            string exportDir = LARS.Configuration.ConfigManager.GetExportPath(LARS.Configuration.ConfigManager.Instance.BomExportDir);
            string outName = Path.GetFileNameWithoutExtension(path) + "_Processed.xlsx";
            string outPath = Path.Combine(exportDir, outName);

            _processor.ProcessSingle(path, outPath, filters);
            MessageBox.Show($"Processed: {outName}", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
        }
        finally { Cursor = Cursors.Default; }
    }

    private void ProcessMultipleFiles(List<string> fileNames)
    {
        var exportDir = LARS.Configuration.ConfigManager.GetExportPath(LARS.Configuration.ConfigManager.Instance.BomExportDir);
        var filesToProcess = _detectedFiles.Where(f => fileNames.Contains(Path.GetFileName(f))).ToList();
        
        var results = new ConcurrentBag<string>();
        
        Cursor = Cursors.WaitCursor;
        Parallel.ForEach(filesToProcess, (path) => 
        {
            try
            {
                var items = _processor.LoadBOM(path);
                var validItems = items.Where(x => x.Level == "0" || x.Level == ".1" || x.Level.Contains("S"));
                
                string outName = Path.GetFileNameWithoutExtension(path) + "_Processed.xlsx";
                string outPath = Path.Combine(exportDir, outName);
                
                _processor.ProcessSingle(path, outPath, validItems);
                results.Add($"[OK] {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                results.Add($"[FAIL] {Path.GetFileName(path)}: {ex.Message}");
            }
        });
        Cursor = Cursors.Default;

        MessageBox.Show($"Batch Processing Complete.\n\n{string.Join("\n", results)}", "Report");
    }
    
    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var checkedItems = LstRawFiles.CheckedItems;
        if (checkedItems.Count == 0) return;

        if (MessageBox.Show($"Delete {checkedItems.Count} files?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            foreach (var item in checkedItems)
            {
                string fullPath = _detectedFiles.FirstOrDefault(f => Path.GetFileName(f) == item.ToString()) ?? "";
                if (File.Exists(fullPath)) try { File.Delete(fullPath); } catch {}
            }
            ScanImportFolder();
        }
    }

    private void ApplyDefaultLevelFilter()
    {
         if (_previewItems == null) return;
         var allLevels = _previewItems.Select(x => x.Level).Distinct().ToList();
         var defaultSelected = allLevels.Where(l => l == "0" || l == ".1" || l.Contains("S")).ToList();
         if (defaultSelected.Count > 0)
         {
             _columnFilters["Level"] = defaultSelected;
             ApplyGlobalFilter();
         }
    }

    private void ApplyGlobalFilter()
    {
        var filtered = _previewItems.AsEnumerable();
        foreach (var filter in _columnFilters)
        {
            filtered = filtered.Where(item => 
            {
                 var prop = typeof(BomItem).GetProperty(filter.Key);
                 var val = prop?.GetValue(item)?.ToString() ?? "";
                 return filter.Value.Contains(val);
            });
        }
        _bindingSource.DataSource = filtered.ToList();
        PreviewGrid.DataSource = _bindingSource;
    }

    private void DataGridView_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex == -1)
        {
            var column = PreviewGrid.Columns[e.ColumnIndex];
            string propName = column.DataPropertyName;
            var distinctValues = _previewItems.Select(x => 
            {
                var p = typeof(BomItem).GetProperty(propName);
                return p?.GetValue(x)?.ToString() ?? "";
            }).Distinct().OrderBy(x=>x).ToList();
            
            List<string> currentFilter = _columnFilters.ContainsKey(propName) ? _columnFilters[propName] : new List<string>(distinctValues);

            using (var popup = new FilterPopupForm(distinctValues, currentFilter))
            {
                var headerRect = PreviewGrid.GetCellDisplayRectangle(e.ColumnIndex, -1, true);
                var screenPoint = PreviewGrid.PointToScreen(new Point(headerRect.Right - popup.Width, headerRect.Bottom));
                popup.Location = screenPoint;
                if (popup.ShowDialog() == DialogResult.OK)
                {
                     _columnFilters[propName] = popup.SelectedValues;
                     ApplyGlobalFilter();
                     if (distinctValues.Count != popup.SelectedValues.Count) column.HeaderCell.Style.BackColor = Color.LightSkyBlue;
                     else column.HeaderCell.Style.BackColor = Color.Empty;
                }
            }
        }
    }
}
