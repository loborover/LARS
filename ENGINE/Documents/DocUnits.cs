namespace LARS.ENGINE.Documents;

/// <summary> 각 item별 공통 메타 정보 (파트별명, 파트번호, 파트단위, 제조사 등) </summary>
public record struct ItemUnit(string? NickName = null, string? PartNumber = null, string? Vender = null, long QTY = 1);

/// <summary> ModelInfo 공통 메타 정보 (모델번호, 별명, 종류, 연료, 등급, 색상, 수출대상국가, 개발단계 등) </summary>
public record struct ModelInfo
{
    public string FullNumber { get; init;} 
    public string? ModelNum { get; init;} 
    public string? Nickname { get; init;} 
    public string? Types { get; init;} 
    public string? Fuel { get; init;} 
    public string? Grade { get; init;} 
    public string? Color { get; init;} 
    public string? Customer { get; init;} 
    public string? DevLevel { get; init;} 
    public ModelInfo(string fullnumber)
    {
        FullNumber = ParseRawData(fullnumber);    
    }
    private static string ParseRawData(string Target)
    {
        ///Parsing Process needed 
        ///Checkout NumberingRules
        ///Numbering Logic needed
        ///Model identifing from Rules Logic
        return Target;
    }
}

/// <summary> LOT 공통 메타 정보(파트별명, 파트번호, 파트단위, 제조사 등) </summary>
public readonly record struct LOT
{
    public string? WorkOrder { get; init;} 
    public string? Tools { get; init;} 
    public DateTime? InputTime { get; init;} 
    public DateTime? RunningDay { get; init;} 
    public long Counts { get; init;}
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