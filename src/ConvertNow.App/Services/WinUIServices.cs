using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ConvertNow.App.Services;

/// <summary>
/// WinUI 3 <see cref="DispatcherQueue"/> implementation of <see cref="IDispatcherService"/>.
/// </summary>
public sealed class DispatcherQueueService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>
    /// Initializes a new instance of <see cref="DispatcherQueueService"/>.
    /// </summary>
    /// <param name="dispatcherQueue">The UI thread dispatcher queue.</param>
    public DispatcherQueueService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    /// <inheritdoc />
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _ = _dispatcherQueue.TryEnqueue(() => action());
    }

    /// <inheritdoc />
    public Task EnqueueAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            // Dispatcher queue has shut down (for example, during window teardown).
            tcs.TrySetResult();
        }

        return tcs.Task;
    }
}

/// <summary>
/// WinUI 3 <see cref="FolderPicker"/> implementation initialized with the host window HWND.
/// </summary>
public sealed class WinUIFolderPickerService : IFolderPickerService
{
    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of <see cref="WinUIFolderPickerService"/>.
    /// </summary>
    /// <param name="window">The owner window for the picker dialog.</param>
    public WinUIFolderPickerService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
        };
        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WindowNative.GetWindowHandle(_window);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

/// <summary>
/// WinUI 3 <see cref="FileOpenPicker"/> implementation initialized with the host window HWND.
/// </summary>
public sealed class WinUIFilePickerService : IFilePickerService
{
    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of <see cref="WinUIFilePickerService"/>.
    /// </summary>
    /// <param name="window">The owner window for the picker dialog.</param>
    public WinUIFilePickerService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <inheritdoc />
    public async Task<string?> PickInputFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
        };
        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WindowNative.GetWindowHandle(_window);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}

/// <summary>
/// Launches Windows Explorer to reveal a converted file or open its directory.
/// </summary>
public sealed class ExplorerShellService : IShellService
{
    /// <inheritdoc />
    public void OpenFolderAndSelectFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (File.Exists(fullPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullPath}\"",
                    UseShellExecute = true,
                });
                return;
            }

            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{directory}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception)
        {
            // Best-effort shell navigation.
        }
    }
}

/// <summary>
/// Implements <see cref="IWindowLifecycleService"/> using a window close delegate.
/// </summary>
public sealed class WindowLifecycleService : IWindowLifecycleService
{
    private readonly Action _closeAction;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowLifecycleService"/>.
    /// </summary>
    /// <param name="closeAction">Action that closes the host window.</param>
    public WindowLifecycleService(Action closeAction)
    {
        _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
    }

    /// <inheritdoc />
    public void RequestClose() => _closeAction();
}
