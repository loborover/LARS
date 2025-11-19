using System;

namespace ENGINE.Documents;

/// <summary>
/// LOT 공통 메타 정보
/// (파트별명, 파트번호, 파트단위, 제조사 등)
/// </summary>
public class LOTUnit
{
    public string? WorkOrder { get; } 
    public string? Tools { get; } 
    public DateTime? InputTime { get; } 
    public DateTime? RunningDay { get; } 
    public long Counts { get; } 
    ///ProductModel

    public LOTUnit(string? workorder = null, string? tools = null, DateTime? inputtime = null, DateTime? runningday = null, long counts = 0)
    {
        WorkOrder = workorder;
        Tools = tools;
        InputTime = inputtime;
        Counts = counts;
        RunningDay= runningday;
    }
}
