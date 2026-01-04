using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LARS.ENGINE.Utilities;

namespace LARS.ENGINE.Documents.DailyPlan;

public class ProcessedDailyPlan
{
    // Data Model for Visualization
    public string DateTitle { get; set; } = "";
    public string LineName { get; set; } = "";
    public List<string> Headers { get; set; } = new List<string>();
    public List<List<string>> Rows { get; set; } = new List<List<string>>();
    public List<Models.DailyPlanItem> RawItems { get; set; } // Or custom Item
    
    // Grouping Info
    public List<GroupRange> SubGroups { get; set; } = new();
    public List<GroupRange> MainGroups { get; set; } = new();
    
    // Column Widths for Layout
    public List<float> ColumnWidths { get; set; } = new();
}

public class DailyPlanDrawer
{
    private ProcessedDailyPlan _plan;
    private Painter _painter;
    
    // Layout Config
    private float _startX = 50;
    private float _startY = 50;
    private float _rowHeight = 20;
    
    public DailyPlanDrawer(ProcessedDailyPlan plan)
    {
        _plan = plan;
        _painter = new Painter();
        
        // Initialize default column widths if empty
        if (_plan.ColumnWidths.Count == 0)
        {
            // Approximate widths based on VBA analysis
            // 6.5, 13, 28, 6, 6, 6, 7.5, 6, 6, 6, 6, 6, 6.5
            // Convert to Pixels (approx * 7)
            _plan.ColumnWidths = new List<float> { 
                45, 90, 200, 42, 42, 42, 52, 42, 42, 42, 42, 42, 45 
            }; 
        }
    }

    public void Draw(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; // Or AntiAlias

        DrawHeader(g);
        DrawGrid(g);
        DrawData(g);
        DrawGroups(g);
    }

    private void DrawHeader(Graphics g)
    {
        // Title
        using (var font = new Font("Arial", 16, FontStyle.Bold))
        {
            g.DrawString($"Daily Plan: {_plan.DateTitle} ({_plan.LineName})", font, Brushes.Black, _startX, _startY - 40);
        }
        
        // Column Headers
        float x = _startX;
        float y = _startY;
        
        using (var font = new Font("Arial", 9, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.FromArgb(199, 253, 240))) // Header BG
        using (var pen = new Pen(Color.Black))
        {
            for (int i = 0; i < _plan.Headers.Count; i++)
            {
                float w = GetColWidth(i);
                RectangleF rect = new RectangleF(x, y, w, _rowHeight * 2); // 2 rows for header usually
                
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, Rectangle.Round(rect));
                
                // Centered Text
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_plan.Headers[i], font, Brushes.Black, rect, sf);
                
                x += w;
            }
        }
    }

    private void DrawGrid(Graphics g)
    {
        // Grid frames are drawn per cell in DrawData usually, or separate layer.
        // Let's do it in DrawData.
    }

    private void DrawData(Graphics g)
    {
        float y = _startY + (_rowHeight * 2); // Start after header
        
        using (var font = new Font("Arial", 9))
        using (var pen = new Pen(Color.Black))
        {
            for (int r = 0; r < _plan.Rows.Count; r++)
            {
                float x = _startX;
                var rowData = _plan.Rows[r];
                
                for (int c = 0; c < rowData.Count; c++)
                {
                    float w = GetColWidth(c);
                    RectangleF rect = new RectangleF(x, y, w, _rowHeight);
                    
                    g.DrawRectangle(pen, Rectangle.Round(rect));
                    
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(rowData[c], font, Brushes.Black, rect, sf);
                    
                    x += w;
                }
                y += _rowHeight;
            }
        }
    }

    private void DrawGroups(Graphics g)
    {
        // Draw Main/Sub Group Brackets relative to grid
        
        // 1. Determine X position for Brackets
        // Usually right side of the table? 
        // VBA used "OvalBridge" on specific columns? 
        // Analysis Report says: "Group right side with brackets".
        float tableWidth = _plan.ColumnWidths.Take(_plan.Headers.Count).Sum();
        float bracketStartX = _startX + tableWidth + 10; 
        
        float headerOffset = _startY + (_rowHeight * 2);

        // Sub Groups
        foreach (var grp in _plan.SubGroups)
        {
            // Calculate Y positions
            // StartRow/EndRow are 1-based indices relative to Data
            // If they are absolute Excel row numbers, we need to map them.
            // Let's assume ProcessedDailyPlan normalizes them to 0-based data index.
            
            float topY = headerOffset + (grp.StartRow * _rowHeight);
            float bottomY = headerOffset + ((grp.EndRow + 1) * _rowHeight); // End of the row
            float centerY = (topY + bottomY) / 2;
            
            PointF start = new PointF(bracketStartX, topY + 5); 
            PointF end = new PointF(bracketStartX, bottomY - 5);
            
            // Draw SubGroup Bracket (Left facing? No, Right side of table, so bracket should face table?)
            // VBA: Drawn ON the sheet.
            // Let's stick to Right Side of Table -> Bracket Faces Left (opens to table).
            // Or Faces Right (wrapping the content).
            // Visual preference: Open to LEFT ( ] shape ).
            
            _painter.DrawOvalBridge(g, start, end, grp.Info?.SpecNumber ?? "", DirectionSide.Right);
        }
    }
    
    private float GetColWidth(int index)
    {
        if (index < _plan.ColumnWidths.Count) return _plan.ColumnWidths[index];
        return 50; // Default
    }
}
