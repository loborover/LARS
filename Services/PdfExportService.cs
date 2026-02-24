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

    /// <summary>
    /// BOM 보고서 전용 PDF 내보내기.
    /// VBA AutoReport_BOM + PS_BOM 설정에 대응합니다.
    /// 열 너비 비율 [2.7, 20, 30, 3, 2.5, 16, 13] 적용, 타이틀/생성일 헤더 포함.
    /// </summary>
    public bool ExportBomToPdf(string outputPath, string title,
        List<string> headers, List<List<string>> rows)
    {
        // VBA Interior_Set_BOM colWidth: [2.7, 20, 30, 3, 2.5, 16, 13]
        // 이를 비율로 환산 (합계 88.2)
        var colRatios = new double[] { 2.7, 20.0, 30.0, 3.0, 2.5, 16.0, 13.0 };
        return ExportWithColumnRatios(outputPath, title, headers, rows,
            colRatios, isLandscape: false, printTitleInHeader: true);
    }

    /// <summary>
    /// DailyPlan 보고서 전용 PDF 내보내기.
    /// VBA PS_DPforPDF 설정에 대응합니다 (가로 방향, 여백 최소화).
    /// </summary>
    public bool ExportDailyPlanToPdf(string outputPath, string title,
        List<string> headers, List<List<string>> rows)
    {
        return ExportTableToPdf(outputPath, title, headers, rows, isLandscape: true);
    }

    /// <summary>
    /// 열 너비 비율(ratio)을 적용한 PDF 내보내기 공통 메서드.
    /// </summary>
    public bool ExportWithColumnRatios(string outputPath, string title,
        List<string> headers, List<List<string>> rows,
        double[] colRatios, bool isLandscape = false, bool printTitleInHeader = true)
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
            var titleFont  = new XFont("Malgun Gothic", 18, XFontStyle.Bold);
            var metaFont   = new XFont("Malgun Gothic", 8, XFontStyle.Regular);
            var headerFont = new XFont("Malgun Gothic", 9, XFontStyle.Bold);
            var bodyFont   = new XFont("Malgun Gothic", 8, XFontStyle.Regular);
            var footerFont = new XFont("Malgun Gothic", 7, XFontStyle.Italic);

            double margin    = 28;   // VBA: 약 1cm = ~28pt
            double pageWidth = page.Width.Point - margin * 2;
            double y         = margin;

            // ── 타이틀 (VBA AutoTitle 대응) ──
            if (printTitleInHeader && !string.IsNullOrWhiteSpace(title))
            {
                gfx.DrawString(title, titleFont, XBrushes.Black,
                    new XRect(margin, y, pageWidth, 28), XStringFormats.TopLeft);
                y += 30;

                // 생성 날짜 (VBA RightHeader: 출력일/출력시)
                string meta = $"출력일: {DateTime.Now:yyMMdd}  출력시: {DateTime.Now:HHmmss}";
                gfx.DrawString(meta, metaFont, XBrushes.Gray,
                    new XRect(margin, y, pageWidth, 14), XStringFormats.TopRight);
                y += 16;
            }

            // ── 열 너비 계산 (비율 기반) ──
            int colCount  = Math.Min(headers.Count, colRatios.Length);
            double ratioSum = colRatios.Take(colCount).Sum();
            double[] colWidths = colRatios.Take(colCount)
                .Select(r => pageWidth * r / ratioSum).ToArray();

            double rowHeight = 16;

            // ── 헤더 행 ──
            var headerBg = new XSolidBrush(XColor.FromArgb(34, 42, 55));
            double xPos = margin;
            gfx.DrawRectangle(headerBg, margin, y, pageWidth, rowHeight);
            for (int c = 0; c < colCount; c++)
            {
                string hText = c < headers.Count ? headers[c] : "";
                gfx.DrawString(hText, headerFont, XBrushes.White,
                    new XRect(xPos + 2, y + 2, colWidths[c] - 4, rowHeight - 4),
                    XStringFormats.TopLeft);
                xPos += colWidths[c];
            }
            y += rowHeight;

            // ── 데이터 행 ──
            var altBrush = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var linePen  = new XPen(XColor.FromArgb(210, 210, 215), 0.4);
            double pageContentHeight = page.Height.Point - margin * 2;
            int maxRowsPerPage = (int)((pageContentHeight - (y - margin)) / rowHeight);

            for (int r = 0; r < rows.Count; r++)
            {
                // 새 페이지 처리
                if (r > 0 && r % maxRowsPerPage == 0)
                {
                    // 하단 페이지 번호
                    gfx.DrawString($"Page {document.PageCount} / ...", footerFont, XBrushes.Gray,
                        new XRect(margin, page.Height.Point - margin, pageWidth, 12),
                        XStringFormats.TopRight);

                    page = document.AddPage();
                    page.Orientation = isLandscape
                        ? PdfSharpCore.PageOrientation.Landscape
                        : PdfSharpCore.PageOrientation.Portrait;
                    gfx = XGraphics.FromPdfPage(page);
                    y   = margin;

                    // 계속 헤더 반복 (VBA PrintTitleRows 대응)
                    xPos = margin;
                    gfx.DrawRectangle(headerBg, margin, y, pageWidth, rowHeight);
                    for (int c = 0; c < colCount; c++)
                    {
                        string hText = c < headers.Count ? headers[c] : "";
                        gfx.DrawString(hText, headerFont, XBrushes.White,
                            new XRect(xPos + 2, y + 2, colWidths[c] - 4, rowHeight - 4),
                            XStringFormats.TopLeft);
                        xPos += colWidths[c];
                    }
                    y += rowHeight;
                }

                if (r % 2 == 1)
                    gfx.DrawRectangle(altBrush, margin, y, pageWidth, rowHeight);
                gfx.DrawLine(linePen, margin, y + rowHeight, margin + pageWidth, y + rowHeight);

                xPos = margin;
                for (int c = 0; c < colCount; c++)
                {
                    string cellText = c < rows[r].Count ? rows[r][c] : "";
                    // 긴 텍스트는 말줄임 (Description 등)
                    double maxChars = colWidths[c] / 5.5;
                    if (cellText.Length > maxChars) cellText = cellText[..(int)maxChars] + "…";
                    gfx.DrawString(cellText, bodyFont, XBrushes.Black,
                        new XRect(xPos + 2, y + 2, colWidths[c] - 4, rowHeight - 4),
                        XStringFormats.TopLeft);
                    xPos += colWidths[c];
                }
                y += rowHeight;
            }

            // ── 마지막 페이지 푸터 ──
            gfx.DrawString($"LARS — {rows.Count}행 출력", footerFont, XBrushes.Gray,
                new XRect(margin, page.Height.Point - margin, pageWidth, 12), XStringFormats.TopLeft);
            gfx.DrawString($"Page {document.PageCount}", footerFont, XBrushes.Gray,
                new XRect(margin, page.Height.Point - margin, pageWidth, 12), XStringFormats.TopRight);

            document.Save(outputPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
