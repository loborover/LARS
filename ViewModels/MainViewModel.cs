using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LARS.Models;
using LARS.Services;
using LARS.Views;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly MultiDocService _multiDocService;
    private readonly DirectoryManager _dirs;

    public MainViewModel(
        BomReportService bomService,
        DailyPlanService dailyPlanService,
        PartListService partListService,
        ItemCounterService itemCounterService,
        FeederService feederService,
        PdfExportService pdfService,
        DirectoryManager dirs,
        SettingsService settingsService,
        MultiDocService multiDocService)
    {
        _bomService = bomService;
        _dailyPlanService = dailyPlanService;
        _partListService = partListService;
        _itemCounterService = itemCounterService;
        _feederService = feederService;
        _pdfService = pdfService;
        _dirs = dirs;
        _multiDocService = multiDocService;
    }

    // ==========================================
    // 커맨드: 매크로 에디터 열기
    // ==========================================

    [RelayCommand]
    private void OpenMacroEditor()
    {
        try
        {
            var sp = ((App)Application.Current).ServiceProvider;
            var editorWindow = sp.GetRequiredService<MacroEditorWindow>();
            editorWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"매크로 에디터를 열 수 없습니다:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
    private async Task ScanBomFilesAsync()
    {
        StatusText = "BOM 파일 스캔 중...";
        IsProcessing = true;
        Progress = 0;
        try
        {
            BomFiles.Clear();
            var progress = new Progress<double>(p => Progress = p * 100);
            var files = await Task.Run(() => _bomService.ScanBomFiles(progress));
            foreach (var f in files) BomFiles.Add(f);
            BomInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"BOM: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; Progress = 0; }
    }

    [RelayCommand]
    private async Task OpenBomFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "BOM 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
            InitialDirectory = _dirs.IsSetup ? _dirs.SourceBOM : ""
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadBomDataAsync(dialog.FileName);
        }
    }

    private async Task LoadBomDataAsync(string filePath)
    {
        StatusText = $"BOM 로딩: {Path.GetFileName(filePath)}…";
        IsProcessing = true;
        try
        {
            var result = await Task.Run(() => _bomService.ReadBomFile(filePath));
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
    private async Task ExportBomPdfAsync()
    {
        if (_currentBomData == null || !_currentBomData.IsSuccess)
        {
            StatusText = "BOM 데이터를 먼저 로드해 주세요.";
            return;
        }

        string defaultName = string.IsNullOrWhiteSpace(_currentBomData.Title)
            ? $"BOM_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            : $"{_currentBomData.Title}_{DateTime.Now:yyyyMMdd}.pdf";

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "BOM PDF 저장",
            Filter = "PDF 파일 (*.pdf)|*.pdf",
            FileName = defaultName,
            InitialDirectory = _dirs.IsSetup ? _dirs.Output : ""
        };

        if (dialog.ShowDialog() == true)
        {
            IsProcessing = true;
            StatusText = "PDF 생성 중...";
            // ExportBomToPdf: VBA Interior_Set_BOM 열너비 비율 적용
            bool ok = await Task.Run(() => _pdfService.ExportBomToPdf(
                dialog.FileName,
                _currentBomData.Title,
                _currentBomData.Headers,
                _currentBomData.Rows));
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
            IsProcessing = false;
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
    private async Task ScanDailyPlanFilesAsync()
    {
        StatusText = "DailyPlan 파일 스캔 중...";
        IsProcessing = true;
        Progress = 0;
        try
        {
            DailyPlanFiles.Clear();
            var progress = new Progress<double>(p => Progress = p * 100);
            var files = await Task.Run(() => _dailyPlanService.ScanDailyPlanFiles(DateTime.Now.Year, progress));
            foreach (var f in files) DailyPlanFiles.Add(f);
            DpInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"DailyPlan: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; Progress = 0; }
    }

    [RelayCommand]
    private async Task OpenDailyPlanFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "DailyPlan 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx",
            InitialDirectory = _dirs.IsSetup ? _dirs.SourceDailyPlan : ""
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText = $"DailyPlan 로딩: {Path.GetFileName(dialog.FileName)}…";
            IsProcessing = true;
            try
            {
                var rawResult = await Task.Run(() => _dailyPlanService.ReadDailyPlanFile(dialog.FileName));
                if (rawResult.IsSuccess)
                {
                    var meta = await Task.Run(() => _dailyPlanService.ReadMetaFromFile(dialog.FileName));
                    rawResult.Meta = meta;

                    DailyPlanDataTable = ToDataTable(rawResult.Headers, rawResult.Rows);
                    string dateLabel = meta.IsValid ? meta.DateLabel : "날짜불명";
                    DpInfoText = $"{rawResult.Rows.Count}행 | {dateLabel} | {Path.GetFileName(dialog.FileName)}";
                    StatusText = $"DailyPlan 로드 완료: {rawResult.Rows.Count}행";
                    _currentDpData = rawResult;
                }
                else
                {
                    StatusText = $"DailyPlan 오류: {rawResult.ErrorMessage}";
                }
            }
            catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
            finally { IsProcessing = false; }
        }
    }

    [RelayCommand]
    private async Task ExportDpPdfAsync()
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
            IsProcessing = true;
            StatusText = "PDF 생성 중...";
            bool ok = await Task.Run(() => _pdfService.ExportDailyPlanToPdf(
                dialog.FileName, "DailyPlan Report",
                _currentDpData.Headers, _currentDpData.Rows.ToList()));
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
            IsProcessing = false;
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
    private async Task ScanPartListFilesAsync()
    {
        StatusText = "PartList 파일 스캔 중...";
        IsProcessing = true;
        Progress = 0;
        try
        {
            PartListFiles.Clear();
            var progress = new Progress<double>(p => Progress = p * 100);
            var files = await Task.Run(() => _partListService.ScanPartListFiles(DateTime.Now.Year, progress));
            foreach (var f in files) PartListFiles.Add(f);
            PlInfoText = $"{files.Count}개 파일 발견";
            StatusText = $"PartList: {files.Count}개 파일 스캔 완료";
        }
        catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
        finally { IsProcessing = false; Progress = 0; }
    }

    [RelayCommand]
    private async Task OpenPartListFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "PartList 파일 열기",
            Filter = "Excel 파일 (*.xlsx)|*.xlsx",
            InitialDirectory = _dirs.IsSetup ? _dirs.SourcePartList : ""
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText = $"PartList 로딩: {Path.GetFileName(dialog.FileName)}…";
            IsProcessing = true;
            try
            {
                var rawResult = await Task.Run(() => _partListService.ReadPartListFile(dialog.FileName));
                if (rawResult.IsSuccess)
                {
                    _currentPlData = rawResult;
                    PartListDataTable = ToDataTable(rawResult.Headers, rawResult.Rows);
                    PlInfoText = $"{rawResult.Rows.Count}행 | {Path.GetFileName(dialog.FileName)}";
                    StatusText = $"PartList 로드 완료: {rawResult.Rows.Count}행";
                }
                else
                {
                    StatusText = $"PartList 오류: {rawResult.ErrorMessage}";
                }
            }
            catch (Exception ex) { StatusText = $"오류: {ex.Message}"; }
            finally { IsProcessing = false; }
        }
    }

    [RelayCommand]
    private async Task ExportPlPdfAsync()
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
            IsProcessing = true;
            StatusText = "PDF 생성 중...";
            bool ok = await Task.Run(() => _pdfService.ExportTableToPdf(
                dialog.FileName, "PartList Report",
                _currentPlData.Headers, _currentPlData.Rows.ToList()));
            StatusText = ok ? $"PDF 저장 완료: {dialog.FileName}" : "PDF 저장 실패";
            IsProcessing = false;
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
    private async Task RunItemCounterAsync()
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
            ItemCounterResult result;
            var schedules = _currentDpData?.Meta?.Schedules;
            bool hasSchedule = schedules != null && schedules.Count > 0 && _currentDpData!.Meta!.IsValid;
            List<(DateTime Date, int LotCount)> dateSchedules = new();

            if (hasSchedule)
            {
                int year = DateTime.Now.Year;
                int month = _currentDpData!.Meta!.Month;
                foreach (var s in schedules!)
                {
                    try { dateSchedules.Add((new DateTime(year, month, s.Day), s.LotCount)); }
                    catch { /* 무효한 날짜 무시 */ }
                }
            }

            if (dateSchedules.Count > 0)
            {
                result = await Task.Run(() => _itemCounterService.RunPipelineWithDates(_currentPlData, dateSchedules));
            }
            else
            {
                result = await Task.Run(() => _itemCounterService.RunPipeline(_currentPlData));
            }

            if (result.IsSuccess && result.MergedGroup != null)
            {
                var dt = new DataTable();
                dt.Columns.Add("NickName");
                dt.Columns.Add("Vendor");
                dt.Columns.Add("PartNumber");

                if (dateSchedules.Count > 0)
                {
                    foreach (var s in dateSchedules)
                        dt.Columns.Add($"{s.Date.Day}일", typeof(long));
                }
                else
                {
                    dt.Columns.Add("QTY", typeof(long));
                }
                
                dt.Columns.Add("Total", typeof(long));

                foreach (var unit in result.MergedGroup.GetAllUnits())
                {
                    var row = dt.NewRow();
                    row["NickName"] = unit.NickName;
                    row["Vendor"] = unit.Vendor;
                    row["PartNumber"] = unit.PartNumber;

                    if (dateSchedules.Count > 0)
                    {
                        foreach (var s in dateSchedules)
                            row[$"{s.Date.Day}일"] = unit[s.Date];
                        row["Total"] = unit.TotalCount;
                    }
                    else
                    {
                        row["QTY"] = unit.QTY;
                        row["Total"] = unit.QTY;
                    }
                    dt.Rows.Add(row);
                }

                ItemCounterDataTable = dt;
                string schedInfo = dateSchedules.Count > 0 ? $" | 스케줄 {dateSchedules.Count}일 연동" : "";
                IcInfoText = $"병합 전 {result.TotalItemsBeforeMerge}건 → 병합 후 {result.MergedGroup.UnitCount}건{schedInfo}";
                StatusText = $"ItemCounter 완료: {result.MergedGroup.UnitCount}개 자재{schedInfo}";
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
    // MultiDocuments 교차 매핑 (Sprint 8)
    // ==========================================

    public ObservableCollection<MultiDocItem> MultiDocuments { get; } = new();

    [ObservableProperty]
    private string _mdInfoText = "대기 중";

    [RelayCommand]
    private async Task LoadMultiDocumentsAsync()
    {
        StatusText = "교차 매핑 스캔 중...";
        IsProcessing = true;
        try
        {
            var dpFiles = await Task.Run(() => _dailyPlanService.ScanDailyPlanFiles(DateTime.Now.Year));
            var plFiles = await Task.Run(() => _partListService.ScanPartListFiles(DateTime.Now.Year));

            var matched = await Task.Run(() => _multiDocService.MatchFiles(dpFiles, plFiles));

            MultiDocuments.Clear();
            foreach (var item in matched)
                MultiDocuments.Add(item);

            MdInfoText = $"{matched.Count}개 그룹 매핑 (완전 매치는 {matched.Count(x => x.HasBoth)}개)";
            StatusText = $"교차 매핑 완료: {matched.Count}개 그룹 발견";
        }
        catch (Exception ex) { StatusText = $"교차 매핑 오류: {ex.Message}"; }
        finally { IsProcessing = false; }
    }

    [RelayCommand]
    private async Task ProcessMultiDocumentsAsync()
    {
        var selected = MultiDocuments.Where(x => x.IsSelected && x.HasBoth).ToList();
        if (selected.Count == 0)
        {
            StatusText = "선택된 완료 항목이 없습니다.";
            return;
        }

        StatusText = $"일괄 처리 시작: {selected.Count}개 항목";
        IsProcessing = true;
        int successCount = 0;
        Progress = 0;
        
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                StatusText = $"처리 중 ({i + 1}/{selected.Count}): {item.Key}...";
                
                // PartList 로드
                var plResult = await Task.Run(() => _partListService.ReadPartListFile(item.PartListFile!.FullPath));
                if (!plResult.IsSuccess) continue;

                // PDF 일괄 내보내기
                string saveDir = _dirs.IsSetup ? _dirs.Output : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string savePath = Path.Combine(saveDir, $"PartList_{item.Key}_{DateTime.Now:HHmmss}.pdf");
                bool ok = await Task.Run(() => _pdfService.ExportTableToPdf(
                    savePath, $"PartList {item.Key}",
                    plResult.Headers, plResult.Rows.ToList()));

                if (ok) successCount++;
                Progress = ((double)(i + 1) / selected.Count) * 100;
            }
            StatusText = $"일괄 처리 완료: {successCount}/{selected.Count} 성공";
        }
        catch (Exception ex) { StatusText = $"일괄 처리 오류: {ex.Message}"; }
        finally { IsProcessing = false; Progress = 0; }
    }

    // ==========================================
    // 설정 (경로 관리)
    // ==========================================

    [ObservableProperty]
    private string _settingsSourceBom = string.Empty;

    [ObservableProperty]
    private string _settingsSourceDailyPlan = string.Empty;

    [ObservableProperty]
    private string _settingsSourcePartList = string.Empty;

    [ObservableProperty]
    private string _settingsOutputBom = string.Empty;

    [ObservableProperty]
    private string _settingsOutputDailyPlan = string.Empty;

    [ObservableProperty]
    private string _settingsOutputPartList = string.Empty;

    private void SyncSettingsToViewModel()
    {
        // SettingsService는 생성자 지연 후기화 혹은 별도 주석처럼 쓰이고 있음
        // 기존 MainViewModel.cs 상단에 `SettingsService _settingsService`가 주입되어야 하지만,
        // 없을 경우 DirectoryManager에서 직접 설정 상태를 얻기도 어려우므로 의존성 주입된 인스턴스를 확인해야 함.
        // 현재 MainViewModel는 _dirs만 갖고 있으므로, SettingsService 필드가 부족하다면 새로 추가하거나,
        // (App.xaml.cs에서 이미 OnStartup 시점에 AppSettings 단위로 넘기므로 App 내부 상태 저장 등 활용)
        
        // **수정 사항:** App.xaml.cs에서 Settings를 넘기면 더 좋으나, DI 컨테이너에서 해소하기 위해
        // System.Windows.Application.Current 기반 ServiceProvider를 통해 가져오도록 합니다.
        var provider = ((App)System.Windows.Application.Current).ServiceProvider;
        var settingsService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsService>(provider);
        var settings = settingsService.Load();

        BasePath = settings.BasePath;
        SettingsSourceBom = settings.SourceBOM;
        SettingsSourceDailyPlan = settings.SourceDailyPlan;
        SettingsSourcePartList = settings.SourcePartList;
        SettingsOutputBom = settings.OutputBOM;
        SettingsOutputDailyPlan = settings.OutputDailyPlan;
        SettingsOutputPartList = settings.OutputPartList;
    }

    private void SaveAndApplySettings()
    {
        var provider = ((App)System.Windows.Application.Current).ServiceProvider;
        var settingsService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsService>(provider);
        
        var newSettings = new AppSettings
        {
            BasePath = this.BasePath,
            SourceBOM = this.SettingsSourceBom,
            SourceDailyPlan = this.SettingsSourceDailyPlan,
            SourcePartList = this.SettingsSourcePartList,
            OutputBOM = this.SettingsOutputBom,
            OutputDailyPlan = this.SettingsOutputDailyPlan,
            OutputPartList = this.SettingsOutputPartList,
            LastFeederName = SelectedFeeder?.Name ?? string.Empty
        };
        settingsService.Save(newSettings);
        _dirs.Setup(newSettings);
        StatusText = "설정이 저장 및 적용되었습니다.";
    }

    [RelayCommand]
    private void BrowseBasePath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "작업 루트 디렉토리 선택" };
        if (dialog.ShowDialog() == true)
        {
            BasePath = dialog.FolderName;
            SaveAndApplySettings();
            LoadFeeders();
        }
    }

    [RelayCommand]
    private void BrowsePath(string param)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = $"{param} 폴더 선택" };
        if (dialog.ShowDialog() == true)
        {
            switch (param)
            {
                case "SourceBOM": SettingsSourceBom = dialog.FolderName; break;
                case "SourceDP": SettingsSourceDailyPlan = dialog.FolderName; break;
                case "SourcePL": SettingsSourcePartList = dialog.FolderName; break;
                case "OutBOM": SettingsOutputBom = dialog.FolderName; break;
                case "OutDP": SettingsOutputDailyPlan = dialog.FolderName; break;
                case "OutPL": SettingsOutputPartList = dialog.FolderName; break;
            }
            SaveAndApplySettings();
        }
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        if (!_dirs.IsSetup)
        {
            StatusText = "먼저 작업 루트 디렉토리를 설정해 주세요.";
            return;
        }

        IsProcessing = true;
        StatusText = "전체 갱신 중...";
        await ScanBomFilesAsync();
        await ScanDailyPlanFilesAsync();
        await ScanPartListFilesAsync();
        await LoadMultiDocumentsAsync();
        LoadFeeders();
        StatusText = "전체 갱신 완료";
        IsProcessing = false;
    }

    // 초기화용 메서드 (생성자 호출 권장)
    public void Initialize()
    {
        SyncSettingsToViewModel();
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
