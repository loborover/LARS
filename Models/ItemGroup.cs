namespace LARS.Models;

/// <summary>
/// 자재 그룹: 여러 ItemUnit을 수집하고 동일 ID로 병합하는 컨테이너.
/// VBA itemGroup.cls를 대체합니다.
///
/// VBA 대비 개선:
/// - Dictionary&lt;string, ItemUnit&gt;으로 O(1) 병합 (VBA: Collection 이중 루프 O(n²))
/// - CompressorLV1/LV2 통합하여 단일 Merge 메서드로 단순화
/// </summary>
public class ItemGroup
{
    // VBA의 CompressorLV1/LV2를 Dictionary로 대체 → O(1) 머지
    private readonly Dictionary<string, ItemUnit> _units = new();
    private readonly SortedSet<DateTime> _dates = new();

    public int UnitCount => _units.Count;
    public int DaysCount => _dates.Count;
    public IReadOnlyCollection<ItemUnit> Units => _units.Values;
    public IReadOnlyCollection<DateTime> Dates => _dates;

    public DateTime LowestDay => _dates.Count > 0 ? _dates.Min : DateTime.MaxValue;
    public DateTime HighestDay => _dates.Count > 0 ? _dates.Max : DateTime.MinValue;

    /// <summary>
    /// ItemUnit을 그룹에 추가합니다.
    /// 동일 IdHash가 이미 있으면 날짜별 카운트를 자동 병합합니다.
    /// </summary>
    public void AddUnit(ItemUnit unit)
    {
        if (string.IsNullOrEmpty(unit.IdHash)) return;

        if (_units.TryGetValue(unit.IdHash, out var existing))
        {
            existing.MergeCountsFrom(unit);
        }
        else
        {
            _units[unit.IdHash] = unit;
        }

        // 날짜 키 수집
        foreach (var date in unit.DateKeys)
            _dates.Add(date);
    }

    /// <summary>
    /// 특정 IdHash의 ItemUnit을 반환합니다.
    /// </summary>
    public ItemUnit? GetUnit(string idHash) =>
        _units.TryGetValue(idHash, out var unit) ? unit : null;

    /// <summary>
    /// 모든 유닛을 순회합니다.
    /// </summary>
    public IEnumerable<ItemUnit> GetAllUnits() => _units.Values;
}
