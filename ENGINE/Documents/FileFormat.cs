namespace LARS.ENGINE.Documents;

///File 집합

/// <summary> 모든 문서/파일 공통 메타 정보 (경로, 날짜 등) </summary>
public record RandomFile(string Path, DateTime? Date = null);
/// <summary> DailyPlan 파일 하나에 대한 메타 정보 / RandomFile(경로, 날짜) + LineName + Mixed 추가된 형태 </summary>
public record DailyPlanFile(string Path, DateTime? Date = null, string? LineName = null, bool? Mixed = null)
            : RandomFile(Path, Date);
/// <summary> BOM 파일 하나에 대한 메타 정보 / RandomFile(경로, 날짜) + ModelNumber 추가된 형태 </summary>
public record BOMFile(string Path, DateTime? Date = null, string? ModelNumber = null) 
            : RandomFile(Path, Date);