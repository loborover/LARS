using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LARS.Models.Macro;
using LARS.Services;
using Microsoft.Win32;

namespace LARS.ViewModels;

/// <summary>
/// Visual Macro Editor의 ViewModel.
/// 블록 배치, 연결, 속성 편집, 실행, 저장/불러오기 전체를 관리합니다.
/// </summary>
public partial class MacroEditorViewModel : ObservableObject
{
    private readonly MacroRunner _runner;
    private readonly MacroStorageService _storage;

    public MacroEditorViewModel(MacroRunner runner, MacroStorageService storage)
    {
        _runner = runner;
        _storage = storage;

        // 기본 팔레트 블록 유형 목록 생성
        AvailableBlockTypes = new ObservableCollection<BlockTypeInfo>(BuildPalette());

        // Target Documents 유형 초기화
        TargetDocumentTypes = new ObservableCollection<string>
        {
            "BOM", "DailyPlan", "PartList"
        };

        // 저장된 매크로 목록 로드
        RefreshMacroList();
    }

    // ==========================================
    // 매크로 정의 (현재 편집 중인 매크로)
    // ==========================================

    [ObservableProperty] private string _macroName = "새 매크로";
    [ObservableProperty] private string _macroDescription = "";

    /// <summary>캔버스에 배치된 블록 목록</summary>
    public ObservableCollection<NodeModel> Nodes { get; } = new();

    /// <summary>블록 간 연결선 목록</summary>
    public ObservableCollection<ConnectionModel> Connections { get; } = new();

    // ==========================================
    // 매크로 ComboBox (저장된 매크로 목록)
    // ==========================================

    /// <summary>ComboBox에 표시할 매크로 이름 목록 (첫 항목은 "+ 매크로 새로 만들기")</summary>
    public ObservableCollection<string> SavedMacroItems { get; } = new();

    [ObservableProperty] private string? _selectedMacroItem;

    partial void OnSelectedMacroItemChanged(string? value)
    {
        if (value == null) return;
        if (value == "+ 매크로 새로 만들기")
        {
            // 초기화
            Nodes.Clear();
            Connections.Clear();
            MacroName = "새 매크로";
            MacroDescription = "";
            SelectedNode = null;
            RawData = null;
            PreviewData = null;
            StatusMessage = "새 매크로를 만듭니다. 블록을 추가하세요.";
        }
        else
        {
            // 해당 매크로 파일 자동 로드
            var files = _storage.ListSavedMacros();
            var match = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == value);
            if (match != null) LoadMacroFromFile(match);
        }
    }

    /// <summary>저장된 매크로 목록을 새로고침합니다.</summary>
    private void RefreshMacroList()
    {
        SavedMacroItems.Clear();
        SavedMacroItems.Add("+ 매크로 새로 만들기");
        foreach (var file in _storage.ListSavedMacros())
            SavedMacroItems.Add(Path.GetFileNameWithoutExtension(file));
    }

    // ==========================================
    // Target Documents (적용 문서 유형)
    // ==========================================

    public ObservableCollection<string> TargetDocumentTypes { get; }
    [ObservableProperty] private string _selectedTargetDoc = "BOM";

    // ==========================================
    // 팔레트 (사용 가능한 블록 유형)
    // ==========================================

    public ObservableCollection<BlockTypeInfo> AvailableBlockTypes { get; }

    // ==========================================
    // 선택 상태
    // ==========================================

    [ObservableProperty] private NodeModel? _selectedNode;
    [ObservableProperty] private string _statusMessage = "블록을 캔버스에 추가하세요.";

    // ==========================================
    // 실행 결과 (Raw / Processed 이중 뷰)
    // ==========================================

    /// <summary>현재 로드된 Raw 파일 경로</summary>
    [ObservableProperty] private string? _rawFilePath;

    /// <summary>원본 데이터 (Excel 로드 직후, 가공 전)</summary>
    [ObservableProperty] private DataTable? _rawData;

    /// <summary>가공 결과 데이터 (매크로 실행 후)</summary>
    [ObservableProperty] private DataTable? _previewData;

    [ObservableProperty] private bool _isRunning;

    // ==========================================
    // 커맨드: 블록 추가
    // ==========================================

    [RelayCommand]
    private void AddBlock(BlockTypeInfo? blockInfo)
    {
        if (blockInfo == null) return;

        var node = new NodeModel
        {
            Id = $"n{Nodes.Count + 1}_{DateTime.Now.Ticks % 10000}",
            Type = blockInfo.Type,
            Label = blockInfo.DisplayName,
            X = 40 + Nodes.Count * 220,
            Y = 120
        };

        // 직전 블록과 자동 연결
        if (Nodes.Count > 0)
        {
            var prevNode = Nodes.Last();
            Connections.Add(new ConnectionModel
            {
                FromNodeId = prevNode.Id,
                ToNodeId = node.Id
            });
        }

        Nodes.Add(node);
        SelectedNode = node;
        StatusMessage = $"'{blockInfo.DisplayName}' 블록 추가됨. (총 {Nodes.Count}개)";
    }

    // ==========================================
    // 커맨드: 블록 선택
    // ==========================================

    [RelayCommand]
    private void SelectNode(NodeModel? node)
    {
        SelectedNode = node;
    }

    // ==========================================
    // 커맨드: 블록 삭제
    // ==========================================

    [RelayCommand]
    private void DeleteSelectedBlock()
    {
        if (SelectedNode == null) return;

        var id = SelectedNode.Id;

        // 관련 연결선도 삭제
        var toRemove = Connections.Where(c => c.FromNodeId == id || c.ToNodeId == id).ToList();
        foreach (var conn in toRemove) Connections.Remove(conn);

        Nodes.Remove(SelectedNode);
        SelectedNode = null;
        StatusMessage = "블록이 삭제되었습니다.";
    }

    // ==========================================
    // 커맨드: 전체 초기화
    // ==========================================

    [RelayCommand]
    private void ClearAll()
    {
        Nodes.Clear();
        Connections.Clear();
        SelectedNode = null;
        PreviewData = null;
        MacroName = "새 매크로";
        MacroDescription = "";
        StatusMessage = "캔버스가 초기화되었습니다.";
    }

    // ==========================================
    // 커맨드: 매크로 실행
    // ==========================================

    [RelayCommand]
    private async Task RunMacroAsync()
    {
        if (Nodes.Count == 0)
        {
            StatusMessage = "실행할 블록이 없습니다. 블록을 추가하세요.";
            return;
        }

        // Raw 파일이 로드되어 있으면 그것을 사용, 없으면 파일 선택
        string? inputFile = RawFilePath;
        if (string.IsNullOrEmpty(inputFile))
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel 파일|*.xlsx;*.xls|모든 파일|*.*",
                Title = "매크로에 입력할 Excel 파일 선택"
            };
            if (dialog.ShowDialog() != true) return;
            inputFile = dialog.FileName;
            await LoadRawFileFromPath(inputFile);
        }

        IsRunning = true;
        StatusMessage = "매크로 실행 중...";

        try
        {
            // 매크로 실행 (Processed View)
            var macro = BuildMacroDefinition();
            var result = await _runner.RunAsync(macro, inputFile);
            PreviewData = result;
            StatusMessage = $"✅ 실행 완료! Raw: {RawData?.Rows.Count ?? 0}행 → Processed: {result.Rows.Count}행 × {result.Columns.Count}열";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 실행 오류: {ex.Message}";
            MessageBox.Show($"매크로 실행 중 오류가 발생했습니다:\n\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsRunning = false;
        }
    }

    // ==========================================
    // 커맨드: 저장
    // ==========================================

    [RelayCommand]
    private void SaveMacro()
    {
        try
        {
            var macro = BuildMacroDefinition();
            _storage.Save(macro);
            RefreshMacroList();
            StatusMessage = $"💾 '{macro.Name}' 저장 완료!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 오류: {ex.Message}";
        }
    }

    // ==========================================
    // 커맨드: 불러오기 (매크로)
    // ==========================================

    [RelayCommand]
    private void LoadMacro()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "매크로 파일|*.json|모든 파일|*.*",
            Title = "매크로 불러오기",
            InitialDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LARS", "Macros")
        };
        if (dialog.ShowDialog() != true) return;
        LoadMacroFromFile(dialog.FileName);
    }

    /// <summary>파일 경로로부터 매크로를 로드합니다.</summary>
    private void LoadMacroFromFile(string filePath)
    {
        try
        {
            var macro = _storage.Load(filePath);
            if (macro == null) { StatusMessage = "파일을 읽을 수 없습니다."; return; }

            Nodes.Clear();
            Connections.Clear();
            foreach (var n in macro.Nodes) Nodes.Add(n);
            foreach (var c in macro.Connections) Connections.Add(c);
            MacroName = macro.Name;
            MacroDescription = macro.Description;
            SelectedNode = null;

            StatusMessage = $"📂 '{macro.Name}' 불러오기 완료! ({Nodes.Count}개 블록)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"불러오기 오류: {ex.Message}";
        }
    }

    // ==========================================
    // 커맨드: Raw 파일 불러오기
    // ==========================================

    [RelayCommand]
    private async Task LoadRawFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel 파일|*.xlsx;*.xls|모든 파일|*.*",
            Title = "Raw 파일 불러오기"
        };
        if (dialog.ShowDialog() != true) return;
        await LoadRawFileFromPath(dialog.FileName);
    }

    /// <summary>지정 경로의 Excel 파일을 Raw View에 로드합니다.</summary>
    private async Task LoadRawFileFromPath(string filePath)
    {
        IsRunning = true;
        StatusMessage = $"📄 Raw 파일 로딩: {Path.GetFileName(filePath)}...";
        try
        {
            var rawTable = await Task.Run(() =>
            {
                using var wb = new ClosedXML.Excel.XLWorkbook(filePath);
                var ws = wb.Worksheet(1);
                var range = ws.RangeUsed();
                if (range == null) return new DataTable();

                var dt = new DataTable();
                int colCount = range.ColumnCount();
                int rowCount = range.RowCount();

                for (int c = 1; c <= colCount; c++)
                    dt.Columns.Add(ws.Cell(1, c).GetString());

                for (int r = 2; r <= rowCount; r++)
                {
                    var row = dt.NewRow();
                    for (int c = 1; c <= colCount; c++)
                        row[c - 1] = ws.Cell(r, c).GetString();
                    dt.Rows.Add(row);
                }
                return dt;
            });

            RawData = rawTable;
            RawFilePath = filePath;
            PreviewData = null; // 이전 가공 결과 초기화
            StatusMessage = $"📄 Raw 로드 완료: {rawTable.Rows.Count}행 × {rawTable.Columns.Count}열 | {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Raw 파일 오류: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    // ==========================================
    // 선택된 블록의 속성 편집 지원 (간이)
    // ==========================================

    /// <summary>선택된 블록의 속성값을 문자열로 표시/편집용</summary>
    [ObservableProperty] private string _selectedNodePropsText = "";

    partial void OnSelectedNodeChanged(NodeModel? value)
    {
        if (value == null)
        {
            SelectedNodePropsText = "";
            return;
        }

        // Props를 key=value 줄바꿈 텍스트로 직렬화
        var lines = value.Props.Select(kvp => $"{kvp.Key}={kvp.Value}");
        SelectedNodePropsText = string.Join("\n", lines);
    }

    [RelayCommand]
    private void ApplyProps()
    {
        if (SelectedNode == null) return;

        // key=value 텍스트를 Props 딕셔너리로 역직렬화
        SelectedNode.Props.Clear();
        var lines = SelectedNodePropsText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var eqIdx = line.IndexOf('=');
            if (eqIdx <= 0) continue;
            string key = line[..eqIdx].Trim();
            string val = line[(eqIdx + 1)..].Trim();

            // 배열 지원: [a,b,c] 형태
            if (val.StartsWith('[') && val.EndsWith(']'))
            {
                var items = val.Trim('[', ']').Split(',').Select(s => (object)s.Trim()).ToList();
                SelectedNode.Props[key] = items;
            }
            else
            {
                SelectedNode.Props[key] = val;
            }
        }
        StatusMessage = $"'{SelectedNode.Label}' 속성이 적용되었습니다.";
    }

    // ==========================================
    // 내부 헬퍼
    // ==========================================

    private MacroDefinition BuildMacroDefinition()
    {
        return new MacroDefinition
        {
            Name = MacroName,
            Description = MacroDescription,
            Nodes = Nodes.ToList(),
            Connections = Connections.ToList()
        };
    }

    private static List<BlockTypeInfo> BuildPalette()
    {
        return new List<BlockTypeInfo>
        {
            new("📥 Excel 읽기", NodeType.ExcelRead, "입력", "sheet=1\nheaderRow=1"),
            new("🗑️ 열 삭제", NodeType.ColumnDelete, "열 조작", "columns=[열1,열2]"),
            new("📌 열 선택", NodeType.ColumnSelect, "열 조작", "columns=[열1,열2]"),
            new("✏️ 열 이름 변경", NodeType.ColumnRename, "열 조작", "mappings (JSON)"),
            new("➕ 열 추가", NodeType.ColumnAdd, "열 조작", "name=새열\ndefault=0"),
            new("🔍 행 필터", NodeType.RowFilter, "행 조작", "column=열\nop===\nvalue=값"),
            new("🗑️ 빈 행 제거", NodeType.EmptyRowRemove, "행 조작", "(없음)"),
            new("🔢 정렬", NodeType.Sort, "행 조작", "column=열\norder=asc"),
            new("🔗 중복 병합", NodeType.DuplicateMerge, "행 조작", "keyColumn=열\nsumColumns=[합산열]"),
            new("🔄 셀 치환", NodeType.CellReplace, "변환", "column=열\nfind=찾을값\nreplace=바꿀값"),
            new("∑ 그룹 합산", NodeType.GroupSum, "집계", "keyColumn=열\nsumColumn=합산열"),
            new("🔢 그룹 건수", NodeType.GroupCount, "집계", "keyColumn=열"),
            new("📤 PDF 출력", NodeType.PdfExport, "출력", "orientation=landscape"),
            new("💾 Excel 저장", NodeType.ExcelExport, "출력", "filename=output.xlsx"),
        };
    }
}

/// <summary>팔레트에 표시할 블록 유형 정보</summary>
public record BlockTypeInfo(string DisplayName, NodeType Type, string Category, string PropsHint);
