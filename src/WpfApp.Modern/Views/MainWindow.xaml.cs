using System.Windows;
using WpfApp.Modern.ViewModels;

namespace WpfApp.Modern.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
