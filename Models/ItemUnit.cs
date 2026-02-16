namespace LARS.Models;

/// <summary>
/// 자재(Part) 단위 정보와 날짜별 투입 수량을 관리하는 클래스.
/// VBA itemUnit.cls를 대체합니다.
/// 
/// VBA 대비 개선:
/// - Dictionary로 O(1) 날짜 키 조회 (VBA: 배열 순차 탐색 O(n))
/// - ID_Hash를 자동 생성하여 병합 기준으로 사용
/// </summary>
public class ItemUnit
{
    private string _nickName = string.Empty;
    private string _vendor = string.Empty;
    private string _partNumber = string.Empty;

    // VBA의 vDateKey/vDateCounts 배열 대신 Dictionary 사용 → O(1) 조회
    private readonly Dictionary<DateTime, long> _dateCounts = new();

    public string IdHash { get; private set; } = string.Empty;

    public string NickName
    {
        get => _nickName;
        set { _nickName = value; RebuildId(); }
    }

    public string Vendor
    {
        get => _vendor;
        set { _vendor = value; RebuildId(); }
    }

    public string PartNumber
    {
        get => _partNumber;
        set { _partNumber = value; RebuildId(); }
    }

    public long QTY { get; set; }

    /// <summary>
    /// 특정 날짜의 투입 수량을 조회/설정합니다.
    /// </summary>
    public long this[DateTime date]
    {
        get => _dateCounts.TryGetValue(date.Date, out var v) ? v : 0;
        set => _dateCounts[date.Date] = value;
    }

    /// <summary>
    /// 전체 날짜의 합산 수량을 반환합니다.
    /// </summary>
    public long TotalCount => _dateCounts.Values.Sum();

    /// <summary>
    /// 등록된 날짜 키 수를 반환합니다.
    /// </summary>
    public int DateKeyCount => _dateCounts.Count;

    /// <summary>
    /// 등록된 날짜 키 목록을 반환합니다.
    /// </summary>
    public IEnumerable<DateTime> DateKeys => _dateCounts.Keys;

    /// <summary>
    /// 다른 ItemUnit의 날짜별 Count를 합산합니다.
    /// VBA MergeCountsFrom에 대응합니다.
    /// </summary>
    public void MergeCountsFrom(ItemUnit other)
    {
        foreach (var (date, count) in other._dateCounts)
        {
            if (_dateCounts.ContainsKey(date))
                _dateCounts[date] += count;
            else
                _dateCounts[date] = count;
        }
    }

    private void RebuildId()
    {
        if (!string.IsNullOrEmpty(_vendor) &&
            !string.IsNullOrEmpty(_nickName) &&
            !string.IsNullOrEmpty(_partNumber))
        {
            IdHash = $"{_vendor}_{_nickName}_{_partNumber}";
        }
    }

    public override string ToString() => $"[{IdHash}] QTY={QTY} Total={TotalCount}";
}
