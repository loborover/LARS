namespace LARS.Models.Macro;

/// <summary>
/// 매크로 전체 정의.
/// 이름, 버전, 블록 목록, 연결선 목록을 포함합니다.
/// JSON으로 직렬화하여 파일로 저장/불러오기합니다.
/// </summary>
public class MacroDefinition
{
    /// <summary>매크로 이름 (예: "BOM 커스텀 보고서")</summary>
    public string Name { get; set; } = "새 매크로";

    /// <summary>매크로 버전 (호환성 관리용)</summary>
    public int Version { get; set; } = 1;

    /// <summary>매크로에 포함된 블록(노드) 목록</summary>
    public List<NodeModel> Nodes { get; set; } = new();

    /// <summary>블록 간 연결선 목록</summary>
    public List<ConnectionModel> Connections { get; set; } = new();

    /// <summary>매크로 설명 (사용자 메모)</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>생성 일시</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>마지막 수정 일시</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}
