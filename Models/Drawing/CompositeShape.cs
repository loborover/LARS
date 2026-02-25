namespace LARS.Models.Drawing;

/// <summary>
/// 응용 도형. Primitive Shape를 조합하여 하나의 단위로 관리합니다.
/// 자식 도형의 좌표는 CompositeShape 기준 로컬 좌표입니다.
/// (Viewport 내 실제 위치 = CompositeShape.X + Child.X)
/// </summary>
public class CompositeShape
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Untitled";

    // ── Viewport 내 위치/변환 ──
    public double X { get; set; }
    public double Y { get; set; }
    public double PivotX { get; set; } = 0.5;
    public double PivotY { get; set; } = 0.5;
    public double Rotation { get; set; }

    // ── 자식 도형 목록 ──
    public List<ShapeBase> Children { get; set; } = new();

    // ── 종속 메타데이터 (자식 Bounding Box 기반 자동 계산) ──

    /// <summary>자식 도형의 Bounding Box 너비</summary>
    public double BoundsWidth => Children.Count > 0
        ? Children.Max(c => c.X + c.Width) - Children.Min(c => c.X)
        : 0;

    /// <summary>자식 도형의 Bounding Box 높이</summary>
    public double BoundsHeight => Children.Count > 0
        ? Children.Max(c => c.Y + c.Height) - Children.Min(c => c.Y)
        : 0;

    /// <summary>자식 도형의 좌상단 X 오프셋</summary>
    public double BoundsLeft => Children.Count > 0 ? Children.Min(c => c.X) : 0;

    /// <summary>자식 도형의 좌상단 Y 오프셋</summary>
    public double BoundsTop => Children.Count > 0 ? Children.Min(c => c.Y) : 0;

    /// <summary>피벗의 절대 좌표</summary>
    public (double PxX, double PxY) GetPivotAbsolute() =>
        (X + BoundsWidth * PivotX, Y + BoundsHeight * PivotY);
}
