using LARS.UI.CLI;

namespace LARS;          // 프로그램 루트 네임스페이스

public class Runner
{
    public static void Main(string[] args)
    {
        // 여기서 TextUI 메뉴 실행
        TextUI.ShowMainMenuAsync(args).GetAwaiter().GetResult();
    }
}
