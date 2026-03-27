using System.Windows;
using FenixFpm.Desktop.ViewModels;

namespace FenixFpm.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
