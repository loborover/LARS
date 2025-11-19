namespace ENGINE.Documents;
/// <summary>
/// DailyPlan 파일 하나에 대한 메타 정보
/// RandomFile(경로, 날짜) + LineName + Mixed 추가된 형태
/// </summary>
public record DailyPlanFile : RandomFile
{
    public string? LineName { get; init; } // 생산 라인 이름
    public bool? Mixed { get; init; } // 혼류 생산 여부
    public DailyPlanFile(string path, DateTime? date = null, string? lineName = null, bool? mixed = null)
        : base(path, date)   // ← 부모(RandomFile)의 생성자 호출
    {
        LineName = lineName;
        Mixed = mixed;
    }
}