using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LARS.Models;
using LARS.Services;

namespace LARS.ViewModels;

/// <summary>
/// 메인 뷰모델. VBA AutoReportHandler에 대응하는 중앙 컨트롤러.
/// 파일 스캔 → 데이터 로드 → PDF 내보내기 파이프라인 관리.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BomReportService _bomService;
    private readonly DailyPlanService _dailyPlanService;
    private readonly PartListService _partListService;
    private readonly ItemCounterService _itemCounterService;
    private readonly FeederService _feederService;
    private readonly PdfExportService _pdfService;
    private readonly DirectoryManager _dirs;

    public MainViewModel(
        BomReportService bomService,
        DailyPlanService dailyPlanService,
        PartListService partListService,
        ItemCounterService itemCounterService,
        FeederService feederService,
        PdfExportService pdfService,
        DirectoryManager dirs)
    {
        _bomService = bomService;
        _dailyPlanService = dailyPlanService;
        _partListService = partListService;
        _itemCounterService = itemCounterService;
        _feederService = feederService;
        _pdfService = pdfService;
        _dirs = dirs;
    }

    // ==========================================
    // 공통 상태 속성
    // ==========================================

    [ObservableProperty]
    private string _statusText = "준비 — 폴더를 설정하고 파일을 스캔하세요";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _basePath = string.Empty;

    // ==========================================
    // BOM 탭
    // ==========================================

    public ObservableCollection<FileMetadata> BomFiles { get; } = new();

    [ObservableProperty]
    private DataTable? _bomDataTable;

    [ObservableProperty]
    private string _bomInfoText = "스캔 대기 중";

    [RelayCommand]
    private void ScanBomFiles()
    {
        StatusText = "BOM 파일 스캔 중...";
        IsProcessing = true;
        try
        {
            BomFiles.Clear();
            var files = _bomService.ScanBomFiles();
            foreach (var f in files) BomFiles.Add(f);
            BomInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"BOM: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private void OpenBomFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "BOM 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
            InitialDirectory = _dirs.IsSetup ? _dirs.BOM : ""
        };

        if (dialog.ShowDialog() == true)
        {
            LoadBomData(dialog.FileName);
        }
    }

    private void LoadBomData(string filePath)
    {
        StatusText = $"BOM 로딩: {Path.GetFileName(filePath)}…";
        IsProcessing = true;
        try
        {
            var result = _bomService.ReadBomFile(filePath);
            if (result.IsSuccess)
            {
                BomDataTable = ToDataTable(result.Headers, result.Rows);
                BomInfoText = $"{result.Rows.Count}행 로드 | {Path.GetFileName(filePath)}";
                StatusText = $"BOM 로드 완료: {result.Rows.Count}행";
                _currentBomData = result;
            }
            else
            {
                StatusText = $"BOM 오류: {result.ErrorMessage}";
            }
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    private BomDataResult? _currentBomData;

    [RelayCommand]
    private void ExportBomPdf()
    {
        if (_currentBomData == null || !_currentBomData.IsSuccess)
        {
            StatusText = "BOM 데이터를 먼저 로드해 주세요.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "BOM PDF 저장",
            Filter = "PDF 파일 (*.pdf)|*.pdf",
            FileName = $"BOM_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
            InitialDirectory = _dirs.IsSetup ? _dirs.Output : ""
        };

        if (dialog.ShowDialog() == true)
        {
            bool ok = _pdfService.ExportTableToPdf(dialog.FileName, "BOM Report",
                _currentBomData.Headers, _currentBomData.Rows);
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
        }
    }

    // ==========================================
    // DailyPlan 탭
    // ==========================================

    public ObservableCollection<FileMetadata> DailyPlanFiles { get; } = new();

    [ObservableProperty]
    private DataTable? _dailyPlanDataTable;

    [ObservableProperty]
    private string _dpInfoText = "스캔 대기 중";

    private DailyPlanDataResult? _currentDpData;

    [RelayCommand]
    private void ScanDailyPlanFiles()
    {
        StatusText = "DailyPlan 파일 스캔 중...";
        IsProcessing = true;
        try
        {
            DailyPlanFiles.Clear();
            var files = _dailyPlanService.ScanDailyPlanFiles(DateTime.Now.Year);
            foreach (var f in files) DailyPlanFiles.Add(f);
            DpInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"DailyPlan: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private void OpenDailyPlanFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "DailyPlan 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx",
            InitialDirectory = _dirs.IsSetup ? _dirs.DailyPlan : ""
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText = $"DailyPlan 로딩: {Path.GetFileName(dialog.FileName)}…";
            IsProcessing = true;
            try
            {
                var result = _dailyPlanService.ReadDailyPlanFile(dialog.FileName);
                if (result.IsSuccess)
                {
                    DailyPlanDataTable = ToDataTable(result.Headers, result.Rows);
                    int lotCount = result.LotGroup?.SubLots.Count ?? 0;
                    DpInfoText = $"{result.Rows.Count}행 | {lotCount}개 LOT | {Path.GetFileName(dialog.FileName)}";
                    StatusText = $"DailyPlan 로드 완료: {result.Rows.Count}행, LOT {lotCount}개";
                    _currentDpData = result;
                }
                else
                {
                    StatusText = $"DailyPlan 오류: {result.ErrorMessage}";
                }
            }
            catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
            finally { IsProcessing = false; }
        }
    }

    [RelayCommand]
    private void ExportDpPdf()
    {
        if (_currentDpData == null || !_currentDpData.IsSuccess)
        {
            StatusText = "DailyPlan 데이터를 먼저 로드해 주세요.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "DailyPlan PDF 저장",
            Filter = "PDF 파일 (*.pdf)|*.pdf",
            FileName = $"DailyPlan_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
            InitialDirectory = _dirs.IsSetup ? _dirs.Output : ""
        };

        if (dialog.ShowDialog() == true)
        {
            bool ok = _pdfService.ExportTableToPdf(dialog.FileName, "DailyPlan Report",
                _currentDpData.Headers, _currentDpData.Rows.ToList(), isLandscape: true);
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
        }
    }

    // ==========================================
    // PartList 탭
    // ==========================================

    public ObservableCollection<FileMetadata> PartListFiles { get; } = new();

    [ObservableProperty]
    private DataTable? _partListDataTable;

    [ObservableProperty]
    private string _plInfoText = "스캔 대기 중";

    private PartListDataResult? _currentPlData;

    [RelayCommand]
    private void ScanPartListFiles()
    {
        StatusText = "PartList 파일 스캔 중...";
        IsProcessing = true;
        try
        {
            PartListFiles.Clear();
            var files = _partListService.ScanPartListFiles(DateTime.Now.Year);
            foreach (var f in files) PartListFiles.Add(f);
            PlInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"PartList: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private void OpenPartListFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "PartList 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx",
            InitialDirectory = _dirs.IsSetup ? _dirs.PartList : ""
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText = $"PartList 로딩: {Path.GetFileName(dialog.FileName)}…";
            IsProcessing = true;
            try
            {
                var result = _partListService.ReadPartListFile(dialog.FileName);
                if (result.IsSuccess)
                {
                    PartListDataTable = ToDataTable(result.Headers, result.Rows);
                    PlInfoText = $"{result.Rows.Count}행 | {Path.GetFileName(dialog.FileName)}";
                    StatusText = $"PartList 로드 완료: {result.Rows.Count}행";
                    _currentPlData = result;
                }
                else
                {
                    StatusText = $"PartList 오류: {result.ErrorMessage}";
                }
            }
            catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
            finally { IsProcessing = false; }
        }
    }

    [RelayCommand]
    private void ExportPlPdf()
    {
        if (_currentPlData == null || !_currentPlData.IsSuccess)
        {
            StatusText = "PartList 데이터를 먼저 로드해 주세요.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "PartList PDF 저장",
            Filter = "PDF 파일 (*.pdf)|*.pdf",
            FileName = $"PartList_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
            InitialDirectory = _dirs.IsSetup ? _dirs.Output : ""
        };

        if (dialog.ShowDialog() == true)
        {
            bool ok = _pdfService.ExportTableToPdf(dialog.FileName, "PartList Report",
                _currentPlData.Headers, _currentPlData.Rows.ToList());
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
        }
    }

    // ==========================================
    // ItemCounter 탭
    // ==========================================

    [ObservableProperty]
    private DataTable? _itemCounterDataTable;

    [ObservableProperty]
    private string _icInfoText = "PartList를 먼저 로드하세요";

    [RelayCommand]
    private void RunItemCounter()
    {
        if (_currentPlData == null || !_currentPlData.IsSuccess)
        {
            StatusText = "PartList 데이터를 먼저 로드해 주세요.";
            return;
        }

        StatusText = "ItemCounter 파이프라인 실행 중...";
        IsProcessing = true;
        try
        {
            var result = _itemCounterService.RunPipeline(_currentPlData);
            if (result.IsSuccess && result.MergedGroup != null)
            {
                // DataTable로 변환
                var dt = new DataTable();
                dt.Columns.Add("NickName");
                dt.Columns.Add("Vendor");
                dt.Columns.Add("PartNumber");
                dt.Columns.Add("QTY", typeof(long));
                dt.Columns.Add("Total", typeof(long));

                foreach (var unit in result.MergedGroup.GetAllUnits())
                {
                    dt.Rows.Add(unit.NickName, unit.Vendor, unit.PartNumber, unit.QTY, unit.TotalCount);
                }

                ItemCounterDataTable = dt;
                IcInfoText = $"병합 전 {result.TotalItemsBeforeMerge}건 → 병합 후 {result.MergedGroup.UnitCount}건";
                StatusText = $"ItemCounter 완료: {result.MergedGroup.UnitCount}개 자재";
            }
            else
            {
                StatusText = $"ItemCounter 오류: {result.ErrorMessage}";
            }
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    // ==========================================
    // Feeder 탭
    // ==========================================

    public ObservableCollection<FeederUnit> Feeders { get; } = new();

    [ObservableProperty]
    private FeederUnit? _selectedFeeder;

    [ObservableProperty]
    private string _newFeederName = string.Empty;

    [ObservableProperty]
    private string _newFeederItem = string.Empty;

    [ObservableProperty]
    private string _feederInfoText = "Feeder 목록";

    [RelayCommand]
    private void LoadFeeders()
    {
        Feeders.Clear();
        var list = _feederService.LoadFeeders();
        foreach (var f in list) Feeders.Add(f);
        FeederInfoText = $"Feeder {list.Count}개 로드";
        StatusText = $"Feeder: {list.Count}개 로드 완료";
    }

    [RelayCommand]
    private void AddFeeder()
    {
        if (string.IsNullOrWhiteSpace(NewFeederName))
        {
            StatusText = "Feeder 이름을 입력해 주세요.";
            return;
        }

        var allFeeders = Feeders.ToList();
        var added = _feederService.AddFeeder(NewFeederName.Trim(), allFeeders);
        Feeders.Add(added);
        NewFeederName = string.Empty;
        FeederInfoText = $"Feeder {Feeders.Count}개";
        StatusText = $"Feeder '{added.Name}' 추가됨";
    }

    [RelayCommand]
    private void RemoveFeeder()
    {
        if (SelectedFeeder == null)
        {
            StatusText = "삭제할 Feeder를 선택해 주세요.";
            return;
        }

        var allFeeders = Feeders.ToList();
        _feederService.RemoveFeeder(SelectedFeeder.Name, allFeeders);
        Feeders.Remove(SelectedFeeder);
        FeederInfoText = $"Feeder {Feeders.Count}개";
        StatusText = "Feeder 삭제됨";
    }

    [RelayCommand]
    private void AddFeederItem()
    {
        if (SelectedFeeder == null || string.IsNullOrWhiteSpace(NewFeederItem))
        {
            StatusText = "Feeder를 선택하고 아이템 이름을 입력해 주세요.";
            return;
        }

        var allFeeders = Feeders.ToList();
        _feederService.AddItemToFeeder(SelectedFeeder.Name, NewFeederItem.Trim(), allFeeders);
        SelectedFeeder.Items.Add(NewFeederItem.Trim());
        NewFeederItem = string.Empty;
        OnPropertyChanged(nameof(SelectedFeeder));
        StatusText = $"'{SelectedFeeder.Name}'에 아이템 추가됨";
    }

    // ==========================================
    // 설정
    // ==========================================

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "작업 디렉토리 선택"
        };

        if (dialog.ShowDialog() == true)
        {
            BasePath = dialog.FolderName;
            _dirs.Setup(BasePath);
            StatusText = $"작업 경로 설정: {BasePath}";
            LoadFeeders();
        }
    }

    [RelayCommand]
    private void RefreshAll()
    {
        if (!_dirs.IsSetup)
        {
            StatusText = "먼저 작업 디렉토리를 설정해 주세요.";
            return;
        }

        ScanBomFiles();
        ScanDailyPlanFiles();
        ScanPartListFiles();
        LoadFeeders();
        StatusText = "전체 갱신 완료";
    }

    // ==========================================
    // 유틸리티
    // ==========================================

    /// <summary>
    /// 헤더 + 행 데이터를 DataTable로 변환합니다.
    /// WPF DataGrid에 바인딩하기 위한 공통 변환기.
    /// </summary>
    private static DataTable ToDataTable(List<string> headers, List<List<string>> rows)
    {
        var dt = new DataTable();
        foreach (var h in headers)
        {
            string colName = string.IsNullOrEmpty(h) ? $"Col{dt.Columns.Count + 1}" : h;
            // 중복 컬럼명 처리
            int suffix = 1;
            string original = colName;
            while (dt.Columns.Contains(colName))
                colName = $"{original}_{suffix++}";
            dt.Columns.Add(colName);
        }

        foreach (var row in rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < Math.Min(row.Count, dt.Columns.Count); i++)
                dr[i] = row[i];
            dt.Rows.Add(dr);
        }

        return dt;
    }
}
