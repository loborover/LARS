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

                // --- 2. Refined 4-Step Logic ---
                var config = LARS.Configuration.ConfigManager.Headers.DailyPlan;
                int targetHeaderRowIdx = config.TargetHeaderRow ?? 1; // Default to 1 (VBA style)

                // 1. Row 1 삭제
                ws.Row(1).Delete();
                // Note: If Row 1 is deleted, the original Header Row 2 becomes Row 1.
                // We assume targetHeaderRowIdx is relative to the *initial* file.
                // If the user meant "Header Row is 2 IN THE ORIGINAL FILE", then after Row 1 delete, it becomes Row 1.
                int activeHeaderRow = targetHeaderRowIdx > 0 ? targetHeaderRowIdx - 1 : 1; 
                if (activeHeaderRow < 1) activeHeaderRow = 1;

                // 2-2. 생산라인 파싱 (Before column deletion)
                var headerRow = ws.Row(activeHeaderRow);
                string lineName = "UnknownLine";
                var lineCol = headerRow.CellsUsed().FirstOrDefault(c => c.GetValue<string>().Contains("생산라인"));
                if (lineCol != null)
                {
                    int lineColIdx = lineCol.Address.ColumnNumber;
                    // Find first non-empty data row in this column
                    lineName = ws.Column(lineColIdx).CellsUsed()
                                 .SkipWhile(c => c.Address.RowNumber <= activeHeaderRow)
                                 .FirstOrDefault()?.GetValue<string>() ?? "UnknownLine";
                }

                // 2. 컬럼 필터링 & 3. 이름 변경 & 재배치
                int currentLastCol = ws.LastColumnUsed().ColumnNumber();
                
                // Identify target columns and their desired names/order
                var targetMappings = config.Mappings
                    .Where(m => !string.IsNullOrEmpty(m.Target))
                    .OrderBy(m => m.Order)
                    .ToList();

                var tempWorkbook = new XLWorkbook();
                var tempWs = tempWorkbook.AddWorksheet("Temp");
                int newColIdx = 1;

                foreach (var mapping in targetMappings)
                {
                    // Find actual column in original sheet
                    var foundColCell = ws.Row(activeHeaderRow).CellsUsed()
                        .FirstOrDefault(c => c.GetValue<string>().Equals(mapping.Target, StringComparison.OrdinalIgnoreCase));

                    if (foundColCell != null)
                    {
                        int srcColIdx = foundColCell.Address.ColumnNumber;
                        // Copy entire column to temp sheet
                        ws.Column(srcColIdx).CopyTo(tempWs.Column(newColIdx));
                        
                        // Apply Width (px conversion: approx 1 unit = 7px)
                        if (mapping.Width > 0) tempWs.Column(newColIdx).Width = mapping.Width / 7.0;

                        // Rename Header in temp sheet
                        string newName = string.IsNullOrWhiteSpace(mapping.UserSet) ? mapping.Target : mapping.UserSet;
                        tempWs.Cell(activeHeaderRow, newColIdx).Value = newName;
                        newColIdx++;
                    }
                }

                // Clear original worksheet columns (Delete from right to left to avoid index issues)
                for (int c = currentLastCol; c >= 1; c--) ws.Column(c).Delete();

                // Paste back from temp sheet
                // Paste back from temp sheet
                if (newColIdx > 1)
                {
                    for (int i = 1; i < newColIdx; i++)
                    {
                        tempWs.Column(i).CopyTo(ws.Column(i));
                    }
                }
                
                // 1. Autofit all first
                ws.Columns().AdjustToContents();
                
                // 2. Override with manual widths from mappings ONLY IF > 0
                int actualColIdx = 1;
                foreach (var mapping in targetMappings)
                {
                    var found = ws.Row(activeHeaderRow).CellsUsed()
                        .FirstOrDefault(c => c.GetValue<string>().Equals(string.IsNullOrWhiteSpace(mapping.UserSet) ? mapping.Target : mapping.UserSet, StringComparison.OrdinalIgnoreCase));
                    
                    if (found != null)
                    {
                        if (mapping.Width > 0)
                        {
                            ws.Column(found.Address.ColumnNumber).Width = mapping.Width / 7.0;
                        }
                        actualColIdx++;
                    }
                }

                tempWorkbook.Dispose();

                // 4. Connecter 병합셀 생성
                // Row 2 (activeHeaderRow + 1 usually) 에서 "OUT" 찾기
                int searchRow = activeHeaderRow + 1;
                var outCell = ws.Row(searchRow).CellsUsed().FirstOrDefault(c => c.GetValue<string>() == "OUT");
                if (outCell != null)
                {
                    int outColIdx = outCell.Address.ColumnNumber;
                    ws.Column(outColIdx + 1).InsertColumnsAfter(2);
                    
                    int connStartCol = outColIdx + 1;
                    int connEndCol = outColIdx + 2;
                    
                    ws.Cell(activeHeaderRow, connStartCol).Value = "Connecter";
                    ws.Range(activeHeaderRow, connStartCol, searchRow, connEndCol).Merge();
                    ws.Cell(activeHeaderRow, connStartCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(activeHeaderRow, connStartCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

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
            
            // --- 2. Refined 4-Step Logic ---
            var config = LARS.Configuration.ConfigManager.Headers.DailyPlan;
            int targetHeaderRowIdx = config.TargetHeaderRow ?? 1;

            // 1. Row 1 삭제
            ws.Row(1).Delete();
            int activeHeaderRow = targetHeaderRowIdx > 0 ? targetHeaderRowIdx - 1 : 1; 
            if (activeHeaderRow < 1) activeHeaderRow = 1;
            
            // 2-2. 생산라인 (Preview meta)
            var headerRow = ws.Row(activeHeaderRow);
            var lineCol = headerRow.CellsUsed().FirstOrDefault(c => c.GetValue<string>().Contains("생산라인"));
            if (lineCol != null)
            {
                plan.LineName = ws.Column(lineCol.Address.ColumnNumber).CellsUsed()
                                 .SkipWhile(c => c.Address.RowNumber <= activeHeaderRow)
                                 .FirstOrDefault()?.GetValue<string>() ?? "DailyPlan";
            }

            // 2. 컬럼 필터링 & 3. 이름 변경 & 재배치
            int currentLastCol = ws.LastColumnUsed().ColumnNumber();
            
            var targetMappings = config.Mappings
                .Where(m => !string.IsNullOrEmpty(m.Target))
                .OrderBy(m => m.Order)
                .ToList();

            var tempWorkbook = new XLWorkbook();
            var tempWs = tempWorkbook.AddWorksheet("Temp");
            int newColIdx = 1;

            foreach (var mapping in targetMappings)
            {
                var foundColCell = ws.Row(activeHeaderRow).CellsUsed()
                    .FirstOrDefault(c => c.GetValue<string>().Equals(mapping.Target, StringComparison.OrdinalIgnoreCase));

                if (foundColCell != null)
                {
                    ws.Column(foundColCell.Address.ColumnNumber).CopyTo(tempWs.Column(newColIdx));
                    
                    // Width is ignored in Preview (Always Autofit)
                    
                    string newName = string.IsNullOrWhiteSpace(mapping.UserSet) ? mapping.Target : mapping.UserSet;
                    tempWs.Cell(activeHeaderRow, newColIdx).Value = newName;
                    newColIdx++;
                }
            }

            for (int c = currentLastCol; c >= 1; c--) ws.Column(c).Delete();
            if (newColIdx > 1)
            {
                for (int i = 1; i < newColIdx; i++)
                {
                    tempWs.Column(i).CopyTo(ws.Column(i));
                }
            }
            
            // Preview Exception: Always Autofit all columns, ignore manual Mapping Width
            ws.Columns().AdjustToContents();

            tempWorkbook.Dispose();

            // 4. Connecter (Preview adjustment)
            int searchRow = activeHeaderRow + 1;
            var outCell = ws.Row(searchRow).CellsUsed().FirstOrDefault(c => c.GetValue<string>() == "OUT");
            if (outCell != null)
            {
                int outColIdx = outCell.Address.ColumnNumber;
                ws.Column(outColIdx + 1).InsertColumnsAfter(2);
                ws.Cell(activeHeaderRow, outColIdx + 1).Value = "Connecter";
                ws.Range(activeHeaderRow, outColIdx + 1, searchRow, outColIdx + 2).Merge();
            }

            plan.DateTitle = File.GetCreationTime(filePath).ToString("yyyy-MM-dd");

            // Extract Headers (After filtering)
            int lastCol = ws.LastColumnUsed().ColumnNumber();
            for (int c = 1; c <= lastCol; c++)
            {
                plan.Headers.Add(ws.Cell(activeHeaderRow, c).GetValue<string>());
            }

            // Extract Data
            int dataStartRow = activeHeaderRow + 1; 
            int lastRowIdx = ws.LastRowUsed().RowNumber();
            
            for(int c=1; c<=lastCol; c++) 
                plan.ColumnWidths.Add((float)ws.Column(c).Width * 7.5f);

            for (int r = dataStartRow; r <= lastRowIdx; r++)
            {
                var rowVals = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    rowVals.Add(ws.Cell(r, c).GetValue<string>());
                }
                plan.Rows.Add(rowVals);
            }
        }

        return plan;
    }
}
