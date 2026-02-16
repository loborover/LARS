using System.IO;
using System.Text.RegularExpressions;

namespace LARS.Utils;

/// <summary>
/// 문자열 파싱 유틸리티.
/// VBA Utillity.bas의 ExtractBracketValue, ExtractSmallBracketValue, RemoveLineBreaks를 대체합니다.
/// </summary>
public static class StringParser
{
    /// <summary>
    /// 대괄호 내 값 추출. "[VENDOR]" → "VENDOR"
    /// </summary>
    public static string ExtractBracketValue(string text)
    {
        int start = text.IndexOf('[');
        int end = text.IndexOf(']');
        if (start >= 0 && end > start)
            return text[(start + 1)..end];
        return string.Empty;
    }

    /// <summary>
    /// 소괄호 내 값 추출. "(3)" → "3"
    /// </summary>
    public static string ExtractSmallBracketValue(string text)
    {
        int start = text.IndexOf('(');
        int end = text.IndexOf(')');
        if (start >= 0 && end > start)
            return text[(start + 1)..end];
        return string.Empty;
    }

    /// <summary>
    /// 줄바꿈 문자 제거.
    /// </summary>
    public static string RemoveLineBreaks(string text) =>
        text.Replace("\r\n", "").Replace("\n", "").Replace("\r", "").Trim();

    /// <summary>
    /// 열 번호를 알파벳으로 변환. 1→A, 27→AA
    /// VBA ColumnLetter 대응.
    /// </summary>
    public static string ColumnLetter(int columnNumber)
    {
        string name = string.Empty;
        int d = columnNumber;
        while (d > 0)
        {
            int m = (d - 1) % 26;
            name = (char)(65 + m) + name;
            d = (d - m) / 26;
        }
        return name;
    }
}

/// <summary>
/// 파일 탐색 유틸리티.
/// VBA FindFilesWithTextInName를 대체합니다.
/// </summary>
public static class FileSearcher
{
    /// <summary>
    /// 디렉토리에서 특정 텍스트가 이름에 포함된 파일을 검색합니다.
    /// </summary>
    public static List<string> FindFiles(string directory, string searchText,
        string extension = ".xlsx")
    {
        if (!Directory.Exists(directory))
            return new List<string>();

        return Directory.GetFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f).Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).StartsWith("~$")) // 임시파일 제외
            .OrderBy(f => f)
            .ToList();
    }
}

/// <summary>
/// 날짜/시간 파싱 유틸리티.
/// VBA TimeKeeper.bas를 대체합니다.
/// .NET DateTime 파싱 + 한국어 오전/오후 처리.
/// </summary>
public static class DateTimeParser
{
    private static readonly string[] _formats = new[]
    {
        "yyyyMMdd HH:mm:ss", "yyyyMMdd HH:mm", "yyyyMMdd",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
        "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd",
        "M월-d일", "M월_d일", "MM-dd"
    };

    /// <summary>
    /// 다양한 포맷의 날짜/시간 문자열을 파싱합니다.
    /// 한국어 오전/오후를 AM/PM으로 변환 후 파싱 시도합니다.
    /// </summary>
    public static bool TryParse(string text, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // 한국어 오전/오후 → AM/PM
        string normalized = NormalizeKoreanAmPm(text.Trim());

        // 표준 포맷 시도
        if (DateTime.TryParseExact(normalized, _formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result))
            return true;

        // 일반 파싱
        if (DateTime.TryParse(normalized, out result))
            return true;

        // YYYYMMDD 숫자 직접 파싱
        if (long.TryParse(text.Trim(), out long numVal) && text.Trim().Length == 8)
        {
            int y = (int)(numVal / 10000);
            int m = (int)(numVal % 10000 / 100);
            int d = (int)(numVal % 100);
            try
            {
                result = new DateTime(y, m, d);
                return true;
            }
            catch { /* 유효하지 않은 날짜 */ }
        }

        return false;
    }

    /// <summary>
    /// 한국어 오전/오후를 AM/PM으로 치환합니다.
    /// </summary>
    public static string NormalizeKoreanAmPm(string text)
    {
        return text
            .Replace("오전", "AM")
            .Replace("오후", "PM")
            .Replace("  ", " ")
            .Trim();
    }
}
