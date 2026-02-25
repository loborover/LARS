using System.Data;
using System.IO;
using ClosedXML.Excel;
using LARS.Models.Macro;

namespace LARS.Services;

/// <summary>
/// 매크로 실행 엔진.
/// MacroDefinition을 해석하여 노드 순서대로 DataTable에 연산을 수행합니다.
/// </summary>
public class MacroRunner
{
    /// <summary>
    /// 매크로를 실행하고 최종 결과 DataTable을 반환합니다.
    /// </summary>
    /// <param name="macro">실행할 매크로 정의</param>
    /// <param name="inputFilePath">입력 Excel 파일 경로 (ExcelRead 노드용)</param>
    public async Task<DataTable> RunAsync(MacroDefinition macro, string inputFilePath)
    {
        // 1) 연결선을 따라 실행 순서(토폴로지 정렬) 결정
        var sortedNodes = TopologicalSort(macro);

        // 2) 각 노드의 출력을 캐싱할 딕셔너리
        var outputs = new Dictionary<string, DataTable>();

        foreach (var node in sortedNodes)
        {
            // 이전 노드의 출력을 현재 노드 입력으로 사용
            DataTable? input = null;
            var incomingConnection = macro.Connections.FirstOrDefault(c => c.ToNodeId == node.Id);
            if (incomingConnection != null && outputs.ContainsKey(incomingConnection.FromNodeId))
            {
                input = outputs[incomingConnection.FromNodeId];
            }

            var result = await ExecuteNodeAsync(node, input, inputFilePath);
            outputs[node.Id] = result;
        }

        // 3) 마지막 노드의 결과 반환
        return sortedNodes.Count > 0
            ? outputs[sortedNodes.Last().Id]
            : new DataTable();
    }

    /// <summary>
    /// 개별 노드를 실행합니다.
    /// </summary>
    private async Task<DataTable> ExecuteNodeAsync(NodeModel node, DataTable? input, string filePath)
    {
        return node.Type switch
        {
            NodeType.ExcelRead      => await ExecuteExcelRead(node, filePath),
            NodeType.ColumnDelete    => ExecuteColumnDelete(node, input!),
            NodeType.ColumnSelect    => ExecuteColumnSelect(node, input!),
            NodeType.ColumnRename    => ExecuteColumnRename(node, input!),
            NodeType.RowFilter       => ExecuteRowFilter(node, input!),
            NodeType.EmptyRowRemove  => ExecuteEmptyRowRemove(input!),
            NodeType.Sort            => ExecuteSort(node, input!),
            NodeType.DuplicateMerge  => ExecuteDuplicateMerge(node, input!),
            NodeType.CellReplace     => ExecuteCellReplace(node, input!),
            NodeType.GroupSum        => ExecuteGroupSum(node, input!),
            NodeType.GroupCount      => ExecuteGroupCount(node, input!),
            _                        => input ?? new DataTable()
        };
    }

    // =====================================================
    // 개별 노드 실행 메서드들
    // =====================================================

    /// <summary>Excel 파일을 DataTable로 읽기</summary>
    private async Task<DataTable> ExecuteExcelRead(NodeModel node, string filePath)
    {
        return await Task.Run(() =>
        {
            int sheetIndex = GetPropInt(node, "sheet", 1);
            int headerRow = GetPropInt(node, "headerRow", 1);

            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheet(sheetIndex);
            var dt = new DataTable();

            // 헤더 구성
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastCol == 0 || lastRow == 0) return dt;

            for (int c = 1; c <= lastCol; c++)
            {
                string colName = ws.Cell(headerRow, c).GetString().Trim();
                if (string.IsNullOrEmpty(colName)) colName = $"Column{c}";
                // 중복 헤더 방지
                if (dt.Columns.Contains(colName))
                    colName = $"{colName}_{c}";
                dt.Columns.Add(colName);
            }

            // 데이터 행 읽기
            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = dt.NewRow();
                for (int c = 1; c <= lastCol; c++)
                {
                    row[c - 1] = ws.Cell(r, c).GetString().Trim();
                }
                dt.Rows.Add(row);
            }

            return dt;
        });
    }

    /// <summary>지정 열 삭제</summary>
    private DataTable ExecuteColumnDelete(NodeModel node, DataTable input)
    {
        var columns = GetPropStringList(node, "columns");
        var dt = input.Copy();
        foreach (var col in columns)
        {
            if (dt.Columns.Contains(col))
                dt.Columns.Remove(col);
        }
        return dt;
    }

    /// <summary>지정 열만 남기기</summary>
    private DataTable ExecuteColumnSelect(NodeModel node, DataTable input)
    {
        var columns = GetPropStringList(node, "columns");
        var dt = new DataTable();

        foreach (var col in columns)
        {
            if (input.Columns.Contains(col))
                dt.Columns.Add(col, input.Columns[col]!.DataType);
        }

        foreach (DataRow srcRow in input.Rows)
        {
            var newRow = dt.NewRow();
            foreach (var col in columns)
            {
                if (input.Columns.Contains(col))
                    newRow[col] = srcRow[col];
            }
            dt.Rows.Add(newRow);
        }
        return dt;
    }

    /// <summary>열 이름 변경</summary>
    private DataTable ExecuteColumnRename(NodeModel node, DataTable input)
    {
        var dt = input.Copy();
        var mappings = GetPropDict(node, "mappings");
        foreach (var kvp in mappings)
        {
            if (dt.Columns.Contains(kvp.Key))
                dt.Columns[kvp.Key]!.ColumnName = kvp.Value;
        }
        return dt;
    }

    /// <summary>조건에 맞는 행만 남기기</summary>
    private DataTable ExecuteRowFilter(NodeModel node, DataTable input)
    {
        string column = GetPropString(node, "column");
        string op = GetPropString(node, "op", "==");
        string value = GetPropString(node, "value");

        var dt = input.Copy();
        var rowsToRemove = new List<DataRow>();

        foreach (DataRow row in dt.Rows)
        {
            string cellVal = row[column]?.ToString() ?? "";
            bool keep = op switch
            {
                "==" => cellVal == value,
                "!=" => cellVal != value,
                "contains" => cellVal.Contains(value, StringComparison.OrdinalIgnoreCase),
                "!contains" => !cellVal.Contains(value, StringComparison.OrdinalIgnoreCase),
                "startswith" => cellVal.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                "endswith" => cellVal.EndsWith(value, StringComparison.OrdinalIgnoreCase),
                ">" => double.TryParse(cellVal, out var a) && double.TryParse(value, out var b) && a > b,
                "<" => double.TryParse(cellVal, out var c) && double.TryParse(value, out var d) && c < d,
                _ => true
            };
            if (!keep) rowsToRemove.Add(row);
        }

        foreach (var r in rowsToRemove) dt.Rows.Remove(r);
        return dt;
    }

    /// <summary>빈 행 제거</summary>
    private DataTable ExecuteEmptyRowRemove(DataTable input)
    {
        var dt = input.Copy();
        var rowsToRemove = new List<DataRow>();
        foreach (DataRow row in dt.Rows)
        {
            bool allEmpty = true;
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]?.ToString()))
                { allEmpty = false; break; }
            }
            if (allEmpty) rowsToRemove.Add(row);
        }
        foreach (var r in rowsToRemove) dt.Rows.Remove(r);
        return dt;
    }

    /// <summary>정렬</summary>
    private DataTable ExecuteSort(NodeModel node, DataTable input)
    {
        string column = GetPropString(node, "column");
        string order = GetPropString(node, "order", "asc");
        var dv = input.DefaultView;
        dv.Sort = $"[{column}] {(order == "desc" ? "DESC" : "ASC")}";
        return dv.ToTable();
    }

    /// <summary>키 기준 중복 행 제거 + 합산</summary>
    private DataTable ExecuteDuplicateMerge(NodeModel node, DataTable input)
    {
        string keyColumn = GetPropString(node, "keyColumn");
        var sumColumns = GetPropStringList(node, "sumColumns");
        var dt = input.Clone();

        var groups = new Dictionary<string, DataRow>();
        foreach (DataRow row in input.Rows)
        {
            string key = row[keyColumn]?.ToString() ?? "";
            if (!groups.ContainsKey(key))
            {
                var newRow = dt.NewRow();
                newRow.ItemArray = row.ItemArray;
                groups[key] = newRow;
                dt.Rows.Add(newRow);
            }
            else
            {
                var existing = groups[key];
                foreach (var col in sumColumns)
                {
                    if (double.TryParse(existing[col]?.ToString(), out var a) &&
                        double.TryParse(row[col]?.ToString(), out var b))
                    {
                        existing[col] = (a + b).ToString();
                    }
                }
            }
        }
        return dt;
    }

    /// <summary>셀 텍스트 찾기/바꾸기</summary>
    private DataTable ExecuteCellReplace(NodeModel node, DataTable input)
    {
        string column = GetPropString(node, "column");
        string find = GetPropString(node, "find");
        string replace = GetPropString(node, "replace");

        var dt = input.Copy();
        foreach (DataRow row in dt.Rows)
        {
            string val = row[column]?.ToString() ?? "";
            row[column] = val.Replace(find, replace);
        }
        return dt;
    }

    /// <summary>그룹 합산</summary>
    private DataTable ExecuteGroupSum(NodeModel node, DataTable input)
    {
        string keyColumn = GetPropString(node, "keyColumn");
        string sumColumn = GetPropString(node, "sumColumn");

        var dt = new DataTable();
        dt.Columns.Add(keyColumn);
        dt.Columns.Add(sumColumn);

        var groups = new Dictionary<string, double>();
        foreach (DataRow row in input.Rows)
        {
            string key = row[keyColumn]?.ToString() ?? "";
            double val = double.TryParse(row[sumColumn]?.ToString(), out var v) ? v : 0;
            groups[key] = groups.GetValueOrDefault(key) + val;
        }

        foreach (var kvp in groups)
        {
            var newRow = dt.NewRow();
            newRow[keyColumn] = kvp.Key;
            newRow[sumColumn] = kvp.Value.ToString();
            dt.Rows.Add(newRow);
        }
        return dt;
    }

    /// <summary>그룹별 건수</summary>
    private DataTable ExecuteGroupCount(NodeModel node, DataTable input)
    {
        string keyColumn = GetPropString(node, "keyColumn");

        var dt = new DataTable();
        dt.Columns.Add(keyColumn);
        dt.Columns.Add("Count");

        var groups = new Dictionary<string, int>();
        foreach (DataRow row in input.Rows)
        {
            string key = row[keyColumn]?.ToString() ?? "";
            groups[key] = groups.GetValueOrDefault(key) + 1;
        }

        foreach (var kvp in groups)
        {
            var newRow = dt.NewRow();
            newRow[keyColumn] = kvp.Key;
            newRow["Count"] = kvp.Value.ToString();
            dt.Rows.Add(newRow);
        }
        return dt;
    }

    // =====================================================
    // 헬퍼: Props에서 값 추출
    // =====================================================

    private string GetPropString(NodeModel node, string key, string fallback = "")
    {
        return node.Props.TryGetValue(key, out var val) ? val?.ToString() ?? fallback : fallback;
    }

    private int GetPropInt(NodeModel node, string key, int fallback = 0)
    {
        if (node.Props.TryGetValue(key, out var val))
        {
            if (val is int i) return i;
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    private List<string> GetPropStringList(NodeModel node, string key)
    {
        if (!node.Props.TryGetValue(key, out var val)) return new List<string>();
        if (val is IEnumerable<object> list) return list.Select(x => x.ToString() ?? "").ToList();
        if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return je.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        }
        return new List<string>();
    }

    private Dictionary<string, string> GetPropDict(NodeModel node, string key)
    {
        if (!node.Props.TryGetValue(key, out var val)) return new();
        if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var dict = new Dictionary<string, string>();
            foreach (var prop in je.EnumerateObject())
                dict[prop.Name] = prop.Value.GetString() ?? "";
            return dict;
        }
        return new();
    }

    // =====================================================
    // 토폴로지 정렬 (연결선 기반 실행 순서 결정)
    // =====================================================

    private List<NodeModel> TopologicalSort(MacroDefinition macro)
    {
        var result = new List<NodeModel>();
        var visited = new HashSet<string>();
        var nodeMap = macro.Nodes.ToDictionary(n => n.Id);

        // 진입 차수가 0인 노드(시작 노드)부터 BFS
        var inDegree = macro.Nodes.ToDictionary(n => n.Id, _ => 0);
        foreach (var conn in macro.Connections)
        {
            if (inDegree.ContainsKey(conn.ToNodeId))
                inDegree[conn.ToNodeId]++;
        }

        var queue = new Queue<string>();
        foreach (var kvp in inDegree.Where(x => x.Value == 0))
            queue.Enqueue(kvp.Key);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!nodeMap.ContainsKey(id) || visited.Contains(id)) continue;
            visited.Add(id);
            result.Add(nodeMap[id]);

            foreach (var conn in macro.Connections.Where(c => c.FromNodeId == id))
            {
                inDegree[conn.ToNodeId]--;
                if (inDegree[conn.ToNodeId] == 0)
                    queue.Enqueue(conn.ToNodeId);
            }
        }

        // 연결되지 않은 고아 노드도 포함
        foreach (var node in macro.Nodes.Where(n => !visited.Contains(n.Id)))
            result.Add(node);

        return result;
    }
}
