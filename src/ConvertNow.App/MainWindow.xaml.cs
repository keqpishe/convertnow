using System.ComponentModel;
using ConvertNow.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace ConvertNow.App;

/// <summary>
/// Compact, non-resizable, screen-centered WinUI 3 host window for Convert.Now.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double FixedDipWidth = 420.0;
    private const double MinDipHeight = 236.0;
    private const double MaxDipHeight = 420.0;

    private bool _hasCenteredOnLoad;
    private bool _isClosingConfirmed;
    private bool _isCancellingForClose;

    /// <summary>
    /// Gets the ViewModel bound to this window.
    /// </summary>
    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindow"/> using a factory that receives the window instance.
    /// </summary>
    /// <param name="viewModelFactory">Factory that constructs <see cref="MainViewModel"/> for this window.</param>
    public MainWindow(Func<MainWindow, MainViewModel> viewModelFactory)
    {
        ArgumentNullException.ThrowIfNull(viewModelFactory);

        ViewModel = viewModelFactory(this);
        InitializeComponent();

        ConfigureWindowChrome();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RootView.Loaded += OnRootViewLoaded;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnWindowClosed;
    }

    private void ConfigureWindowChrome()
    {
        Title = "Convert.Now";

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        // Apply an initial compact size and screen center before the first Activate() call to avoid visual flicker.
        AppWindow.ResizeClient(new SizeInt32((int)FixedDipWidth, 250));
        CenterOnCurrentDisplay();
    }

    private void OnRootViewLoaded(object sender, RoutedEventArgs e)
    {
        if (RootView.XamlRoot is not null)
        {
            RootView.XamlRoot.Changed += OnXamlRootChanged;
        }

        bool shouldCenter = !_hasCenteredOnLoad;
        _hasCenteredOnLoad = true;
        UpdateWindowSize(shouldCenter);
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        UpdateWindowSize(centerOnScreen: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.State)
            or nameof(MainViewModel.UseCustomOutputDirectory)
            or nameof(MainViewModel.ShowDiskWarning)
            or nameof(MainViewModel.ErrorMessage)
            or nameof(MainViewModel.UnsupportedMessage))
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => UpdateWindowSize(centerOnScreen: false));
        }
    }

    private void UpdateWindowSize(bool centerOnScreen)
    {
        if (RootView.XamlRoot is null)
        {
            return;
        }

        double scale = RootView.XamlRoot.RasterizationScale;
        if (scale <= 0)
        {
            scale = 1.0;
        }

        RootView.UpdateLayout();
        RootView.Measure(new Windows.Foundation.Size(FixedDipWidth, double.PositiveInfinity));

        double desiredDipHeight = Math.Clamp(RootView.DesiredSize.Height, MinDipHeight, MaxDipHeight);
        int clientWidthPx = (int)Math.Round(FixedDipWidth * scale);
        int clientHeightPx = (int)Math.Round(desiredDipHeight * scale);

        AppWindow.ResizeClient(new SizeInt32(clientWidthPx, clientHeightPx));

        if (centerOnScreen)
        {
            CenterOnCurrentDisplay();
        }
    }

    private void CenterOnCurrentDisplay()
    {
        DisplayArea? displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return;
        }

        RectInt32 workArea = displayArea.WorkArea;
        SizeInt32 windowSize = AppWindow.Size;

        int x = workArea.X + Math.Max(0, (workArea.Width - windowSize.Width) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - windowSize.Height) / 2);
        AppWindow.Move(new PointInt32(x, y));
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isClosingConfirmed || !ViewModel.IsConverting)
        {
            return;
        }

        args.Cancel = true;

        if (_isCancellingForClose)
        {
            return;
        }

        _isCancellingForClose = true;
        try
        {
            await ViewModel.CancelActiveConversionAsync();
        }
        finally
        {
            _isClosingConfirmed = true;
            _isCancellingForClose = false;
            DispatcherQueue.TryEnqueue(Close);
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        ViewModel.TryCancelImmediately();

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        RootView.Loaded -= OnRootViewLoaded;
        AppWindow.Closing -= OnAppWindowClosing;
        Closed -= OnWindowClosed;

        if (RootView.XamlRoot is not null)
        {
            RootView.XamlRoot.Changed -= OnXamlRootChanged;
        }
    }
}
