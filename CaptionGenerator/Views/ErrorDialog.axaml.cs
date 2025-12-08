using Avalonia.Controls;
using CaptionGenerator.ViewModels;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using System;

namespace CaptionGenerator.Views;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
    }

    private ErrorDialogViewModel? ViewModel => DataContext as ErrorDialogViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel is not null)
        {
            ViewModel.Close = (result) => Close(result);
        }
    }

    public static async Task<ErrorDialogResult> ShowAsync(Window parent, string errorMessage)
    {
        var dialog = new ErrorDialog
        {
            DataContext = new ErrorDialogViewModel { ErrorMessage = errorMessage }
        };

        var result = await dialog.ShowDialog<ErrorDialogResult>(parent);
        return result;
    }
}
