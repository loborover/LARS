// MenuTemplate.cs (메뉴 로직 템플릿)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract class MenuTemplate
{
    // 하위 클래스에서 정의해야 할 메뉴 제목
    protected abstract string MenuTitle { get; }

    // 하위 클래스에서 정의해야 할 메뉴 항목 목록
    protected abstract List<MenuItem> MenuItems { get; }

    public async Task ShowMenuAsync()
    {
        while (true)
        {
            // 1. 메뉴 출력
            DisplayMenu();

            // 2. 사용자 입력 처리
            var input = Console.ReadKey(true).KeyChar;
            Console.WriteLine();
            
            // '0'은 메인 메뉴로 돌아가기 (탈출)
            if (input == '0')
                break;

            // 3. 기능 실행
            if (int.TryParse(input.ToString(), out int selection) && selection >= 1 && selection <= MenuItems.Count)
            {
                // 선택된 MenuItem의 Action 실행
                await ExecuteMenuItemAsync(selection - 1);
            }
            else
            {
                Console.WriteLine("잘못된 선택입니다.");
            }
            
            await Task.Delay(200); // 잠시 대기
            Console.WriteLine();
        }
    }

    private void DisplayMenu()
    {
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"**{MenuTitle}**"); // MenuTitle 사용
        Console.WriteLine(new string('-', 50));

        // MenuItems 목록을 순회하며 출력
        for (int i = 0; i < MenuItems.Count; i++)
        {
            // 순번 + 기능 제목 출력
            Console.WriteLine($"{i + 1}. {MenuItems[i].Title}"); 
        }

        Console.WriteLine("0. 메인 메뉴로 돌아가기");
        Console.Write("선택: ");
    }
    
    private async Task ExecuteMenuItemAsync(int index)
    {
        var menuItem = MenuItems[index];
        Console.WriteLine($"**{menuItem.Title}** 실행 중...");
        await menuItem.Action(); // 정의된 Action 실행
        Console.WriteLine($"**{menuItem.Title}** 실행 완료.");
    }
}