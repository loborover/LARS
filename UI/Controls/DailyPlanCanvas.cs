using System;
using System.Drawing;
using System.Windows.Forms;
using LARS.ENGINE.Documents.DailyPlan;

namespace LARS.UI.Controls;

public class DailyPlanCanvas : UserControl
{
    private ProcessedDailyPlan? _plan;
    private DailyPlanDrawer? _drawer;

    public DailyPlanCanvas()
    {
        this.DoubleBuffered = true; // Reduce flicker
        this.BackColor = Color.White;
        this.AutoScroll = true;
    }

    public void LoadPlan(ProcessedDailyPlan plan)
    {
        _plan = plan;
        _drawer = new DailyPlanDrawer(plan);
        
        // Calculate Scroll Size (Approx)
        int totalHeight = 100 + (plan.Rows.Count * 25) + 100; // Header + Data + Padding
        int totalWidth = 1000; // Need to sum columns really
        if (plan.ColumnWidths.Count > 0)
            totalWidth = (int)plan.ColumnWidths.Sum() + 200; // + Grouping Area
            
        this.AutoScrollMinSize = new Size(totalWidth, totalHeight);
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        if (_drawer != null)
        {
            // Handle Scrolling
            e.Graphics.TranslateTransform(this.AutoScrollPosition.X, this.AutoScrollPosition.Y);
            _drawer.Draw(e.Graphics);
        }
        else
        {
            e.Graphics.DrawString("No Data Loaded", this.Font, Brushes.Gray, 10, 10);
        }
    }
}
