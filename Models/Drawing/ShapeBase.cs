namespace LARS.Models.Drawing;

/// <summary>
/// Drawing Engine의 모든 도형이 상속하는 기본 클래스.
/// 위치, 크기, 피벗, 회전, Z순서 등 공통 속성을 정의합니다.
/// </summary>
public abstract class ShapeBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    // ── 위치 & 크기 ──
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    // ── 피벗 (0.0~1.0 비율) ──
    // (0,0)=좌상단, (0.5,0.5)=중심, (1,1)=우하단
    // 회전·크기 변환 시 이 점을 기준으로 적용됩니다.
    public double PivotX { get; set; } = 0.5;
    public double PivotY { get; set; } = 0.5;

    // ── 회전 (도) ──
    public double Rotation { get; set; }

    // ── 겹침 순서 ──
    public int ZIndex { get; set; }

    // ── 선택 상태 (UI용) ──
    public bool IsSelected { get; set; }

    /// <summary>
    /// 피벗의 절대 좌표를 계산합니다.
    /// </summary>
    public (double PxX, double PxY) GetPivotAbsolute() =>
        (X + Width * PivotX, Y + Height * PivotY);
}
