namespace LARS.Models;

public class StickerData
{
    // 메인 텍스트 (예: 부품 번호)
    public string MainText { get; set; } = string.Empty;

    // 서브 텍스트들 (예: 수량, 위치 등)
    public List<string> SubTexts { get; set; } = new List<string>();

    // 라벨 모양 타입 (VBA Enum 참조)
    public LabelShape Shape { get; set; } = LabelShape.Round;
    
    // 방향
    public LabelDirection Direction { get; set; } = LabelDirection.Right;
}

public enum LabelShape
{
    Box = 1,
    Round = 69,
    Box_Hexagon = 10,
    Arrow = 58
}

public enum LabelDirection
{
    Left,
    Right,
    Up,
    Down
}
