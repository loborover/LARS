namespace LARS.ENGINE.Documents.BOM;

/// <summary>
/// BOM 관련 파일을 찾고, 가공(처리)하는 역할을 담당하는 클래스
/// </summary>
/// 지금은 파일 검색 + 자리만 잡고, 나중에 엑셀 가공 로직을 채워넣습니다.
public class BOMProcessor
{
    private readonly string _sourceDirectory;

    public BOMProcessor(string sourceDirectory)
    {
        _sourceDirectory = sourceDirectory;
    }

    /// <summary>
    /// 소스 폴더에서 BOM 후보 파일들을 검색합니다.
    /// 예: 파일명에 "Excel_Export_" 가 포함된 xlsx 파일
    /// </summary>
    public IEnumerable<BOMFile> FindBOMFiles()
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
            yield return new BOMFile(file);
        }
    }

    /// <summary>
    /// BOM 파일 하나를 가공하는 자리.    
    /// </summary>
    public void ProcessSingle(BOMFile dp)
    {
        Console.WriteLine($"[가공 시작] {dp.Path}");
        // TODO: 여기서 엑셀 열고, 기존 VBA AutoReport_BOM 로직을 C#으로 옮길 예정
            AutoReport(dp.Path);
        Console.WriteLine($"[가공 완료] {dp.Path}");
    }

    /// <summary> 소스 폴더의 BOM 파일들을 전부 순회하면서 가공합니다. </summary>
    public void ProcessAll()
    {
        foreach (var dp in FindBOMFiles())
        {
            ProcessSingle(dp);
        }
    }
    /// <summary> Target AutoReport </summary>
    private string AutoReport(string Target)
    {
        string Exportpath = Target;
        /// 사용할 열 선정, 열 제목 변경
        /// 
        return Exportpath;
    }
    /// <summary> 사용자가 만든 Column List를 활용함 .json </summary>
    private List<string> GetColumnList()
    {
        List<string> ColumnList = new List<string>()
        {
            "파트번호",
            "파트별명",
            "파트단위",
            "제조사",
            "수량",
            "비고"
        };
        return ColumnList;
    }

}
