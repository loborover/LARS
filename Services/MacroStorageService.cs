using System.IO;
using System.Text.Json;
using LARS.Models.Macro;

namespace LARS.Services;

/// <summary>
/// 매크로 JSON 파일의 저장/불러오기/목록 관리 서비스.
/// 매크로 파일은 %AppData%/LARS/Macros/ 디렉토리에 저장됩니다.
/// </summary>
public class MacroStorageService
{
    private static readonly string MacroDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LARS", "Macros");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 매크로를 JSON 파일로 저장합니다.
    /// </summary>
    public void Save(MacroDefinition macro, string? customPath = null)
    {
        macro.ModifiedAt = DateTime.Now;

        string path = customPath ?? GetDefaultPath(macro.Name);
        string dir = Path.GetDirectoryName(path) ?? MacroDir;
        Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(macro, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// JSON 파일에서 매크로를 불러옵니다.
    /// </summary>
    public MacroDefinition? Load(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<MacroDefinition>(json, JsonOptions);
    }

    /// <summary>
    /// 저장된 매크로 파일 목록을 반환합니다.
    /// </summary>
    public List<string> ListSavedMacros()
    {
        if (!Directory.Exists(MacroDir)) return new List<string>();
        return Directory.GetFiles(MacroDir, "*.json")
                        .OrderByDescending(File.GetLastWriteTime)
                        .ToList();
    }

    /// <summary>
    /// 매크로 파일을 삭제합니다.
    /// </summary>
    public bool Delete(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        return true;
    }

    /// <summary>
    /// 매크로 이름으로 기본 저장 경로를 생성합니다.
    /// </summary>
    private string GetDefaultPath(string macroName)
    {
        // 파일명에 사용 불가한 문자 제거
        string safeName = string.Join("_", macroName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(MacroDir, $"{safeName}.json");
    }
}
