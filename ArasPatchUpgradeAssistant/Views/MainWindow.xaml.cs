using System.Windows;
using ArasPatchUpgradeAssistant.ViewModels;

namespace ArasPatchUpgradeAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
