<#
.SYNOPSIS
    Registers the Convert.Now Sparse MSIX package with an external application directory.

.DESCRIPTION
    Calls `Add-AppxPackage -Path <msix> -ExternalLocation <dir>` to grant package identity
    and register the Windows 10/11 File Explorer context menu (`IExplorerCommand`) and COM
    surrogate server declared in AppxManifest.xml.

    PREREQUISITES:
    1. The .msix package MUST be signed with a certificate trusted on this machine
       (e.g., imported into Cert:\LocalMachine\TrustedPeople as Administrator).
    2. Windows Developer Mode (or Sideloading) must be enabled in Settings.
    3. The ExternalLocation folder MUST contain both:
         - ConvertNow.exe
         - ConvertNow.ShellExtension.comhost.dll (plus ConvertNow.ShellExtension.dll,
           deps.json, runtimeconfig.json, and Assets\context-menu-icon.ico)

.EXAMPLE
    .\Register-SparsePackage.ps1 -PackagePath ..\out\ConvertNow.msix -ExternalLocation C:\Staging\ConvertNow
#>
[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'out\ConvertNow.msix'),
    [Parameter(Mandatory)]
    [string]$ExternalLocation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PackagePath)) {
    throw "MSIX package not found at '$PackagePath'. Run Build-SparsePackage.ps1 first."
}

if (-not (Test-Path $ExternalLocation)) {
    throw "ExternalLocation directory not found: '$ExternalLocation'"
}

$resolvedPackage = (Resolve-Path $PackagePath).Path
$resolvedExternal = (Resolve-Path $ExternalLocation).Path

$expectedFiles = @(
    'ConvertNow.exe',
    'ConvertNow.ShellExtension.comhost.dll',
    'ConvertNow.ShellExtension.dll'
)

$missingFiles = $expectedFiles | Where-Object { -not (Test-Path (Join-Path $resolvedExternal $_)) }
if ($missingFiles.Count -gt 0) {
    Write-Warning ("ExternalLocation is missing expected runtime files: {0}. " +
        "Ensure ConvertNow.App and ConvertNow.ShellExtension outputs are staged together.") -f ($missingFiles -join ', ')
}

$signature = Get-AuthenticodeSignature -FilePath $resolvedPackage
if ($signature.Status -ne 'Valid') {
    Write-Warning ("Package signature status is '$($signature.Status)'. " +
        "Add-AppxPackage will fail unless the package is signed with a trusted certificate " +
        "(see New-DevCertificate.ps1 and import the .cer into Cert:\LocalMachine\TrustedPeople as Administrator).")
}

Write-Host "Registering sparse MSIX package..."
Write-Host "  PackagePath     : $resolvedPackage"
Write-Host "  ExternalLocation: $resolvedExternal"

Add-AppxPackage -Path $resolvedPackage -ExternalLocation $resolvedExternal

Write-Host 'Registration succeeded. If File Explorer does not show "Convert now" immediately, restart explorer.exe.'
