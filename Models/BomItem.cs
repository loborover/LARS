namespace LARS.Models;

public class BomItem
{
    // BOM 계층 레벨 (예: 1, 2, .1, ..2 등)
    public string Level { get; set; } = string.Empty;

    // 부품 번호 (Key)
    public string PartNo { get; set; } = string.Empty;

    // 부품 설명
    public string Description { get; set; } = string.Empty;

    // 수량
    public double Quantity { get; set; }

    // 단위 (예: EA, M)
    public string Uom { get; set; } = string.Empty;

    // 제조사
    public string Maker { get; set; } = string.Empty;

    // 공급 유형 (예: Vendor, In-house)
    public string SupplyType { get; set; } = string.Empty;
}
