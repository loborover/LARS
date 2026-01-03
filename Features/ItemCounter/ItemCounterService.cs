using System.Data;
using LARS.Models;
using LARS.Utils;

namespace LARS.Features.ItemCounter;

public class ItemCounterService
{
    public List<ItemSummary> AggregateItems(DateTime targetDate)
    {
        var summaries = new Dictionary<string, ItemSummary>();
        string dateStr = targetDate.ToString("yyyy-MM-dd"); // 파일 날짜 포맷에 맞게 조정 필요

        // PartList 폴더의 모든 엑셀 파일 검색
        // 실제로는 파일명에 날짜가 포함된 것만 필터링하는 로직이 더 효율적일 수 있음
        var files = Directory.GetFiles(DirectoryHelper.PartListPath, "*.xlsx");

        foreach (var file in files)
        {
            // 파일 생성일 또는 파일명에서 날짜 확인
            // 여기서는 단순하게 파일 생성일로 가정 (추후 파일명 파싱 로직 강화 필요)
            var fileDate = File.GetCreationTime(file).ToString("yyyy-MM-dd");
            
            // 타겟 날짜와 일치하는지 확인 (Mock 데이터나 실제 환경에 따라 조건 완화 가능)
            bool isTarget = fileDate == dateStr || Path.GetFileName(file).Contains(targetDate.ToString("yyyyMMdd"));
            
            if (!isTarget) continue;

            try
            {
                DataTable dt = ExcelHelper.ReadExcelToDataTable(file);
                
                // PartList 구조: Part No, Qty 등의 컬럼이 있다고 가정
                foreach (DataRow row in dt.Rows)
                {
                    string partNo = GetString(row, "Part No");
                    if (string.IsNullOrWhiteSpace(partNo)) continue;

                    double qty = GetDouble(row, "Qty");
                    string desc = GetString(row, "Description");

                    if (summaries.ContainsKey(partNo))
                    {
                        summaries[partNo].TotalQuantity += qty;
                        summaries[partNo].FileCount++;
                    }
                    else
                    {
                        summaries[partNo] = new ItemSummary
                        {
                            PartNo = partNo,
                            TotalQuantity = qty,
                            Description = desc,
                            Date = dateStr,
                            FileCount = 1
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {file}: {ex.Message}");
                // 개별 파일 에러는 무시하고 계속 진행할지 여부 결정
            }
        }

        return summaries.Values.OrderBy(x => x.PartNo).ToList();
    }

    private string GetString(DataRow row, string colName)
    {
        return row.Table.Columns.Contains(colName) ? row[colName]?.ToString() ?? "" : "";
    }

    private double GetDouble(DataRow row, string colName)
    {
        if (row.Table.Columns.Contains(colName) && double.TryParse(row[colName]?.ToString(), out double val))
            return val;
        return 0.0;
    }
}
