using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ENGINE.Core;
using ENGINE.Documents.DailyPlan;
using Microsoft.VisualBasic.FileIO;

namespace ENGINE;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== LARS (Logistics Automation and Reporting System) ===");
        Console.WriteLine();

        // 1) 버전/업데이트 서비스 준비
        var localVersionPath = "Version.txt"; // 실행 폴더 기준, 나중에 원하는 경로로 변경
        var remoteVersionUrl = null as string;
        // TODO: GitHub Raw / 사내 서버의 version.txt URL 로 교체

        var versionService = new VersionService(localVersionPath, remoteVersionUrl);
        var updater        = new Updater(versionService);

        // 2) DailyPlan 처리기 준비
        var dailyPlanSourceDir = @"E:\DailyPlan_Source"; // TODO: 실제 폴더로 바꾸기
        var dailyPlanProcessor = new DailyPlanProcessor(dailyPlanSourceDir);

        while (true)
        {
            Console.WriteLine(new string('-', 50)); // 구분선 50자
            Console.WriteLine("현재 버전 : " + versionService.GetLocalVersion());
            Console.WriteLine("DownloadPath  : " + Directories.DownloadPath);
            Console.WriteLine("DPPath  : " + Directories.DPPath);
            Console.WriteLine("BOMPath  : " + Directories.BOMPath);
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. 코어 업데이트 체크");
            Console.WriteLine("2. DailyPlan 전체 가공");
            Console.WriteLine("0. 종료");
            Console.Write("선택: ");

            var key = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            if (key.KeyChar == '0')
                break;

            switch (key.KeyChar)
            {
                case '1':
                    await updater.CheckAndUpdateAsync(interactive: true);
                    break;

                case '2':
                    dailyPlanProcessor.ProcessAll();
                    break;

                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }

            Console.WriteLine();
        }

        Console.WriteLine("프로그램을 종료합니다.");
    }
}
