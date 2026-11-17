[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\src\ConvertNow.ShellExtension\bin\x64\Release\net10.0-windows\win-x64'),
    [string]$ApplicationDirectory = (Join-Path $PSScriptRoot '..\src\ConvertNow.App\bin\x64\Release\net10.0-windows10.0.22621.0'),
    [string]$ResultPath = (Join-Path $PSScriptRoot '..\dist\shell-repair-result.json')
)

$ErrorActionPreference = 'Stop'
try {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this repair as Administrator to update the installed extension.'
    }

    $installDir = (Get-ItemProperty -LiteralPath 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ConvertNow').InstallLocation
    $installDir = (Resolve-Path -LiteralPath $installDir).Path
    if (-not (Test-Path -LiteralPath (Join-Path $installDir 'ConvertNow.exe') -PathType Leaf)) {
        throw 'The registered install directory does not contain ConvertNow.exe.'
    }

    $source = (Resolve-Path -LiteralPath (Join-Path $SourceDirectory 'ConvertNow.ShellExtension.dll')).Path
    $destination = Join-Path $installDir 'ConvertNow.ShellExtension.dll'
    $backupDir = Join-Path (Split-Path $ResultPath -Parent) ('shell-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination $backupDir
    foreach ($name in @('ConvertNow.dll', 'ConvertNow.exe', 'ConvertNow.msix')) {
        Copy-Item -LiteralPath (Join-Path $installDir $name) -Destination $backupDir
    }
    if (Get-Process -Name ConvertNow -ErrorAction SilentlyContinue) {
        throw 'Close Convert.Now before updating the installed application.'
    }

    # Only stop the surrogate that has this extension loaded. Leave Explorer and other COM servers running.
    foreach ($candidate in Get-Process -Name dllhost -ErrorAction SilentlyContinue) {
        $containsExtension = $false
        try {
            $containsExtension = @($candidate.Modules | Where-Object { $_.FileName -ieq $destination }).Count -gt 0
        } catch { }
        if ($containsExtension) {
            Stop-Process -Id $candidate.Id -ErrorAction Stop
            $candidate.WaitForExit(5000) | Out-Null
        }
    }

    Copy-Item -LiteralPath $source -Destination $destination -Force
    foreach ($name in @('ConvertNow.dll', 'ConvertNow.exe')) {
        Copy-Item -LiteralPath (Join-Path $ApplicationDirectory $name) -Destination (Join-Path $installDir $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\src\ConvertNow.Package\out\ConvertNow.msix') -Destination (Join-Path $installDir 'ConvertNow.msix') -Force
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHash) {
        throw 'The installed extension did not match the repaired build.'
    }
    Add-AppxPackage -Path (Join-Path $installDir 'ConvertNow.msix') -ExternalLocation $installDir -ErrorAction Stop
    @{ Success = $true; InstalledFile = $destination; BackupDirectory = $backupDir; SHA256 = $sourceHash } |
        ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
} catch {
    @{ Success = $false; Error = $_.Exception.Message } |
        ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    exit 1
}
