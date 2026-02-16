using System.IO;
using System.Text.Json;
using LARS.Models;

namespace LARS.Services;

/// <summary>
/// Feeder 관리 서비스. VBA BCA_PLIV_Feeder.bas를 대체합니다.
/// Feeder 설정을 JSON 파일로 저장/로드합니다.
/// </summary>
public class FeederService
{
    private readonly DirectoryManager _dirs;
    private const string FeederFileName = "feeders.json";

    public FeederService(DirectoryManager dirs)
    {
        _dirs = dirs;
    }

    private string FeederFilePath => Path.Combine(_dirs.Feeder, FeederFileName);

    /// <summary>
    /// Feeder 목록을 JSON에서 로드합니다.
    /// VBA SetUp_FeederTrackers에 대응합니다.
    /// </summary>
    public List<FeederUnit> LoadFeeders()
    {
        if (!File.Exists(FeederFilePath))
            return new List<FeederUnit>();

        var json = File.ReadAllText(FeederFilePath);
        return JsonSerializer.Deserialize<List<FeederUnit>>(json) ?? new List<FeederUnit>();
    }

    /// <summary>
    /// Feeder 목록을 JSON으로 저장합니다.
    /// VBA A_Save_Feeder에 대응합니다.
    /// </summary>
    public void SaveFeeders(List<FeederUnit> feeders)
    {
        var json = JsonSerializer.Serialize(feeders, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        Directory.CreateDirectory(_dirs.Feeder);
        File.WriteAllText(FeederFilePath, json);
    }

    /// <summary>
    /// 새 Feeder를 추가합니다.
    /// VBA A_New_Feeder에 대응합니다.
    /// </summary>
    public FeederUnit AddFeeder(string name, List<FeederUnit> feeders)
    {
        var newFeeder = new FeederUnit { Name = name };
        feeders.Add(newFeeder);
        SaveFeeders(feeders);
        return newFeeder;
    }

    /// <summary>
    /// Feeder를 삭제합니다.
    /// VBA A_Delete_Feeder에 대응합니다.
    /// </summary>
    public bool RemoveFeeder(string name, List<FeederUnit> feeders)
    {
        var found = feeders.FindIndex(f => f.Name == name);
        if (found < 0) return false;
        feeders.RemoveAt(found);
        SaveFeeders(feeders);
        return true;
    }

    /// <summary>
    /// Feeder에 아이템을 추가합니다.
    /// VBA C_Additem_List에 대응합니다.
    /// </summary>
    public bool AddItemToFeeder(string feederName, string item, List<FeederUnit> feeders)
    {
        var feeder = feeders.FirstOrDefault(f => f.Name == feederName);
        if (feeder == null) return false;
        if (feeder.Items.Contains(item)) return false;
        feeder.Items.Add(item);
        SaveFeeders(feeders);
        return true;
    }

    /// <summary>
    /// Feeder에서 아이템을 제거합니다.
    /// VBA C_Removeitem_List에 대응합니다.
    /// </summary>
    public bool RemoveItemFromFeeder(string feederName, string item, List<FeederUnit> feeders)
    {
        var feeder = feeders.FirstOrDefault(f => f.Name == feederName);
        if (feeder == null) return false;
        if (!feeder.Items.Remove(item)) return false;
        SaveFeeders(feeders);
        return true;
    }
}
