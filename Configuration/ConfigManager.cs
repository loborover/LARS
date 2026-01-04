using System;
using System.IO;
using System.Text.Json;

namespace LARS.Configuration;

public class AppSettings
{
    // General Paths
    public string GlobalImportPath { get; set; } = @"C:\LARS\Import";
    public string GlobalExportPath { get; set; } = @"C:\LARS\Export";

    // Viewer Specific Sub-folders
    public string BomExportDir { get; set; } = "BOM";
    public string PartListExportDir { get; set; } = "PartList";
    public string DailyPlanExportDir { get; set; } = "DailyPlan";

    // Debug Mode
    public bool IsDebugMode { get; set; } = false;
    public string DebugImportPath { get; set; } = @"E:\AAA_WorkShop\LARS\TestSet\DebugImport";
    public string DebugExportPath { get; set; } = @"E:\AAA_WorkShop\LARS\TestSet\DebugExport";
}

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    public static AppSettings Instance { get; private set; } = new AppSettings();

    static ConfigManager()
    {
        Load();
    }

    public static void Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) Instance = settings;
            }
            catch { /* Ignore load errors, use default */ }
        }
        else
        {
            Save(); // Create default if missing
        }
    }

    public static void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Instance, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* Handle save errors */ }
    }

    // Helper to get actual path based on mode
    public static string GetImportPath()
    {
        return Instance.IsDebugMode ? Instance.DebugImportPath : Instance.GlobalImportPath;
    }

    public static string GetExportPath(string viewerSubDir)
    {
        var root = Instance.IsDebugMode ? Instance.DebugExportPath : Instance.GlobalExportPath;
        var fullPath = Path.Combine(root, viewerSubDir);
        
        // Ensure directory exists
        if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        
        return fullPath;
    }
}
