using System;

namespace ENGINE.Documents;

/// <summary>
/// 모든 문서/파일 공통 메타 정보
/// (경로, 날짜 등)
/// </summary>
public class RandomFile
{
    public string Path { get; }
    public DateTime? Date { get; }

    public RandomFile(string path, DateTime? date = null)
    {
        Path = path;
        Date = date;
    }

    public override string ToString()
        => $"{System.IO.Path.GetFileName(Path)} (Date={Date?.ToShortDateString() ?? "?"})";
}
