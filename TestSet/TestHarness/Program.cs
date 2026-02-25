using System.IO;
using System.Text.Json;
using LARS.Services;

namespace LARS.TestHarness;

/// <summary>
/// Headless 테스트 하네스. UI 없이 Service/ViewModel 로직을 직접 실행·검증합니다.
/// AI 에이전트가 'dotnet run -- [command] [args]' 형태로 호출합니다.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var excel = new ExcelReaderService();
        var command = args[0].ToLower();

        try
        {
            switch (command)
            {
                case "bom-read":
                    return RunBomRead(excel, args);

                case "dp-read":
                    return RunDpRead(excel, args);

                case "dp-meta":
                    return RunDpMeta(args);

                case "pl-read":
                    return RunPlRead(excel, args);

                case "item-count":
                    return RunItemCount(excel, args);

                case "macro-run":
                    return await RunMacroAsync(excel, args);

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    // ==========================================
    // Commands
    // ==========================================

    static int RunBomRead(ExcelReaderService excel, string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: bom-read <file>"); return 1; }
        var dirs = new DirectoryManager();
        var svc = new BomReportService(excel, dirs);
        var result = svc.ReadBomFile(args[1]);
        OutputJson(new
        {
            command = "bom-read",
            file = args[1],
            success = result.IsSuccess,
            error = result.ErrorMessage,
            headerCount = result.Headers.Count,
            rowCount = result.Rows.Count,
            headers = result.Headers,
            sampleRows = result.Rows.Take(5).ToList()
        });
        return result.IsSuccess ? 0 : 2;
    }

    static int RunDpRead(ExcelReaderService excel, string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: dp-read <file>"); return 1; }
        var dirs = new DirectoryManager();
        var svc = new DailyPlanService(excel, dirs);
        var result = svc.ReadDailyPlanFile(args[1]);
        OutputJson(new
        {
            command = "dp-read",
            file = args[1],
            success = result.IsSuccess,
            error = result.ErrorMessage,
            headerCount = result.Headers.Count,
            rowCount = result.Rows.Count,
            headers = result.Headers,
            sampleRows = result.Rows.Take(5).ToList()
        });
        return result.IsSuccess ? 0 : 2;
    }

    static int RunDpMeta(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: dp-meta <file>"); return 1; }
        var excel = new ExcelReaderService();
        var dirs = new DirectoryManager();
        var svc = new DailyPlanService(excel, dirs);
        var meta = svc.ReadMetaFromFile(args[1]);
        OutputJson(new
        {
            command = "dp-meta",
            file = args[1],
            isValid = meta.IsValid,
            month = meta.Month,
            day = meta.Day,
            line = meta.Line,
            dateLabel = meta.DateLabel,
            scheduleCount = meta.Schedules.Count,
            schedules = meta.Schedules.Select(s => new { s.Day, s.LotCount }).ToList()
        });
        return meta.IsValid ? 0 : 2;
    }

    static int RunPlRead(ExcelReaderService excel, string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: pl-read <file>"); return 1; }
        var dirs = new DirectoryManager();
        var svc = new PartListService(excel, dirs);
        var result = svc.ReadPartListFile(args[1]);
        OutputJson(new
        {
            command = "pl-read",
            file = args[1],
            success = result.IsSuccess,
            error = result.ErrorMessage,
            headerCount = result.Headers.Count,
            rowCount = result.Rows.Count,
            headers = result.Headers,
            sampleRows = result.Rows.Take(5).ToList()
        });
        return result.IsSuccess ? 0 : 2;
    }

    static int RunItemCount(ExcelReaderService excel, string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: item-count <partlist-file>"); return 1; }
        var dirs = new DirectoryManager();
        var plSvc = new PartListService(excel, dirs);
        var plData = plSvc.ReadPartListFile(args[1]);
        if (!plData.IsSuccess)
        {
            Console.Error.WriteLine($"Failed to read PartList: {plData.ErrorMessage}");
            return 2;
        }

        var icSvc = new ItemCounterService(excel);
        var result = icSvc.RunPipeline(plData);
        OutputJson(new
        {
            command = "item-count",
            file = args[1],
            success = result.IsSuccess,
            error = result.ErrorMessage,
            totalBeforeMerge = result.TotalItemsBeforeMerge,
            mergedCount = result.MergedGroup?.UnitCount ?? 0
        });
        return result.IsSuccess ? 0 : 2;
    }

    static async Task<int> RunMacroAsync(ExcelReaderService excel, string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: macro-run <macro-json-file> <input-excel>");
            return 1;
        }

        var storageSvc = new MacroStorageService();
        var runner = new MacroRunner();

        // 매크로 JSON 파일에서 MacroDefinition 로드
        string macroJson = await File.ReadAllTextAsync(args[1]);
        var macro = JsonSerializer.Deserialize<LARS.Models.Macro.MacroDefinition>(macroJson, _json);
        if (macro == null)
        {
            Console.Error.WriteLine("Failed to parse macro JSON");
            return 2;
        }

        var result = await runner.RunAsync(macro, args[2]);
        OutputJson(new
        {
            command = "macro-run",
            macroFile = args[1],
            inputFile = args[2],
            rowCount = result.Rows.Count,
            colCount = result.Columns.Count,
            sampleHeaders = Enumerable.Range(0, Math.Min(result.Columns.Count, 20))
                .Select(i => result.Columns[i].ColumnName).ToList(),
            sampleRowCount = Math.Min(result.Rows.Count, 5)
        });
        return 0;
    }

    // ==========================================
    // Helpers
    // ==========================================

    static void OutputJson(object data)
    {
        Console.WriteLine(JsonSerializer.Serialize(data, _json));
    }

    static void PrintUsage()
    {
        Console.WriteLine("""
            LARS TestHarness — Headless ViewModel/Service 테스트

            Usage: dotnet run -- <command> [args...]

            Commands:
              bom-read    <file>                BOM 파일 읽기 → JSON
              dp-read     <file>                DailyPlan 파일 읽기 → JSON
              dp-meta     <file>                DailyPlan 메타데이터 추출 → JSON
              pl-read     <file>                PartList 파일 읽기 → JSON
              item-count  <partlist-file>       ItemCounter 파이프라인 실행 → JSON
              macro-run   <macro-json> <input>  매크로 실행 → JSON
            """);
    }
}
