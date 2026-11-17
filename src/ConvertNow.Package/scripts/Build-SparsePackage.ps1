<#
.SYNOPSIS
    Builds (and optionally signs) the Convert.Now Sparse MSIX package.

.DESCRIPTION
    Locates makeappx.exe (and signtool.exe when signing is requested) from:
      1. %USERPROFILE%\.nuget\packages\microsoft.windows.sdk.buildtools
      2. C:\Program Files (x86)\Windows Kits\10\bin
      3. PATH

    Packs AppxManifest.xml and Assets via mapping.txt using `makeappx.exe pack /nv`
    (/nv is required for Sparse MSIX packages whose binaries reside in ExternalLocation).

.EXAMPLE
    .\Build-SparsePackage.ps1
    Builds an unsigned sparse package at src\ConvertNow.Package\out\ConvertNow.msix.

.EXAMPLE
    .\Build-SparsePackage.ps1 -CertificatePath ..\certs\ConvertNowDev.pfx -CertificatePassword (ConvertTo-SecureString 'ConvertNowDev!' -AsPlainText -Force)
    Builds and signs ConvertNow.msix using the provided PFX certificate.
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'out\ConvertNow.msix'),
    [ValidateSet('x64', 'x86', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$CertificatePath,
    $CertificatePassword,
    [string]$CertThumbprint,
    [string]$TimestampUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-WindowsSdkTool {
    param(
        [Parameter(Mandatory)]
        [string]$ToolName,
        [Parameter(Mandatory)]
        [string]$Arch
    )

    $candidates = New-Object System.Collections.Generic.List[string]
    $versionSort = {
        if ($_.FullName -match '(\d+\.\d+\.\d+\.\d+)') {
            [version]$Matches[1]
        }
        else {
            [version]'0.0.0.0'
        }
    }

    $nugetRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    if (Test-Path $nugetRoot) {
        Get-ChildItem -Path $nugetRoot -Recurse -Filter $ToolName -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "[\\/]$Arch[\\/]" } |
            Sort-Object $versionSort -Descending |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kitsRoot) {
        Get-ChildItem -Path $kitsRoot -Recurse -Filter $ToolName -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "[\\/]$Arch[\\/]" } |
            Sort-Object $versionSort -Descending |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    $cmd = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        return $cmd.Source
    }

    return $null
}

$packageRoot = Split-Path $PSScriptRoot -Parent
$manifestPath = Join-Path $packageRoot 'AppxManifest.xml'
$mappingPath = Join-Path $packageRoot 'mapping.txt'

if (-not (Test-Path $manifestPath)) {
    throw "AppxManifest.xml not found at: $manifestPath"
}

if (-not (Test-Path $mappingPath)) {
    throw "mapping.txt not found at: $mappingPath"
}

$makeAppx = Find-WindowsSdkTool -ToolName 'makeappx.exe' -Arch $Architecture
if ([string]::IsNullOrWhiteSpace($makeAppx)) {
    throw 'Could not locate makeappx.exe in %USERPROFILE%\.nuget\packages\microsoft.windows.sdk.buildtools, Windows Kits 10, or PATH.'
}

$outputDir = Split-Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)

Write-Host "Using makeappx.exe: $makeAppx"
Write-Host "Packing sparse MSIX -> $resolvedOutputPath"

Push-Location $packageRoot
try {
    # /nv skips payload file-existence checks because ConvertNow.exe and
    # ConvertNow.ShellExtension.comhost.dll live in ExternalLocation at registration time.
    & $makeAppx pack /v /o /nv /h SHA256 /f $mappingPath /p $resolvedOutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx.exe failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$shouldSign = -not [string]::IsNullOrWhiteSpace($CertificatePath) -or -not [string]::IsNullOrWhiteSpace($CertThumbprint)
if ($shouldSign) {
    $signTool = Find-WindowsSdkTool -ToolName 'signtool.exe' -Arch $Architecture
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw 'Could not locate signtool.exe to sign the MSIX package.'
    }

    Write-Host "Using signtool.exe: $signTool"
    $signArgs = @('sign', '/fd', 'SHA256')

    if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
        if (-not (Test-Path $CertificatePath)) {
            throw "Certificate file not found: $CertificatePath"
        }

        $signArgs += @('/f', [System.IO.Path]::GetFullPath($CertificatePath))

        if ($PSBoundParameters.ContainsKey('CertificatePassword') -and $null -ne $CertificatePassword) {
            $plainPassword = if ($CertificatePassword -is [System.Security.SecureString]) {
                $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertificatePassword)
                try {
                    [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
                }
                finally {
                    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
                }
            } else {
                [string]$CertificatePassword
            }
            if (-not [string]::IsNullOrEmpty($plainPassword)) {
                $signArgs += @('/p', $plainPassword)
            }
        }
    }
    else {
        $signArgs += @('/sha1', $CertThumbprint)
    }

    if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
        $signArgs += @('/tr', $TimestampUrl, '/td', 'SHA256')
    }

    $signArgs += $resolvedOutputPath

    & $signTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }

    Write-Host "Successfully built and signed: $resolvedOutputPath"
}
else {
    Write-Host "Successfully built unsigned sparse MSIX: $resolvedOutputPath"
    Write-Host 'Note: Sign the package with -CertificatePath or -CertThumbprint before calling Add-AppxPackage.'
}

Get-Item $resolvedOutputPath
