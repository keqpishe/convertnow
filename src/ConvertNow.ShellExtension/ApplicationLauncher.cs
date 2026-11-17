using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConvertNow.ShellExtension;

internal static class ApplicationLauncher
{
    internal static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ConvertNow", "logs", "shell-launch.log");

    internal static ProcessStartInfo CreateStartInfo(string executablePath, string inputPath)
    {
        var info = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
        };
        info.ArgumentList.Add("--file");
        info.ArgumentList.Add(inputPath);
        return info;
    }

    internal static void Launch(string executablePath, string inputPath)
    {
        // Delegate activation to the shell instead of directly creating a child of the COM surrogate.
        Process? process = Process.Start(CreateStartInfo(executablePath, inputPath));
        WriteLog($"Launch requested. Executable={executablePath}; PID={process?.Id.ToString() ?? "shell activation"}");
        if (process is not null)
            _ = ObserveStartupAsync(process);
    }

    private static async Task ObserveStartupAsync(Process process)
    {
        using (process)
        {
            try
            {
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(15))
                {
                    process.Refresh();
                    if (process.HasExited)
                    {
                        if (process.ExitCode != 0)
                            ReportFailure($"Convert.Now exited during startup (code 0x{process.ExitCode:X8}).");
                        else
                            WriteLog("Application closed during startup (exit code 0).");
                        return;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                    {
                        WriteLog($"Application window opened after {timer.ElapsedMilliseconds} ms.");
                        return;
                    }

                    await Task.Delay(150).ConfigureAwait(false);
                }

                ReportFailure("Convert.Now did not open a window within 15 seconds. Try opening Convert.Now from the Start menu.");
            }
            catch (Exception ex)
            {
                WriteLog($"Could not observe application startup: {ex}");
            }
        }
    }

    internal static void ReportFailure(string message)
    {
        WriteLog($"ERROR: {message}");
        // A failed Invoke HRESULT alone is silently discarded by Explorer.
        MessageBoxW(IntPtr.Zero, $"Convert.Now could not open.\n\n{message}\n\nDetails: {LogPath}",
            "Convert.Now", 0x00000010 | 0x00010000);
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not prevent activation or cross the COM exception boundary.
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
