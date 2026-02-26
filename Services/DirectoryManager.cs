using System.IO;

namespace LARS.Services;

/// <summary>
/// 디렉토리 관리 서비스.
/// 앱 실행 경로 기반으로 하위 폴더 참조를 제공하며, AppSettings를 활용하여 커스텀 지정된 경로를 우선합니다.
/// BasePath 하위에 기본적으로 LARS_Documents 폴더를 생성하여 모든 내용을 격리합니다.
/// </summary>
public class DirectoryManager
{
    private AppSettings _currentSettings = new AppSettings();
    public bool IsSetup => !string.IsNullOrEmpty(_currentSettings.BasePath);

    // 내부 루트 캐싱
    private string GetRoot() => IsSetup ? Path.Combine(_currentSettings.BasePath, "LARS_Documents") : string.Empty;

    /// <summary>
    /// 설정 정보를 받아 앱의 디렉토리 환경을 구성합니다.
    /// </summary>
    public void Setup(AppSettings settings)
    {
        _currentSettings = settings;
        EnsureDirectories();
    }

    // ==========================================
    // 앱 설정 직접 노출 프로퍼티 (루트)
    // ==========================================
    public string BasePath => _currentSettings.BasePath;
    public string DocumentsRoot => GetRoot();
    
    /// <summary>기본 소스 스캔 경로: 사용자 지정 전역 경로 -> OS 다운로드 폴더 -> 내문서 폴더</summary>
    public string DefaultSourcePath
    {
        get
        {
            // 1순위: 사용자가 지정한 전역 기본 스캔 경로
            if (!string.IsNullOrWhiteSpace(_currentSettings.DefaultSourcePath))
                return _currentSettings.DefaultSourcePath;

            // 2순위: OS 다운로드 폴더
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads))
                return downloads;
            
            // 3순위: OS 내문서 폴더
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    // ==========================================
    // Source 경로 (스캔 대상) 읽기 : 설정 없으면 DefaultSourcePath
    // ==========================================
    public string SourceBOM => ResolveSourcePath(_currentSettings.SourceBOM);
    public string SourceDailyPlan => ResolveSourcePath(_currentSettings.SourceDailyPlan);
    public string SourcePartList => ResolveSourcePath(_currentSettings.SourcePartList);

    // ==========================================
    // Output 폴더 접근자 : 설정 없으면 DocumentsRoot/{항목}
    // ==========================================
    public string BOM => ResolveOutputPath(_currentSettings.OutputBOM, "Output", "BOM");
    public string DailyPlan => ResolveOutputPath(_currentSettings.OutputDailyPlan, "Output", "DailyPlan");
    public string PartList => ResolveOutputPath(_currentSettings.OutputPartList, "Output", "PartList");
    public string Feeder => ResolveOutputPath(_currentSettings.OutputFeeder, "Config", "Feeder");
    public string Backup => ResolveOutputPath(_currentSettings.OutputBackup, "Backup");
    public string Output => ResolveOutputPath(_currentSettings.OutputPdf, "Output", "PDF");

    /// <summary>
    /// Source 전용: 사용자 정의 경로(userSetPath)가 있으면 우선적으로 사용하고, 없으면 다운로드 폴더를 반환합니다.
    /// </summary>
    private string ResolveSourcePath(string userSetPath)
    {
        return !string.IsNullOrWhiteSpace(userSetPath) ? userSetPath : DefaultSourcePath;
    }

    /// <summary>
    /// Output 전용: 사용자 정의 경로(userSetPath)가 있으면 우선적으로 사용하고,
    /// 없으면 DocumentsRoot 아래에 fallbackSubDirs를 결합하여 반환합니다.
    /// </summary>
    private string ResolveOutputPath(string userSetPath, params string[] fallbackSubDirs)
    {
        if (!string.IsNullOrWhiteSpace(userSetPath))
            return userSetPath;

        if (!IsSetup) return string.Empty;

        string path = DocumentsRoot;
        foreach (var dir in fallbackSubDirs)
        {
            path = Path.Combine(path, dir);
        }
        return path;
    }

    /// <summary>
    /// 구동에 필수적인 출력용 하위 디렉토리를 자동 생성합니다.
    /// (Source 경로는 사용자의 다운로드 폴더 또는 외부 지정경로이므로 강제 생성하지 않음)
    /// </summary>
    private void EnsureDirectories()
    {
        if (!IsSetup) return;

        Directory.CreateDirectory(BOM);
        Directory.CreateDirectory(DailyPlan);
        Directory.CreateDirectory(PartList);
        Directory.CreateDirectory(Feeder);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Output);
    }
}
