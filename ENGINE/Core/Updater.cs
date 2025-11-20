using System;
using System.Threading.Tasks;

namespace ENGINE;

/// <summary>
/// LARS 코어 업데이트 흐름을 담당하는 클래스
/// (지금은 "버전 비교 + 안내"까지만, 나중에 실제 업데이트 코드 붙임)
/// </summary>
public class Updater
{
    private readonly VersionService _versionService;

    public Updater(VersionService versionService)
    {
        _versionService = versionService;
    }

    /// <summary>
    /// 버전을 비교하고, 업데이트 필요 여부를 출력합니다.
    /// 나중에는 여기서 실제 다운로드/교체 로직을 추가할 예정.
    /// </summary>
    public async Task CheckAndUpdateAsync(bool interactive = true)
    {
        var local = _versionService.GetLocalVersion();
        var remote = await _versionService.GetRemoteVersionAsync();

        Console.WriteLine($"현재 버전 : {local}");

        if (remote is null)
        {
            Console.WriteLine("서버 최신 버전 정보를 가져오지 못했습니다.");
            return;
        }

        Console.WriteLine($"서버 최신 버전 : {remote}");

        if (remote > local)
        {
            Console.WriteLine();
            Console.WriteLine("※ 새 버전이 있습니다!");

            if (interactive)
            {
                Console.Write("지금 업데이트를 진행하시겠습니까? (Y/N): ");
                var key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.Y)
                {
                    // TODO: 여기서 실제 업데이트(다운로드, 압축 해제, 파일 교체 등) 구현
                    Console.WriteLine("업데이트 기능은 아직 구현 전입니다. (TODO)");
                }
                else
                {
                    Console.WriteLine("업데이트를 취소했습니다.");
                }
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("현재 버전이 최신입니다. 업데이트가 필요 없습니다.");
        }
    }
}