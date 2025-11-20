using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ENGINE; // (가정: 기존 코드에 있던 네임스페이스)
///<summary> SubMenu.cs (실제 메뉴 클래스) </summary>
namespace UI.Texts
{
    // MenuTemplate을 상속받아 구현
    public class SubMenu : MenuTemplate
    {
        // 1. 메뉴 제목 정의
        protected override string MenuTitle => "서브 메뉴";

        // 2. 메뉴 항목 정의
        protected override List<MenuItem> MenuItems => new List<MenuItem>
        {
            // MenuItem(제목, 실행할 비동기 Action)
            new MenuItem("서브 기능 1 실행", async () => 
            {
                // 여기에 서브 기능 1의 실제 로직을 구현합니다.
                Console.WriteLine("    [LOG] 서브 기능 1이 실행되었습니다.");
                // 예: await Engine.RunSubFunction1();
                await Task.Delay(100); 
            }),
            new MenuItem("서브 기능 2 실행", async () => 
            {
                // 여기에 서브 기능 2의 실제 로직을 구현합니다.
                Console.WriteLine("    [LOG] 서브 기능 2가 실행되었습니다.");
                // 예: await Engine.RunSubFunction2();
                await Task.Delay(200); 
            })
            // 필요한 만큼 항목 추가 가능
        };
        
        // 이 메서드는 MenuTemplate의 ShowMenuAsync를 호출하는 래퍼 역할만 합니다.
        // 또는 그냥 MenuTemplate을 인스턴스화하여 ShowMenuAsync를 바로 호출할 수도 있습니다.
        public static async Task ShowSubMenuAsync()
        {
            var subMenu = new SubMenu();
            await subMenu.ShowMenuAsync();
        }
    }
}