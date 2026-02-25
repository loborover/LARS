namespace LARS.Models.Macro;

/// <summary>
/// 매크로 블록(노드)의 종류를 정의합니다.
/// Visual Macro Editor에서 사용자가 배치할 수 있는 모든 블록 유형입니다.
/// </summary>
public enum NodeType
{
    // ── 입력 ──
    /// <summary>Excel 파일에서 시트 데이터를 테이블로 로드</summary>
    ExcelRead,

    // ── 열 조작 ──
    /// <summary>지정 열 제거</summary>
    ColumnDelete,
    /// <summary>지정 열만 남기기</summary>
    ColumnSelect,
    /// <summary>헤더 이름 변경</summary>
    ColumnRename,
    /// <summary>새 열 삽입</summary>
    ColumnAdd,

    // ── 행 조작 ──
    /// <summary>조건에 맞는 행만 남기기</summary>
    RowFilter,
    /// <summary>전체가 비어있는 행 삭제</summary>
    EmptyRowRemove,
    /// <summary>특정 열 기준 정렬</summary>
    Sort,
    /// <summary>키 기준 중복 행 제거 + 합산</summary>
    DuplicateMerge,

    // ── 변환 ──
    /// <summary>셀 텍스트 찾기/바꾸기</summary>
    CellReplace,
    /// <summary>셀 → 날짜 형식 변환</summary>
    DateParse,
    /// <summary>구분자로 셀 분할</summary>
    TextSplit,

    // ── 집계 ──
    /// <summary>그룹별 합산</summary>
    GroupSum,
    /// <summary>그룹별 건수</summary>
    GroupCount,

    // ── 출력 ──
    /// <summary>결과를 PDF로 렌더링</summary>
    PdfExport,
    /// <summary>결과를 새 Excel로 저장</summary>
    ExcelExport
}
