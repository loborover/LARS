using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LARS.Models;
using LARS.Services;

namespace LARS.Views;

/// <summary>
/// StickerLabel 인쇄 Dialog 코드비하인드.
/// PartList 또는 ItemCounter 데이터를 받아 라벨 미리보기와 PDF 저장 기능을 제공합니다.
/// </summary>
public partial class StickerLabelDialog : Window
{
    private readonly StickerLabelService _stickerService;
    private readonly IList<StickerLabelInfo> _partListLabels;
    private readonly IList<StickerLabelInfo> _itemCounterLabels;
    private          IList<StickerLabelInfo> _currentLabels;

    /// <summary>DataGrid 바인딩용 래퍼 (번호 포함)</summary>
    private record LabelRow(int No, string NickName, string Vendor, string PartNumber, long QTY);

    public StickerLabelDialog(
        StickerLabelService stickerService,
        IList<StickerLabelInfo> partListLabels,
        IList<StickerLabelInfo> itemCounterLabels)
    {
        InitializeComponent();
        _stickerService     = stickerService;
        _partListLabels     = partListLabels;
        _itemCounterLabels  = itemCounterLabels;
        _currentLabels      = partListLabels;
        RefreshGrid();
    }

    // ──────────────────────────────
    //  이벤트 핸들러
    // ──────────────────────────────

    private void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbSource.SelectedIndex == 1)
            _currentLabels = _itemCounterLabels;
        else
            _currentLabels = _partListLabels;

        RefreshGrid();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLabels.Count == 0)
        {
            MessageBox.Show("출력할 라벨 데이터가 없습니다.\n먼저 PartList 또는 ItemCounter 데이터를 로드해 주세요.",
                "정보", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = BuildSettings();
        if (settings == null) return; // 파싱 오류 시 이미 메시지 표시됨

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "스티커 라벨 PDF 저장",
            Filter      = "PDF 파일 (*.pdf)|*.pdf",
            FileName    = $"StickerLabel_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
            DefaultExt  = ".pdf"
        };

        if (dialog.ShowDialog(this) != true) return;

        bool ok = _stickerService.GenerateStickerPdf(dialog.FileName, _currentLabels, settings);
        if (ok)
        {
            var result = MessageBox.Show(
                $"PDF 저장 완료!\n\n{dialog.FileName}\n\n파일을 바로 열어볼까요?",
                "완료", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = dialog.FileName,
                    UseShellExecute = true
                });
        }
        else
        {
            MessageBox.Show("PDF 저장에 실패했습니다. 경로와 권한을 확인해 주세요.",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ──────────────────────────────
    //  내부 헬퍼
    // ──────────────────────────────

    private void RefreshGrid()
    {
        var rows = _currentLabels
            .Select((l, i) => new LabelRow(i + 1, l.NickName, l.Vendor, l.PartNumber, l.QTY))
            .ToList();

        LabelGrid.ItemsSource = rows;
        TxtLabelCount.Text    = $"라벨 {rows.Count}개";
    }

    private StickerLabelSettings? BuildSettings()
    {
        if (!double.TryParse(TxtWidth.Text,   out double w)   || w   <= 0 ||
            !double.TryParse(TxtHeight.Text,  out double h)   || h   <= 0 ||
            !int.TryParse   (TxtColumns.Text, out int    cols) || cols <= 0 ||
            !double.TryParse(TxtGap.Text,     out double gap))
        {
            MessageBox.Show("설정값이 올바르지 않습니다. 숫자를 확인해 주세요.",
                "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return new StickerLabelSettings
        {
            WidthMm  = w,
            HeightMm = h,
            Columns  = cols,
            GapMm    = gap
        };
    }
}
