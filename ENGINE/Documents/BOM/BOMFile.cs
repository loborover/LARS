namespace ENGINE.Documents;
/// <summary>
/// BOM 파일 하나에 대한 메타 정보
/// RandomFile(경로, 날짜) + ModelNumber 이 추가된 형태
/// </summary>
public record BOMFile : RandomFile
{
    public string? ModelNumber { get; init; }

    public BOMFile(string path, DateTime? date = null, string? modelNumber = null)
        : base(path, date)   // ← 부모(RandomFile)의 생성자 호출
    {
        ModelNumber = modelNumber;
    }
}