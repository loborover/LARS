using System.IO;

namespace LARS.Services;

/// <summary>
/// 인쇄 서비스 (스텁). VBA Printer.bas를 대체합니다.
/// WPF PrintDialog를 활용한 인쇄 기능의 기반을 제공합니다.
/// </summary>
public class PrintService
{
    /// <summary>
    /// PDF로 저장합니다.
    /// </summary>
    public bool SaveToPdf(string outputPath, string title, byte[] content)
    {
        try
        {
            File.WriteAllBytes(outputPath, content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 인쇄 대화상자를 표시합니다 (UI 스레드에서 호출).
    /// </summary>
    public bool ShowPrintDialog()
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        return dialog.ShowDialog() == true;
    }
}
