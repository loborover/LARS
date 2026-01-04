using ClosedXML.Excel;
using System.Diagnostics;
using LARS.Models;

namespace LARS.ENGINE.Documents.BOM;

public class BOMProcessor
{
    private readonly string _sourceDirectory;

    private readonly List<string> _columnsForReport = new()
    {
        "Lvl", "Part No", "Description", "Qty", "UOM", "Maker", "Supply Type"
    };

    public BOMProcessor(string sourceDirectory = "")
    {
        _sourceDirectory = sourceDirectory;
    }

    public IEnumerable<string> FindBOMFiles()
    {
        if (!Directory.Exists(_sourceDirectory))
            yield break;

        var files = Directory.GetFiles(_sourceDirectory, "*.xlsx", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            if (Path.GetFileName(file).Contains("CVZ", StringComparison.OrdinalIgnoreCase) || 
                Path.GetFileName(file).Contains("Excel_Export_", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    public List<BomItem> LoadBOM(string filePath)
    {
        var items = new List<BomItem>();
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.Worksheet(1);
                var headerRow = ws.Row(1);
                var lastRow = ws.LastRowUsed().RowNumber();

                var colMap = new Dictionary<string, int>();
                foreach (var cell in headerRow.CellsUsed())
                {
                    colMap[cell.GetValue<string>()] = cell.Address.ColumnNumber; // Property on Address
                }

                bool HasCol(string name) => colMap.ContainsKey(name);

                for (int i = 2; i <= lastRow; i++)
                {
                    var row = ws.Row(i);
                    if (HasCol("Part No") && row.Cell(colMap["Part No"]).IsEmpty()) continue;

                    var item = new BomItem
                    {
                        Level = HasCol("Lvl") ? row.Cell(colMap["Lvl"]).GetValue<string>().Trim() : "",
                        PartNo = HasCol("Part No") ? row.Cell(colMap["Part No"]).GetValue<string>().Trim() : "",
                        Description = HasCol("Description") ? row.Cell(colMap["Description"]).GetValue<string>().Trim() : "",
                        Quantity = HasCol("Qty") ? GetDouble(row.Cell(colMap["Qty"])) : 0,
                        Uom = HasCol("UOM") ? row.Cell(colMap["UOM"]).GetValue<string>().Trim() : "",
                        Maker = HasCol("Maker") ? row.Cell(colMap["Maker"]).GetValue<string>().Trim() : "",
                        SupplyType = HasCol("Supply Type") ? row.Cell(colMap["Supply Type"]).GetValue<string>().Trim() : ""
                    };
                    items.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading BOM: {ex.Message}");
        }
        return items;
    }
    
    private double GetDouble(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number) return cell.GetValue<double>();
        if (double.TryParse(cell.GetValue<string>(), out double val)) return val;
        return 0;
    }

    public void ProcessSingle(string filePath, string? outputPath = null, IEnumerable<BomItem>? filterItems = null)
    {
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.Worksheet(1);
                
                // 1. Filter Rows based on View (If provided)
                if (filterItems != null)
                {
                    ApplyItemFilter(ws, filterItems);
                }

                FilterColumns(ws);
                InsertTitleRows(ws, 3);
                AutoTitle(ws);
                ApplyAutoFilter(ws);
                ApplyStyles(ws);

                string savePath = outputPath ?? Path.ChangeExtension(filePath, "_Processed.xlsx");
                workbook.SaveAs(savePath);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to process BOM: {ex.Message}", ex);
        }
    }

    private void ApplyItemFilter(IXLWorksheet ws, IEnumerable<BomItem> items)
    {
        // Debug: Check items count
        var itemList = items.ToList();
        if (itemList.Count == 0) 
        {
            // If filter list is empty, it means we should delete everything? 
            // Or did something go wrong? 
            // Let's assume strict filtering.
        }

        // Build a content-based hash for robust matching
        // Using PartNo + Level + Description as unique key
        // Added Trim() for safety
        var validKeys = new HashSet<string>(itemList.Select(x => 
            $"{x.Level.Trim()}|{x.PartNo.Trim()}|{x.Description.Trim()}"
        ));

        var headerRow = ws.Row(1);
        var colMap = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            colMap[cell.GetValue<string>()] = cell.Address.ColumnNumber;
        }

        if (!colMap.ContainsKey("Part No")) return; 
        
        var lastRow = ws.LastRowUsed().RowNumber();
        
        // Delete from bottom to top
        for (int i = lastRow; i >= 2; i--)
        {
            var row = ws.Row(i);
            string lvl = colMap.ContainsKey("Lvl") ? row.Cell(colMap["Lvl"]).GetValue<string>().Trim() : "";
            string part = colMap.ContainsKey("Part No") ? row.Cell(colMap["Part No"]).GetValue<string>().Trim() : "";
            string desc = colMap.ContainsKey("Description") ? row.Cell(colMap["Description"]).GetValue<string>().Trim() : "";
            
            string key = $"{lvl}|{part}|{desc}";

            if (!validKeys.Contains(key))
            {
                row.Delete();
            }
        }
    }

    private void FilterColumns(IXLWorksheet ws)
    {
        var headerRow = ws.Row(1);
        int lastCol = headerRow.LastCellUsed().Address.ColumnNumber; // Property on Address

        for (int i = lastCol; i >= 1; i--)
        {
            var cellValue = headerRow.Cell(i).GetValue<string>();
            if (!_columnsForReport.Contains(cellValue))
            {
                ws.Column(i).Delete();
            }
        }
    }

    private void InsertTitleRows(IXLWorksheet ws, int count)
    {
        ws.Row(1).InsertRowsAbove(count);
    }

    private void AutoTitle(IXLWorksheet ws)
    {
        var headerRow = ws.Row(4);
        var lvlCell = headerRow.CellsUsed().FirstOrDefault(c => c.GetValue<string>() == "Lvl");
        var partNoCell = headerRow.CellsUsed().FirstOrDefault(c => c.GetValue<string>() == "Part No");

        if (lvlCell == null || partNoCell == null) return;

        var lvlColIdx = lvlCell.Address.ColumnNumber;
        var partNoColIdx = partNoCell.Address.ColumnNumber;
        
        var zeroRow = ws.Column(lvlColIdx).CellsUsed()
                        .FirstOrDefault(c => c.GetValue<string>() == "0")?.Address.RowNumber;

        string title = "BOM Report";
        if (zeroRow.HasValue)
        {
            title = ws.Cell(zeroRow.Value, partNoColIdx).GetValue<string>();
            int atIndex = title.IndexOf('@');
            if (atIndex > 0)
            {
                title = title.Substring(0, atIndex);
            }
        }

        var titleCell = ws.Cell(1, 1);
        titleCell.Value = title;
        
        int lastCol = ws.LastColumnUsed().ColumnNumber(); // Method on cell/row/col? 
        // Wait, IXLColumn.ColumnNumber() is a method. IXLAddress.ColumnNumber is property.
        // ws.LastColumnUsed() returns IXLColumn? No, IXLCell/IXLRange/IXLColumn...
        // LastColumnUsed() returns IXLCell usually? No, IXLColumn? 
        // Docs say LastColumnUsed() returns IXLColumn.
        // So .ColumnNumber() is correct.
        
        var titleRange = ws.Range(1, 1, 3, lastCol);
        titleRange.Merge();
        
        titleCell.Style.Font.FontName = "LG Smart_H Bold";
        titleCell.Style.Font.FontSize = 25;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private void ApplyAutoFilter(IXLWorksheet ws)
    {
        var lastCol = ws.LastColumnUsed().ColumnNumber();
        var lastRow = ws.LastRowUsed().RowNumber();

        var dataRange = ws.Range(4, 1, lastRow, lastCol);
        dataRange.SetAutoFilter();
    }

    private void ApplyStyles(IXLWorksheet ws)
    {
        var lastCol = ws.LastColumnUsed().ColumnNumber();
        var lastRow = ws.LastRowUsed().RowNumber();
        var tableRange = ws.Range(4, 1, lastRow, lastCol);

        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.OutsideBorderColor = XLColor.Black;
        tableRange.Style.Border.InsideBorderColor = XLColor.Black;

        SetColWidth(ws, "Lvl", 3.5);
        SetColWidth(ws, "Part No", 20);
        SetColWidth(ws, "Description", 40);
        SetColWidth(ws, "Qty", 5);
        SetColWidth(ws, "UOM", 5);
        SetColWidth(ws, "Maker", 20);
        SetColWidth(ws, "Supply Type", 15);
    }

    private void SetColWidth(IXLWorksheet ws, string headerName, double width)
    {
        var header = ws.Row(4).CellsUsed().FirstOrDefault(c => c.GetValue<string>() == headerName);
        if (header != null)
        {
            ws.Column(header.Address.ColumnNumber).Width = width;
        }
    }
}
