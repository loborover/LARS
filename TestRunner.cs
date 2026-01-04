using LARS.ENGINE.Documents.BOM;
using LARS.ENGINE.Documents.DailyPlan;

namespace LARS;

public static class TestRunner
{
    public static void Run()
    {
        string rawFilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestSet", "RawFiles");
        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestSet", "VerificationOutput");
        string logFile = Path.Combine(rawFilesDir, "..", "VerificationReport.txt");

        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        
        // --- TEMP: Dump VBA File ---
        try {
            string vbaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "VBA", "Modules", "TimeKeeper.bas");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string content = File.ReadAllText(vbaPath, System.Text.Encoding.GetEncoding(949));
            Console.WriteLine("--- VBA CONTENT START ---");
            Console.WriteLine(content);
            Console.WriteLine("--- VBA CONTENT END ---");
            File.WriteAllText("vba_dump_utf8.txt", content);
        } catch(Exception ex) { Console.WriteLine($"VBA Read Error: {ex}"); }
        // ---------------------------

        using (StreamWriter writer = new StreamWriter(logFile))
        {
            void Log(string msg) { Console.WriteLine(msg); writer.WriteLine(msg); }

            Log($"[Verification] Starting at {DateTime.Now}...");
            Log($"Raw Files Dir: {rawFilesDir}");

            // 1. Verify BOM
            Log("[Verification] Testing BOM Processor...");
            try
            {
                var bomFiles = Directory.GetFiles(rawFilesDir, "*@CVZ*.xlsx");
                if (bomFiles.Any())
                {
                    var bomFile = bomFiles.First();
                    var processor = new BOMProcessor();
                    string outputPath = Path.Combine(outputDir, "BOM_Verified.xlsx");
                    
                    processor.ProcessSingle(bomFile, outputPath);
                    Log($"[Success] BOM processed: {outputPath}");
                }
                else
                {
                    Log("[Warning] No BOM file found in RawFiles.");
                }
            }
            catch (Exception ex)
            {
                Log($"[Error] BOM Verification Failed: {ex.Message}");
                Log(ex.StackTrace);
            }

            // 2. Verify DailyPlan
            Log("[Verification] Testing DailyPlan Processor...");
            try
            {
                var dpFiles = Directory.GetFiles(rawFilesDir, "Excel_Export_*.xlsx");
                if (dpFiles.Any())
                {
                    var dpFile = dpFiles.First();
                    var processor = new DailyPlanProcessor();
                    string outputPath = Path.Combine(outputDir, "DailyPlan_Verified.xlsx");
                    
                    processor.ProcessSingle(dpFile, outputPath);
                    Log($"[Success] DailyPlan processed: {outputPath}");
                }
                else
                {
                    Log("[Warning] No DailyPlan file found in RawFiles.");
                }
            }
            catch (Exception ex)
            {
                Log($"[Error] DailyPlan Verification Failed: {ex.Message}");
                Log(ex.StackTrace);
            }

            Log("[Verification] Finished.");
        }
    }
}
