using System.Text.Json.Serialization;

namespace LARS.Models.Macro;

/// <summary>
/// 매크로 내 개별 블록(노드) 모델.
/// 각 블록은 고유 ID, 타입, 속성(Props), 캔버스 위의 좌표를 가집니다.
/// </summary>
public class NodeModel
{
    /// <summary>블록 고유 식별자 (예: "n1", "n2")</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>블록 종류</summary>
    public NodeType Type { get; set; }

    /// <summary>블록의 사용자 지정 레이블 (표시명)</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 블록별 속성값 딕셔너리.
    /// 예: ColumnSelect → { "columns": ["Lvl","Part No","Qty"] }
    ///     RowFilter   → { "column": "Lvl", "op": "!=", "value": "0" }
    /// </summary>
    public Dictionary<string, object> Props { get; set; } = new();

    // ── 캔버스 위치 (Visual Editor용) ──
    /// <summary>캔버스 X 좌표</summary>
    public double X { get; set; }

    /// <summary>캔버스 Y 좌표</summary>
    public double Y { get; set; }
}
