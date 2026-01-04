using ClosedXML.Excel;
using LARS.Models;
using System.Diagnostics;

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
            list.Add(new DailyPlanItem
            {
                FilePath = file,
                Date = File.GetCreationTime(file).ToString("yyyy-MM-dd"), // Simplified
                Line = "Unknown",
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

                // 1. Search for "Planned Start Time" to identify the starting block
                var startCell = ws.Search("Planned Start Time").FirstOrDefault();
                if (startCell == null) throw new Exception("Could not find 'Planned Start Time' anchor.");

                int headerRowIdx = startCell.Address.RowNumber;
                
                // 2. Delete rows above header
                ws.Row(1).Delete();
                ws.Columns("B:D").Delete();

                // Re-find anchor after delete
                startCell = ws.Search("Planned Start Time").FirstOrDefault();
                if (startCell != null) headerRowIdx = startCell.Address.RowNumber;

                // 3. Rename "W/O 계획수량" -> "계획"
                var planCell = ws.Search("W/O 계획수량").FirstOrDefault();
                if (planCell != null)
                {
                    planCell.Value = "계획";
                    int planCol = planCell.Address.ColumnNumber;

                    // 4. Insert IN, OUT columns
                    ws.Column(planCol + 1).InsertColumnsAfter(2);
                    ws.Cell(planCell.Address.RowNumber, planCol + 1).Value = "IN";
                    ws.Cell(planCell.Address.RowNumber, planCol + 2).Value = "OUT";

                    // 5. Insert Connecter columns
                    int dateCol = planCol + 3; 
                    ws.Column(dateCol).InsertColumnsBefore(2);
                    ws.Cell(planCell.Address.RowNumber, dateCol).Value = "Connecter"; 
                    ws.Range(planCell.Address.RowNumber, dateCol, planCell.Address.RowNumber, dateCol + 1).Merge();
                }

                // 6. Formulas (Sum)
                int lastRow = ws.LastRowUsed().RowNumber(); // Method call fixed
                var planColIdx = ws.Search("계획").FirstOrDefault()?.Address.ColumnNumber;
                
                if (planColIdx.HasValue)
                {
                    int sumRow = lastRow + 1;
                    ws.Cell(sumRow, 1).Value = "Total";
                    
                    for(int i = 0; i < 3; i++)
                    {
                        int col = planColIdx.Value + i;
                        var colLetter = ws.Column(col).ColumnLetter();
                        ws.Cell(sumRow, col).FormulaA1 = $"=SUM({colLetter}{headerRowIdx+2}:{colLetter}{lastRow})";
                        ws.Cell(sumRow, col).Style.NumberFormat.Format = "#,##0";
                    }
                }

                // 7. Styles
                var tableRange = ws.RangeUsed();
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                
                string savePath = outputPath ?? Path.ChangeExtension(filePath, "_Processed.xlsx");
                workbook.SaveAs(savePath);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to process DailyPlan: {ex.Message}", ex);
        }
    }
}