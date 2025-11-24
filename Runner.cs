using LARS.UI.CLI;

namespace LARS; // 프로그램 루트 네임스페이스

public class Runner
{
    public static void Main(string[] args)
    {
        // 업데이트 체크
        // 초기화 진행
        // 메인메뉴 로드
        TextUI.ShowMainMenuAsync(args).GetAwaiter().GetResult();

    }
}
