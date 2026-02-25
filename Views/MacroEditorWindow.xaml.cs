using System.Windows;
using LARS.ViewModels;

namespace LARS.Views;

/// <summary>
/// Visual Macro Editor 윈도우의 코드비하인드.
/// </summary>
public partial class MacroEditorWindow : Window
{
    public MacroEditorWindow(MacroEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
