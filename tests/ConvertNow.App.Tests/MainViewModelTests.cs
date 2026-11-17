using ConvertNow.App.Services;
using ConvertNow.App.ViewModels;
using ConvertNow.Core;

namespace ConvertNow.App.Tests;

public class MainViewModelTests
{
    [Fact]
    public void LoadFile_NullOrWhitespace_TransitionsToNoFile()
    {
        MainViewModel vm = CreateViewModel();

        vm.LoadFile(null);
        Assert.Equal(ConversionViewState.NoFile, vm.State);

        vm.LoadFile("   ");
        Assert.Equal(ConversionViewState.NoFile, vm.State);
        Assert.False(vm.ConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseInputFileCommand_LoadsPickedFileOrKeepsNoFileOnCancel()
    {
        var filePicker = new FakeFilePickerService { NextFile = null };
        MainViewModel vm = CreateViewModel(filePicker: filePicker, fileExists: ExistingOnly(@"C:\Videos\clip.mp4"));

        await vm.BrowseInputFileCommand.ExecuteAsync(null);
        Assert.Equal(ConversionViewState.NoFile, vm.State);

        filePicker.NextFile = @"C:\Videos\clip.mp4";
        await vm.BrowseInputFileCommand.ExecuteAsync(null);

        Assert.Equal(ConversionViewState.FormatSelection, vm.State);
        Assert.Equal("clip.mp4", vm.InputFileName);
    }

    [Fact]
    public void LoadFile_UnsupportedFormat_TransitionsToUnsupportedState()
    {
        MainViewModel vm = CreateViewModel(fileExists: ExistingOnly(@"C:\Tools\app.exe"));

        vm.LoadFile(@"C:\Tools\app.exe");

        Assert.Equal(ConversionViewState.Unsupported, vm.State);
        Assert.Equal("app.exe", vm.InputFileName);
        Assert.Equal(
            "The file \"app.exe\" cannot be converted. Convert.Now supports video, audio, and image files.",
            vm.UnsupportedMessage);
        Assert.Empty(vm.AvailableFormats);
        Assert.Null(vm.SelectedTargetFormat);
        Assert.False(vm.ConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadFile_MissingFile_TransitionsToErrorAndRetryReloadsWhenFileAppears()
    {
        bool exists = false;
        MainViewModel vm = CreateViewModel(
            fileExists: path => exists && path.Equals(@"C:\Videos\missing.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\missing.mp4");

        Assert.Equal(ConversionViewState.Error, vm.State);
        Assert.Contains("File not found", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.RetryCommand.CanExecute(null));

        exists = true;
        await vm.RetryCommand.ExecuteAsync(null);

        Assert.Equal(ConversionViewState.FormatSelection, vm.State);
        Assert.Equal("missing.mp4", vm.InputFileName);
    }

    [Fact]
    public void LoadFile_SupportedFormats_PopulatesCompatibleFormatsAndDefaults()
    {
        MainViewModel vm = CreateViewModel(
            fileExists: ExistingOnly(@"C:\Videos\movie.mp4", @"C:\Audio\track.mp3", @"C:\Images\photo.png"),
            getFileSize: _ => 1_572_864);

        vm.LoadFile(@"C:\Videos\movie.mp4");
        Assert.Equal(ConversionViewState.FormatSelection, vm.State);
        Assert.Equal("movie.mp4", vm.InputFileName);
        Assert.Equal("1.5 MB", vm.InputFileSizeText);
        Assert.Equal(FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Video), vm.AvailableFormats);
        Assert.Equal("avi", vm.SelectedTargetFormat?.Extension);
        Assert.Equal(@"C:\Videos\movie.avi", vm.TargetOutputPath);
        Assert.Equal("movie.avi", vm.TargetOutputFileName);
        Assert.True(vm.ConvertCommand.CanExecute(null));

        vm.LoadFile(@"C:\Audio\track.mp3");
        Assert.Equal(FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Audio), vm.AvailableFormats);
        Assert.Equal("wav", vm.SelectedTargetFormat?.Extension);

        vm.LoadFile(@"C:\Images\photo.png");
        Assert.Equal(FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Image), vm.AvailableFormats);
        Assert.Equal("jpg", vm.SelectedTargetFormat?.Extension);
    }

    [Fact]
    public void OutputNameCollision_UsesNumericSuffixInTargetPreview()
    {
        MainViewModel vm = CreateViewModel(
            fileExists: path =>
                path.Equals(@"C:\Videos\movie.mp4", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(@"C:\Videos\movie.avi", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\movie.mp4");

        Assert.Equal(@"C:\Videos\movie (1).avi", vm.TargetOutputPath);
        Assert.Equal("movie (1).avi", vm.TargetOutputFileName);
    }

    [Fact]
    public async Task CustomSaveLocationToggleAndBrowse_UpdatesOutputPathAndRechecksDiskSpace()
    {
        var folderPicker = new FakeFolderPickerService { NextFolder = @"D:\Exports" };
        var checkedDirs = new List<string>();
        MainViewModel vm = CreateViewModel(
            folderPicker: folderPicker,
            fileExists: path => path.Equals(@"C:\Videos\movie.mp4", StringComparison.OrdinalIgnoreCase),
            diskSpaceChecker: (_, dir) =>
            {
                checkedDirs.Add(dir);
                return dir.StartsWith(@"D:", StringComparison.OrdinalIgnoreCase)
                    ? new DiskSpaceCheckResult(
                        HasCriticalLowSpace: false,
                        ShowLowSpaceWarning: true,
                        AvailableBytes: 1_200,
                        RecommendedBytes: 1_500,
                        InputFileSizeBytes: 1_000,
                        WarningMessage: "Low disk space on D:")
                    : new DiskSpaceCheckResult(false, false, 10_000_000, 1_500, 1_000, null);
            });

        vm.LoadFile(@"C:\Videos\movie.mp4");
        Assert.False(vm.UseCustomOutputDirectory);
        Assert.False(vm.IsCustomPathInputVisible);
        Assert.False(vm.ShowDiskWarning);
        Assert.Equal([@"C:\Videos"], checkedDirs);

        vm.UseCustomOutputDirectory = true;
        Assert.True(vm.IsCustomPathInputVisible);
        Assert.Equal(@"C:\Videos", vm.CustomOutputDirectory);

        await vm.BrowseOutputFolderCommand.ExecuteAsync(null);
        Assert.Equal(@"D:\Exports", vm.CustomOutputDirectory);
        Assert.Equal(@"D:\Exports\movie.avi", vm.TargetOutputPath);
        Assert.True(vm.ShowDiskWarning);
        Assert.False(vm.HasCriticalDiskSpace);
        Assert.Equal("Low disk space on D:", vm.DiskWarningMessage);
        Assert.True(vm.ConvertCommand.CanExecute(null));

        vm.UseCustomOutputDirectory = false;
        Assert.False(vm.IsCustomPathInputVisible);
        Assert.Equal(@"C:\Videos\movie.avi", vm.TargetOutputPath);
        Assert.False(vm.ShowDiskWarning);
    }

    [Fact]
    public async Task ConvertCommand_ReportsProgressAndEta_PreventsAndRestoresSleep_AndTransitionsToCompleted()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        var shell = new SpyShellService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            shell: shell,
            fileExists: path => path.Equals(@"C:\Videos\video.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\video.mp4");

        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);

        Assert.Equal(ConversionViewState.Converting, vm.State);
        Assert.True(vm.IsConverting);
        Assert.Equal("video.mp4 → video.avi", vm.ConversionPairText);
        Assert.Equal(1, executionState.PreventSleepCount);
        Assert.Equal(0, executionState.RestoreSleepCount);
        Assert.True(executionState.IsSleepPrevented);
        Assert.NotNull(runner.LastJob);
        Assert.Equal(@"C:\Videos\video.mp4", runner.LastJob!.InputFilePath);
        Assert.Equal(@"C:\Videos\video.avi", runner.LastJob.OutputFilePath);
        Assert.Equal("avi", runner.LastJob.TargetFormat.Extension);
        Assert.Equal(FormatCategory.Video, runner.LastJob.SourceCategory);

        runner.ReportProgress(new ConversionProgress(
            Percentage: 62.4,
            ProcessedTime: TimeSpan.FromSeconds(100),
            TotalDuration: TimeSpan.FromSeconds(160),
            EstimatedRemaining: new TimeSpan(0, 2, 34),
            SpeedText: "2.4x",
            IsIndeterminate: false));

        Assert.False(vm.IsProgressIndeterminate);
        Assert.Equal(62.4, vm.ProgressPercentage);
        Assert.Equal("62%", vm.ProgressPercentageText);
        Assert.Equal("ETA: 2m 34s", vm.EtaText);
        Assert.Equal("2.4x", vm.SpeedText);

        runner.Complete(new ConversionResult(
            Success: true,
            IsCancelled: false,
            OutputFilePath: @"C:\Videos\video.avi",
            OutputFileSizeBytes: 4_509_715_660,
            Elapsed: TimeSpan.FromSeconds(150),
            ErrorMessage: null));

        await convertTask;

        Assert.Equal(ConversionViewState.Completed, vm.State);
        Assert.False(vm.IsConverting);
        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
        Assert.Equal(100, vm.ProgressPercentage);
        Assert.Equal("100%", vm.ProgressPercentageText);
        Assert.Equal("video.avi (4.2 GB)", vm.CompletedOutputSummary);
        Assert.Equal(@"Saved to: C:\Videos\", vm.CompletedDirectoryText);

        Assert.True(vm.OpenFolderCommand.CanExecute(null));
        vm.OpenFolderCommand.Execute(null);
        Assert.Equal([@"C:\Videos\video.avi"], shell.OpenedPaths);
    }

    [Fact]
    public async Task ConvertCommand_WhenConversionFails_TransitionsToError_RestoresSleep_AndRetrySucceeds()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            fileExists: path => path.Equals(@"C:\Videos\video.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\video.mp4");

        Task firstRun = vm.ConvertCommand.ExecuteAsync(null);
        runner.Complete(new ConversionResult(
            Success: false,
            IsCancelled: false,
            OutputFilePath: @"C:\Videos\video.avi",
            OutputFileSizeBytes: 0,
            Elapsed: TimeSpan.FromSeconds(2),
            ErrorMessage: "Codec not supported for this format combination."));

        await firstRun;

        Assert.Equal(ConversionViewState.Error, vm.State);
        Assert.Equal("Error: Codec not supported for this format combination.", vm.ErrorDisplayText);
        Assert.Equal(1, executionState.PreventSleepCount);
        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
        Assert.True(vm.RetryCommand.CanExecute(null));

        Task retryRun = vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(ConversionViewState.Converting, vm.State);
        Assert.Equal(2, executionState.PreventSleepCount);
        Assert.True(executionState.IsSleepPrevented);

        runner.Complete(new ConversionResult(
            Success: true,
            IsCancelled: false,
            OutputFilePath: @"C:\Videos\video.avi",
            OutputFileSizeBytes: 2_048,
            Elapsed: TimeSpan.FromSeconds(5),
            ErrorMessage: null));

        await retryRun;

        Assert.Equal(ConversionViewState.Completed, vm.State);
        Assert.Equal(2, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
    }

    [Fact]
    public async Task ConvertCommand_WhenRunnerThrows_TransitionsToErrorAndRestoresSleep()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            fileExists: path => path.Equals(@"C:\Videos\video.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\video.mp4");

        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);
        runner.Fail(new InvalidOperationException("Unexpected failure"));

        await convertTask;

        Assert.Equal(ConversionViewState.Error, vm.State);
        Assert.Equal("Error: Unexpected failure", vm.ErrorDisplayText);
        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
    }

    [Fact]
    public async Task CancelCommand_CancelsConversion_RestoresSleep_AndReturnsToFormatSelection()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            fileExists: path => path.Equals(@"C:\Videos\video.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\video.mp4");

        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);
        Assert.True(vm.CancelCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);

        Assert.True(vm.IsCancelling);
        Assert.Equal("Cancelling...", vm.ConvertingStatusText);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.True(runner.LastCancellationToken.IsCancellationRequested);

        runner.Complete(new ConversionResult(
            Success: false,
            IsCancelled: true,
            OutputFilePath: @"C:\Videos\video.avi",
            OutputFileSizeBytes: 0,
            Elapsed: TimeSpan.FromSeconds(1),
            ErrorMessage: "Conversion was cancelled."));

        await convertTask;

        Assert.Equal(ConversionViewState.FormatSelection, vm.State);
        Assert.False(vm.IsCancelling);
        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
    }

    [Fact]
    public async Task CloseCommand_DuringConversion_CancelsAndWaitsBeforeClosing()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        var windowLifecycle = new SpyWindowLifecycleService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            windowLifecycle: windowLifecycle,
            fileExists: path => path.Equals(@"C:\Videos\video.mp4", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Videos\video.mp4");

        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);
        Task closeTask = vm.CloseCommand.ExecuteAsync(null);

        Assert.True(runner.LastCancellationToken.IsCancellationRequested);
        Assert.Equal(0, windowLifecycle.CloseRequestCount);

        runner.Complete(new ConversionResult(
            Success: false,
            IsCancelled: true,
            OutputFilePath: @"C:\Videos\video.avi",
            OutputFileSizeBytes: 0,
            Elapsed: TimeSpan.FromSeconds(1),
            ErrorMessage: "Conversion was cancelled."));

        await Task.WhenAll(convertTask, closeTask);

        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
        Assert.Equal(1, windowLifecycle.CloseRequestCount);
    }

    [Fact]
    public async Task ConvertCommand_IndeterminateProgress_And_LateProgressAfterCompletion_AreHandledSafely()
    {
        var runner = new FakeFFmpegRunner();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            fileExists: path => path.Equals(@"C:\Images\photo.png", StringComparison.OrdinalIgnoreCase));

        vm.LoadFile(@"C:\Images\photo.png");

        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);

        runner.ReportProgress(new ConversionProgress(
            Percentage: 0,
            ProcessedTime: TimeSpan.Zero,
            TotalDuration: null,
            EstimatedRemaining: null,
            SpeedText: null,
            IsIndeterminate: true));

        Assert.True(vm.IsProgressIndeterminate);
        Assert.Equal("...", vm.ProgressPercentageText);
        Assert.Equal("ETA: --", vm.EtaText);

        runner.Complete(new ConversionResult(
            Success: true,
            IsCancelled: false,
            OutputFilePath: @"C:\Images\photo.jpg",
            OutputFileSizeBytes: 512_000,
            Elapsed: TimeSpan.FromSeconds(1),
            ErrorMessage: null));

        await convertTask;

        Assert.Equal(ConversionViewState.Completed, vm.State);
        Assert.Equal(100, vm.ProgressPercentage);
        Assert.Equal("100%", vm.ProgressPercentageText);

        // Late progress callback after completion must be ignored.
        runner.ReportProgress(new ConversionProgress(50, TimeSpan.Zero, null, null, null, false));
        Assert.Equal(100, vm.ProgressPercentage);
        Assert.Equal(ConversionViewState.Completed, vm.State);
    }

    [Fact]
    public void CriticalDiskSpace_SetsHasCriticalDiskSpaceWithoutBlockingConversion()
    {
        MainViewModel vm = CreateViewModel(
            fileExists: ExistingOnly(@"C:\Videos\clip.mp4"),
            diskSpaceChecker: (_, _) => new DiskSpaceCheckResult(
                HasCriticalLowSpace: true,
                ShowLowSpaceWarning: true,
                AvailableBytes: 500,
                RecommendedBytes: 1_500,
                InputFileSizeBytes: 1_000,
                WarningMessage: "Critically low disk space."));

        vm.LoadFile(@"C:\Videos\clip.mp4");

        Assert.True(vm.ShowDiskWarning);
        Assert.True(vm.HasCriticalDiskSpace);
        Assert.Equal("Critically low disk space.", vm.DiskWarningMessage);
        Assert.True(vm.ConvertCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedTargetFormatChanged_UpdatesTargetOutputFileNameAndPath()
    {
        MainViewModel vm = CreateViewModel(fileExists: ExistingOnly(@"C:\Videos\clip.mp4"));

        vm.LoadFile(@"C:\Videos\clip.mp4");
        MediaFormat mp3Format = vm.AvailableFormats.First(f => f.Extension == "mp3");
        vm.SelectedTargetFormat = mp3Format;

        Assert.Equal(@"C:\Videos\clip.mp3", vm.TargetOutputPath);
        Assert.Equal("clip.mp3", vm.TargetOutputFileName);
    }

    [Fact]
    public async Task CancelActiveConversionAsync_And_LoadFileDuringConversion_BehaveCorrectly()
    {
        var runner = new FakeFFmpegRunner();
        var executionState = new SpyExecutionStateService();
        MainViewModel vm = CreateViewModel(
            ffmpegRunner: runner,
            executionState: executionState,
            fileExists: ExistingOnly(@"C:\Videos\clip.mp4", @"C:\Audio\other.mp3"));

        vm.LoadFile(@"C:\Videos\clip.mp4");
        Task convertTask = vm.ConvertCommand.ExecuteAsync(null);

        // LoadFile while converting should be ignored.
        vm.LoadFile(@"C:\Audio\other.mp3");
        Assert.Equal(ConversionViewState.Converting, vm.State);
        Assert.Equal("clip.mp4", vm.InputFileName);

        Task cancelCleanupTask = vm.CancelActiveConversionAsync();
        Assert.True(runner.LastCancellationToken.IsCancellationRequested);

        runner.Complete(new ConversionResult(
            Success: false,
            IsCancelled: true,
            OutputFilePath: @"C:\Videos\clip.avi",
            OutputFileSizeBytes: 0,
            Elapsed: TimeSpan.Zero,
            ErrorMessage: "Conversion was cancelled."));

        await Task.WhenAll(convertTask, cancelCleanupTask);

        Assert.Equal(1, executionState.RestoreSleepCount);
        Assert.False(executionState.IsSleepPrevented);
        Assert.Equal(ConversionViewState.FormatSelection, vm.State);
    }

    [Fact]
    public async Task CloseCommand_InNonConvertingStates_RequestsWindowClose()
    {
        var windowLifecycle = new SpyWindowLifecycleService();
        MainViewModel vm = CreateViewModel(windowLifecycle: windowLifecycle, fileExists: ExistingOnly(@"C:\Tools\readme.txt"));

        await vm.CloseCommand.ExecuteAsync(null);
        vm.LoadFile(@"C:\Tools\readme.txt");
        await vm.CloseCommand.ExecuteAsync(null);

        Assert.Equal(ConversionViewState.Unsupported, vm.State);
        Assert.Equal(2, windowLifecycle.CloseRequestCount);
    }

    private static Func<string, bool> ExistingOnly(params string[] paths)
    {
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        return path => set.Contains(path);
    }

    private static MainViewModel CreateViewModel(
        IFFmpegRunner? ffmpegRunner = null,
        IFolderPickerService? folderPicker = null,
        IFilePickerService? filePicker = null,
        IExecutionStateService? executionState = null,
        IShellService? shell = null,
        IWindowLifecycleService? windowLifecycle = null,
        Func<string, bool>? fileExists = null,
        Func<string, long>? getFileSize = null,
        Func<string, string, DiskSpaceCheckResult>? diskSpaceChecker = null)
    {
        return new MainViewModel(
            ffmpegRunner ?? new FakeFFmpegRunner(),
            new ImmediateDispatcherService(),
            folderPicker ?? new FakeFolderPickerService(),
            filePicker ?? new FakeFilePickerService(),
            executionState ?? new SpyExecutionStateService(),
            shell ?? new SpyShellService(),
            windowLifecycle ?? new SpyWindowLifecycleService(),
            fileExists ?? (_ => false),
            getFileSize ?? (_ => 1_024),
            diskSpaceChecker ?? ((_, _) => new DiskSpaceCheckResult(false, false, 10_000_000_000, 1_536, 1_024, null)));
    }

    private sealed class ImmediateDispatcherService : IDispatcherService
    {
        public void Enqueue(Action action) => action();

        public Task EnqueueAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFolderPickerService : IFolderPickerService
    {
        public string? NextFolder { get; set; }

        public Task<string?> PickFolderAsync() => Task.FromResult(NextFolder);
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public string? NextFile { get; set; }

        public Task<string?> PickInputFileAsync() => Task.FromResult(NextFile);
    }

    private sealed class SpyExecutionStateService : IExecutionStateService
    {
        public int PreventSleepCount { get; private set; }
        public int RestoreSleepCount { get; private set; }
        public bool IsSleepPrevented { get; private set; }

        public void PreventSleep()
        {
            PreventSleepCount++;
            IsSleepPrevented = true;
        }

        public void RestoreSleep()
        {
            RestoreSleepCount++;
            IsSleepPrevented = false;
        }
    }

    private sealed class SpyShellService : IShellService
    {
        public List<string> OpenedPaths { get; } = [];

        public void OpenFolderAndSelectFile(string filePath) => OpenedPaths.Add(filePath);
    }

    private sealed class SpyWindowLifecycleService : IWindowLifecycleService
    {
        public int CloseRequestCount { get; private set; }

        public void RequestClose() => CloseRequestCount++;
    }

    private sealed class FakeFFmpegRunner : IFFmpegRunner
    {
        private TaskCompletionSource<ConversionResult>? _tcs;
        private IProgress<ConversionProgress>? _progress;

        public ConversionJob? LastJob { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<TimeSpan?> ProbeDurationAsync(string inputFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<TimeSpan?>(TimeSpan.FromSeconds(120));

        public Task<ConversionResult> ConvertAsync(
            ConversionJob job,
            IProgress<ConversionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastJob = job;
            LastCancellationToken = cancellationToken;
            _progress = progress;
            _tcs = new TaskCompletionSource<ConversionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _tcs.Task;
        }

        public void ReportProgress(ConversionProgress progress) => _progress?.Report(progress);

        public void Complete(ConversionResult result) => _tcs?.TrySetResult(result);

        public void Fail(Exception exception) => _tcs?.TrySetException(exception);
    }
}
