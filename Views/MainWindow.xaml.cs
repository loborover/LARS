using System.Windows;
using LARS.ViewModels;

namespace LARS.Views;

/// <summary>
/// MainWindow 코드비하인드.
/// DataContext를 DI를 통해 주입받은 MainViewModel로 설정합니다.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
