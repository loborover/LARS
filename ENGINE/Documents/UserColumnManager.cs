namespace LARS.ENGINE.Documents;

public static class UserColumnManager
{
    // 💡 Lock 객체: 동시 수정 방지
    private static readonly object _lock = new object();
    
    // 💡 컬럼 데이터를 저장하는 Dictionary. 런타임에 수정될 수 있음.
    private static Dictionary<string, List<string>> _columnDefinitions = new Dictionary<string, List<string>>
    {
        // 초기 기본값 정의 (런타임에 사용자가 수정하기 전까지 사용)
        { "BOM", new List<string> { "ModelNumber", "PartID", "Quantity" } },
        { "DailyPlan", new List<string> { "LineName", "TargetDate", "TargetVolume" } },
        { "PartList", new List<string> { "PartName", "SupplierCode" } }
    };

    /// <summary> 문서 타입 이름으로 컬럼 리스트를 가져옵니다. (읽기 기능) </summary>
    public static List<string>? GetColumns(string documentType)
    {
        // 락(Lock)을 걸고 읽기: 데이터가 읽히는 동안 수정되지 않도록 보장
        lock (_lock)
        {
            if (_columnDefinitions.TryGetValue(documentType, out List<string>? columns))
            {
                // 외부에서 원본 리스트를 직접 수정하지 못하도록 복사본을 반환합니다.
                return new List<string>(columns); 
            }
            return null;
        }
    }

    /// <summary> 특정 문서 타입의 컬럼 리스트를 새로운 리스트로 업데이트합니다. (수정 기능) </summary>
    public static void UpdateColumns(string documentType, List<string> newColumns)
    {
        // 락(Lock)을 걸고 쓰기: 데이터가 수정되는 동안 접근을 막아 데이터 충돌을 방지
        lock (_lock)
        {
            // 기존 키가 있으면 업데이트하고, 없으면 새로 추가합니다.
            _columnDefinitions[documentType] = newColumns;
        }
    }

    /// <summary> 사용자의 설정 파일(JSON, DB 등)에서 데이터를 로드하여 초기화합니다. </summary>
    public static void LoadFromConfiguration(Dictionary<string, List<string>> configData)
    {
        lock (_lock)
        {
            _columnDefinitions = configData;
        }
    }
}