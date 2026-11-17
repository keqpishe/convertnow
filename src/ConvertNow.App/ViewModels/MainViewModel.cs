using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConvertNow.App.Services;
using ConvertNow.Core;

namespace ConvertNow.App.ViewModels;

/// <summary>
/// Main MVVM ViewModel driving all Convert.Now window states, validation, conversion lifecycle, and shell actions.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFFmpegRunner _ffmpegRunner;
    private readonly IDispatcherService _dispatcherService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly IExecutionStateService _executionStateService;
    private readonly IShellService _shellService;
    private readonly IWindowLifecycleService _windowLifecycleService;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, long> _getFileSize;
    private readonly Func<string, string, DiskSpaceCheckResult> _diskSpaceChecker;

    private FormatCategory? _sourceCategory;
    private CancellationTokenSource? _conversionCts;
    private Task? _activeConversionTask;
    private bool _sleepPrevented;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConverting))]
    [NotifyPropertyChangedFor(nameof(IsCustomPathInputVisible))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial ConversionViewState State { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial string InputFilePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionPairText))]
    public partial string InputFileName { get; set; }

    [ObservableProperty]
    public partial string InputFileSizeText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MediaFormat> AvailableFormats { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    public partial MediaFormat? SelectedTargetFormat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomPathInputVisible))]
    public partial bool UseCustomOutputDirectory { get; set; }

    [ObservableProperty]
    public partial string CustomOutputDirectory { get; set; }

    [ObservableProperty]
    public partial string TargetOutputPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConversionPairText))]
    public partial string TargetOutputFileName { get; set; }

    [ObservableProperty]
    public partial double ProgressPercentage { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial string ProgressPercentageText { get; set; }

    [ObservableProperty]
    public partial string EtaText { get; set; }

    [ObservableProperty]
    public partial string SpeedText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConvertingStatusText))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsCancelling { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    public partial string CompletedOutputPath { get; set; }

    [ObservableProperty]
    public partial string CompletedOutputFileName { get; set; }

    [ObservableProperty]
    public partial string CompletedOutputSizeText { get; set; }

    [ObservableProperty]
    public partial string CompletedOutputSummary { get; set; }

    [ObservableProperty]
    public partial string CompletedDirectoryText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorDisplayText))]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string UnsupportedMessage { get; set; }

    [ObservableProperty]
    public partial bool ShowDiskWarning { get; set; }

    [ObservableProperty]
    public partial bool HasCriticalDiskSpace { get; set; }

    [ObservableProperty]
    public partial string DiskWarningMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    public MainViewModel(
        IFFmpegRunner ffmpegRunner,
        IDispatcherService dispatcherService,
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService,
        IExecutionStateService executionStateService,
        IShellService shellService,
        IWindowLifecycleService windowLifecycleService,
        Func<string, bool>? fileExists = null,
        Func<string, long>? getFileSize = null,
        Func<string, string, DiskSpaceCheckResult>? diskSpaceChecker = null)
    {
        _ffmpegRunner = ffmpegRunner ?? throw new ArgumentNullException(nameof(ffmpegRunner));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _executionStateService = executionStateService ?? throw new ArgumentNullException(nameof(executionStateService));
        _shellService = shellService ?? throw new ArgumentNullException(nameof(shellService));
        _windowLifecycleService = windowLifecycleService ?? throw new ArgumentNullException(nameof(windowLifecycleService));
        _fileExists = fileExists ?? File.Exists;
        _getFileSize = getFileSize ?? DefaultGetFileSize;
        _diskSpaceChecker = diskSpaceChecker ?? DiskSpaceChecker.CheckDiskSpace;

        State = ConversionViewState.NoFile;
        InputFilePath = string.Empty;
        InputFileName = string.Empty;
        InputFileSizeText = string.Empty;
        AvailableFormats = [];
        CustomOutputDirectory = string.Empty;
        TargetOutputPath = string.Empty;
        TargetOutputFileName = string.Empty;
        IsProgressIndeterminate = true;
        ProgressPercentageText = "...";
        EtaText = "ETA: --";
        SpeedText = string.Empty;
        CompletedOutputPath = string.Empty;
        CompletedOutputFileName = string.Empty;
        CompletedOutputSizeText = string.Empty;
        CompletedOutputSummary = string.Empty;
        CompletedDirectoryText = string.Empty;
        ErrorMessage = string.Empty;
        UnsupportedMessage = string.Empty;
        DiskWarningMessage = string.Empty;
    }

    /// <summary>Gets a value indicating whether a conversion is currently running.</summary>
    public bool IsConverting => State == ConversionViewState.Converting;

    /// <summary>Gets a value indicating whether the custom save path row should be visible.</summary>
    public bool IsCustomPathInputVisible =>
        State == ConversionViewState.FormatSelection && UseCustomOutputDirectory;

    /// <summary>Gets the header string describing the source and target filenames during conversion.</summary>
    public string ConversionPairText =>
        string.IsNullOrEmpty(TargetOutputFileName)
            ? InputFileName
            : $"{InputFileName} → {TargetOutputFileName}";

    /// <summary>Gets the conversion status label shown above the progress bar.</summary>
    public string ConvertingStatusText => IsCancelling ? "Cancelling..." : "Converting...";

    /// <summary>Gets the error message formatted for UI display.</summary>
    public string ErrorDisplayText =>
        string.IsNullOrWhiteSpace(ErrorMessage)
            ? "Error: An unknown error occurred."
            : ErrorMessage.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                ? ErrorMessage
                : $"Error: {ErrorMessage}";

    /// <summary>
    /// Loads the specified input file path and transitions to the appropriate initial state
    /// (<see cref="ConversionViewState.NoFile"/>, <see cref="ConversionViewState.Unsupported"/>,
    /// <see cref="ConversionViewState.Error"/>, or <see cref="ConversionViewState.FormatSelection"/>).
    /// </summary>
    /// <param name="filePath">Path to the input media file, or <see langword="null"/> when launched without a file.</param>
    public void LoadFile(string? filePath)
    {
        if (IsConverting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            ResetToNoFileState();
            return;
        }

        string trimmedPath = filePath.Trim().Trim('"').Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            ResetToNoFileState();
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(trimmedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _sourceCategory = null;
            InputFilePath = trimmedPath;
            InputFileName = GetDisplayFileName(trimmedPath);
            AvailableFormats = [];
            SelectedTargetFormat = null;
            ClearDiskSpaceWarning();
            ErrorMessage = $"The file path is invalid: {ex.Message}";
            State = ConversionViewState.Error;
            return;
        }

        if (!_fileExists(normalizedPath))
        {
            _sourceCategory = null;
            InputFilePath = normalizedPath;
            InputFileName = GetDisplayFileName(normalizedPath);
            AvailableFormats = [];
            SelectedTargetFormat = null;
            ClearDiskSpaceWarning();
            ErrorMessage = $"File not found: \"{normalizedPath}\".";
            State = ConversionViewState.Error;
            return;
        }

        if (!FormatRegistry.TryGetInputFormat(normalizedPath, out MediaFormat? inputFormat))
        {
            _sourceCategory = null;
            InputFilePath = normalizedPath;
            InputFileName = GetDisplayFileName(normalizedPath);
            InputFileSizeText = string.Empty;
            AvailableFormats = [];
            SelectedTargetFormat = null;
            ClearDiskSpaceWarning();
            ErrorMessage = string.Empty;
            UnsupportedMessage =
                $"The file \"{InputFileName}\" cannot be converted. Convert.Now supports video, audio, and image files.";
            State = ConversionViewState.Unsupported;
            return;
        }

        _sourceCategory = inputFormat.Category;
        InputFilePath = normalizedPath;
        InputFileName = GetDisplayFileName(normalizedPath);
        long sizeBytes = SafeGetFileSize(normalizedPath);
        InputFileSizeText = FileSizeFormatter.Format(sizeBytes);

        UseCustomOutputDirectory = false;
        CustomOutputDirectory = string.Empty;
        ErrorMessage = string.Empty;
        UnsupportedMessage = string.Empty;
        ClearCompletedDetails();

        IReadOnlyList<MediaFormat> compatibleFormats = FormatCompatibility.GetCompatibleOutputFormats(inputFormat.Category);
        AvailableFormats = compatibleFormats;
        SelectedTargetFormat = FormatCompatibility.GetDefaultOutputFormat(normalizedPath)
            ?? (compatibleFormats.Count > 0 ? compatibleFormats[0] : null);

        UpdateTargetOutputPreview();
        RefreshDiskSpaceCheck();
        State = ConversionViewState.FormatSelection;
    }

    /// <summary>
    /// Cancels any active conversion and waits for FFmpeg process termination, cleanup, and sleep restoration.
    /// </summary>
    public async Task CancelActiveConversionAsync()
    {
        CancellationTokenSource? cts = _conversionCts;
        Task? activeTask = _activeConversionTask;

        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (activeTask is not null)
        {
            try
            {
                await activeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Conversion errors and cancellation are handled within ExecuteConversionAsync.
            }
        }
    }

    /// <summary>
    /// Synchronously signals cancellation and restores system sleep state during forced window teardown.
    /// </summary>
    public void TryCancelImmediately()
    {
        CancellationTokenSource? cts = _conversionCts;
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        RestoreSleepIfNeeded();
    }

    [RelayCommand]
    private async Task BrowseInputFileAsync()
    {
        if (IsConverting)
        {
            return;
        }

        string? pickedFile = await _filePickerService.PickInputFileAsync();
        if (!string.IsNullOrWhiteSpace(pickedFile))
        {
            LoadFile(pickedFile);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseOutputFolder))]
    private async Task BrowseOutputFolderAsync()
    {
        string? pickedFolder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(pickedFolder))
        {
            CustomOutputDirectory = pickedFolder.Trim();
            UseCustomOutputDirectory = true;
        }
    }

    private bool CanBrowseOutputFolder() => State == ConversionViewState.FormatSelection;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private Task ConvertAsync()
    {
        if (!CanConvert())
        {
            return Task.CompletedTask;
        }

        return StartConversionCoreAsync();
    }

    private bool CanConvert() =>
        State == ConversionViewState.FormatSelection
        && _sourceCategory is not null
        && SelectedTargetFormat is not null
        && !string.IsNullOrWhiteSpace(InputFilePath);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (_conversionCts is null || IsCancelling)
        {
            return;
        }

        IsCancelling = true;
        try
        {
            _conversionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool CanCancel() => State == ConversionViewState.Converting && !IsCancelling;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync()
    {
        if (!CanRetry())
        {
            return Task.CompletedTask;
        }

        if (_sourceCategory is null || SelectedTargetFormat is null || !_fileExists(InputFilePath))
        {
            LoadFile(InputFilePath);
            return Task.CompletedTask;
        }

        return StartConversionCoreAsync();
    }

    private bool CanRetry() =>
        State == ConversionViewState.Error && !string.IsNullOrWhiteSpace(InputFilePath);

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (!CanOpenFolder())
        {
            return;
        }

        _shellService.OpenFolderAndSelectFile(CompletedOutputPath);
    }

    private bool CanOpenFolder() =>
        State == ConversionViewState.Completed && !string.IsNullOrWhiteSpace(CompletedOutputPath);

    [RelayCommand]
    private async Task CloseAsync()
    {
        await CancelActiveConversionAsync();
        _windowLifecycleService.RequestClose();
    }

    partial void OnSelectedTargetFormatChanged(MediaFormat? value)
    {
        UpdateTargetOutputPreview();
    }

    partial void OnUseCustomOutputDirectoryChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(CustomOutputDirectory) && !string.IsNullOrWhiteSpace(InputFilePath))
        {
            CustomOutputDirectory = Path.GetDirectoryName(InputFilePath) ?? string.Empty;
        }

        UpdateTargetOutputPreview();
        RefreshDiskSpaceCheck();
    }

    partial void OnCustomOutputDirectoryChanged(string value)
    {
        UpdateTargetOutputPreview();
        RefreshDiskSpaceCheck();
    }

    private async Task StartConversionCoreAsync()
    {
        if (_activeConversionTask is not null || _sourceCategory is null || SelectedTargetFormat is null || string.IsNullOrWhiteSpace(InputFilePath))
        {
            return;
        }

        MediaFormat targetFormat = SelectedTargetFormat;
        FormatCategory sourceCategory = _sourceCategory.Value;

        string outputPath = ResolveOutputPath(targetFormat);
        TargetOutputPath = outputPath;
        TargetOutputFileName = Path.GetFileName(outputPath);

        ResetProgressState();
        ErrorMessage = string.Empty;
        IsCancelling = false;
        State = ConversionViewState.Converting;

        var cts = new CancellationTokenSource();
        _conversionCts = cts;

        PreventSleepIfNeeded();

        var job = new ConversionJob(InputFilePath, outputPath, targetFormat, sourceCategory);
        var progressReporter = new CallbackProgress<ConversionProgress>(progress =>
            _dispatcherService.Enqueue(() => ApplyProgress(progress)));

        Task conversionTask = ExecuteConversionAsync(job, progressReporter, cts);
        _activeConversionTask = conversionTask;
        try
        {
            await conversionTask;
        }
        finally
        {
            if (ReferenceEquals(_activeConversionTask, conversionTask))
            {
                _activeConversionTask = null;
            }
        }
    }

    private async Task ExecuteConversionAsync(
        ConversionJob job,
        IProgress<ConversionProgress> progressReporter,
        CancellationTokenSource cts)
    {
        ConversionResult result;
        try
        {
            result = await _ffmpegRunner.ConvertAsync(job, progressReporter, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new ConversionResult(
                Success: false,
                IsCancelled: true,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: TimeSpan.Zero,
                ErrorMessage: "Conversion was cancelled.");
        }
        catch (Exception ex)
        {
            result = new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: TimeSpan.Zero,
                ErrorMessage: ex.Message);
        }
        finally
        {
            await _dispatcherService.EnqueueAsync(RestoreSleepIfNeeded).ConfigureAwait(false);
        }

        await _dispatcherService.EnqueueAsync(() =>
        {
            ApplyConversionResult(result);
            CleanupConversionRun(cts);
        }).ConfigureAwait(false);
    }

    private void ApplyProgress(ConversionProgress progress)
    {
        if (State != ConversionViewState.Converting)
        {
            return;
        }

        IsProgressIndeterminate = progress.IsIndeterminate;
        double clampedPercentage = Math.Clamp(progress.Percentage, 0, 100);
        ProgressPercentage = clampedPercentage;
        ProgressPercentageText = progress.IsIndeterminate
            ? "..."
            : $"{Math.Round(clampedPercentage):0}%";
        EtaText = $"ETA: {ConversionProgress.FormatEta(progress.EstimatedRemaining)}";
        SpeedText = progress.SpeedText ?? string.Empty;
    }

    private void ApplyConversionResult(ConversionResult result)
    {
        IsCancelling = false;

        if (result.IsCancelled)
        {
            UpdateTargetOutputPreview();
            RefreshDiskSpaceCheck();
            State = ConversionViewState.FormatSelection;
            return;
        }

        if (result.Success)
        {
            CompletedOutputPath = result.OutputFilePath;
            CompletedOutputFileName = GetDisplayFileName(result.OutputFilePath);
            CompletedOutputSizeText = FileSizeFormatter.Format(result.OutputFileSizeBytes);
            CompletedOutputSummary = $"{CompletedOutputFileName} ({CompletedOutputSizeText})";

            string directory = Path.GetDirectoryName(result.OutputFilePath) ?? string.Empty;
            CompletedDirectoryText = string.IsNullOrEmpty(directory)
                ? string.Empty
                : $"Saved to: {EnsureTrailingSeparator(directory)}";

            ProgressPercentage = 100;
            IsProgressIndeterminate = false;
            ProgressPercentageText = "100%";
            State = ConversionViewState.Completed;
            return;
        }

        ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? "An unknown error occurred during conversion."
            : result.ErrorMessage;
        State = ConversionViewState.Error;
    }

    private void CleanupConversionRun(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_conversionCts, cts))
        {
            _conversionCts = null;
        }

        _activeConversionTask = null;
        cts.Dispose();
    }

    private void PreventSleepIfNeeded()
    {
        if (_sleepPrevented)
        {
            return;
        }

        _sleepPrevented = true;
        _executionStateService.PreventSleep();
    }

    private void RestoreSleepIfNeeded()
    {
        if (!_sleepPrevented)
        {
            return;
        }

        _sleepPrevented = false;
        _executionStateService.RestoreSleep();
    }

    private void ResetToNoFileState()
    {
        _sourceCategory = null;
        InputFilePath = string.Empty;
        InputFileName = string.Empty;
        InputFileSizeText = string.Empty;
        AvailableFormats = [];
        SelectedTargetFormat = null;
        UseCustomOutputDirectory = false;
        CustomOutputDirectory = string.Empty;
        TargetOutputPath = string.Empty;
        TargetOutputFileName = string.Empty;
        ErrorMessage = string.Empty;
        UnsupportedMessage = string.Empty;
        ClearCompletedDetails();
        ClearDiskSpaceWarning();
        ResetProgressState();
        State = ConversionViewState.NoFile;
    }

    private void ResetProgressState()
    {
        ProgressPercentage = 0;
        IsProgressIndeterminate = true;
        ProgressPercentageText = "...";
        EtaText = "ETA: --";
        SpeedText = string.Empty;
    }

    private void ClearCompletedDetails()
    {
        CompletedOutputPath = string.Empty;
        CompletedOutputFileName = string.Empty;
        CompletedOutputSizeText = string.Empty;
        CompletedOutputSummary = string.Empty;
        CompletedDirectoryText = string.Empty;
    }

    private void UpdateTargetOutputPreview()
    {
        if (string.IsNullOrWhiteSpace(InputFilePath) || SelectedTargetFormat is null)
        {
            TargetOutputPath = string.Empty;
            TargetOutputFileName = string.Empty;
            return;
        }

        try
        {
            string resolved = ResolveOutputPath(SelectedTargetFormat);
            TargetOutputPath = resolved;
            TargetOutputFileName = Path.GetFileName(resolved);
        }
        catch (Exception)
        {
            TargetOutputPath = string.Empty;
            TargetOutputFileName = string.Empty;
        }
    }

    private string ResolveOutputPath(MediaFormat targetFormat)
    {
        string? customDir = UseCustomOutputDirectory && !string.IsNullOrWhiteSpace(CustomOutputDirectory)
            ? CustomOutputDirectory.Trim()
            : null;

        return OutputNameResolver.ResolveUniqueOutputPath(
            InputFilePath,
            targetFormat.Extension,
            customDir,
            _fileExists);
    }

    private void RefreshDiskSpaceCheck()
    {
        string? directoryToCheck = GetEffectiveOutputDirectory();
        if (string.IsNullOrWhiteSpace(InputFilePath) || string.IsNullOrWhiteSpace(directoryToCheck))
        {
            ClearDiskSpaceWarning();
            return;
        }

        try
        {
            DiskSpaceCheckResult check = _diskSpaceChecker(InputFilePath, directoryToCheck);
            ShowDiskWarning = check.ShowLowSpaceWarning;
            HasCriticalDiskSpace = check.HasCriticalLowSpace;
            DiskWarningMessage = check.WarningMessage ?? string.Empty;
        }
        catch (Exception)
        {
            ClearDiskSpaceWarning();
        }
    }

    private string? GetEffectiveOutputDirectory()
    {
        if (UseCustomOutputDirectory && !string.IsNullOrWhiteSpace(CustomOutputDirectory))
        {
            return CustomOutputDirectory.Trim();
        }

        return string.IsNullOrWhiteSpace(InputFilePath)
            ? null
            : Path.GetDirectoryName(InputFilePath);
    }

    private void ClearDiskSpaceWarning()
    {
        ShowDiskWarning = false;
        HasCriticalDiskSpace = false;
        DiskWarningMessage = string.Empty;
    }

    private long SafeGetFileSize(string filePath)
    {
        try
        {
            return _getFileSize(filePath);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static long DefaultGetFileSize(string filePath)
    {
        var info = new FileInfo(filePath);
        return info.Exists ? info.Length : 0;
    }

    private static string GetDisplayFileName(string path)
    {
        string fileName = Path.GetFileName(path);
        return string.IsNullOrEmpty(fileName) ? path : fileName;
    }

    private static string EnsureTrailingSeparator(string directory) =>
        directory.EndsWith(Path.DirectorySeparatorChar) || directory.EndsWith(Path.AltDirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

    private sealed class CallbackProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
