namespace LARS.Models;

/// <summary>
/// Feeder 단위. 이름과 자재 목록을 관리합니다.
/// VBA FeederUnit.cls를 대체합니다.
/// </summary>
public class FeederUnit
{
    public string Name { get; set; } = string.Empty;
    public List<string> Items { get; } = new();

    public FeederUnit Copy()
    {
        var copy = new FeederUnit { Name = Name };
        copy.Items.AddRange(Items);
        return copy;
    }

    public override string ToString() => $"Feeder[{Name}] Items={Items.Count}";
}

/// <summary>
/// 생산 모델 트래커. 이전/현재/다음 모델을 추적합니다.
/// VBA ProductModel2.cls를 대체합니다.
/// </summary>
public class ProductModel
{
    public ModelInfo Previous { get; private set; } = new();
    public ModelInfo Current { get; private set; } = new();
    public ModelInfo Next { get; private set; } = new();
    public int LotCount { get; private set; } = 1;

    /// <summary>
    /// 다음 모델로 전진합니다 (Previous ← Current ← Next ← new)
    /// </summary>
    public void Advance(string nextFullName, string workOrder = "")
    {
        Previous = Current.Copy();
        Current = Next.Copy();
        Next = ModelInfo.Parse(nextFullName, workOrder);
        LotCount++;
    }

    /// <summary>
    /// 두 모델의 특정 필드를 비교합니다.
    /// </summary>
    public static bool CompareField(ModelInfo a, ModelInfo b, ModelInfoField field) => field switch
    {
        ModelInfoField.WorkOrder => a.WorkOrder == b.WorkOrder,
        ModelInfoField.FullName => a.FullName == b.FullName,
        ModelInfoField.Number => a.Number == b.Number,
        ModelInfoField.SpecNumber => a.SpecNumber == b.SpecNumber,
        ModelInfoField.Spec => a.Spec == b.Spec,
        ModelInfoField.ModelType => a.ModelType == b.ModelType,
        ModelInfoField.Species => a.Species == b.Species,
        ModelInfoField.TySpec => a.TySpec == b.TySpec,
        ModelInfoField.Color => a.Color == b.Color,
        ModelInfoField.Suffix => a.Suffix == b.Suffix,
        _ => false
    };
}

/// <summary>
/// 모델 비교에 사용할 필드 (VBA ModelinfoFeild Enum 대응)
/// </summary>
public enum ModelInfoField
{
    WorkOrder,
    FullName,
    Number,
    SpecNumber,
    Spec,
    ModelType,
    Species,
    TySpec,
    Color,
    Suffix
}
