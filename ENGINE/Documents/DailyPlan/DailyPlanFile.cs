using System;
using ENGINE.Documents;   // RandomFile이 있는 네임스페이스

namespace ENGINE.Documents;

/// <summary>
/// DailyPlan 파일 하나에 대한 메타 정보
/// RandomFile(경로, 날짜) + LineName 이 추가된 형태
/// </summary>
public class DailyPlanFile : RandomFile
{
    public string? LineName { get; } // 생산 라인 이름
    public bool? Mixed { get; } // 혼류 생산 여부

    public DailyPlanFile(string path, DateTime? date = null, string? lineName = null, bool? mixed = null)
        : base(path, date)   // ← 부모(RandomFile)의 생성자 호출
    {
        LineName = lineName;
        Mixed = mixed;
    }

    public override string ToString()
        => $"{base.ToString()}, Line={LineName ?? "?"}, Mixed={Mixed?.ToString() ?? "?"}";
}