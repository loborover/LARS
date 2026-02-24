namespace LARS.Models;

/// <summary>
/// 스티커 라벨 한 장에 출력될 자재 정보.
/// VBA StickerLabel.cls의 데이터 모델을 C#으로 이관.
/// </summary>
public record StickerLabelInfo(
    string NickName,
    string Vendor,
    string PartNumber,
    long   QTY
);

/// <summary>
/// 스티커 라벨 PDF 출력 설정.
/// </summary>
public record StickerLabelSettings
{
    /// <summary>라벨 너비 (mm). 기본 70mm.</summary>
    public double WidthMm  { get; init; } = 70;

    /// <summary>라벨 높이 (mm). 기본 37mm.</summary>
    public double HeightMm { get; init; } = 37;

    /// <summary>한 행에 배치할 라벨 열 수. 기본 2열.</summary>
    public int Columns { get; init; } = 2;

    /// <summary>페이지 여백 (mm). 기본 10mm.</summary>
    public double MarginMm { get; init; } = 10;

    /// <summary>라벨 간격 (mm). 기본 3mm.</summary>
    public double GapMm { get; init; } = 3;
}
