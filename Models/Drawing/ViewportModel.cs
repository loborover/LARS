using System.Collections.ObjectModel;

namespace LARS.Models.Drawing;

/// <summary>
/// Viewport(캔버스) 모델. 페이지 크기, 방향, 격자 스냅, 줌 등을 관리합니다.
/// </summary>
public class ViewportModel
{
    // ── 페이지 설정 ──
    public double PageWidthMm { get; set; } = 210;   // A4 세로
    public double PageHeightMm { get; set; } = 297;
    public bool IsLandscape { get; set; }

    /// <summary>실제 표시 너비 (방향 반영)</summary>
    public double EffectiveWidth => IsLandscape ? PageHeightMm : PageWidthMm;

    /// <summary>실제 표시 높이 (방향 반영)</summary>
    public double EffectiveHeight => IsLandscape ? PageWidthMm : PageHeightMm;

    // ── 격자 & 줌 ──
    public double GridSnapMm { get; set; } = 1;       // 1mm 단위 스냅
    public bool IsGridVisible { get; set; } = true;
    public double Zoom { get; set; } = 1.0;           // 1.0 = 100%

    // ── 배치된 도형 ──
    public ObservableCollection<ShapeBase> Shapes { get; set; } = new();
    public ObservableCollection<CompositeShape> Composites { get; set; } = new();

    /// <summary>
    /// mm 좌표를 격자에 스냅합니다.
    /// </summary>
    public double Snap(double valueMm)
    {
        if (GridSnapMm <= 0) return valueMm;
        return Math.Round(valueMm / GridSnapMm) * GridSnapMm;
    }
}
