using System.IO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;

namespace LARS.Services;

/// <summary>
/// PDF 내보내기 서비스. VBA Print_BOM/Print_DailyPlan의 PDF 저장을 대체합니다.
/// PdfSharpCore(MIT)를 사용합니다.
/// </summary>
public class PdfExportService
{
    /// <summary>
    /// 테이블 데이터를 PDF로 내보냅니다.
    /// </summary>
    public bool ExportTableToPdf(string outputPath, string title,
        List<string> headers, List<List<string>> rows,
        bool isLandscape = false)
    {
        try
        {
            var document = new PdfDocument();
            document.Info.Title = title;
            document.Info.Author = "LARS";

            var page = document.AddPage();
            page.Orientation = isLandscape
                ? PdfSharpCore.PageOrientation.Landscape
                : PdfSharpCore.PageOrientation.Portrait;

            var gfx = XGraphics.FromPdfPage(page);
            var titleFont = new XFont("Malgun Gothic", 16, XFontStyle.Bold);
            var headerFont = new XFont("Malgun Gothic", 9, XFontStyle.Bold);
            var bodyFont = new XFont("Malgun Gothic", 8, XFontStyle.Regular);
            var footerFont = new XFont("Malgun Gothic", 7, XFontStyle.Italic);

            double margin = 40;
            double y = margin;
            double pageWidth = page.Width.Point - (margin * 2);

            // 타이틀
            gfx.DrawString(title, titleFont, XBrushes.DarkBlue,
                new XRect(margin, y, pageWidth, 30), XStringFormats.TopLeft);
            y += 35;

            // 날짜
            gfx.DrawString($"생성일: {DateTime.Now:yyyy-MM-dd HH:mm}", footerFont, XBrushes.Gray,
                new XRect(margin, y, pageWidth, 15), XStringFormats.TopLeft);
            y += 20;

            // 테이블
            int colCount = Math.Min(headers.Count, 10); // 최대 10열
            double colWidth = pageWidth / Math.Max(colCount, 1);
            double rowHeight = 18;

            // 헤더 행
            var headerBrush = new XSolidBrush(XColor.FromArgb(34, 42, 55));
            gfx.DrawRectangle(headerBrush, margin, y, pageWidth, rowHeight);
            for (int c = 0; c < colCount; c++)
            {
                string hText = c < headers.Count ? headers[c] : "";
                gfx.DrawString(hText, headerFont, XBrushes.White,
                    new XRect(margin + c * colWidth + 3, y + 2, colWidth - 6, rowHeight - 4),
                    XStringFormats.TopLeft);
            }
            y += rowHeight;

            // 데이터 행
            var altBrush = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var linePen = new XPen(XColor.FromArgb(220, 220, 225), 0.5);
            int maxRowsPerPage = (int)((page.Height.Point - y - margin) / rowHeight);

            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0 && r % maxRowsPerPage == 0)
                {
                    // 새 페이지
                    page = document.AddPage();
                    page.Orientation = isLandscape
                        ? PdfSharpCore.PageOrientation.Landscape
                        : PdfSharpCore.PageOrientation.Portrait;
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }

                if (r % 2 == 1)
                    gfx.DrawRectangle(altBrush, margin, y, pageWidth, rowHeight);

                gfx.DrawLine(linePen, margin, y + rowHeight, margin + pageWidth, y + rowHeight);

                for (int c = 0; c < colCount; c++)
                {
                    string cellText = c < rows[r].Count ? rows[r][c] : "";
                    // 너무 긴 텍스트 자르기
                    if (cellText.Length > 30) cellText = cellText[..27] + "...";
                    gfx.DrawString(cellText, bodyFont, XBrushes.Black,
                        new XRect(margin + c * colWidth + 3, y + 2, colWidth - 6, rowHeight - 4),
                        XStringFormats.TopLeft);
                }
                y += rowHeight;
            }

            // 푸터
            double footerY = page.Height.Point - margin + 5;
            gfx.DrawString($"LARS — {rows.Count}행 출력", footerFont, XBrushes.Gray,
                new XRect(margin, footerY, pageWidth, 15), XStringFormats.TopLeft);
            gfx.DrawString($"Page {document.PageCount}", footerFont, XBrushes.Gray,
                new XRect(margin, footerY, pageWidth, 15), XStringFormats.TopRight);

            document.Save(outputPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
