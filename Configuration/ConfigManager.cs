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

    // Printer
    public string DefaultPrinter { get; set; } = "";

    // Global Processing Options
    public bool SaveAsFile { get; set; } = true;
    public bool DirectPrint { get; set; } = false;
}

public class HeaderMapping
{
    public int Order { get; set; } = 0;          // Column Order (1, 2, 3...)
    public string Target { get; set; } = "";     // Original Header Name
    public string UserSet { get; set; } = "";   // Renamed Header Name (Display)
    public double Width { get; set; } = 15;     // Column Width in Excel units
}

public class ViewerHeaderConfig
{
    public List<HeaderMapping> Mappings { get; set; } = new();
    public int? TargetHeaderRow { get; set; } // Optional: Row index where Target Headers are located
}

public class HeaderSettings
{
    public ViewerHeaderConfig DailyPlan { get; set; } = new();
    public ViewerHeaderConfig Bom { get; set; } = new();
    public ViewerHeaderConfig PartList { get; set; } = new();

    static HeaderSettings()
    {
        // Default mappings (Migration of hardcoded values)
        // DailyPlan Defaults
        // Target: "W/O 계획수량", UserSet: "계획"
        // Target: "Line", UserSet: "Line"
        // Target: "부품번호", UserSet: "부품번호"
    }
}

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    private static readonly string HeaderConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "headers.json");
    
    public static AppSettings Instance { get; private set; } = new AppSettings();
    public static HeaderSettings Headers { get; private set; } = new HeaderSettings();

    static ConfigManager()
    {
        Load();
        LoadHeaders();
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

    public static void LoadHeaders()
    {
        if (File.Exists(HeaderConfigPath))
        {
            try
            {
                var json = File.ReadAllText(HeaderConfigPath);
                var settings = JsonSerializer.Deserialize<HeaderSettings>(json);
                if (settings != null) Headers = settings;
            }
            catch { /* Ignore load errors */ }
        }
        else
        {
            // Initialize Defaults for DailyPlan (Porting hardcoded values)
            Headers.DailyPlan.Mappings.Add(new HeaderMapping { Order = 1, Target = "Planned Start Time", UserSet = "투입시점" });
            Headers.DailyPlan.Mappings.Add(new HeaderMapping { Order = 2, Target = "W/O 계획수량", UserSet = "계획" });
            Headers.DailyPlan.Mappings.Add(new HeaderMapping { Order = 3, Target = "Line", UserSet = "Line" });
            Headers.DailyPlan.Mappings.Add(new HeaderMapping { Order = 4, Target = "부품번호", UserSet = "부품번호" });

            // Initialize Defaults for BOM
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 1, Target = "Lvl", UserSet = "Lv", Width = 40 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 2, Target = "Part No", UserSet = "Part Number", Width = 150 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 3, Target = "Description", UserSet = "Description", Width = 250 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 4, Target = "Qty", UserSet = "Qty", Width = 60 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 5, Target = "UOM", UserSet = "UOM", Width = 60 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 6, Target = "Maker", UserSet = "Maker", Width = 120 });
            Headers.Bom.Mappings.Add(new HeaderMapping { Order = 7, Target = "Supply Type", UserSet = "Supply Type", Width = 100 });
            
            SaveHeaders();
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

    public static void SaveHeaders()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Headers, options);
            File.WriteAllText(HeaderConfigPath, json);
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
