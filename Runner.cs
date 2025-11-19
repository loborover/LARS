using System;
using System.Threading.Tasks;
using UI.Texts;          // 방금 만든 TextUI를 가져옴

namespace LARS;          // 프로그램 루트 네임스페이스

public class Runner
{
    public static void Main(string[] args)
    {
        // 여기서 TextUI 메뉴 실행
        TextUI.ShowMainMenuAsync(args).GetAwaiter().GetResult();
    }
}
