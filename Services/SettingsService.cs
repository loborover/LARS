using System.IO;
using System.Text.Json;

namespace LARS.Services;

/// <summary>
/// 앱 설정 영속성 서비스.
/// Sprint 5: 앱 재시작 후 BasePath 복원.
/// %AppData%/LARS/settings.json에 저장.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LARS");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    /// <summary>
    /// 설정을 파일에서 로드합니다. 파일이 없으면 기본값 반환.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    /// <summary>
    /// 설정을 파일에 저장합니다.
    /// </summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 저장 실패는 조용히 무시 */ }
    }
}

/// <summary>
/// 앱 설정 DTO.
/// </summary>
public record AppSettings
{
    /// <summary>마지막으로 사용한 작업 기본 경로.</summary>
    public string BasePath { get; init; } = string.Empty;

    /// <summary>마지막으로 사용한 소스 경로 (선택).</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>마지막으로 선택한 Feeder 이름.</summary>
    public string LastFeederName { get; init; } = string.Empty;
}
