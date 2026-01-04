using ClosedXML.Excel;
using System.IO;

namespace LARS.Utils;

public static class MockDataGenerator
{
    // 테스트용 샘플 BOM 파일 생성
    public static void GenerateSampleBomFile()
    {
        string filePath = Path.Combine(LARS.Configuration.ConfigManager.GetImportPath(), "Excel_Export_Sample_BOM.xlsx");
        
        // 이미 존재하면 생성 안 함 (덮어쓰기 옵션 추가 가능)
        if (File.Exists(filePath)) return;

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");

            // 헤더 작성
            worksheet.Cell(1, 1).Value = "Lvl";
            worksheet.Cell(1, 2).Value = "Part No";
            worksheet.Cell(1, 3).Value = "Description";
            worksheet.Cell(1, 4).Value = "Qty";
            worksheet.Cell(1, 5).Value = "UOM";
            worksheet.Cell(1, 6).Value = "Maker";
            worksheet.Cell(1, 7).Value = "Supply Type";

            // 샘플 데이터 작성
            var data = new[]
            {
                new { Lvl = "1", PartNo = "PN-001", Desc = "Main Board", Qty = 1, Uom = "EA", Maker = "Samsung", Type = "In-house" },
                new { Lvl = ".2", PartNo = "PN-002", Desc = "Resistor 10k", Qty = 5, Uom = "EA", Maker = "Yageo", Type = "Vendor" },
                new { Lvl = ".2", PartNo = "PN-003", Desc = "Capacitor 10uF", Qty = 3, Uom = "EA", Maker = "Murata", Type = "Vendor" },
                new { Lvl = "1", PartNo = "PN-004", Desc = "Casing", Qty = 1, Uom = "EA", Maker = "LG", Type = "In-house" }
            };

            for (int i = 0; i < data.Length; i++)
            {
                int r = i + 2;
                worksheet.Cell(r, 1).Value = data[i].Lvl;
                worksheet.Cell(r, 2).Value = data[i].PartNo;
                worksheet.Cell(r, 3).Value = data[i].Desc;
                worksheet.Cell(r, 4).Value = data[i].Qty;
                worksheet.Cell(r, 5).Value = data[i].Uom;
                worksheet.Cell(r, 6).Value = data[i].Maker;
                worksheet.Cell(r, 7).Value = data[i].Type;
            }

            workbook.SaveAs(filePath);
        }
    }
}
