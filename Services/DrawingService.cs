using System.IO;
using System.Text.Json;
using LARS.Models.Drawing;

namespace LARS.Services;

/// <summary>
/// Drawing Engine 서비스. Snap 동기화, 템플릿 저장/불러오기를 담당합니다.
/// </summary>
public class DrawingService
{
    private readonly string _templateDir;

    public DrawingService()
    {
        _templateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LARS", "Templates");
        Directory.CreateDirectory(_templateDir);
    }

    // ==========================================
    // Snap 동기화
    // ==========================================

    /// <summary>
    /// 같은 SnapGroupId를 가진 모든 Dot의 위치를 동기화합니다.
    /// 기준 Dot의 위치로 나머지를 이동시킵니다.
    /// </summary>
    public void SyncSnapGroup(ViewportModel viewport, DotShape sourceDot)
    {
        if (string.IsNullOrEmpty(sourceDot.SnapGroupId)) return;

        var groupDots = viewport.Shapes
            .OfType<DotShape>()
            .Where(d => d.SnapGroupId == sourceDot.SnapGroupId && d.Id != sourceDot.Id)
            .ToList();

        foreach (var dot in groupDots)
        {
            dot.X = sourceDot.X;
            dot.Y = sourceDot.Y;
        }
    }

    /// <summary>
    /// 두 Dot을 같은 Snap 그룹으로 연결합니다.
    /// </summary>
    public void CreateSnap(DotShape dotA, DotShape dotB)
    {
        string groupId = dotA.SnapGroupId ?? dotB.SnapGroupId ?? Guid.NewGuid().ToString("N")[..8];
        dotA.SnapGroupId = groupId;
        dotB.SnapGroupId = groupId;
        // B를 A 위치로 이동
        dotB.X = dotA.X;
        dotB.Y = dotA.Y;
    }

    /// <summary>
    /// Dot의 Snap 연결을 해제합니다.
    /// </summary>
    public void RemoveSnap(DotShape dot)
    {
        dot.SnapGroupId = null;
    }

    // ==========================================
    // 템플릿 저장/불러오기
    // ==========================================

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// CompositeShape를 JSON 템플릿으로 저장합니다.
    /// </summary>
    public void SaveTemplate(CompositeShape composite)
    {
        string fileName = $"{composite.Name}.json";
        string filePath = Path.Combine(_templateDir, fileName);
        string json = JsonSerializer.Serialize(composite, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// JSON 템플릿에서 CompositeShape를 불러옵니다.
    /// </summary>
    public CompositeShape? LoadTemplate(string templateName)
    {
        string filePath = Path.Combine(_templateDir, $"{templateName}.json");
        if (!File.Exists(filePath)) return null;

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<CompositeShape>(json, _jsonOptions);
    }

    /// <summary>
    /// 저장된 템플릿 목록을 반환합니다.
    /// </summary>
    public List<string> ListTemplates()
    {
        if (!Directory.Exists(_templateDir)) return new();
        return Directory.GetFiles(_templateDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();
    }
}
