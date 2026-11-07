using Avalonia.Controls;
using Avalonia.Layout;
using LedCube.App.ViewModels;

namespace LedCube.App.Views;

public sealed partial class MainWindow : Window
{
    private VisualizerBuilderViewModel? _builder;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachBuilder();
    }

    private void AttachBuilder()
    {
        if (_builder is not null) _builder.SaveAsRequested -= ShowSaveAsDialogAsync;
        _builder = (DataContext as MainWindowViewModel)?.Builder;
        if (_builder is not null) _builder.SaveAsRequested += ShowSaveAsDialogAsync;
    }

    private Task<string?> ShowSaveAsDialogAsync(SaveAsDialogRequest request)
    {
        var name = new TextBox { Text = request.SuggestedName, MinWidth = 320, IsEnabled = !request.ConfirmOverwrite };
        var dialog = new Window
        {
            Title = request.ConfirmOverwrite ? "Confirm Overwrite" : "Save Visualizer As",
            Width = 430,
            Height = request.ConfirmOverwrite ? 190 : 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var accept = new Button { Content = request.ConfirmOverwrite ? "Overwrite" : "Save As" };
        var cancel = new Button { Content = "Cancel" };
        accept.Click += (_, _) => dialog.Close(name.Text?.Trim());
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = request.ConfirmOverwrite
                        ? "A saved visualizer with this name already exists. Overwrite it?"
                        : "Choose the name for this visualizer. The saved file will use the same name.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                name,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, accept }
                }
            }
        };
        return dialog.ShowDialog<string?>(this);
    }
}
