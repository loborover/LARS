namespace LARS.Models.Macro;

/// <summary>
/// 블록 간 연결선 모델.
/// FromNodeId → ToNodeId 방향으로 데이터가 흐릅니다.
/// </summary>
public class ConnectionModel
{
    /// <summary>출발 블록 ID</summary>
    public string FromNodeId { get; set; } = string.Empty;

    /// <summary>도착 블록 ID</summary>
    public string ToNodeId { get; set; } = string.Empty;
}
