using ConvertNow.App.Services;
using ConvertNow.App.ViewModels;
using ConvertNow.Core;
using Microsoft.UI.Xaml;

namespace ConvertNow.App;

/// <summary>
/// Application entry point for Convert.Now.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    /// <summary>
    /// Initializes a new instance of <see cref="App"/>.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? inputFilePath = CliArgumentParser.ParseProcessArgs(Environment.GetCommandLineArgs());

        _window = new MainWindow(window => new MainViewModel(
            ffmpegRunner: new FFmpegRunner(),
            dispatcherService: new DispatcherQueueService(window.DispatcherQueue),
            folderPickerService: new WinUIFolderPickerService(window),
            filePickerService: new WinUIFilePickerService(window),
            executionStateService: new Win32ExecutionStateService(),
            shellService: new ExplorerShellService(),
            windowLifecycleService: new WindowLifecycleService(window.Close)));

        _window.ViewModel.LoadFile(inputFilePath);
        _window.Activate();
    }
}
