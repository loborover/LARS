namespace LARS.Models;

/// <summary>
/// LOT(생산 단위) 영역 정보.
/// VBA D_LOT.cls를 대체합니다.
/// Range 의존성을 제거하고 행/열 인덱스 기반으로 변환.
/// </summary>
public class Lot
{
    public int StartRow { get; set; }
    public int StartCol { get; set; }
    public int EndRow { get; set; }
    public int EndCol { get; set; }
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// LOT 내 모델 정보 목록 (VBA vLot_info 대응)
    /// </summary>
    public List<ModelInfo> Models { get; } = new();

    public Lot Copy() => new()
    {
        StartRow = StartRow,
        StartCol = StartCol,
        EndRow = EndRow,
        EndCol = EndCol,
        SheetName = SheetName
    };

    public override string ToString() =>
        $"LOT[{SheetName}] ({StartRow},{StartCol})-({EndRow},{EndCol}) Models={Models.Count}";
}

/// <summary>
/// LOT 그룹 관리. Main/Sub 두 종류의 그룹을 관리합니다.
/// VBA D_Maps.cls를 대체합니다.
/// </summary>
public class LotGroup
{
    public List<Lot> MainLots { get; } = new();
    public List<Lot> SubLots { get; } = new();

    public void AddLot(Lot lot, LotGroupType groupType = LotGroupType.Main)
    {
        var target = groupType == LotGroupType.Main ? MainLots : SubLots;
        target.Add(lot);
    }

    public void RemoveAll(LotGroupType groupType = LotGroupType.Main)
    {
        var target = groupType == LotGroupType.Main ? MainLots : SubLots;
        target.Clear();
    }

    public int Count(LotGroupType groupType) =>
        groupType == LotGroupType.Main ? MainLots.Count : SubLots.Count;

    public Lot? RecentLot(LotGroupType groupType = LotGroupType.Main, int offset = 0)
    {
        var target = groupType == LotGroupType.Main ? MainLots : SubLots;
        int index = target.Count - 1 + offset;
        return index >= 0 && index < target.Count ? target[index] : null;
    }
}

public enum LotGroupType
{
    Main,
    Sub
}
