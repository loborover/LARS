using LARS.ENGINE.Documents.BOM;
using LARS.ENGINE.Documents.DailyPlan;

namespace LARS;

public static class TestRunner
{
    public static void Run()
    {
        Console.WriteLine("[Verification] Starting...");

        string rawFilesDir = @"d:\Workshop\LARS\TestSet\RawFiles";
        string outputDir = @"d:\Workshop\LARS\TestSet\VerificationOutput";
        
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        // 1. Verify BOM
        Console.WriteLine("[Verification] Testing BOM Processor...");
        try
        {
            var bomFiles = Directory.GetFiles(rawFilesDir, "*@CVZ*.xlsx");
            if (bomFiles.Any())
            {
                var bomFile = bomFiles.First();
                var processor = new BOMProcessor();
                string outputPath = Path.Combine(outputDir, "BOM_Verified.xlsx");
                
                processor.ProcessSingle(bomFile, outputPath);
                Console.WriteLine($"[Success] BOM processed: {outputPath}");
            }
            else
            {
                Console.WriteLine("[Warning] No BOM file found in RawFiles.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] BOM Verification Failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        // 2. Verify DailyPlan
        Console.WriteLine("[Verification] Testing DailyPlan Processor...");
        try
        {
            var dpFiles = Directory.GetFiles(rawFilesDir, "Excel_Export_*.xlsx");
            if (dpFiles.Any())
            {
                var dpFile = dpFiles.First();
                var processor = new DailyPlanProcessor();
                string outputPath = Path.Combine(outputDir, "DailyPlan_Verified.xlsx");
                
                processor.ProcessSingle(dpFile, outputPath);
                Console.WriteLine($"[Success] DailyPlan processed: {outputPath}");
            }
            else
            {
                Console.WriteLine("[Warning] No DailyPlan file found in RawFiles.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] DailyPlan Verification Failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("[Verification] Finished.");
    }
}
