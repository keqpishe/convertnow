namespace ConvertNow.App.Services;

/// <summary>
/// Dispatches actions onto the application's UI thread.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Schedules an action for execution on the UI thread.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    void Enqueue(Action action);

    /// <summary>
    /// Schedules an action on the UI thread and returns a task that completes when it finishes.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A task representing the dispatched execution.</returns>
    Task EnqueueAsync(Action action);
}

/// <summary>
/// Presents a native folder selection dialog.
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Prompts the user to select an output folder.
    /// </summary>
    /// <returns>The selected directory path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> PickFolderAsync();
}

/// <summary>
/// Presents a native file selection dialog.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Prompts the user to select an input file to convert.
    /// </summary>
    /// <returns>The selected file path, or <see langword="null"/> if cancelled.</returns>
    Task<string?> PickInputFileAsync();
}

/// <summary>
/// Controls system sleep prevention during long-running conversions.
/// </summary>
public interface IExecutionStateService
{
    /// <summary>
    /// Prevents the system from entering sleep automatically while work is active.
    /// </summary>
    void PreventSleep();

    /// <summary>
    /// Restores normal system sleep behavior once work completes, fails, or is cancelled.
    /// </summary>
    void RestoreSleep();
}

/// <summary>
/// Interacts with the Windows shell to reveal output files.
/// </summary>
public interface IShellService
{
    /// <summary>
    /// Opens Windows Explorer and highlights the specified file (or opens its parent folder if missing).
    /// </summary>
    /// <param name="filePath">Path to the converted output file.</param>
    void OpenFolderAndSelectFile(string filePath);
}

/// <summary>
/// Abstracts closing the host window from the ViewModel.
/// </summary>
public interface IWindowLifecycleService
{
    /// <summary>
    /// Requests the host window to close.
    /// </summary>
    void RequestClose();
}
