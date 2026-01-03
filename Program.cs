using LARS.Forms;

namespace LARS;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    ///  (애플리케이션의 주 진입점입니다.)
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
