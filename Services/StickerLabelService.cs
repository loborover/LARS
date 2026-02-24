using System.IO;
using LARS.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace LARS.Services;

/// <summary>
/// 스티커 라벨 PDF 생성 서비스.
/// VBA StickerLabel.cls의 라벨 레이아웃 + Printer.bas의 PDF 출력을 통합하여 C#으로 이관.
/// PdfSharpCore를 사용해 A4 용지에 그리드 형태로 라벨을 렌더링합니다.
/// </summary>
public class StickerLabelService
{
    // mm → point 변환 상수 (1mm = 2.8346 pt)
    private const double MmToPt = 2.8346;

    /// <summary>
    /// 라벨 목록을 PDF로 저장합니다.
    /// </summary>
    /// <param name="outputPath">저장할 PDF 경로</param>
    /// <param name="labels">출력할 라벨 목록</param>
    /// <param name="settings">라벨 크기/레이아웃 설정 (null 시 기본값 사용)</param>
    /// <returns>성공 여부</returns>
    public bool GenerateStickerPdf(
        string outputPath,
        IList<StickerLabelInfo> labels,
        StickerLabelSettings? settings = null)
    {
        settings ??= new StickerLabelSettings();

        if (labels.Count == 0) return false;

        try
        {
            var document = new PdfDocument();
            document.Info.Title  = "LARS StickerLabel";
            document.Info.Author = "LARS";

            // mm → pt 변환
            double labelW  = settings.WidthMm  * MmToPt;
            double labelH  = settings.HeightMm * MmToPt;
            double margin  = settings.MarginMm  * MmToPt;
            double gap     = settings.GapMm     * MmToPt;
            int    cols    = settings.Columns;

            // A4 포트레이트 기준 (595.28 × 841.89 pt)
            var page       = AddPage(document);
            var gfx        = XGraphics.FromPdfPage(page);
            double pageW   = page.Width.Point;
            double pageH   = page.Height.Point;

            // 한 페이지에 들어가는 행 수 계산
            double usableH = pageH - margin * 2;
            int    rows    = (int)Math.Floor(usableH / (labelH + gap));
            if (rows < 1) rows = 1;

            int labelsPerPage = cols * rows;

            // 폰트
            var fontTitle  = new XFont("Malgun Gothic", 11, XFontStyle.Bold);
            var fontBody   = new XFont("Malgun Gothic", 8,  XFontStyle.Regular);
            var fontQty    = new XFont("Malgun Gothic", 10, XFontStyle.BoldItalic);

            var borderPen  = new XPen(XColors.DimGray, 0.8);
            var bgBrush    = new XSolidBrush(XColor.FromArgb(248, 249, 252));
            var accentBrush= new XSolidBrush(XColor.FromArgb(30, 80, 160));
            var textBrush  = XBrushes.Black;
            var mutedBrush = new XSolidBrush(XColor.FromArgb(90, 90, 100));

            for (int i = 0; i < labels.Count; i++)
            {
                // 페이지 전환
                if (i > 0 && i % labelsPerPage == 0)
                {
                    page = AddPage(document);
                    gfx  = XGraphics.FromPdfPage(page);
                }

                int indexOnPage = i % labelsPerPage;
                int col         = indexOnPage % cols;
                int row         = indexOnPage / cols;

                // 라벨 좌상단 좌표 계산 (열 간격은 균등 배분)
                double totalLabelAreaW = pageW - margin * 2;
                double stepX           = totalLabelAreaW / cols;
                double x               = margin + col * stepX + (stepX - labelW) / 2;
                double y               = margin + row * (labelH + gap);

                DrawLabel(gfx, labels[i], x, y, labelW, labelH,
                    borderPen, bgBrush, accentBrush, textBrush, mutedBrush,
                    fontTitle, fontBody, fontQty);
            }

            document.Save(outputPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────────────────
    //  라벨 1장 그리기
    // ──────────────────────────────────────
    private static void DrawLabel(
        XGraphics gfx, StickerLabelInfo label,
        double x, double y, double w, double h,
        XPen borderPen, XBrush bgBrush, XBrush accentBrush, XBrush textBrush, XBrush mutedBrush,
        XFont fontTitle, XFont fontBody, XFont fontQty)
    {
        double pad     = 4;
        double innerX  = x + pad;
        double innerW  = w - pad * 2;

        // ① 배경
        gfx.DrawRectangle(bgBrush, x, y, w, h);

        // ② 상단 강조 띠 (NickName 배경)
        double bandH = 20;
        gfx.DrawRectangle(accentBrush, x, y, w, bandH);

        // ③ 테두리 (라운드 효과는 PdfSharpCore에서 DrawRoundedRectangle로)
        gfx.DrawRectangle(borderPen, x, y, w, h);

        // ④ NickName (상단 띠 안)
        string nickName = Truncate(label.NickName, 20);
        gfx.DrawString(nickName, fontTitle, XBrushes.White,
            new XRect(innerX, y + 2, innerW, bandH - 2), XStringFormats.TopLeft);

        // ⑤ Vendor
        double curY = y + bandH + 3;
        gfx.DrawString("Vendor:", fontBody, mutedBrush,
            new XRect(innerX, curY, 40, 12), XStringFormats.TopLeft);
        gfx.DrawString(Truncate(label.Vendor, 22), fontBody, textBrush,
            new XRect(innerX + 42, curY, innerW - 42, 12), XStringFormats.TopLeft);
        curY += 13;

        // ⑥ Part No
        gfx.DrawString("Part:", fontBody, mutedBrush,
            new XRect(innerX, curY, 40, 12), XStringFormats.TopLeft);
        gfx.DrawString(Truncate(label.PartNumber, 22), fontBody, textBrush,
            new XRect(innerX + 42, curY, innerW - 42, 12), XStringFormats.TopLeft);
        curY += 13;

        // ⑦ 구분선
        gfx.DrawLine(new XPen(XColor.FromArgb(210, 220, 235), 0.5),
            x + pad, curY, x + w - pad, curY);
        curY += 3;

        // ⑧ QTY (크게)
        string qtyText = $"QTY: {label.QTY:N0}";
        gfx.DrawString(qtyText, fontQty, accentBrush,
            new XRect(innerX, curY, innerW, h - (curY - y) - pad),
            XStringFormats.TopLeft);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static PdfPage AddPage(PdfDocument doc)
    {
        var p = doc.AddPage();
        p.Orientation = PdfSharpCore.PageOrientation.Portrait;
        return p;
    }
}
