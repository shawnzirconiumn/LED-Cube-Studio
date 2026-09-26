using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using LedCube.App.ViewModels;
using LedCube.App.Views;

namespace LedCube.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow { DataContext = viewModel };
            var shutdownReady = false;
            mainWindow.Closing += async (_, eventArgs) =>
            {
                if (shutdownReady) return;
                eventArgs.Cancel = true;
                shutdownReady = true;
                try
                {
                    await viewModel.DisposeAsync().ConfigureAwait(true);
                }
                catch (Exception exception)
                {
                    Trace.WriteLine($"[shutdown] {exception}");
                }
                desktop.Shutdown();
            };
            desktop.MainWindow = mainWindow;
            _ = viewModel.InitializeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
