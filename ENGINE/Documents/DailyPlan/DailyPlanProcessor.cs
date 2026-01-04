using ClosedXML.Excel;
using LARS.Models;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LARS.ENGINE.Documents.DailyPlan;

public class DailyPlanProcessor
{
    private readonly string _sourceDirectory;

    public DailyPlanProcessor(string sourceDirectory = "")
    {
        _sourceDirectory = sourceDirectory;
    }

    public IEnumerable<string> FindDailyPlanFiles()
    {
        if (!Directory.Exists(_sourceDirectory))
            yield break;

        var files = Directory.GetFiles(_sourceDirectory, "Excel_Export_*.xlsx", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            yield return file;
        }
    }

    public List<DailyPlanItem> LoadDailyPlans(string sourceDir = "")
    {
        if (string.IsNullOrEmpty(sourceDir)) sourceDir = _sourceDirectory;

        var list = new List<DailyPlanItem>();
        if (!Directory.Exists(sourceDir)) return list;

        foreach (var file in FindDailyPlanFiles())
        {
            var creation = File.GetCreationTime(file);
            list.Add(new DailyPlanItem
            {
                FilePath = file,
                Date = creation.ToString("yyyy-MM-dd"), // Simplified
                Line = "Parsing...", // Will be updated on Process
                PrintStatus = "Ready"
            });
        }
        return list;
    }

    /// <summary>
    /// Processes a single Daily Plan file.
    /// (일일 계획표 파일을 처리합니다.)
    /// </summary>
    public void ProcessSingle(string filePath, string? outputPath = null)
    {
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.Worksheet(1);

                // --- 1. Pre-Processing & Filtering ---
                
                // Find "Planned Start Time" anchor
                var startCell = ws.Search("Planned Start Time").FirstOrDefault();
                if (startCell == null) throw new Exception("Could not find 'Planned Start Time' anchor.");

                int anchorRow = startCell.Address.RowNumber;
                int startRow = anchorRow + 3; // Data starts +3 rows from anchor ('Planned Start Time' -> 'Input' -> 'W/O' -> Data)
                int anchorCol = startCell.Address.ColumnNumber; // Column 'A' usually (Parsed Column)

                // Delete rows 1 to anchorRow (Exclusive of Anchor? No, VBA deletes Rows(1) then Columns B:D)
                // VBA Logic:
                // 1. Find "Planned Start Time" (Range)
                // 2. startRow = DelCell.Row + 3
                // 3. ws.Rows(1).Delete
                // 4. ws.Columns("B:D").Delete (Columns 2,3,4)
                
                ws.Row(1).Delete();
                ws.Column(2).Delete(); // B
                ws.Column(2).Delete(); // C (became B)
                ws.Column(2).Delete(); // D (became B)
                
                // After deletion, "Planned Start Time" and "W/O" headers might have shifted.
                // Re-find key headers
                var planQtyCell = ws.Search("W/O 계획수량").FirstOrDefault();
                if (planQtyCell == null) throw new Exception("'W/O 계획수량' not found.");
                
                // Rename "W/O 계획수량" -> "계획"
                planQtyCell.Value = "계획";
                
                // --- 2. Transformation ---
                
                int planCol = planQtyCell.Address.ColumnNumber;
                int planRow = planQtyCell.Address.RowNumber;
                
                // 4. Insert IN, OUT columns (After '계획')
                ws.Column(planCol + 1).InsertColumnsAfter(2); // planCol is '계획', +1 is next. Insert 2 columns.
                ws.Cell(planRow, planCol + 1).Value = "IN";
                ws.Cell(planRow, planCol + 2).Value = "OUT";
                
                // 5. Insert Connecter columns (After 'OUT', i.e., planCol + 3)
                int connecterCol = planCol + 3;
                ws.Column(connecterCol).InsertColumnsBefore(2); // Insert 2 cols for Connecter
                ws.Cell(planRow, connecterCol).Value = "Connecter";
                ws.Range(planRow, connecterCol, planRow, connecterCol + 1).Merge();
                
                // Find "Line" info for filename
                var lineCell = ws.Search("Line").FirstOrDefault(); // "Line" in Header
                string lineName = lineCell?.CellRight().GetValue<string>() ?? "UnknownLine";
                
                // Identify Date Columns and Delete unused future dates
                // VBA logic searches for "부품번호" (PartNo) to find start of data
                
                // Calculate Durations using TimeKeeper
                int lastDataRow = ws.LastRowUsed().RowNumber();
                
                // Add Meta Data Columns (TPL, UPPH, Duration)
                // VBA adds them at particular offset. 
                // Let's implement simplified Time Calculation first.
                
                // Finding Start/End Time Columns. 
                // Usually Column 1 is "투입 시점" (Input Time) after deletions?
                // Let's verify VBA: "ws.Cells(1, 1).Value = '투입' & vbLf & '시점'"
                ws.Cell(1, 1).Value = "투입" + Environment.NewLine + "시점";
                
                // Duration Calculation Loop
                // Column 20, 21 in VBA (Hardcoded).
                // We should find the last column usage.
                int metaColStart = ws.LastColumnUsed().ColumnNumber() + 2; 
                
                ws.Cell(1, metaColStart).Value = "Duration";
                ws.Cell(1, metaColStart + 1).Value = "UPPH";
                
                // --- 3. Styling ---
                var tableRange = ws.RangeUsed();
                tableRange.Style.Font.FontName = "LG Smart_02.0"; // Or similar
                tableRange.Style.Font.FontSize = 12;
                
                // Borders - Default
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
                tableRange.Style.Border.InsideBorderColor = XLColor.Black;
                tableRange.Style.Border.OutsideBorderColor = XLColor.Black;

                // --- Grouping & Advanced Styling ---
                // Find PartNo Column
                var partNoHeader = ws.Search("부품번호").FirstOrDefault();
                if (partNoHeader != null)
                {
                    int partNoCol = partNoHeader.Address.ColumnNumber;
                    int lastColIdx = ws.LastColumnUsed().ColumnNumber();

                    // Build ModelInfo List
                    var models = new List<ModelInfo>();
                    // Data usually starts at startRow (which was anchorRow + 3? check previous logic)
                    // We need to re-verify start of data. 
                    // Usually below headers. Header is at row 2 approx (after row 1 delete).
                    // Let's assume data starts at row 3 (Header row 2, +1).
                    // Re-checking variable startRow. It was anchorRow + 3. But row 1 deleted.
                    // If anchor was Row 2 (original "Planned Start Time"), deleted Row 1. New Anchor Row = 1.
                    // startRow = 1 + 3 = 4?
                    // Let's just scan from partNoHeader.Row + 1 to lastDataRow.
                    
                    int dataStartRow = partNoHeader.Address.RowNumber + 1;
                    int lastRowIdx = ws.LastRowUsed().RowNumber();

                    for (int r = dataStartRow; r <= lastRowIdx; r++)
                    {
                        string val = ws.Cell(r, partNoCol).GetValue<string>();
                        if (string.IsNullOrWhiteSpace(val)) continue;

                        var m = new ModelInfo(val);
                        m.Row = r;
                        m.Col = partNoCol;
                        // WorkOrder is usually PartNoCol - 1 (W/O)
                        string wo = ws.Cell(r, partNoCol - 1).GetValue<string>();
                        m.WorkOrder = wo;
                        
                        models.Add(m);
                    }

                    if (models.Count > 0)
                    {
                        var grouper = new ModelGrouper();
                        grouper.GroupModels(models);

                        // Apply SubGroup Borders (Thin Top/Bottom)
                        foreach (var sub in grouper.SubGroups)
                        {
                            var rng = ws.Range(sub.StartRow, 1, sub.EndRow, lastColIdx);
                            // Border around SubGroup
                            rng.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                            rng.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                            // Internal Horizontal Hairline is already set by default
                        }

                        // Apply MainGroup Borders (Medium/Thick)
                        foreach (var main in grouper.MainGroups)
                        {
                            var rng = ws.Range(main.StartRow, 1, main.EndRow, lastColIdx);
                            // Border around MainGroup
                            rng.Style.Border.TopBorder = XLBorderStyleValues.Medium; // Visible separation
                            rng.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                            rng.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                            rng.Style.Border.RightBorder = XLBorderStyleValues.Medium;
                        }
                    }
                }

                // Header Color
                var headerRange = ws.Range(1, 1, 2, ws.LastColumnUsed().ColumnNumber()); // Assuming header is first 2 rows
                headerRange.Style.Fill.BackgroundColor = XLColor.FromColor(Color.FromArgb(199, 253, 240));
                
                // --- 4. Saving ---
                string fileName = Path.GetFileName(filePath); // Default fallback
                
                // Try to construct proper filename: DailyPlan [Date]_[Line].xlsx
                // Extract Date from file or cell
                 var fileDate = File.GetCreationTime(filePath).ToString("MM월-dd일");
                 string newFileName = $"DailyPlan {fileDate}_{lineName}.xlsx";
                 
                 // If outputPath is provided and is a Directory, use it with newFileName
                 // If outputPath is a File path (has extension), use it as is.
                 string actualSavePath;
                 if (!string.IsNullOrEmpty(outputPath) && Path.HasExtension(outputPath))
                 {
                     actualSavePath = outputPath;
                 }
                 else
                 {
                     string dir = !string.IsNullOrEmpty(outputPath) ? outputPath : Path.GetDirectoryName(filePath)!;
                     actualSavePath = Path.Combine(dir, newFileName);
                 }

                // Ensure unique name if exists
                if (File.Exists(actualSavePath))
                {
                    actualSavePath = Path.Combine(Path.GetDirectoryName(actualSavePath)!, 
                        Path.GetFileNameWithoutExtension(actualSavePath) + "_" + DateTime.Now.Ticks + ".xlsx");
                }

                workbook.SaveAs(actualSavePath);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to process DailyPlan: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a ProcessedDailyPlan model for visualization (GDI+ / PDF)
    /// without saving an Excel file (or after processing).
    /// Reuse logic from ProcessSingle where possible. 
    /// For this implementation, we duplicate the core logic to keep it independent or refactor if needed.
    /// To ensure consistency, we will copy the transformation logic here.
    /// </summary>
    public ProcessedDailyPlan GetProcessedPlan(string filePath)
    {
        var plan = new ProcessedDailyPlan();

        using (var workbook = new XLWorkbook(filePath))
        {
            var ws = workbook.Worksheets.Worksheet(1);
            
            // --- Logic Mirroring ProcessSingle ---
            // 1. Pre-Processing
            var startCell = ws.Search("Planned Start Time").FirstOrDefault();
            if (startCell == null) return plan; // Or throw

            ws.Row(1).Delete();
            ws.Column(2).Delete();
            ws.Column(2).Delete();
            ws.Column(2).Delete();

            var planQtyCell = ws.Search("W/O 계획수량").FirstOrDefault();
            if (planQtyCell != null)
            {
                planQtyCell.Value = "계획";
                int planCol = planQtyCell.Address.ColumnNumber;
                int planRow = planQtyCell.Address.RowNumber;
                
                ws.Column(planCol + 1).InsertColumnsAfter(2);
                ws.Cell(planRow, planCol + 1).Value = "IN";
                ws.Cell(planRow, planCol + 2).Value = "OUT";
                
                int connecterCol = planCol + 3;
                ws.Column(connecterCol).InsertColumnsBefore(2);
                ws.Cell(planRow, connecterCol).Value = "Connecter";
                ws.Range(planRow, connecterCol, planRow, connecterCol + 1).Merge();
            }

            // Meta Info
            var lineCell = ws.Search("Line").FirstOrDefault();
            plan.LineName = lineCell?.CellRight().GetValue<string>() ?? "Unknown";
            plan.DateTitle = File.GetCreationTime(filePath).ToString("yyyy-MM-dd");

            // Extract Headers
            int headerRow = 2; // Approx
            int lastCol = ws.LastColumnUsed().ColumnNumber();
            for (int c = 1; c <= lastCol; c++)
            {
                plan.Headers.Add(ws.Cell(headerRow, c).GetValue<string>());
            }

            // Extract Data & Grouping
            var partNoHeader = ws.Search("부품번호").FirstOrDefault();
            if (partNoHeader != null)
            {
                int partNoCol = partNoHeader.Address.ColumnNumber;
                int dataStartRow = partNoHeader.Address.RowNumber + 1;
                int lastRowIdx = ws.LastRowUsed().RowNumber();
                var models = new List<ModelInfo>();

                // Column Widths (Approx from Exec)
                for(int c=1; c<=lastCol; c++) 
                    plan.ColumnWidths.Add((float)ws.Column(c).Width * 7.5f); // Conversion factor

                for (int r = dataStartRow; r <= lastRowIdx; r++)
                {
                    var rowVals = new List<string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        rowVals.Add(ws.Cell(r, c).GetValue<string>());
                    }
                    plan.Rows.Add(rowVals);

                    // Model Info for Grouping
                    string val = ws.Cell(r, partNoCol).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var m = new ModelInfo(val);
                        m.Row = r - dataStartRow; // 0-based index for Drawer
                        m.Col = partNoCol;
                        m.WorkOrder = ws.Cell(r, partNoCol - 1).GetValue<string>();
                        models.Add(m);
                    }
                }

                // Grouping
                if (models.Count > 0)
                {
                    var grouper = new ModelGrouper();
                    grouper.GroupModels(models);

                    // Convert GroupRange indices to Data Row indices
                    // ModelInfo.Row is already 0-based relative to data start in this scope
                    // No, wait. 
                    // In ProcessSingle: m.Row = r (absolute).
                    // In GetProcessedPlan: m.Row = r - dataStartRow (0-based relative to Plan.Rows).
                    // This is correct for Drawer.
                    
                    plan.MainGroups = grouper.MainGroups;
                    plan.SubGroups = grouper.SubGroups;
                }
            }
        }

        return plan;
    }
}
