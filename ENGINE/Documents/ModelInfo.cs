namespace LARS.ENGINE.Documents;

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
    private string ParseRawData(string Target)
    {
        ///Parsing Process needed 
        ///Checkout NumberingRules
        ///Numbering Logic needed
        ///Model identifing from Rules Logic
        return Target;
    }
}
