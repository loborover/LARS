using System.Text.Json;
namespace LARS.ENGINE.Core;

public static class Directories
{
    public static string OwnPath { get; }
    public static string DocumentsPath { get; }

    public static string ConfigFilePath { get; }

    public static string DefaultDownloadPath { get; }
    public static string DefaultDailyPlanPath { get; }
    public static string DefaultPartListPath { get; }
    public static string DefaultBOMPath { get; }

    private static DirectorySettings _settings;

    // ----- 외부에서 가져다 쓸 경로들 -----

    // DownloadPath : JSON 값 있으면 그거 / 없으면 DefaultDownloadPath
    public static string DownloadPath
        => ResolvePath(OwnPath, _settings.DownloadPath, DefaultDownloadPath);

    // DPPath : JSON 값 없으면 Documents\DailyPlan
    public static string DPPath
        => ResolvePath(DocumentsPath, _settings.DailyPlanPath, DefaultDailyPlanPath);

    // PLPath : JSON 값 없으면 Documents\PartList
    public static string PLPath
        => ResolvePath(DocumentsPath, _settings.PartListPath, DefaultPartListPath);

    // BOMPath : JSON 값 없으면 Documents\BOM
    public static string BOMPath
        => ResolvePath(DocumentsPath, _settings.BOMPath, DefaultBOMPath);

    // ----- static ctor : 프로그램 시작 시 한 번만 실행 -----
    static Directories()
    {
        // 1) 실행 파일 위치 (bin\Debug\net10.0\ 등)
        OwnPath = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 2) 기본 Documents 폴더 = 실행 폴더 기준 Documents 하위
        DocumentsPath = Path.Combine(OwnPath, "Documents");

        // 3) 기본 서브 폴더 이름들
        DefaultDownloadPath  = Path.Combine(OwnPath,     "Downloads");
        DefaultDailyPlanPath = Path.Combine(DocumentsPath, "DailyPlan");
        DefaultPartListPath  = Path.Combine(DocumentsPath, "PartList");
        DefaultBOMPath       = Path.Combine(DocumentsPath, "BOM");

        // 4) 설정 파일 위치 : 실행 폴더\directories.json
        ConfigFilePath = Path.Combine(OwnPath, "directories.json");

        // 5) JSON 읽기
        _settings = LoadSettings();

        // 6) 실제로 쓸 경로들 폴더 생성
        EnsureDirectoryExists(DocumentsPath);
        EnsureDirectoryExists(DownloadPath);
        EnsureDirectoryExists(DPPath);
        EnsureDirectoryExists(PLPath);
        EnsureDirectoryExists(BOMPath);
    }

    // ----- public setter 메서드 (원하면 나중에 UI에서 호출) -----

    public static void SetDownloadPath(string newPath)
    {
        _settings.DownloadPath = NormalizePath(newPath);
        SaveSettings();
        EnsureDirectoryExists(DownloadPath);
    }

    public static void SetDailyPlanPath(string newPath)
    {
        _settings.DailyPlanPath = newPath; // 상대/절대 둘 다 허용
        SaveSettings();
        EnsureDirectoryExists(DPPath);
    }

    public static void SetPartListPath(string newPath)
    {
        _settings.PartListPath = newPath;
        SaveSettings();
        EnsureDirectoryExists(PLPath);
    }

    public static void SetBOMPath(string newPath)
    {
        _settings.BOMPath = newPath;
        SaveSettings();
        EnsureDirectoryExists(BOMPath);
    }

    // ----- 내부 유틸들 -----

    private static DirectorySettings LoadSettings()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new DirectorySettings();

            var json = File.ReadAllText(ConfigFilePath);
            var settings = JsonSerializer.Deserialize<DirectorySettings>(json);

            return settings ?? new DirectorySettings();
        }
        catch
        {
            return new DirectorySettings();
        }
    }

    private static void SaveSettings()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_settings, options);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>
    /// baseDir 기준으로 설정값과 기본값을 섞어서 실제 경로를 만든다.
    /// - 설정값이 비어 있으면 defaultFullPath 사용
    /// - 설정값이 절대경로면 그대로 사용
    /// - 설정값이 상대경로면 baseDir 밑으로 붙인다.
    /// </summary>
    private static string ResolvePath(string baseDir, string? settingValue, string defaultFullPath)
    {
        if (string.IsNullOrWhiteSpace(settingValue))
            return defaultFullPath;

        var trimmed = settingValue.Trim();

        // 절대 경로면 그대로
        if (Path.IsPathRooted(trimmed))
            return Path.GetFullPath(trimmed);

        // 상대 경로면 baseDir 밑으로
        return Path.Combine(baseDir, trimmed);
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path);

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}

// JSON 구조와 매핑되는 클래스
public class DirectorySettings
{
    public string? DownloadPath { get; set; }
    public string? DailyPlanPath { get; set; }
    public string? PartListPath { get; set; }
    public string? BOMPath { get; set; }
}