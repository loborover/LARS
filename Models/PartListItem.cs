namespace LARS.Models;

public class PartListItem
{
    // 생성 날짜 (파일명에서 추출)
    public string Date { get; set; } = string.Empty;

    // 파일 전체 경로
    public string FilePath { get; set; } = string.Empty;

    // 인쇄 상태 (Ready, Done 등)
    public string PrintStatus { get; set; } = "Ready";

    // PDF 생성 여부
    public string PdfStatus { get; set; } = "Pending";

    // 파일명만 반환
    public string FileName => System.IO.Path.GetFileName(FilePath);
}
