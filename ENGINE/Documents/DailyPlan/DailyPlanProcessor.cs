using System;
using System.Collections.Generic;
using System.IO;

namespace ENGINE.Documents.DailyPlan;

/// <summary>
/// DailyPlan 관련 파일을 찾고, 가공(처리)하는 역할을 담당하는 클래스
/// 지금은 파일 검색 + 자리만 잡고, 나중에 엑셀 가공 로직을 채워넣습니다.
/// </summary>
public class DailyPlanProcessor
{
    private readonly string _sourceDirectory;

    public DailyPlanProcessor(string sourceDirectory)
    {
        _sourceDirectory = sourceDirectory;
    }

    /// <summary>
    /// 소스 폴더에서 DailyPlan 후보 파일들을 검색합니다.
    /// 예: 파일명에 "Excel_Export_" 가 포함된 xlsx 파일
    /// </summary>
    public IEnumerable<DailyPlanFile> FindDailyPlanFiles()
    {
        if (!Directory.Exists(_sourceDirectory))
            yield break;

        var files = Directory.GetFiles(_sourceDirectory, "*.xlsx", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (!name.Contains("Excel_Export_", StringComparison.OrdinalIgnoreCase))
                continue;

            // 날짜/라인명 파싱은 나중에 파일명 규칙 보고 추가
            yield return new DailyPlanFile(file);
        }
    }

    /// <summary>
    /// DailyPlan 파일 하나를 가공하는 자리.
    /// 지금은 단순히 "여기서 엑셀 열어서 가공할 것"이라고 출력만 하고,
    /// 나중에 EPPlus/ClosedXML 로직을 이 안에 넣습니다.
    /// </summary>
    public void ProcessSingle(DailyPlanFile dp)
    {
        Console.WriteLine($"[가공 시작] {dp.Path}");
        // TODO: 여기서 엑셀 열고, 기존 VBA AutoReport_DailyPlan 로직을 C#으로 옮길 예정
        Console.WriteLine($"[가공 완료] {dp.Path}");
    }

    /// <summary>
    /// 소스 폴더의 DailyPlan 파일들을 전부 순회하면서 가공합니다.
    /// </summary>
    public void ProcessAll()
    {
        foreach (var dp in FindDailyPlanFiles())
        {
            ProcessSingle(dp);
        }
    }
}
