namespace LARS.Models;

public class DailyPlanItem
{
    // 날짜 (파일명 또는 데일리 플랜 내용에서 추출)
    public string Date { get; set; } = string.Empty;

    // 라인 정보 (예: A, B Line)
    public string Line { get; set; } = string.Empty;

    // 파일 경로
    public string FilePath { get; set; } = string.Empty;

    // 인쇄 상태
    public string PrintStatus { get; set; } = "Ready";

    // PDF 상태
    public string PdfStatus { get; set; } = "Pending";

    // 파일명 반환
    public string FileName => System.IO.Path.GetFileName(FilePath);
}
