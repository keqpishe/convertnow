[CmdletBinding()]
param(
    [string]$InputFile = (Join-Path $PSScriptRoot '..\src\ConvertNow.App\Assets\StoreLogo.png')
)

$ErrorActionPreference = 'Stop'
$installDir = (Get-ItemProperty -LiteralPath 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ConvertNow').InstallLocation
Add-Type -Path (Join-Path $installDir 'ConvertNow.ShellExtension.dll')
Add-Type -ReferencedAssemblies (Join-Path $installDir 'ConvertNow.ShellExtension.dll') -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using ConvertNow.ShellExtension;
public static class RegisteredShellLaunchTest {
    private static object activeCommand;
    public static string Icon;
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IntPtr item);
    [DllImport("shell32.dll")]
    static extern int SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid iid, out IntPtr array);
    public static int Invoke(string path) {
        var itemId = typeof(IShellItem).GUID;
        var arrayId = typeof(IShellItemArray).GUID;
        IntPtr item, array = IntPtr.Zero;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item));
        object command = null;
        try {
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item, ref arrayId, out array));
            var selection = (IShellItemArray)Marshal.GetObjectForIUnknown(array);
            command = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("8F5C9E2A-4D7B-4A1E-9B3C-6E2D8A1F5C40")));
            var explorerCommand = (IExplorerCommand)command;
            string icon;
            explorerCommand.GetIcon(selection, out icon);
            // Keep the COM server alive long enough to collect asynchronous startup diagnostics.
            Icon = icon;
            activeCommand = command;
            return explorerCommand.Invoke(selection, IntPtr.Zero);
        } finally {
            if (array != IntPtr.Zero) Marshal.Release(array);
            Marshal.Release(item);
        }
    }
}
'@
$inputPath = (Resolve-Path -LiteralPath $InputFile).Path
$testStarted = [DateTimeOffset]::Now
$hr = [RegisteredShellLaunchTest]::Invoke($inputPath)
if ($hr -ne 0) { throw ('The registered shell command failed: 0x{0:X8}' -f $hr) }
Write-Host 'Registered COM command returned success. Waiting for startup diagnostics...'
Write-Host ('Registered extension icon: ' + [RegisteredShellLaunchTest]::Icon)
$logPath = Join-Path $env:LOCALAPPDATA 'ConvertNow\logs\shell-launch.log'
$packageLog = Join-Path $env:LOCALAPPDATA 'Packages\ConvertNow.SparsePackage_qqpkce0dfcczj\LocalCache\Local\ConvertNow\logs\shell-launch.log'
$timer = [Diagnostics.Stopwatch]::StartNew()
while ($timer.Elapsed.TotalSeconds -lt 17) {
    foreach ($candidateLog in @($logPath, $packageLog)) {
        if (-not (Test-Path -LiteralPath $candidateLog)) { continue }
        $newLines = @(Get-Content -LiteralPath $candidateLog -Tail 20 | Where-Object {
            [DateTimeOffset]::Parse($_.Split(' ')[0]) -ge $testStarted
        })
        if ($newLines -match 'ERROR:') { throw ($newLines -join [Environment]::NewLine) }
        if ($newLines -match 'Application window opened') {
            $newLines
            Write-Host 'PASS: the registered shell extension opened the application window.'
            exit 0
        }
    }
    Start-Sleep -Milliseconds 150
}
throw 'The registered shell extension did not open an application window within 17 seconds.'
