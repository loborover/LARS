namespace ENGINE.Documents;

/// <summary>
/// 모든 문서/파일 공통 메타 정보
/// (경로, 날짜 등)
/// </summary>
public record RandomFile
{
    public string Path { get; init; }
    public DateTime? Date { get; init; }
    public RandomFile(string path, DateTime? date = null)
    {
        Path = path;
        Date = date;
    }
}
