namespace LARS.Models.Drawing;

/// <summary>
/// 텍스트 도형. 폰트, 정렬, 색상 설정 + 데이터 바인딩용 플레이스홀더 지원.
/// 플레이스홀더 예시: "{Model}", "{W/O}" → 매크로 결과에서 자동 치환.
/// </summary>
public class TextBoxShape : ShapeBase
{
    public string Text { get; set; } = "";
    public string FontFamily { get; set; } = "맑은 고딕";
    public double FontSize { get; set; } = 12;
    public string FontWeight { get; set; } = "Normal";  // Normal, Bold
    public string TextColor { get; set; } = "#000000";
    public string HAlign { get; set; } = "Left";        // Left, Center, Right
    public string VAlign { get; set; } = "Top";          // Top, Center, Bottom
    public string? BackgroundColor { get; set; }
}
