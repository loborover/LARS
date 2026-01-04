using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LARS.ENGINE.Utilities;

public enum DirectionSide { Left, Right }
public enum LabelShape { Round, Box_Hexagon }

public class Painter
{
    // Settings equivalent to VBA properties
    public float LineWeight { get; set; } = 2.0f;
    public Color ForeColor { get; set; } = Color.Black;
    public Color FillColor { get; set; } = Color.White; // Filled shapes usually white inside? VBA said Black ForeColor... check VBA
    // VBA: .Fill.ForeColor.RGB = RGB(0,0,0) (Black Fill?)
    // Wait, VBA DrawOval: Fill.ForeColor = RGB(0,0,0) -> Black Circle?
    // And Line.ForeColor = RGB(255,255,255) -> White Line?
    // Let's stick to standard black lines for now unless specified.
    
    public Painter()
    {
    }

    /// <summary>
    /// Draws an Oval (Circle) at the specified center coordinate.
    /// </summary>
    public void DrawOval(Graphics g, float x, float y, float size)
    {
        // VBA DrawOval: Shapes.AddShape(msoShapeOval, X, Y, Size, Size)
        // VBA Pivot calculations centered it.
        float radius = size / 2;
        RectangleF rect = new RectangleF(x - radius, y - radius, size, size);

        using (var brush = new SolidBrush(Color.Black)) // VBA used Black Fill
        using (var pen = new Pen(Color.White, 1))      // VBA used White Line
        {
            g.FillEllipse(brush, rect);
            g.DrawEllipse(pen, rect);
        }
    }

    /// <summary>
    /// Draws the complex "OvalBridge" structure connecting two points.
    /// Used for grouping visualization.
    /// </summary>
    public void DrawOvalBridge(Graphics g, PointF startPt, PointF endPt, 
                               string label, DirectionSide side)
    {
        // Logic ported from VBA OvalBridge
        // 1. Draw Start and End Ovals
        // 2. Draw Connecting Lines (The "Bracket" shape)
        // 3. Draw Label
        
        float ovalSize = 5.0f;
        float lineLength = 10.0f; // Horizontal extension

        // Draw Anchors (Ovals)
        DrawOval(g, startPt.X, startPt.Y, ovalSize);
        DrawOval(g, endPt.X, endPt.Y, ovalSize);

        // Calculate Bracket Points
        // If Side is Right, we extend to the Right.
        float directionFactor = (side == DirectionSide.Right) ? 1.0f : -1.0f;
        float extensionX = startPt.X + (lineLength * directionFactor); 
        // Note: VBA Logic implies extension is calculated relative to Pivot.
        
        // Let's create proper bracket path
        // Start -> Extension -> Vertical -> Extension -> End
        
        // Adjust for uniformity if start/end X are not aligned (skewed bracket)
        // Usually StartX and EndX are aligned if they are in same column.
        
        PointF p1 = startPt;
        PointF p2 = new PointF(p1.X + (lineLength * directionFactor), p1.Y);
        
        PointF p4 = endPt;
        PointF p3 = new PointF(p4.X + (lineLength * directionFactor), p4.Y);
        
        // Usually p2.X and p3.X should be same if p1.X and p4.X are same.
        // If not, we take max/min? Let's assume straight vertical bridge for now.
        float bridgeX = p2.X; 
        
        using (var pen = new Pen(ForeColor, LineWeight))
        {
            // Horizontal 1
            g.DrawLine(pen, p1, p2);
            // Vertical
            g.DrawLine(pen, p2, p3);
            // Horizontal 2
            g.DrawLine(pen, p3, p4);
        }

        // Draw Label
        if (!string.IsNullOrEmpty(label))
        {
            // Label Position: Middle of the vertical bridge
            PointF midPoint = new PointF(bridgeX, (p1.Y + p4.Y) / 2);
            DrawLabel(g, midPoint, label, side);
        }
    }

    private void DrawLabel(Graphics g, PointF pos, string text, DirectionSide side)
    {
        // Simple Label Box for now
        // Determine alignment
        // If Side Right, Label to the Right of Bridge?
        // VBA Logic: Label is attached to the bridge.
        
        float labelPadding = 2f;
        using (var font = new Font("Arial", 8))
        using (var brush = new SolidBrush(Color.Black))
        using (var bgBrush = new SolidBrush(Color.White)) // Label background
        {
            SizeF size = g.MeasureString(text, font);
            
            float x = pos.X;
            if (side == DirectionSide.Right) x += 2; // Offset slightly right
            else x -= (size.Width + 2); // Offset left
            
            float y = pos.Y - (size.Height / 2);
            
            RectangleF rect = new RectangleF(x, y, size.Width, size.Height);
            
            // Draw background to clear lines behind it if any (or just nice look)
            g.FillRectangle(bgBrush, rect);
            g.DrawString(text, font, brush, rect.Location);
             
            // Border for label?
            using(var p = new Pen(Color.Black, 1))
                g.DrawRectangle(p, Rectangle.Round(rect));
        }
    }
}
