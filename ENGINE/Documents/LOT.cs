namespace LARS.ENGINE.Documents;

/// <summary>
/// LOT 공통 메타 정보
/// (파트별명, 파트번호, 파트단위, 제조사 등)
/// </summary>
public record LOT
{
    public string? WorkOrder { get; } 
    public string? Tools { get; } 
    public DateTime? InputTime { get; init;} 
    public DateTime? RunningDay { get; init;} 
    public long Counts { get; } 
    public ModelInfo ProductModel { get; init;} 
    ///ProductModel

    public LOT( string? workorder = null, string? tools = null,  string? ModelNumber = null,
                DateTime? inputtime = null, DateTime? runningday = null, long counts = 0)
    {
        WorkOrder = workorder;
        Tools = tools;
        InputTime = inputtime;
        Counts = counts;
        RunningDay= runningday;
        ProductModel = new ModelInfo(ModelNumber ?? string.Empty);
    }
}
