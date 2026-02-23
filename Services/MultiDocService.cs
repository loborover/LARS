using LARS.Models;

namespace LARS.Services;

/// <summary>
/// DailyPlan과 PartList 파일들을 스캔하여 교차 매핑하는 서비스.
/// VBA BD_MultiDocuments.bas 논리에 대응합니다.
/// </summary>
public class MultiDocService
{
    /// <summary>
    /// 두 파일 목록을 입력받아 Date와 Line을 조합한 Key를 기준으로 매핑 리스트를 반환합니다.
    /// </summary>
    public List<MultiDocItem> MatchFiles(List<FileMetadata> dailyPlans, List<FileMetadata> partLists)
    {
        var dict = new Dictionary<string, MultiDocItem>();

        string MakeKey(DateTime? date, string line) => 
            $"{date?.ToString("yyyy-MM-dd") ?? "Unknown"}_{line}";

        foreach (var dp in dailyPlans)
        {
            string key = MakeKey(dp.DateValue, dp.Line);
            if (!dict.TryGetValue(key, out var item))
            {
                item = new MultiDocItem { Key = key, Date = dp.DateValue, Line = dp.Line };
                dict[key] = item;
            }
            item.DailyPlanFile = dp;
        }

        foreach (var pl in partLists)
        {
            string key = MakeKey(pl.DateValue, pl.Line);
            if (!dict.TryGetValue(key, out var item))
            {
                item = new MultiDocItem { Key = key, Date = pl.DateValue, Line = pl.Line };
                dict[key] = item;
            }
            item.PartListFile = pl;
        }

        return dict.Values
            .OrderBy(x => x.Date ?? DateTime.MaxValue)
            .ThenBy(x => x.Line)
            .ToList();
    }
}
