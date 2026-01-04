using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using LARS.ENGINE.Documents.DailyPlan;

namespace LARS.ENGINE.Documents.DailyPlan;

public class DailyPlanPdfExporter
{
    private ProcessedDailyPlan _plan;

    public DailyPlanPdfExporter(ProcessedDailyPlan plan)
    {
        _plan = plan;
    }

    /// <summary>
    /// Opens the Print Preview dialog to allow user to print to PDF or Physical Printer.
    /// Uses GDI+ Painter logic.
    /// </summary>
    public void Print(string docName = "DailyPlan")
    {
        PrintDocument pd = new PrintDocument();
        pd.DocumentName = docName;
        pd.PrintPage += Pdf_PrintPage;
        
        // Page Settings implementation (Landscape, Margins)
        // VBA Printer.bas says: Top 0.3, Bottom 0.3, Left 0, Right 0 (Inches approx)
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new Margins(20, 20, 30, 30); // 100 = 1 inch

        PrintPreviewDialog ppd = new PrintPreviewDialog();
        ppd.Document = pd;
        ppd.ShowDialog();
    }

    private void Pdf_PrintPage(object sender, PrintPageEventArgs e)
    {
        DailyPlanDrawer drawer = new DailyPlanDrawer(_plan);
        // We might need scaling if content is too wide.
        // For now, draw 1:1.
        drawer.Draw(e.Graphics);
    }
}
