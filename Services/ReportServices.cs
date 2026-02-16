using System.IO;
using ClosedXML.Excel;
using LARS.Models;
using LARS.Utils;

namespace LARS.Services;

/// <summary>
/// BOM 보고서 서비스. VBA BA_BOM_Viewer.bas를 대체합니다.
/// BOM 파일을 읽고 구조화된 데이터를 반환합니다.
/// </summary>
public class BomReportService
{
    private readonly ExcelReaderService _excel;
    private readonly DirectoryManager _dirs;

    public BomReportService(ExcelReaderService excel, DirectoryManager dirs)
    {
        _excel = excel;
        _dirs = dirs;
    }

    /// <summary>
    /// BOM 디렉토리에서 파일 목록을 스캔합니다.
    /// </summary>
    public List<FileMetadata> ScanBomFiles()
    {
        var files = FileSearcher.FindFiles(_dirs.BOM, "", ".xlsx");
        return files.Select(f => FileMetadata.Parse(f)).ToList();
    }

    /// <summary>
    /// BOM 파일에서 데이터를 읽어 구조화하여 반환합니다.
    /// 컬럼 헤더를 감지하고 데이터 행을 BomRow 리스트로 변환합니다.
    /// </summary>
    public BomDataResult ReadBomFile(string filePath)
    {
        var result = new BomDataResult { FilePath = filePath };
        try
        {
            var allData = _excel.ReadAll(filePath);
            if (allData.Count == 0) return result;

            // 첫 행을 헤더로 사용
            result.Headers = allData[0];

            // 나머지를 데이터로
            for (int i = 1; i < allData.Count; i++)
            {
                var row = allData[i];
                // 빈 행 건너뛰기
                if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;
                result.Rows.Add(row);
            }

            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        return result;
    }

    /// <summary>
    /// BOM 데이터를 레벨별로 필터링합니다.
    /// VBA AutoFilltering_BOM에 대응합니다.
    /// </summary>
    public List<List<string>> FilterByLevel(BomDataResult data, List<string> levels)
    {
        if (levels.Count == 0 || data.Headers.Count == 0) return data.Rows;

        // Level 컬럼 찾기
        int levelCol = data.Headers.FindIndex(h =>
            h.Contains("Level", StringComparison.OrdinalIgnoreCase) ||
            h.Contains("레벨", StringComparison.OrdinalIgnoreCase));

        if (levelCol < 0) return data.Rows;

        return data.Rows.Where(row =>
            levelCol < row.Count &&
            levels.Any(lv => row[levelCol].Contains(lv, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }
}

/// <summary>
/// BOM 읽기 결과 DTO
/// </summary>
public class BomDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; } = new();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// DailyPlan 보고서 서비스. VBA BB_DailyPlan_Viewer.bas를 대체합니다.
/// </summary>
public class DailyPlanService
{
    private readonly ExcelReaderService _excel;
    private readonly DirectoryManager _dirs;

    public DailyPlanService(ExcelReaderService excel, DirectoryManager dirs)
    {
        _excel = excel;
        _dirs = dirs;
    }

    /// <summary>
    /// DailyPlan 디렉토리에서 파일 목록을 스캔합니다.
    /// </summary>
    public List<FileMetadata> ScanDailyPlanFiles(int baseYear = 0)
    {
        var files = FileSearcher.FindFiles(_dirs.DailyPlan, "", ".xlsx");
        return files.Select(f => FileMetadata.Parse(f, baseYear)).ToList();
    }

    /// <summary>
    /// DailyPlan 파일을 읽어 구조화된 데이터를 반환합니다.
    /// </summary>
    public DailyPlanDataResult ReadDailyPlanFile(string filePath)
    {
        var result = new DailyPlanDataResult { FilePath = filePath };
        try
        {
            var allData = _excel.ReadAll(filePath);
            if (allData.Count == 0) return result;

            result.Headers = allData[0];
            for (int i = 1; i < allData.Count; i++)
            {
                var row = allData[i];
                if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;
                result.Rows.Add(row);
            }

            // 모델 그루핑 수행
            result.LotGroup = GroupModels(allData);
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        return result;
    }

    /// <summary>
    /// DailyPlan 데이터에서 모델 그루핑을 수행합니다.
    /// VBA AR_2_ModelGrouping에 대응합니다.
    /// </summary>
    public LotGroup GroupModels(List<List<string>> data, int modelCol = 2, int startRow = 1)
    {
        var group = new LotGroup();
        if (data.Count <= startRow) return group;

        ModelInfo? prevModel = null;
        int lotStart = startRow;

        for (int r = startRow; r < data.Count; r++)
        {
            if (modelCol >= data[r].Count) continue;
            string cellValue = data[r][modelCol].Trim();
            if (string.IsNullOrEmpty(cellValue)) continue;

            var currentModel = ModelInfo.Parse(cellValue);

            if (prevModel != null && currentModel.SpecNumber != prevModel.SpecNumber)
            {
                group.AddLot(new Lot
                {
                    StartRow = lotStart,
                    EndRow = r - 1
                }, LotGroupType.Sub);
                lotStart = r;
            }
            prevModel = currentModel;
        }

        if (prevModel != null)
        {
            group.AddLot(new Lot
            {
                StartRow = lotStart,
                EndRow = data.Count - 1
            }, LotGroupType.Sub);
        }

        return group;
    }
}

public class DailyPlanDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; } = new();
    public LotGroup? LotGroup { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// PartList 보고서 서비스. VBA BC_PartListItem_Viewer.bas를 대체합니다.
/// </summary>
public class PartListService
{
    private readonly ExcelReaderService _excel;
    private readonly DirectoryManager _dirs;

    public PartListService(ExcelReaderService excel, DirectoryManager dirs)
    {
        _excel = excel;
        _dirs = dirs;
    }

    /// <summary>
    /// PartList 디렉토리에서 파일 목록을 스캔합니다.
    /// </summary>
    public List<FileMetadata> ScanPartListFiles(int baseYear = 0)
    {
        var files = FileSearcher.FindFiles(_dirs.PartList, "", ".xlsx");
        return files.Select(f => FileMetadata.Parse(f, baseYear)).ToList();
    }

    /// <summary>
    /// PartList 파일을 읽어 구조화된 데이터를 반환합니다.
    /// </summary>
    public PartListDataResult ReadPartListFile(string filePath)
    {
        var result = new PartListDataResult { FilePath = filePath };
        try
        {
            var allData = _excel.ReadAll(filePath);
            if (allData.Count == 0) return result;

            result.Headers = allData[0];
            for (int i = 1; i < allData.Count; i++)
            {
                var row = allData[i];
                if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;
                result.Rows.Add(row);
            }
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        return result;
    }
}

public class PartListDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; } = new();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// 자재 카운터 서비스. VBA CA_itemCounter.bas를 대체합니다.
/// PartList → ItemUnit 분해 → 병합 파이프라인.
/// </summary>
public class ItemCounterService
{
    private readonly ExcelReaderService _excel;

    public ItemCounterService(ExcelReaderService excel)
    {
        _excel = excel;
    }

    /// <summary>
    /// 셀 텍스트를 파싱하여 ItemUnit 리스트로 분해합니다.
    /// VBA Re_Categorizing를 대체 (공통화하여 코드 중복 해소).
    /// </summary>
    public List<ItemUnit> ParseCellText(string cellText, string nickName = "Unknown",
        DateTime? inputDate = null, long lotCounts = 1)
    {
        var result = new List<ItemUnit>();
        if (string.IsNullOrWhiteSpace(cellText)) return result;

        string sample = cellText.Trim().Replace(" [", "$[");
        var vendors = sample.Split('$');

        foreach (var vendorBlock in vendors)
        {
            string vendor = StringParser.ExtractBracketValue(vendorBlock);
            string remainder = vendorBlock.Replace($"[{vendor}]", "").Trim();
            var partNumbers = remainder.Split('/');

            foreach (var pn in partNumbers)
            {
                if (string.IsNullOrWhiteSpace(pn)) continue;

                string partNumber;
                long qty = 1;

                int parenIdx = pn.IndexOf('(');
                if (parenIdx > 0)
                {
                    partNumber = pn[..parenIdx].Trim();
                    string qtyStr = StringParser.ExtractSmallBracketValue(pn);
                    if (long.TryParse(qtyStr, out long parsed))
                        qty = parsed;
                }
                else
                {
                    partNumber = pn.Trim();
                }

                var unit = new ItemUnit
                {
                    NickName = StringParser.RemoveLineBreaks(nickName),
                    Vendor = StringParser.RemoveLineBreaks(vendor),
                    PartNumber = StringParser.RemoveLineBreaks(partNumber),
                    QTY = qty
                };

                if (inputDate.HasValue)
                    unit[inputDate.Value] = lotCounts * qty;

                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// ItemUnit 리스트를 IdHash 기준으로 병합합니다.
    /// VBA PL_Compressor를 대체. Dictionary로 O(n) 성능.
    /// </summary>
    public ItemGroup MergeItems(IEnumerable<ItemUnit> items)
    {
        var group = new ItemGroup();
        foreach (var item in items)
            group.AddUnit(item);
        return group;
    }

    /// <summary>
    /// PartList 데이터에서 ItemCounter 파이프라인을 실행합니다.
    /// VBA PL2IC 전체 파이프라인에 대응합니다.
    /// </summary>
    public ItemCounterResult RunPipeline(PartListDataResult partListData)
    {
        var result = new ItemCounterResult();
        if (!partListData.IsSuccess || partListData.Rows.Count == 0)
        {
            result.ErrorMessage = "PartList 데이터가 없습니다.";
            return result;
        }

        var allItems = new List<ItemUnit>();

        // 각 행에서 자재 데이터 추출
        foreach (var row in partListData.Rows)
        {
            string nickName = row.Count > 0 ? row[0] : "Unknown";

            // 자재 데이터 컬럼들을 순회
            for (int col = 1; col < row.Count; col++)
            {
                string cellText = row[col];
                if (string.IsNullOrWhiteSpace(cellText)) continue;

                var items = ParseCellText(cellText, nickName);
                allItems.AddRange(items);
            }
        }

        // 병합
        result.MergedGroup = MergeItems(allItems);
        result.IsSuccess = true;
        result.TotalItemsBeforeMerge = allItems.Count;
        return result;
    }
}

public class ItemCounterResult
{
    public ItemGroup? MergedGroup { get; set; }
    public int TotalItemsBeforeMerge { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
