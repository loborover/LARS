using System.Drawing;
using System.Drawing.Drawing2D;
using LARS.Models;

namespace LARS.Features.StickerLabel;

public class StickerRenderer
{
    // GDI+ Graphics 객체에 라벨을 그리는 메서드
    public void DrawSticker(Graphics g, RectangleF bounds, StickerData data)
    {
        // 안티앨리어싱 설정
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 배경 그리기 (모양에 따라 다름)
        using (var pen = new Pen(Color.Black, 2))
        using (var brush = new SolidBrush(Color.White)) // 혹은 투명
        {
            var path = GetShapePath(bounds, data.Shape, data.Direction);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        // 텍스트 그리기
        using (var font = new Font("Malgun Gothic", 10, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.Black))
        {
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(data.MainText, font, brush, bounds, format);
        }
    }

    private GraphicsPath GetShapePath(RectangleF bounds, LabelShape shape, LabelDirection dir)
    {
        var path = new GraphicsPath();
        float r = 10; // 둥근 모서리 반지름

        switch (shape)
        {
            case LabelShape.Round:
                path.AddArc(bounds.X, bounds.Y, r, r, 180, 90);
                path.AddArc(bounds.Right - r, bounds.Y, r, r, 270, 90);
                path.AddArc(bounds.Right - r, bounds.Bottom - r, r, r, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - r, r, r, 90, 90);
                path.CloseFigure();
                break;
            
            case LabelShape.Box:
            default:
                path.AddRectangle(bounds);
                break;
        }

        return path;
    }
}
