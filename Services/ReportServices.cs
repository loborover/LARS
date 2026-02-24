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
        var files = FileSearcher.FindFiles(_dirs.SourceBOM, "", ".xlsx");
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

    // VBA SetUsingColumns 대응: DailyPlan 보존 열 목록
    private static readonly HashSet<string> _dpEssentialColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "W/O", "부품번호", "W/O 계획수량", "W/O계획수량", "계획수량", "W/O Input", "W/O실적",
        "모델", "Suffix", "-Line", "Line"
    };

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
        var files = FileSearcher.FindFiles(_dirs.SourceDailyPlan, "", ".xlsx");
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

            // 병합 범위 가져오기 (ClosedXML: IsMerged() + MergedRange())
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

            // Row 3에서 최솟값 날짜 찾기 (VBA: smallestValue < 31) 및 스케줄 추출
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

    // ==================================================================================
    // VBA AutoReport_DailyPlan → AR_1_EssentialDataExtraction 대응: 가공 파이프라인
    // ==================================================================================

    /// <summary>
    /// DailyPlan RAW 데이터를 보고서 출력용으로 가공합니다.
    /// VBA AutoReport_DailyPlan → AR_1_EssentialDataExtraction 전체 파이프라인에 대응합니다.
    ///
    /// 원본 구조 (MES Export):
    ///   Row 0(C#): 그룹 헤더 (No, +, +, ...)  — 삭제 대상
    ///   Row 1(C#): 실제 헤더 (No, 조직, 공장, 생산 라인, W/O, 모델, Suffix, 부품번호, ..., 수량, 2월, 3월)
    ///   Row 2(C#): 날짜별 세부열 또는 하위 헤더
    ///   Row 3+(C#): 데이터
    ///
    /// 가공 결과:
    ///   보존 열: W/O, 부품번호(=모델.Suffix), 수량(계획/IN/OUT), 날짜 열, -Line
    ///   추가 열: Meta_Data(3001,2101,...), TPL, UPPH
    ///   헤더 리네이밍: W/O 계획수량→계획, W/O Input→IN, W/O실적→OUT
    /// </summary>
    public DailyPlanDataResult ProcessDailyPlanForExport(DailyPlanDataResult raw, int dayCount = 3)
    {
        if (!raw.IsSuccess || raw.Rows.Count == 0) return raw;

        // 원본 데이터를 행 단위로 재구성 (Headers = Row 0, Rows[0] = Row 1, ...)
        // ExcelReaderService.ReadAll()은 Row 1부터 읽으므로:
        //   raw.Headers = Excel Row 1 (그룹 헤더: No, +, +, ...)
        //   raw.Rows[0] = Excel Row 2 (실제 헤더)
        //   raw.Rows[1] = Excel Row 3 (날짜 세부 또는 데이터 시작)
        //   raw.Rows[2+] = Excel Row 4+ (데이터)

        // Step 0: 실제 헤더 찾기 (Row 2 = raw.Rows[0])
        var actualHeaders = raw.Rows.Count > 0 ? raw.Rows[0] : raw.Headers;
        var dataRows = raw.Rows.Skip(1).ToList(); // Row 3+ (날짜 세부 포함)

        // Step 1: 보존할 열 인덱스 결정
        var keepIndices = new List<int>();
        var headerRenames = new Dictionary<int, string>();

        // 날짜 열 범위 찾기 (월 패턴 헤더: "N월")
        int dateStartCol = -1;
        for (int i = 0; i < actualHeaders.Count; i++)
        {
            string h = actualHeaders[i].Replace("\n", "").Trim();

            // W/O
            if (h.Equals("W/O", StringComparison.OrdinalIgnoreCase))
            {
                keepIndices.Add(i);
                continue;
            }

            // 부품번호 (= 모델.Suffix 병합 결과)
            if (h.Equals("부품번호", StringComparison.OrdinalIgnoreCase))
            {
                keepIndices.Add(i);
                continue;
            }

            // 수량 관련 (수량 → 계획, 나머지 → IN, OUT)
            if (h.Contains("수량", StringComparison.OrdinalIgnoreCase) ||
                h.Contains("계획수량", StringComparison.OrdinalIgnoreCase))
            {
                keepIndices.Add(i);
                continue;
            }

            // 날짜 관련 열 (N월 패턴)
            if (h.EndsWith("월") && int.TryParse(h.Replace("월", ""), out _))
            {
                if (dateStartCol < 0) dateStartCol = i;
                keepIndices.Add(i);
                continue;
            }
        }

        // 만약 "부품번호" 열이 없으면 모델+Suffix를 합쳐서 부품번호 생성
        int modelCol = -1, suffixCol = -1, partNoCol = -1;
        for (int i = 0; i < actualHeaders.Count; i++)
        {
            string h = actualHeaders[i].Replace("\n", "").Trim();
            if (h.Equals("모델", StringComparison.OrdinalIgnoreCase)) modelCol = i;
            if (h.Equals("Suffix", StringComparison.OrdinalIgnoreCase)) suffixCol = i;
            if (h.Equals("부품번호", StringComparison.OrdinalIgnoreCase)) partNoCol = i;
        }

        // 모델+Suffix → 부품번호 병합 (데이터 레벨)
        if (modelCol >= 0 && suffixCol >= 0 && partNoCol >= 0)
        {
            foreach (var row in dataRows)
            {
                string m = partNoCol < row.Count ? row[partNoCol] : "";
                // 부품번호 열이 이미 "모델.Suffix" 형태인 경우가 많음 (MES Export)
                if (string.IsNullOrWhiteSpace(m) && modelCol < row.Count && suffixCol < row.Count)
                {
                    string model = row[modelCol].Trim();
                    string suffix = row[suffixCol].Trim();
                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        while (row.Count <= partNoCol) row.Add(string.Empty);
                        row[partNoCol] = !string.IsNullOrWhiteSpace(suffix)
                            ? $"{model}.{suffix}"
                            : model;
                    }
                }
            }
        }

        // Step 2: 결과 구조 생성
        var result = new DailyPlanDataResult
        {
            FilePath = raw.FilePath,
            IsSuccess = true,
            Meta = raw.Meta
        };

        // 결과 헤더 생성 (보존 열만)
        result.Headers = keepIndices.Select(i => actualHeaders[i]).ToList();

        // 수량 열 리네이밍: 가공 파일 기준 (Row 2의 세부 헤더)
        // 원본 Row 2에서 수량 하위열이 "W/O 계획수량", "W/O Input", "W/O실적" 등
        // 가공 결과에서는 "계획", "IN", "OUT"으로 리네이밍
        for (int i = 0; i < result.Headers.Count; i++)
        {
            string h = result.Headers[i].Replace("\n", "").Trim();
            if (h.Contains("계획수량") || h.Contains("W/O 계획수량"))
                result.Headers[i] = "계획";
            else if (h.Contains("W/O Input") || h.Contains("Input"))
                result.Headers[i] = "IN";
            else if (h.Contains("W/O실적") || h.Contains("실적"))
                result.Headers[i] = "OUT";
        }

        // Step 3: 데이터 행 구성 (보존 열만 추출)
        foreach (var row in dataRows)
        {
            // 빈 행 스킵
            if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;

            var newRow = keepIndices
                .Select(i => i < row.Count ? row[i] : string.Empty)
                .ToList();
            result.Rows.Add(newRow);
        }

        // Step 4: 날짜 열 트리밍 — 생산량 0인 날짜 열 삭제 + D-Day N일 제한
        TrimDateColumns(result, dayCount, dateStartCol >= 0
            ? keepIndices.IndexOf(dateStartCol)
            : -1);

        // Step 5: -Line 열 추가 (메타데이터에서 라인 정보)
        if (raw.Meta != null && !string.IsNullOrWhiteSpace(raw.Meta.Line))
        {
            result.Headers.Add($"{raw.Meta.Line}-Line");
        }

        // Step 6: 모델 그루핑 수행
        var fullData = new List<List<string>> { result.Headers };
        fullData.AddRange(result.Rows);
        result.LotGroup = GroupModels(fullData);

        result.IsProcessed = true;
        return result;
    }

    /// <summary>
    /// 날짜 열에서 생산량 합계가 0인 열을 삭제하고, D-Day N일까지만 보존합니다.
    /// </summary>
    private void TrimDateColumns(DailyPlanDataResult data, int maxDays, int dateStartIdx)
    {
        if (dateStartIdx < 0 || dateStartIdx >= data.Headers.Count) return;

        // 날짜 열 영역 파악 (dateStartIdx부터 끝까지, 단 비날짜 열 제외)
        var dateColIndices = new List<int>();
        for (int c = dateStartIdx; c < data.Headers.Count; c++)
        {
            string h = data.Headers[c].Replace("\n", "").Trim();
            // 날짜 열 판별: "N월" 패턴이거나 날짜 문자열
            if (h.EndsWith("월") || DateTimeParser.TryParse(h, out _))
                dateColIndices.Add(c);
        }

        // 생산량 합계 0인 날짜 열 삭제
        var colsToRemove = new List<int>();
        foreach (int c in dateColIndices)
        {
            long sum = 0;
            foreach (var row in data.Rows)
            {
                if (c < row.Count && long.TryParse(row[c].Trim(), out long val))
                    sum += val;
            }
            if (sum == 0) colsToRemove.Add(c);
        }

        // D-Day N일 제한: 유효 날짜 열 중 N개만 보존
        var validDateCols = dateColIndices.Except(colsToRemove).ToList();
        if (maxDays > 0 && validDateCols.Count > maxDays)
        {
            colsToRemove.AddRange(validDateCols.Skip(maxDays));
        }

        // 열 삭제 (뒤에서 앞으로)
        foreach (int c in colsToRemove.Distinct().OrderByDescending(x => x))
        {
            if (c < data.Headers.Count) data.Headers.RemoveAt(c);
            foreach (var row in data.Rows)
            {
                if (c < row.Count) row.RemoveAt(c);
            }
        }
    }

    // ==================================================================================
    // VBA AR_2_ModelGrouping 대응: 3단계 폴백 모델 그루핑
    // ==================================================================================

    /// <summary>
    /// DailyPlan 데이터에서 모델 그루핑을 수행합니다.
    /// VBA AR_2_ModelGrouping의 3단계 폴백 비교 로직에 대응합니다:
    ///   1차: SpecNumber 비교 (예: LSGL6335)
    ///   2차: TySpec 비교 (예: LS6335) — Species ≠ "LS63"일 때만
    ///   3차: Species 비교 (예: LS63)
    /// </summary>
    public LotGroup GroupModels(List<List<string>> data, int modelCol = 2, int startRow = 1)
    {
        var group = new LotGroup();
        if (data.Count <= startRow) return group;

        // 모델 열 자동 탐지: "모델" 헤더 찾기
        if (data.Count > 0 && data[0].Count > modelCol)
        {
            for (int c = 0; c < data[0].Count; c++)
            {
                string h = data[0][c].Replace("\n", "").Trim();
                if (h.Contains("모델", StringComparison.OrdinalIgnoreCase))
                {
                    modelCol = c;
                    break;
                }
            }
        }

        ModelInfo? prevModel = null;
        int lotStart = startRow;

        for (int r = startRow; r < data.Count; r++)
        {
            if (modelCol >= data[r].Count) continue;
            string cellValue = data[r][modelCol].Trim();
            if (string.IsNullOrEmpty(cellValue)) continue;

            var currentModel = ModelInfo.Parse(cellValue);

            if (prevModel != null && !IsSameGroup(prevModel, currentModel))
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

    /// <summary>
    /// VBA Compare2Models의 3단계 폴백 비교 로직.
    ///   1차: SpecNumber가 같으면 → 같은 그룹
    ///   2차: Species ≠ "LS63" 이고 TySpec가 같으면 → 같은 그룹
    ///   3차: Species가 같으면 → 같은 그룹
    /// </summary>
    private static bool IsSameGroup(ModelInfo prev, ModelInfo current)
    {
        // 1차: SpecNumber 비교 (가장 세밀)
        if (!string.IsNullOrEmpty(prev.SpecNumber) && prev.SpecNumber == current.SpecNumber)
            return true;

        // 2차: TySpec 비교 — Species가 "LS63"이 아닐 때만
        if (!string.IsNullOrEmpty(prev.TySpec) &&
            prev.Species != "LS63" && current.Species != "LS63" &&
            prev.TySpec == current.TySpec)
            return true;

        // 3차: Species 비교 (가장 너그러운)
        if (!string.IsNullOrEmpty(prev.Species) && prev.Species == current.Species)
            return true;

        return false;
    }

    // ---- Internal Helpers ----

    private static DailyPlanDataResult DeepCopyDp(DailyPlanDataResult source)
    {
        var copy = new DailyPlanDataResult
        {
            FilePath = source.FilePath,
            IsSuccess = source.IsSuccess,
            ErrorMessage = source.ErrorMessage,
            Headers = new List<string>(source.Headers),
            Meta = source.Meta,
            LotGroup = source.LotGroup
        };
        foreach (var row in source.Rows)
            copy.Rows.Add(new List<string>(row));
        return copy;
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
    /// <summary>ProcessDailyPlanForExport 가공 완료 여부</summary>
    public bool IsProcessed { get; set; }
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

/// <summary>
/// PartList 보고서 서비스. VBA BC_PartListItem_Viewer.bas를 대체합니다.
/// </summary>
public class PartListService
{
    private readonly ExcelReaderService _excel;
    private readonly DirectoryManager _dirs;

    // VBA SetUsingColumns 대응: PartList 보존 열 목록
    private static readonly HashSet<string> _essentialColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "투입\n시점", "투입시점", "W/O", "모델", "Suffix", "계획 수량", "계획수량", "Tool",
        "수량", "-Line", "Line"
    };

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
        var files = FileSearcher.FindFiles(_dirs.SourcePartList, "", ".xlsx");
        var result = new List<FileMetadata>(files.Count);
        for (int i = 0; i < files.Count; i++)
        {
            result.Add(FileMetadata.Parse(files[i], baseYear));
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

    // ==================================================================================
    // VBA AR_1_EssentialDataExtraction 대응: 7단계 가공 파이프라인
    // ==================================================================================

    /// <summary>
    /// PartList 데이터를 보고서 출력용으로 가공합니다.
    /// VBA AR_1_EssentialDataExtraction(PartList)의 전체 파이프라인에 대응합니다.
    /// 
    /// 처리 순서 (VBA 원본과 동일):
    ///   1. 투입시점 병합 (YYYYMMDD + Input Time → DateTime)
    ///   2. D-Day N일 트리밍 (N일차까지만 보존)
    ///   3. 불필요 열 삭제 (SetUsingColumns)
    ///   4. 모델+Suffix 병합 ("모델.Suffix")
    ///   5. 동일 부품명 열 합치기 (PartCombine)
    ///   6. W/O 중복 행 제거 + 수량 합산
    ///   7. 벤더명 정규화 (Replacing_Parts)
    /// </summary>
    public PartListDataResult ProcessPartListForExport(PartListDataResult raw, int dayCount = 3)
    {
        if (!raw.IsSuccess || raw.Rows.Count == 0) return raw;

        // deep copy
        var result = DeepCopy(raw);

        // Step 1: 투입시점 병합
        MergeDateTimeColumns(result);

        // Step 2: D-Day N일 트리밍
        TrimByDayCount(result, dayCount);

        // Step 3: 불필요 열 삭제
        FilterEssentialColumns(result);

        // Step 4: 모델+Suffix 병합
        MergeModelSuffix(result);

        // Step 5: 동일 부품명 열 합치기
        CombineDuplicateParts(result);

        // Step 6: W/O 중복 행 제거 + 수량 합산
        RemoveDuplicateWorkOrders(result);

        // Step 7: 벤더 정규화 (자재 열 전체에 NormalizeCellValue 적용)
        NormalizeAllPartColumns(result);

        result.IsProcessed = true;
        return result;
    }

    // ─── Step 1: 투입시점 병합 ───────────────────────────────────────────

    /// <summary>
    /// YYYYMMDD 열 + Input Time 열 → 투입시점 열에 합산.
    /// VBA MergeDateTime_Flexible에 대응합니다.
    /// </summary>
    private void MergeDateTimeColumns(PartListDataResult data)
    {
        int dateCol = FindHeaderIndex(data.Headers, "YYYYMMDD", true);
        int timeCol = FindHeaderIndex(data.Headers, "Input Time", true);
        int targetCol = FindHeaderIndex(data.Headers, "투입", true); // "투입\n시점" 또는 "투입시점"

        if (dateCol < 0 || targetCol < 0) return;

        foreach (var row in data.Rows)
        {
            string dateStr = GetCell(row, dateCol);
            string timeStr = timeCol >= 0 ? GetCell(row, timeCol) : "";

            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            // 날짜 파싱
            if (!DateTimeParser.TryParse(dateStr, out DateTime baseDate)) continue;

            // 시간 병합
            if (!string.IsNullOrWhiteSpace(timeStr) && DateTimeParser.TryParse($"{dateStr} {timeStr}", out DateTime merged))
            {
                SetCell(row, targetCol, merged.ToString("yyyy-MM-dd HH:mm"));
            }
            else
            {
                SetCell(row, targetCol, baseDate.ToString("yyyy-MM-dd"));
            }
        }
    }

    // ─── Step 2: D-Day N일 트리밍 ────────────────────────────────────────

    /// <summary>
    /// 날짜 변경 횟수 기준으로 N일차까지만 보존하고 나머지 행을 삭제합니다.
    /// VBA D-Day 트리밍 로직에 대응합니다.
    /// </summary>
    private void TrimByDayCount(PartListDataResult data, int maxDays)
    {
        if (maxDays <= 0) return;

        int dateCol = FindHeaderIndex(data.Headers, "투입", true);
        if (dateCol < 0) return;

        // 날짜 변경 횟수 카운트
        var distinctDates = new HashSet<string>();
        var rowsToKeep = new List<List<string>>();

        foreach (var row in data.Rows)
        {
            string dateStr = GetCell(row, dateCol);
            if (!string.IsNullOrWhiteSpace(dateStr) && DateTimeParser.TryParse(dateStr, out DateTime dt))
            {
                string dateKey = dt.ToString("yyyy-MM-dd");
                distinctDates.Add(dateKey);
            }

            if (distinctDates.Count <= maxDays)
            {
                rowsToKeep.Add(row);
            }
        }

        data.Rows.Clear();
        data.Rows.AddRange(rowsToKeep);
    }

    // ─── Step 3: 불필요 열 삭제 ──────────────────────────────────────────

    /// <summary>
    /// 보존 열 목록에 없는 열을 삭제합니다.
    /// VBA SetUsingColumns에 대응합니다.
    /// 자재 데이터 열 ("-Line" 이후의 열)은 무조건 보존합니다.
    /// </summary>
    private void FilterEssentialColumns(PartListDataResult data)
    {
        if (data.Headers.Count == 0) return;

        // "-Line"을 찾아 자재 열의 시작 지점 파악
        int lineColIdx = FindHeaderIndex(data.Headers, "-Line", true);
        int partStartIdx = lineColIdx >= 0 ? lineColIdx + 1 : data.Headers.Count;

        var keepIndices = new List<int>();
        for (int i = 0; i < data.Headers.Count; i++)
        {
            string header = data.Headers[i].Replace("\n", "").Trim();

            // 자재 열(partStartIdx 이후)은 무조건 보존
            if (i >= partStartIdx)
            {
                keepIndices.Add(i);
                continue;
            }

            // 필수 열 목록에 포함되면 보존
            if (_essentialColumns.Any(e => header.Contains(e.Replace("\n", ""), StringComparison.OrdinalIgnoreCase)))
            {
                keepIndices.Add(i);
            }
        }

        // 재구성
        data.Headers = keepIndices.Select(i => data.Headers[i]).ToList();
        for (int r = 0; r < data.Rows.Count; r++)
        {
            data.Rows[r] = keepIndices
                .Select(i => i < data.Rows[r].Count ? data.Rows[r][i] : string.Empty)
                .ToList();
        }
    }

    // ─── Step 4: 모델+Suffix 병합 ───────────────────────────────────────

    /// <summary>
    /// 모델 열과 Suffix 열을 "모델.Suffix" 형식으로 합친 후 Suffix 열 삭제.
    /// </summary>
    private void MergeModelSuffix(PartListDataResult data)
    {
        int modelCol = FindHeaderIndex(data.Headers, "모델", false);
        int suffixCol = FindHeaderIndex(data.Headers, "Suffix", false);
        if (modelCol < 0 || suffixCol < 0) return;

        foreach (var row in data.Rows)
        {
            string model = GetCell(row, modelCol);
            string suffix = GetCell(row, suffixCol);
            if (!string.IsNullOrWhiteSpace(model) && !string.IsNullOrWhiteSpace(suffix))
            {
                SetCell(row, modelCol, $"{model}.{suffix}");
            }
        }

        // Suffix 열 삭제
        RemoveColumn(data, suffixCol);
    }

    // ─── Step 5: 동일 부품명 열 합치기 ──────────────────────────────────

    /// <summary>
    /// 동일 헤더명을 가진 자재 열들의 데이터를 합치고 잉여 열을 삭제합니다.
    /// VBA PartCombine에 대응합니다.
    /// </summary>
    private void CombineDuplicateParts(PartListDataResult data)
    {
        // 자재 열 영역 식별 ("-Line" 이후)
        int lineCol = FindHeaderIndex(data.Headers, "-Line", true);
        int partStart = lineCol >= 0 ? lineCol + 1 : data.Headers.Count;
        if (partStart >= data.Headers.Count) return;

        // 중복 헤더 찾기
        var headerGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = partStart; i < data.Headers.Count; i++)
        {
            string key = StringParser.ExtractBracketValue(data.Headers[i]);
            if (string.IsNullOrWhiteSpace(key)) key = data.Headers[i].Trim();
            if (!headerGroups.ContainsKey(key))
                headerGroups[key] = new List<int>();
            headerGroups[key].Add(i);
        }

        // 중복이 있는 그룹만 처리
        var columnsToRemove = new HashSet<int>();
        foreach (var group in headerGroups.Values.Where(g => g.Count > 1))
        {
            int primaryCol = group[0];
            foreach (int dupCol in group.Skip(1))
            {
                // 데이터를 주(primary) 열로 합치기
                foreach (var row in data.Rows)
                {
                    string primary = GetCell(row, primaryCol);
                    string dup = GetCell(row, dupCol);
                    if (!string.IsNullOrWhiteSpace(dup))
                    {
                        if (string.IsNullOrWhiteSpace(primary))
                            SetCell(row, primaryCol, dup);
                        else
                            SetCell(row, primaryCol, primary + "\n" + dup);
                    }
                }
                columnsToRemove.Add(dupCol);
            }
        }

        // 삭제 (뒤에서 앞으로)
        foreach (int col in columnsToRemove.OrderByDescending(c => c))
        {
            RemoveColumn(data, col);
        }
    }

    // ─── Step 6: W/O 중복 행 제거 + 수량 합산 ──────────────────────────

    /// <summary>
    /// W/O 열 기준으로 중복 행을 제거하고 수량을 합산합니다.
    /// VBA DeleteDuplicateRowsInColumn에 대응합니다.
    /// </summary>
    private void RemoveDuplicateWorkOrders(PartListDataResult data)
    {
        int woCol = FindHeaderIndex(data.Headers, "W/O", false);
        int qtyCol = FindHeaderIndex(data.Headers, "수량", true);
        if (woCol < 0) return;

        var seen = new Dictionary<string, int>(); // W/O → 첫 출현 행 인덱스
        var rowsToRemove = new HashSet<int>();

        for (int i = data.Rows.Count - 1; i >= 0; i--)
        {
            string wo = GetCell(data.Rows[i], woCol).Trim();
            if (string.IsNullOrWhiteSpace(wo)) continue;

            if (seen.ContainsKey(wo))
            {
                // 수량 합산
                int existingIdx = seen[wo];
                if (qtyCol >= 0)
                {
                    long existingQty = ParseLong(GetCell(data.Rows[existingIdx], qtyCol));
                    long currentQty = ParseLong(GetCell(data.Rows[i], qtyCol));
                    SetCell(data.Rows[existingIdx], qtyCol, (existingQty + currentQty).ToString());
                }
                rowsToRemove.Add(i);
            }
            else
            {
                seen[wo] = i;
            }
        }

        // 행 삭제 (인덱스 역순)
        foreach (int idx in rowsToRemove.OrderByDescending(i => i))
        {
            data.Rows.RemoveAt(idx);
        }
    }

    // ─── Step 7: 전체 자재 열 벤더 정규화 ───────────────────────────────

    /// <summary>
    /// 자재 데이터 열의 모든 셀에 벤더 정규화를 적용합니다.
    /// </summary>
    private void NormalizeAllPartColumns(PartListDataResult data)
    {
        int lineCol = FindHeaderIndex(data.Headers, "-Line", true);
        int partStart = lineCol >= 0 ? lineCol + 1 : data.Headers.Count;
        if (partStart >= data.Headers.Count) return;

        for (int c = partStart; c < data.Headers.Count; c++)
        {
            string colHeader = data.Headers[c];
            foreach (var row in data.Rows)
            {
                string val = GetCell(row, c);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    SetCell(row, c, NormalizeCellValue(val, colHeader));
                }
            }
        }
    }

    // ==================================================================================
    // 기존 메서드 (NormalizeCellValue / FilterByFeeder)
    // ==================================================================================

    /// <summary>
    /// 자재 셀 값을 표준 형식으로 정규화합니다.
    /// VBA Replacing_Parts에 대응합니다.
    /// "[벤더] 파트1/파트2(수량)" 형식으로 통일하고, Burner 특수 매핑을 처리합니다.
    /// </summary>
    public string NormalizeCellValue(string rawValue, string columnHeader = "")
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return rawValue;

        // Burner 컬럼 특수 매핑 (VBA 하드코딩 로직)
        if (columnHeader.Contains("Burner", StringComparison.OrdinalIgnoreCase))
            return MapBurnerValue(rawValue.Trim());

        // " [" → "$[" 치환 후 "$" 기준으로 벤더 블록 분리
        string sample = rawValue.Trim().Replace(" [", "$[");
        var vendorBlocks = sample.Split('$', StringSplitOptions.RemoveEmptyEntries);

        var parts = new List<string>();
        foreach (var block in vendorBlocks)
        {
            string vendor = LARS.Utils.StringParser.ExtractBracketValue(block);

            // 벤더명 정규화: VBA 조건 그대로
            vendor = NormalizeVendorName(vendor);

            string remainder = block.Replace($"[{LARS.Utils.StringParser.ExtractBracketValue(block)}]", "").Trim();
            if (!string.IsNullOrWhiteSpace(remainder))
                parts.Add($"[{vendor}] {remainder}");
        }

        return string.Join(" ", parts).Trim();
    }

    /// <summary>
    /// 벤더명을 정규화합니다.
    /// VBA Replacing_Parts의 벤더 정규화 로직에 대응합니다.
    /// </summary>
    private static string NormalizeVendorName(string vendor)
    {
        if (string.IsNullOrWhiteSpace(vendor) || vendor.Length < 1)
            return "자사품";

        // 불필요 문자열 제거 (VBA 로직)
        vendor = vendor
            .Replace("(주)", "")
            .Replace("㈜", "")
            .Replace("EKHQ_", "")
            .Replace(" Co., Ltd.", "")
            .Replace(" Co.,Ltd.", "")
            .Replace(" Corp.", "")
            .Replace(" Inc.", "")
            .Trim();

        // 5글자 이상이면서 한글 없음 → "도입품" (영문 벤더)
        if (vendor.Length >= 5 && !ContainsKorean(vendor))
            return "도입품";

        // 빈 문자열이면 "자사품"
        if (string.IsNullOrWhiteSpace(vendor))
            return "자사품";

        return vendor;
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
        "[기미] 4102/4202/4402/4502" => "[피킹] Oval/Best",
        "[기미] 4102/4202/4402/4502 [SABAF S.P.A.] 6904/7302" => "[피킹] Oval/Best/Sabaf",
        "[기미] 4102/4202/4402/4502(2)" => "[피킹] Oval/Better",
        "[기미] 7906/8506/8606/8706" => "[피킹] FZ≒FH/Better",
        _ => raw.Trim()  // 매칭 없으면 원본 유지 (일반 정규화 처리)
    };

    // ==================================================================================
    // Internal Helpers
    // ==================================================================================

    /// <summary>헤더에서 열 인덱스 찾기. partialMatch=true면 부분 일치.</summary>
    private static int FindHeaderIndex(List<string> headers, string search, bool partialMatch)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            string h = headers[i].Replace("\n", "").Trim();
            if (partialMatch)
            {
                if (h.Contains(search.Replace("\n", ""), StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            else
            {
                if (h.Equals(search, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }
        return -1;
    }

    private static string GetCell(List<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index] : string.Empty;

    private static void SetCell(List<string> row, int index, string value)
    {
        while (row.Count <= index) row.Add(string.Empty);
        row[index] = value;
    }

    private static void RemoveColumn(PartListDataResult data, int colIndex)
    {
        if (colIndex < 0 || colIndex >= data.Headers.Count) return;
        data.Headers.RemoveAt(colIndex);
        foreach (var row in data.Rows)
        {
            if (colIndex < row.Count) row.RemoveAt(colIndex);
        }
    }

    private static long ParseLong(string s) =>
        long.TryParse(s.Trim(), out long v) ? v : 0;

    private static PartListDataResult DeepCopy(PartListDataResult source)
    {
        var copy = new PartListDataResult
        {
            FilePath = source.FilePath,
            IsSuccess = source.IsSuccess,
            ErrorMessage = source.ErrorMessage,
            Headers = new List<string>(source.Headers),
            DateInfo = source.DateInfo,
            LineInfo = source.LineInfo
        };
        foreach (var row in source.Rows)
            copy.Rows.Add(new List<string>(row));
        return copy;
    }
}

public class PartListDataResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    /// <summary>FilterByFeeder 적용 여부</summary>
    public bool IsFiltered { get; set; }
    /// <summary>날짜 정보 (예: "5월-28일")</summary>
    public string? DateInfo { get; set; }
    /// <summary>라인 정보 (예: "C11")</summary>
    public string? LineInfo { get; set; }
    /// <summary>ProcessPartListForExport 가공 완료 여부</summary>
    public bool IsProcessed { get; set; }
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

    /// <summary>
    /// PartList 데이터와 DailyPlan 스케줄(날짜, 롯트 수)을 결합해 파이프라인을 실행합니다.
    /// 자재 수량에 날짜별 롯트 수를 곱해 집계합니다.
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

                // 각 스케줄 날짜/롯트 수에 대해 아이템 생성 후 합산
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
