using System.Data;
using LARS.Models;
using LARS.Utils;

namespace LARS.Features.BomViewer;

public class BomParser
{
    // 엑셀 파일을 읽어 BOM 리스트로 변환
    public List<BomItem> ParseBomFile(string filePath)
    {
        var bomList = new List<BomItem>();
        
        try
        {
            // 엑셀 데이터를 DataTable로 로드
            DataTable dt = ExcelHelper.ReadExcelToDataTable(filePath);

            foreach (DataRow row in dt.Rows)
            {
                // 유효한 데이터인지 확인 (예: Part No가 없으면 건너뜀)
                if (string.IsNullOrWhiteSpace(row["Part No"].ToString())) continue;

                var item = new BomItem
                {
                    // 각 컬럼 매핑 (컬럼명이 정확하다고 가정, 실제로는 유연하게 처리 필요할 수 있음)
                    Level = GetString(row, "Lvl"),
                    PartNo = GetString(row, "Part No"),
                    Description = GetString(row, "Description"),
                    Quantity = GetDouble(row, "Qty"),
                    Uom = GetString(row, "UOM"),
                    Maker = GetString(row, "Maker"),
                    SupplyType = GetString(row, "Supply Type")
                };

                bomList.Add(item);
            }
        }
        catch (Exception ex)
        {
            // 로깅 또는 예외 처리 (여기서는 콘솔 출력 후 재던짐)
            Console.WriteLine($"Error parsing BOM file: {ex.Message}");
            throw;
        }

        return bomList;
    }

    // 문자열 값 안전하게 가져오기
    private string GetString(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName) ? row[columnName]?.ToString() ?? string.Empty : string.Empty;
    }

    // 숫자 값 안전하게 가져오기
    private double GetDouble(DataRow row, string columnName)
    {
        if (row.Table.Columns.Contains(columnName) && double.TryParse(row[columnName]?.ToString(), out double result))
        {
            return result;
        }
        return 0.0;
    }
}
