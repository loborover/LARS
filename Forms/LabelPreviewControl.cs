using System.Drawing;
using LARS.Models;
using LARS.Features.StickerLabel;
using System.Drawing.Printing;

namespace LARS.Forms;

public partial class LabelPreviewControl : UserControl
{
    private PictureBox pictureBox;
    private Button btnPrintPdf;
    private StickerRenderer renderer;
    private StickerData sampleData;

    public LabelPreviewControl()
    {
        InitializeComponent();
        renderer = new StickerRenderer();
        sampleData = new StickerData { MainText = "LARS-001", Shape = LabelShape.Round };
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.White;

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.WhiteSmoke };
        
        btnPrintPdf = new Button
        {
            Text = "PDF 저장 (Print)",
            Location = new Point(10, 10),
            Size = new Size(120, 30),
            BackColor = Color.DarkSlateBlue,
            ForeColor = Color.White
        };
        btnPrintPdf.Click += BtnPrintPdf_Click;

        topPanel.Controls.Add(btnPrintPdf);

        pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(20)
        };
        pictureBox.Paint += PictureBox_Paint;

        this.Controls.Add(pictureBox);
        this.Controls.Add(topPanel);
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        // 화면 미리보기 그리기
        var rect = new RectangleF(50, 50, 200, 100);
        renderer.DrawSticker(e.Graphics, rect, sampleData);
    }

    private void BtnPrintPdf_Click(object? sender, EventArgs e)
    {
        PrintDocument pd = new PrintDocument();
        pd.PrintPage += (s, args) =>
        {
            // 실제 인쇄/PDF 저장 시 그리기
            if (args.Graphics != null)
            {
                var rect = new RectangleF(100, 100, 200, 100);
                renderer.DrawSticker(args.Graphics, rect, sampleData);
            }
        };

        PrintDialog dialog = new PrintDialog();
        dialog.Document = pd;
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            pd.Print();
        }
    }
}
