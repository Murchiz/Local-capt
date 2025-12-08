using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace CaptionGenerator.ViewModels;

public enum ErrorDialogResult
{
    Stop,
    Skip
}

public partial class ErrorDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public Action<ErrorDialogResult>? Close { get; set; }

    [RelayCommand]
    private void Stop()
    {
        Close?.Invoke(ErrorDialogResult.Stop);
    }

    [RelayCommand]
    private void Skip()
    {
        Close?.Invoke(ErrorDialogResult.Skip);
    }
}
