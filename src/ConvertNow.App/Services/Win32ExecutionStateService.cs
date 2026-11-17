using System.Runtime.InteropServices;

namespace ConvertNow.App.Services;

/// <summary>
/// Prevents Windows from entering sleep during active conversions using Win32 <c>SetThreadExecutionState</c>.
/// </summary>
public sealed class Win32ExecutionStateService : IExecutionStateService
{
    [Flags]
    private enum ExecutionState : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_CONTINUOUS = 0x80000000,
    }

    /// <inheritdoc />
    public void PreventSleep()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = SetThreadExecutionState(ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED);
    }

    /// <inheritdoc />
    public void RestoreSleep()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
}
