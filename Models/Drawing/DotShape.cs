namespace LARS.Models.Drawing;

/// <summary>
/// 점 도형. 앵커 포인트 역할을 하며, Snap 시스템으로 다른 Dot과 위치를 동기화합니다.
/// 같은 SnapGroupId를 가진 Dot들은 항상 같은 위치에 고정됩니다.
/// </summary>
public class DotShape : ShapeBase
{
    public double Radius { get; set; } = 3;
    public string FillColor { get; set; } = "#FFD700";  // Gold

    /// <summary>앵커 포인트로 사용할지 여부</summary>
    public bool IsAnchor { get; set; } = true;

    /// <summary>
    /// 같은 SnapGroupId를 가진 Dot들은 위치가 자동 동기화됩니다.
    /// null이면 독립 Dot입니다.
    /// </summary>
    public string? SnapGroupId { get; set; }
}
