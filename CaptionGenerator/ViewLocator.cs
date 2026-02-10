using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CaptionGenerator.ViewModels;
using CaptionGenerator.Views; // Ensure you have this namespace

namespace CaptionGenerator;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Not Found: data is null" };

        // FIX: Explicitly map ViewModels to Views using a switch expression.
        // This ensures the compiler knows these Views are used and keeps them.
        return data switch
        {
            MainViewModel vm => new MainView { DataContext = vm },
            SettingsViewModel vm => new SettingsView { DataContext = vm },
            
            // Add other ViewModels here as you create them:
            // AboutViewModel vm => new AboutView { DataContext = vm },

            _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}