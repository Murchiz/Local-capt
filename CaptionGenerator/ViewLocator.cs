using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CaptionGenerator.ViewModels;
using CaptionGenerator.Views; // Required to see MainWindow/SettingsWindow

namespace CaptionGenerator;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Not Found: data is null" };

        return data switch
        {
            // FIX: Map MainViewModel to MainWindow (not MainView)
            MainViewModel vm => new MainWindow { DataContext = vm },

            // FIX: Map SettingsViewModel to SettingsWindow (not SettingsView)
            SettingsViewModel vm => new SettingsWindow { DataContext = vm },

            // FIX: Map ErrorDialogViewModel to ErrorDialog (if you use it here)
            ErrorDialogViewModel vm => new ErrorDialog { DataContext = vm },

            _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}