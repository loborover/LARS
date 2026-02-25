namespace LARS.Models.Drawing;

/// <summary>
/// 사각형 도형. 배경색, 테두리, 모서리 둥글기를 지원합니다.
/// </summary>
public class BoxShape : ShapeBase
{
    public string FillColor { get; set; } = "#FFFFFF";
    public string BorderColor { get; set; } = "#000000";
    public double BorderThickness { get; set; } = 1;
    public double CornerRadius { get; set; }
}
