using ConvertNow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ConvertNow.App.Views;

/// <summary>
/// Primary view hosting all Convert.Now conversion UI states.
/// </summary>
public sealed partial class MainView : UserControl
{
    private MainViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of <see cref="MainView"/>.
    /// </summary>
    public MainView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the ViewModel bound to this view.
    /// </summary>
    public MainViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value))
            {
                return;
            }

            _viewModel = value;
            if (_viewModel is not null)
            {
                Bindings.Update();
            }
        }
    }
}
