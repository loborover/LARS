using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace LARS.ENGINE;

public enum SupportedFileType
{
    Unknown,
    BOM,
    DailyPlan,
    PartList
}

public static class FileClassifier
{
    public static SupportedFileType Classify(string filePath)
    {
        if (!File.Exists(filePath)) return SupportedFileType.Unknown;

        string fileName = Path.GetFileName(filePath);

        // 1. Fast Check: Filename
        if (fileName.Contains("CVZ", StringComparison.OrdinalIgnoreCase)) return SupportedFileType.BOM;
        // User might have specific naming conventions not fully known, but we know CVZ is usually BOM.

        // 2. Slow Check: Content Header Inspection
        try
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null) return SupportedFileType.Unknown;

                // Inspect first few rows for keywords
                var searchRange = ws.Range(1, 1, 10, 20); // Top-left 10x20 block
                var cellValues = searchRange.CellsUsed().Select(c => c.GetValue<string>().Trim()).ToHashSet();

                if (cellValues.Contains("Planned Start Time")) return SupportedFileType.DailyPlan;
                if (cellValues.Contains("Part No") && cellValues.Contains("Lvl")) return SupportedFileType.BOM;
                if (cellValues.Contains("Part List") || cellValues.Contains("Material No")) return SupportedFileType.PartList;
            }
        }
        catch 
        { 
            // File might be open or invalid
            return SupportedFileType.Unknown; 
        }

        return SupportedFileType.Unknown;
    }
}
