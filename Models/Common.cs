using System.IO;

namespace LARS.Models;

/// <summary>
/// 문서 유형 열거형 (VBA DocumentTypes 대응)
/// </summary>
public enum DocumentType
{
    BOM,
    DailyPlan,
    PartList
}

/// <summary>
/// 파일 메타데이터: 경로, 날짜, 라인, 문서 유형 등을 저장합니다.
/// VBA MDToken 구조체 + GetFoundSentences를 대체합니다.
/// </summary>
public class FileMetadata
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime? DateValue { get; set; }
    public string Line { get; set; } = string.Empty;
    public DayOfWeek? Weekday { get; set; }
    public string WeekdayKorean { get; set; } = string.Empty;
    public DocumentType DocType { get; set; }
    public string Status { get; set; } = "Ready";

    /// <summary>
    /// 파일 경로로부터 메타데이터를 파싱합니다.
    /// VBA ParseMDToken을 대체합니다.
    /// </summary>
    public static FileMetadata Parse(string fullPath, int baseYear = 0)
    {
        var meta = new FileMetadata { FullPath = fullPath };
        meta.FileName = Path.GetFileName(fullPath);

        // 문서 타입 판별
        if (meta.FileName.Contains("DailyPlan", StringComparison.OrdinalIgnoreCase))
            meta.DocType = DocumentType.DailyPlan;
        else if (meta.FileName.Contains("PartList", StringComparison.OrdinalIgnoreCase))
            meta.DocType = DocumentType.PartList;
        else if (meta.FileName.Contains("BOM", StringComparison.OrdinalIgnoreCase))
            meta.DocType = DocumentType.BOM;

        // 날짜 파싱: "5월-28일" 패턴
        TryParseKoreanDate(meta.FileName, baseYear, meta);

        // 라인 파싱: "C11" 패턴
        TryParseLine(meta.FileName, meta);

        return meta;
    }

    private static void TryParseKoreanDate(string fileName, int baseYear, FileMetadata meta)
    {
        // "5월-28일" 또는 "5월_28일" 패턴 매칭
        var match = System.Text.RegularExpressions.Regex.Match(
            fileName, @"(\d{1,2})월[-_]?(\d{1,2})일");

        if (match.Success)
        {
            int month = int.Parse(match.Groups[1].Value);
            int day = int.Parse(match.Groups[2].Value);
            int year = baseYear > 0 ? baseYear : DateTime.Now.Year;

            try
            {
                meta.DateValue = new DateTime(year, month, day);
                meta.Weekday = meta.DateValue.Value.DayOfWeek;
                meta.WeekdayKorean = meta.Weekday switch
                {
                    DayOfWeek.Monday => "월",
                    DayOfWeek.Tuesday => "화",
                    DayOfWeek.Wednesday => "수",
                    DayOfWeek.Thursday => "목",
                    DayOfWeek.Friday => "금",
                    DayOfWeek.Saturday => "토",
                    DayOfWeek.Sunday => "일",
                    _ => ""
                };
            }
            catch { /* 유효하지 않은 날짜 → 무시 */ }
        }
    }

    private static void TryParseLine(string fileName, FileMetadata meta)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            fileName, @"[_]?(C\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
            meta.Line = match.Groups[1].Value.ToUpper();
    }

    public override string ToString() =>
        $"[{DocType}] {FileName} Date={DateValue:yyyy-MM-dd} Line={Line}";
}

/// <summary>
/// 인쇄 설정 (VBA PrintSetting Type 대응)
/// </summary>
public class PrintSettings
{
    public string PrintArea { get; set; } = string.Empty;
    public bool IsLandscape { get; set; }
    public double LeftMargin { get; set; } = 0.3;
    public double RightMargin { get; set; } = 0.3;
    public double TopMargin { get; set; } = 0.6;
    public double BottomMargin { get; set; } = 0.4;
    public bool CenterHorizontally { get; set; } = true;
    public bool CenterVertically { get; set; }
    public string PrintTitleRows { get; set; } = string.Empty;
    public int FitToPagesWide { get; set; } = 1;
    public int FitToPagesTall { get; set; }
    public string HeaderLeft { get; set; } = string.Empty;
    public string HeaderRight { get; set; } = string.Empty;
}
