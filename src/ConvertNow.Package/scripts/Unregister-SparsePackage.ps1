<#
.SYNOPSIS
    Unregisters the Convert.Now Sparse MSIX package from the current user (or all users).

.DESCRIPTION
    Finds installed packages matching ConvertNow.SparsePackage and removes them using
    Remove-AppxPackage. Does not delete your external application binaries or certificates.

.EXAMPLE
    .\Unregister-SparsePackage.ps1

.EXAMPLE
    .\Unregister-SparsePackage.ps1 -AllUsers
#>
[CmdletBinding()]
param(
    [string]$PackageName = 'ConvertNow.SparsePackage',
    [switch]$AllUsers
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($AllUsers) {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Removing an Appx package for -AllUsers requires an elevated (Administrator) PowerShell session.'
    }

    $packages = @(Get-AppxPackage -Name $PackageName -AllUsers)
}
else {
    $packages = @(Get-AppxPackage -Name $PackageName)
}

if ($packages.Count -eq 0) {
    Write-Host "Package '$PackageName' is not currently registered."
    return
}

foreach ($pkg in $packages) {
    Write-Host "Removing package: $($pkg.PackageFullName)"
    if ($AllUsers) {
        Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers
    }
    else {
        Remove-AppxPackage -Package $pkg.PackageFullName
    }
}

Write-Host "Unregistered '$PackageName'. Restart explorer.exe if needed to clear cached shell entries."
