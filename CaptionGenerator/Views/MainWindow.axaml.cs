using Avalonia.Controls;
using CaptionGenerator.ViewModels;

namespace CaptionGenerator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var mainViewModel = new MainViewModel
        {
            StorageProvider = this.StorageProvider,
            MainWindow = this
        };
        DataContext = mainViewModel;
    }
}
