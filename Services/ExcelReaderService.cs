using ClosedXML.Excel;
using LARS.Models;
using LARS.Utils;

namespace LARS.Services;

/// <summary>
/// Excel 파일 읽기/쓰기 서비스.
/// VBA의 Excel COM 의존성을 ClosedXML(MIT)로 대체합니다.
/// </summary>
public class ExcelReaderService
{
    /// <summary>
    /// Excel 파일에서 셀 값을 2차원 문자열 배열로 읽어옵니다.
    /// </summary>
    public string[,] ReadRange(string filePath, string sheetName, int startRow, int startCol, int endRow, int endCol)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(sheetName);
        int rows = endRow - startRow + 1;
        int cols = endCol - startCol + 1;
        var data = new string[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                data[r, c] = worksheet.Cell(startRow + r, startCol + c).GetString();

        return data;
    }

    /// <summary>
    /// 워크시트의 사용 영역 정보를 반환합니다.
    /// </summary>
    public (int LastRow, int LastCol) GetUsedRange(string filePath, string? sheetName = null)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = sheetName != null ? workbook.Worksheet(sheetName) : workbook.Worksheets.First();
        var range = worksheet.RangeUsed();
        if (range == null) return (0, 0);
        return (range.LastRow().RowNumber(), range.LastColumn().ColumnNumber());
    }

    /// <summary>
    /// Excel 파일에서 특정 텍스트가 포함된 셀을 검색합니다.
    /// VBA ws.Find를 대체합니다.
    /// </summary>
    public (int Row, int Col)? FindCell(IXLWorksheet worksheet, string searchText, bool exactMatch = true)
    {
        var cells = worksheet.CellsUsed();
        foreach (var cell in cells)
        {
            string val = cell.GetString();
            if (exactMatch ? val == searchText : val.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return (cell.Address.RowNumber, cell.Address.ColumnNumber);
        }
        return null;
    }

    /// <summary>
    /// 워크북을 열고 워크시트 목록을 반환합니다.
    /// </summary>
    public List<string> GetSheetNames(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        return workbook.Worksheets.Select(ws => ws.Name).ToList();
    }

    /// <summary>
    /// 워크시트의 전체 데이터를 읽어 List로 반환합니다.
    /// </summary>
    public List<List<string>> ReadAll(string filePath, string? sheetName = null)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = sheetName != null ? workbook.Worksheet(sheetName) : workbook.Worksheets.First();
        var range = worksheet.RangeUsed();
        if (range == null) return new();

        var result = new List<List<string>>();
        for (int r = 1; r <= range.RowCount(); r++)
        {
            var row = new List<string>();
            for (int c = 1; c <= range.ColumnCount(); c++)
                row.Add(range.Cell(r, c).GetString());
            result.Add(row);
        }
        return result;
    }
}
