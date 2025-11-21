namespace LARS.ENGINE.Documents.DailyPlan;
/// <summary> DailyPlan 파일 하나에 대한 메타 정보 / RandomFile(경로, 날짜) + LineName + Mixed 추가된 형태 </summary>
public record DailyPlanFile:RandomFile
{
    public DailyPlanFile(string path, DateTime? date = null, string? lineName = null, bool? mixed = null) 
        : base(path, date);

}
    