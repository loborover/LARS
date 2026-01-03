namespace LARS.Models;

public class ItemSummary
{
    // 부품 번호 (Key)
    public string PartNo { get; set; } = string.Empty;

    // 총 수량
    public double TotalQuantity { get; set; }

    // 설명
    public string Description { get; set; } = string.Empty;

    // 집계된 날짜
    public string Date { get; set; } = string.Empty;

    // 포함된 파일 수 (디버깅/정보용)
    public int FileCount { get; set; }
}
