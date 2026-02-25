namespace LARS.Models.Drawing;

/// <summary>
/// 선 도형. 시작점(X,Y)에서 끝점(X2,Y2)까지의 직선.
/// </summary>
public class LineShape : ShapeBase
{
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string StrokeColor { get; set; } = "#000000";
    public double StrokeThickness { get; set; } = 1;
    public string DashStyle { get; set; } = "Solid";  // Solid, Dash, Dot, DashDot
}
