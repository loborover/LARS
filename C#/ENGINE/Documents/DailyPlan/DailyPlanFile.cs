using System;

namespace ENGINE.Documents.DailyPlan;

/// <summary>
/// DailyPlan 파일 하나에 대한 메타 정보
/// (경로, 날짜, 라인명 등을 나중에 여기에 붙여갈 수 있음)
/// </summary>
public class DailyPlanFile
{
    public string Path { get; }
    public DateTime? Date { get; }
    public string? LineName { get; }

    public DailyPlanFile(string path, DateTime? date = null, string? lineName = null)
    {
        Path = path;
        Date = date;
        LineName = lineName;
    }

    public override string ToString()
        => $"{System.IO.Path.GetFileName(Path)} (Date={Date?.ToShortDateString() ?? "?"}, Line={LineName ?? "?"})";
}