using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CaptionGenerator.ViewModels;
using CaptionGenerator.Views; // <--- FIX: This was missing!

namespace CaptionGenerator;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "Not Found: data is null" };

        return data switch
        {
            MainViewModel vm => new MainView { DataContext = vm },
            SettingsViewModel vm => new SettingsView { DataContext = vm },
            _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}