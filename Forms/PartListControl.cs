using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using LARS.ENGINE;
using LARS.Models;
using LARS.UI.Controls;

namespace LARS.Forms;

public partial class PartListControl : BaseViewerControl
{
    private List<string> _detectedFiles = new();

    public PartListControl()
    {
        this.Load += (s, e) => ScanImportFolder();

        BtnRefresh.Click += (s,e) => ScanImportFolder();
        BtnDelete.Click += BtnDelete_Click;
        BtnProcess.Click += (s,e) => MessageBox.Show("Not Implemented", "Info");
        BtnSettings.Click += (s, e) => {
            if (new ViewerSettingsForm(ViewerType.PartList).ShowDialog() == DialogResult.OK)
            {
                if (LstRawFiles.SelectedIndex >= 0) LstRawFiles_SelectedIndexChanged(null, EventArgs.Empty);
            }
        };
        
        LstRawFiles.SelectedIndexChanged += LstRawFiles_SelectedIndexChanged;
    }

    private void ScanImportFolder()
    {
        LstRawFiles.Items.Clear();
        _detectedFiles.Clear();
        MetaPropertyGrid.SelectedObject = null;
        PreviewGrid.DataSource = null;

        string path = LARS.Configuration.ConfigManager.GetImportPath();
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        var allFiles = Directory.GetFiles(path, "*.xlsx").Concat(Directory.GetFiles(path, "*.xls"));

        foreach (var file in allFiles)
        {
            if (FileClassifier.Classify(file) == SupportedFileType.PartList)
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
             MetaPropertyGrid.SelectedObject = new FileMetadata { Name = fi.Name, SizeKB = fi.Length/1024, Created = fi.CreationTime, Modified = fi.LastWriteTime, Directory = fi.DirectoryName };
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var checkedItems = LstRawFiles.CheckedItems;
        if (checkedItems.Count == 0) return;
        if (MessageBox.Show($"Delete {checkedItems.Count} files?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            foreach (var item in checkedItems) {
                string fullPath = _detectedFiles.FirstOrDefault(f => Path.GetFileName(f) == item.ToString()) ?? "";
                if(File.Exists(fullPath)) try{ File.Delete(fullPath); } catch{}
            }
            ScanImportFolder();
        }
    }
}
