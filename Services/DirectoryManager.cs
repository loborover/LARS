using System.IO;

namespace LARS.Services;

/// <summary>
/// 디렉토리 관리 서비스.
/// VBA Z_Directory.bas를 대체합니다.
/// 앱 실행 경로 기반으로 하위 폴더 참조를 제공합니다.
/// </summary>
public class DirectoryManager
{
    private string _basePath = string.Empty;
    private string _sourcePath = string.Empty;

    /// <summary>
    /// 기본 경로(앱 디렉토리)를 설정합니다.
    /// </summary>
    public void Setup(string basePath, string sourcePath = "")
    {
        _basePath = basePath;
        _sourcePath = sourcePath;
        EnsureDirectories();
    }

    public string BasePath => _basePath;
    public string BOM => Path.Combine(_basePath, "BOM");
    public string DailyPlan => Path.Combine(_basePath, "DailyPlan");
    public string PartList => Path.Combine(_basePath, "PartList");
    public string Feeder => Path.Combine(_basePath, "Feeder");
    public string Backup => Path.Combine(_basePath, "Backup");
    public string Output => Path.Combine(_basePath, "Output");
    public string Source => string.IsNullOrEmpty(_sourcePath) ? _basePath : _sourcePath;

    public bool IsSetup => !string.IsNullOrEmpty(_basePath);

    /// <summary>
    /// 필요한 하위 디렉토리를 자동 생성합니다.
    /// </summary>
    private void EnsureDirectories()
    {
        Directory.CreateDirectory(BOM);
        Directory.CreateDirectory(DailyPlan);
        Directory.CreateDirectory(PartList);
        Directory.CreateDirectory(Feeder);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Output);
    }
}
