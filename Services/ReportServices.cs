using System.IO;
using ClosedXML.Excel;
using LARS.Models;
using LARS.Utils;

namespace LARS.Services;

/// <summary>
/// BOM 보고서 서비스. BOM 파일을 읽고 구조화된 데이터를 반환합니다.
/// 가공 파이프라인은 VME(Visual Macro Editor)로 이관되었습니다.
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
    public List<FileMetadata> ScanBomFiles(IProgress<double>? progress = null)
    {
        // 1차 필터링: VBA 로직과 동일하게 파일명에 "@CVZ"가 포함된 엑셀 파일만 검색
        var files = FileSearcher.FindFiles(_dirs.SourceBOM, "@CVZ", ".xlsx");
        var result = new List<FileMetadata>();
        for (int i = 0; i < files.Count; i++)
        {
            try
            {
                // 2차 검증(Deep Validation): C2 셀에 유효한 값이 존재하는지 확인
                using var wb = new XLWorkbook(files[i]);
                var ws = wb.Worksheet(1);
                string c2Value = ws.Cell(2, 3).GetString();

                if (!string.IsNullOrWhiteSpace(c2Value))
                {
                    var meta = FileMetadata.Parse(files[i]);
                    meta.Status = "Validated";
                    result.Add(meta);
                }
            }
            catch { /* 열 수 없거나 잘못된 포맷인 경우 스킵 */ }
            progress?.Report((double)(i + 1) / files.Count);
        }
        return result;
    }

    /// <summary>
    /// BOM 파일에서 데이터를 읽어 구조화하여 반환합니다.
    /// </summary>
    public BomDataResult ReadBomFile(string filePath)
    {
        var result = new BomDataResult { FilePath = filePath };
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

    /// <summary>
    /// BOM 데이터를 레벨별로 필터링합니다.
    /// </summary>
    public List<List<string>> FilterByLevel(BomDataResult data, List<string> levels)
    {
        if (levels.Count == 0 || data.Headers.Count == 0) return data.Rows;

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

public class BomDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public string? Title { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

// ======================================================================================

/// <summary>
/// DailyPlan 보고서 서비스. DailyPlan 파일을 읽고 메타데이터를 추출합니다.
/// 가공 파이프라인은 VME로 이관되었습니다.
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
    public List<FileMetadata> ScanDailyPlanFiles(int baseYear = 0, IProgress<double>? progress = null)
    {
        // 1차 필터링: VBA 로직과 동일하게 파일명에 "Excel_Export_"가 포함된 엑셀 파일만 검색
        var files = FileSearcher.FindFiles(_dirs.SourceDailyPlan, "Excel_Export_", ".xlsx");
        var result = new List<FileMetadata>();
        for (int i = 0; i < files.Count; i++)
        {
            // 2차 검증(Deep Validation): 내부 로직에서 '*월' 패턴 헤더 및 수치 검사를 수행
            var metaData = ReadMetaFromFile(files[i]);
            if (metaData.IsValid)
            {
                var meta = FileMetadata.Parse(files[i], baseYear);
                meta.Status = "Validated";
                result.Add(meta);
            }
            progress?.Report((double)(i + 1) / files.Count);
        }
        return result;
    }

    /// <summary>
    /// DailyPlan 파일에서 날짜/라인 메타데이터를 셀에서 직접 읽습니다.
    /// </summary>
    public DailyPlanMetadata ReadMetaFromFile(string filePath)
    {
        try
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheet(1);

            // Row 2에서 "월"로 끝나는 셀 탐색
            ClosedXML.Excel.IXLCell? monthCell = null;
            foreach (var cell in ws.Row(2).CellsUsed())
            {
                string val = cell.GetString().Trim();
                if (val.EndsWith("월"))
                {
                    monthCell = cell;
                    break;
                }
            }
            if (monthCell == null)
                return DailyPlanMetadata.Invalid(filePath);

            int firstCol, lastCol;
            if (monthCell.IsMerged())
            {
                var mergeArea = monthCell.MergedRange();
                firstCol = mergeArea.FirstCell().Address.ColumnNumber;
                lastCol  = mergeArea.LastCell().Address.ColumnNumber;
            }
            else
            {
                firstCol = lastCol = monthCell.Address.ColumnNumber;
            }

            int minDay = 31;
            var schedules = new List<(int Day, int LotCount)>();
            for (int c = firstCol; c <= lastCol; c++)
            {
                string dayVal = ws.Cell(3, c).GetString();
                string cntVal = ws.Cell(4, c).GetString();
                if (int.TryParse(dayVal, out int day) &&
                    double.TryParse(cntVal, out double cnt) && cnt > 0)
                {
                    if (day < minDay) minDay = day;
                    schedules.Add((day, (int)cnt));
                }
            }

            string monthStr = monthCell.GetString().Replace("월", "").Trim();
            int.TryParse(monthStr, out int month);

            string line = "";
            foreach (var cell in ws.Row(2).CellsUsed())
            {
                string v = cell.GetString();
                if (v.Contains("Line", StringComparison.OrdinalIgnoreCase) ||
                    v.Contains("라인", StringComparison.OrdinalIgnoreCase))
                {
                    line = ws.Cell(cell.Address.RowNumber + 2, cell.Address.ColumnNumber).GetString();
                    break;
                }
            }

            return new DailyPlanMetadata
            {
                FilePath = filePath,
                Month = month,
                Day = minDay < 31 ? minDay : 1,
                Line = line,
                IsValid = month > 0,
                Schedules = schedules
            };
        }
        catch
        {
            return DailyPlanMetadata.Invalid(filePath);
        }
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
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        return result;
    }
}

public class DailyPlanDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public LotGroup? LotGroup { get; set; }
    public DailyPlanMetadata? Meta { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// DailyPlan 파일에서 추출한 날짜/라인 메타데이터.
/// </summary>
public class DailyPlanMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Day { get; set; }
    public string Line { get; set; } = string.Empty;
    public bool IsValid { get; set; }

    /// <summary>일별 생산 스케줄(일자, 총 롯트 수) 리스트</summary>
    public List<(int Day, int LotCount)> Schedules { get; set; } = new();

    /// <summary>날짜 문자열: "5월-28일" 형식</summary>
    public string DateLabel => IsValid ? $"{Month}월-{Day}일" : "(날짜 없음)";

    /// <summary>DateTime으로 변환 (연도는 현재 연도 사용)</summary>
    public DateTime? ToDateTime(int year = 0)
    {
        if (!IsValid) return null;
        int y = year > 0 ? year : DateTime.Now.Year;
        try { return new DateTime(y, Month, Day); }
        catch { return null; }
    }

    public static DailyPlanMetadata Invalid(string filePath) =>
        new() { FilePath = filePath, IsValid = false };
}

// ======================================================================================

/// <summary>
/// PartList 보고서 서비스. PartList 파일을 읽고 구조화된 데이터를 반환합니다.
/// 가공 파이프라인은 VME로 이관되었습니다.
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
    public List<FileMetadata> ScanPartListFiles(int baseYear = 0, IProgress<double>? progress = null)
    {
        // 1차 필터링: VBA 로직과 동일하게 파일명에 "Excel_Export_"가 포함된 엑셀 파일만 검색
        var files = FileSearcher.FindFiles(_dirs.SourcePartList, "Excel_Export_", ".xlsx");
        var result = new List<FileMetadata>();
        for (int i = 0; i < files.Count; i++)
        {
            try
            {
                using var wb = new XLWorkbook(files[i]);
                var ws = wb.Worksheet(1);
                bool isValid = false;

                // 2차 검증(Deep Validation): Row 1에 8자리 숫자(YYYYMMDD 포맷) 헤더가 하나라도 존재하는지 파악
                foreach (var cell in ws.Row(1).CellsUsed())
                {
                    string val = cell.GetString().Trim();
                    if (val.Length == 8 && int.TryParse(val, out _))
                    {
                        isValid = true;
                        break;
                    }
                }

                if (isValid)
                {
                    var meta = FileMetadata.Parse(files[i], baseYear);
                    meta.Status = "Validated";
                    result.Add(meta);
                }
            }
            catch { /* 열 수 없거나 잘못된 포맷인 경우 스킵 */ }
            progress?.Report((double)(i + 1) / files.Count);
        }
        return result;
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
    public List<List<string>> Rows { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

// ======================================================================================

/// <summary>
/// 자재 카운터 서비스. PartList → ItemUnit 분해 → 병합 파이프라인.
/// VME 노드에서도 활용할 수 있는 범용 서비스입니다.
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

        foreach (var row in partListData.Rows)
        {
            string nickName = row.Count > 0 ? row[0] : "Unknown";
            for (int col = 1; col < row.Count; col++)
            {
                string cellText = row[col];
                if (string.IsNullOrWhiteSpace(cellText)) continue;
                var items = ParseCellText(cellText, nickName);
                allItems.AddRange(items);
            }
        }

        result.MergedGroup = MergeItems(allItems);
        result.IsSuccess = true;
        result.TotalItemsBeforeMerge = allItems.Count;
        return result;
    }

    /// <summary>
    /// PartList 데이터와 DailyPlan 스케줄(날짜, 롯트 수)을 결합해 파이프라인을 실행합니다.
    /// </summary>
    public ItemCounterResult RunPipelineWithDates(PartListDataResult partListData, List<(DateTime Date, int LotCount)> schedules)
    {
        var result = new ItemCounterResult();
        if (!partListData.IsSuccess || partListData.Rows.Count == 0)
        {
            result.ErrorMessage = "PartList 데이터가 없습니다.";
            return result;
        }

        var allItems = new List<ItemUnit>();

        foreach (var row in partListData.Rows)
        {
            string nickName = row.Count > 0 ? row[0] : "Unknown";
            for (int col = 1; col < row.Count; col++)
            {
                string cellText = row[col];
                if (string.IsNullOrWhiteSpace(cellText)) continue;

                foreach (var schedule in schedules)
                {
                    if (schedule.LotCount == 0) continue;
                    var items = ParseCellText(cellText, nickName, schedule.Date, schedule.LotCount);
                    allItems.AddRange(items);
                }
            }
        }

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
