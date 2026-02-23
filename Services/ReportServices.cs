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
    public List<FileMetadata> ScanBomFiles(IProgress<double>? progress = null)
    {
        var files = FileSearcher.FindFiles(_dirs.BOM, "", ".xlsx");
        var result = new List<FileMetadata>(files.Count);
        for (int i = 0; i < files.Count; i++)
        {
            result.Add(FileMetadata.Parse(files[i]));
            progress?.Report((double)(i + 1) / files.Count);
        }
        return result;
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

    /// <summary>
    /// BOM 파일을 보고서 출력용으로 가공합니다.
    /// VBA AutoReport_BOM에 대응합니다:
    ///   1. 지정 컬럼(Lvl/Part No/Description/Qty/UOM/Maker/Supply Type)만 추출
    ///   2. Lvl=0 행의 Part No에서 타이틀 파싱 ("@" 앞까지)
    ///   3. 빈 행 제외
    /// </summary>
    public BomDataResult ProcessBomForExport(string filePath)
    {
        var result = new BomDataResult { FilePath = filePath };
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(filePath);
            var ws = wb.Worksheet(1);

            // VBA SetUsingColumns: 보고서에 표시할 컬럼명 목록
            var targetCols = new[] { "Lvl", "Part No", "Description", "Qty", "UOM", "Maker", "Supply Type" };

            // 헤더 행 탐색: 사용 영역에서 "Lvl" 셀 찾기
            ClosedXML.Excel.IXLCell? headerCell = null;
            foreach (var cell in ws.CellsUsed())
            {
                if (cell.GetString().Trim() == "Lvl") { headerCell = cell; break; }
            }
            if (headerCell == null)
            {
                result.ErrorMessage = "BOM 헤더(Lvl)를 찾을 수 없습니다.";
                return result;
            }

            int headerRow = headerCell.Address.RowNumber;
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

            // 헤더 행에서 각 대상 컬럼의 열 번호 매핑
            var colMap = new Dictionary<string, int>();
            foreach (var cell in ws.Row(headerRow).CellsUsed())
            {
                string val = cell.GetString().Trim();
                if (targetCols.Contains(val))
                    colMap[val] = cell.Address.ColumnNumber;
            }

            // 존재하는 컬럼만 헤더로 추가
            var orderedCols = targetCols.Where(c => colMap.ContainsKey(c)).ToList();
            result.Headers = orderedCols.ToList();

            // 타이틀 추출: Lvl 컬럼에서 "0" 값인 행의 Part No 읽기 → "@" 앞까지
            if (colMap.TryGetValue("Lvl", out int lvlCol) && colMap.TryGetValue("Part No", out int partNoCol))
            {
                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    if (ws.Cell(r, lvlCol).GetString().Trim() == "0")
                    {
                        string fullName = ws.Cell(r, partNoCol).GetString();
                        int atIdx = fullName.IndexOf('@');
                        result.Title = atIdx > 0 ? fullName[..atIdx].Trim() : fullName.Trim();
                        break;
                    }
                }
            }

            // 데이터 행 추출 (headerRow+1 부터)
            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = orderedCols.Select(c => ws.Cell(r, colMap[c]).GetString()).ToList();
                if (row.All(string.IsNullOrWhiteSpace)) continue;
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

/// <summary>
/// BOM 읽기 결과 DTO
/// </summary>
public class BomDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
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
    public List<FileMetadata> ScanDailyPlanFiles(int baseYear = 0, IProgress<double>? progress = null)
    {
        var files = FileSearcher.FindFiles(_dirs.DailyPlan, "", ".xlsx");
        var result = new List<FileMetadata>(files.Count);
        for (int i = 0; i < files.Count; i++)
        {
            result.Add(FileMetadata.Parse(files[i], baseYear));
            progress?.Report((double)(i + 1) / files.Count);
        }
        return result;
    }

    /// <summary>
    /// DailyPlan 파일에서 날짜/라인 메타데이터를 셀에서 직접 읽습니다.
    /// VBA GetDailyPlanWhen에 대응합니다.
    /// Row 2의 "월"로 끝나는 병합셀을 찾아 가장 작은 일자와 라인을 추출합니다.
    /// </summary>
    public DailyPlanMetadata ReadMetaFromFile(string filePath)
    {
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(filePath);
            var ws = wb.Worksheet(1);

            // Row 2에서 "월"로 끝나는 셀 탐색 (VBA: "월" Like 패턴)
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

            // 병합 범위 가져오기
            var mergeArea = monthCell.MergeArea;
            int firstCol = mergeArea.FirstColumn().ColumnNumber;
            int lastCol = mergeArea.LastColumn().ColumnNumber;

            // Row 3에서 최솟값 날짜 찾기 (VBA: smallestValue < 31)
            int minDay = 31;
            for (int c = firstCol; c <= lastCol; c++)
            {
                string dayVal = ws.Cell(3, c).GetString();
                string cntVal = ws.Cell(4, c).GetString();
                if (int.TryParse(dayVal, out int day) &&
                    double.TryParse(cntVal, out double cnt) && cnt > 0 &&
                    day < minDay)
                    minDay = day;
            }

            // 월 문자열에서 숫자 추출: "5월" → 5
            string monthStr = monthCell.GetString().Replace("월", "").Trim();
            int.TryParse(monthStr, out int month);

            // 라인 정보: "생산 라인" 또는 "-Line" 포함 헤더 셀 찾기, Offset(2,0)
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
                IsValid = month > 0
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
    public DailyPlanMetadata? Meta { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// DailyPlan 파일에서 추출한 날짜/라인 메타데이터.
/// VBA GetDailyPlanWhen 반환값에 대응합니다.
/// </summary>
public class DailyPlanMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Day { get; set; }
    public string Line { get; set; } = string.Empty;
    public bool IsValid { get; set; }

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

    /// <summary>
    /// 자재 셀 값을 표준 형식으로 정규화합니다.
    /// VBA Re_Categorizing_PL에 대응합니다.
    /// "[벤더] 파트1/파트2(수량)" 형식으로 통일하고, Burner 특수 매핑을 처리합니다.
    /// </summary>
    public string NormalizeCellValue(string rawValue, string columnHeader = "")
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return rawValue;

        // Burner 컬럼 특수 매핑 (VBA 하드코딩 로직)
        if (columnHeader.Equals("Burner", StringComparison.OrdinalIgnoreCase))
            return MapBurnerValue(rawValue.Trim());

        // " [" → "$[" 치환 후 "$" 기준으로 벤더 블록 분리
        string sample = rawValue.Trim().Replace(" [", "$[");
        var vendorBlocks = sample.Split('$', StringSplitOptions.RemoveEmptyEntries);

        var parts = new List<string>();
        foreach (var block in vendorBlocks)
        {
            string vendor = LARS.Utils.StringParser.ExtractBracketValue(block);
            // 벤더명 검증:
            //   길이 < 1 → "자사품"
            //   5글자 이상이면서 한글 없음 → "자사품" (VBA 조건)
            if (vendor.Length < 1 || (vendor.Length >= 5 && !ContainsKorean(vendor)))
                vendor = "자사품";

            string remainder = block.Replace($"[{vendor}]", "").Trim();
            if (!string.IsNullOrWhiteSpace(remainder))
                parts.Add($"[{vendor}] {remainder}");
        }

        return string.Join(" ", parts).Trim();
    }

    /// <summary>
    /// Feeder 설정에 따라 PartList 컬럼을 필터링합니다.
    /// VBA SortColumnByFeeder에 대응합니다.
    /// Feeder의 item 목록에 없는 컬럼(1행 헤더 기준)은 결과에서 제외합니다.
    /// </summary>
    public PartListDataResult FilterByFeeder(PartListDataResult data, LARS.Models.FeederUnit feeder)
    {
        if (feeder == null || !feeder.Items.Any()) return data;

        // VBA Replacing_item2Feeder: vbLf → " ", "_" → " " 정규화
        var feederItems = feeder.Items
            .Select(i => i.Replace("\n", " ").Replace("_", " ").Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1열(NickName 등 고정열) + Feeder에 포함된 컬럼만 선택
        var keepIndices = new List<int>();
        for (int i = 0; i < data.Headers.Count; i++)
        {
            string header = data.Headers[i].Replace("\n", " ").Replace("_", " ").Trim();
            if (i == 0 || feederItems.Contains(header))
                keepIndices.Add(i);
        }

        var filtered = new PartListDataResult
        {
            FilePath = data.FilePath,
            IsSuccess = true,
            Headers = keepIndices.Select(i => data.Headers[i]).ToList()
        };
        foreach (var row in data.Rows)
        {
            filtered.Rows.Add(keepIndices
                .Select(i => i < row.Count ? row[i] : string.Empty)
                .ToList());
        }
        return filtered;
    }

    private static bool ContainsKorean(string s) =>
        s.Any(c => c >= 0xAC00 && c <= 0xD7A3);

    /// <summary>
    /// Burner 컬럼 특수 매핑. VBA 하드코딩 로직을 그대로 이식합니다.
    /// </summary>
    private static string MapBurnerValue(string raw) => raw.Trim() switch
    {
        "[오성] 4102/4202/4402/4502" => "[매칭] Oval/Best",
        "[오성] 4102/4202/4402/4502 [SABAF S.P.A.] 6904/7302" => "[매칭] Oval/Best/Sabaf",
        "[오성] 4102/4202/4402/4502(2)" => "[매칭] Oval/Better",
        "[오성] 7906/8506/8606/8706" => "[매칭] FZ~FH/Better",
        _ => "Matching Error"
    };
}

public class PartListDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; } = new();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    /// <summary>FilterByFeeder 적용 여부</summary>
    public bool IsFiltered { get; set; }
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
