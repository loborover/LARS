using ClosedXML.Excel;
using System.Data;
using System.Collections.Generic;

namespace LARS.Utils;

public static class ExcelHelper
{
    // 엑셀 파일을 읽어서 DataTable로 반환
    public static DataTable ReadExcelToDataTable(string filePath)
    {
        using (var workbook = new XLWorkbook(filePath))
        {
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();
            if (range == null) return new DataTable();

            var table = range.AsTable();
            var dataTable = new DataTable();

            foreach (var cell in table.HeadersRow().Cells())
            {
                dataTable.Columns.Add(cell.Value.ToString());
            }

            foreach (var row in table.DataRange.Rows())
            {
                var newRow = dataTable.NewRow();
                int i = 0;
                foreach (var cell in row.Cells())
                {
                    newRow[i++] = cell.Value.ToString();
                }
                dataTable.Rows.Add(newRow);
            }

            return dataTable;
        }
    }

    // DataTable을 엑셀 파일로 저장
    public static void SaveDataTableToExcel(DataTable dataTable, string filePath)
    {
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell(1, 1).InsertTable(dataTable);
            workbook.SaveAs(filePath);
        }
    }

    // 리스트 데이터를 엑셀 파일로 저장 (제네릭)
    public static void SaveListToExcel<T>(IEnumerable<T> data, string filePath)
    {
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell(1, 1).InsertTable(data);
            workbook.SaveAs(filePath);
        }
    }
}
