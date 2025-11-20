///<summary> MenuItem.cs (메뉴 항목 정의) </summary>
public struct MenuItem
{
    // 메뉴에 표시될 기능의 제목
    public string Title { get; }

    // 메뉴 항목 선택 시 실행될 비동기 Action
    public Func<Task> Action { get; }

    public MenuItem(string title, Func<Task> action)
    {
        Title = title;
        Action = action;
    }
}