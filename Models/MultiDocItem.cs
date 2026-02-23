namespace LARS.Models;

/// <summary>
/// DailyPlan과 PartList 파일의 교차 매핑 단위.
/// (Date, Line) 쌍을 키로 사용하여 두 파일을 그룹핑합니다.
/// </summary>
public class MultiDocItem
{
    public string Key { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string Line { get; set; } = string.Empty;

    public FileMetadata? DailyPlanFile { get; set; }
    public FileMetadata? PartListFile { get; set; }

    /// <summary>두 개의 파일이 모두 스캔되었는지 여부</summary>
    public bool HasBoth => DailyPlanFile != null && PartListFile != null;

    /// <summary>UI 체크박스 바인딩용 속성</summary>
    public bool IsSelected { get; set; }

    public string DailyPlanName => DailyPlanFile?.FileName ?? "(없음)";
    public string PartListName => PartListFile?.FileName ?? "(없음)";
}
