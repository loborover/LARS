namespace LARS.Models;

/// <summary>
/// 모델 정보를 파싱하고 저장하는 레코드 클래스.
/// VBA의 ModelInfo.cls를 대체합니다.
/// 예: "LSGL6335F.A" → Type=LSGL, Spec=6335, Color=F, Suffix=A
/// </summary>
public class ModelInfo
{
    public string WorkOrder { get; set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string SpecNumber { get; private set; } = string.Empty;
    public string Spec { get; private set; } = string.Empty;
    public string ModelType { get; private set; } = string.Empty;
    public string Species { get; private set; } = string.Empty;
    public string TySpec { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
    public string Suffix { get; private set; } = string.Empty;
    public int Row { get; set; }
    public int Column { get; set; }

    /// <summary>
    /// 풀네임(예: "LSGL6335F.A")을 파싱하여 각 필드를 자동 분해합니다.
    /// VBA ParseModelinfo 서브루틴에 대응합니다.
    /// </summary>
    public void SetFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || FullName == fullName)
            return;

        FullName = fullName;

        int dot = fullName.IndexOf('.');
        if (dot <= 0) return;

        Number = fullName[..dot];
        Suffix = fullName[(dot + 1)..];

        // Number가 최소 9자: LSGL6335F (Type4 + Spec4 + Color1+)
        if (Number.Length >= 8)
        {
            ModelType = Number[..4];           // LSGL
            Spec = Number.Substring(4, 4);     // 6335
            SpecNumber = ModelType + Spec;      // LSGL6335
            Species = ModelType[..2] + Spec[..2]; // LS63
            TySpec = ModelType[..2] + Spec;     // LS6335
            Color = Number.Length > 8 ? Number[8..] : string.Empty; // F
        }
    }

    /// <summary>
    /// 깊은 복사본을 반환합니다.
    /// </summary>
    public ModelInfo Copy() => new()
    {
        WorkOrder = WorkOrder,
        Row = Row,
        Column = Column,
        // SetFullName을 호출하여 파싱된 필드를 모두 복사
        FullName = FullName,
        Number = Number,
        SpecNumber = SpecNumber,
        Spec = Spec,
        ModelType = ModelType,
        Species = Species,
        TySpec = TySpec,
        Color = Color,
        Suffix = Suffix
    };

    /// <summary>
    /// 모델명으로부터 ModelInfo를 생성하는 정적 팩토리 메서드.
    /// </summary>
    public static ModelInfo Parse(string fullName, string workOrder = "")
    {
        var info = new ModelInfo { WorkOrder = workOrder };
        info.SetFullName(fullName);
        return info;
    }

    public override string ToString() => FullName;
}
