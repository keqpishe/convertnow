[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CertificateThumbprint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed (exit $LASTEXITCODE)." }
}

foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot "tools/ffmpeg/$name") -PathType Leaf)) {
        throw "Missing tools/ffmpeg/$name. Supply local FFmpeg binaries before building the installer."
    }
}
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
$manifest = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'src/ConvertNow.Package/AppxManifest.xml') -Raw)
if (-not $certificate.HasPrivateKey -or $certificate.Subject -ne $manifest.Package.Identity.Publisher) {
    throw 'The signing certificate must have a private key and match the MSIX publisher.'
}

Push-Location $repoRoot
try {
    Invoke-Dotnet -Arguments @('build', 'src/ConvertNow.App/ConvertNow.App.csproj', '-c', 'Release', '-p:Platform=x64')
    Invoke-Dotnet -Arguments @('build', 'src/ConvertNow.ShellExtension/ConvertNow.ShellExtension.csproj', '-c', 'Release', '-p:Platform=x64')
    & (Join-Path $repoRoot 'src/ConvertNow.Package/scripts/Build-SparsePackage.ps1') -CertThumbprint $CertificateThumbprint | Out-Host

    $buildDirectory = Join-Path $repoRoot ('artifacts/installer-' + [Guid]::NewGuid().ToString('N'))
    $stageDirectory = Join-Path $buildDirectory 'payload'
    New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
    $appOutput = Join-Path $repoRoot 'src/ConvertNow.App/bin/x64/Release/net10.0-windows10.0.22621.0'
    $shellOutput = Join-Path $repoRoot 'src/ConvertNow.ShellExtension/bin/x64/Release/net10.0-windows/win-x64'
    foreach ($item in Get-ChildItem -LiteralPath $appOutput) {
        Copy-Item -LiteralPath $item.FullName -Destination $stageDirectory -Recurse -Force
    }
    foreach ($item in Get-ChildItem -LiteralPath $shellOutput -File) {
        Copy-Item -LiteralPath $item.FullName -Destination $stageDirectory -Force
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/ConvertNow.Package/out/ConvertNow.msix') -Destination $stageDirectory
    Export-Certificate -Cert $certificate -FilePath (Join-Path $stageDirectory 'ConvertNowDev.cer') -Force | Out-Null

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $newPayload = Join-Path $buildDirectory 'payload.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($stageDirectory, $newPayload)
    $payloadPath = Join-Path $repoRoot 'src/ConvertNow.Installer/Resources/payload.zip'
    New-Item -ItemType Directory -Path (Split-Path $payloadPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $newPayload -Destination $payloadPath -Force

    Invoke-Dotnet -Arguments @('publish', 'src/ConvertNow.Installer/ConvertNow.Installer.csproj', '-c', 'Release', '-p:Platform=x64', '-o', 'dist/installer')
    Write-Host ('Installer: ' + (Join-Path $repoRoot 'dist/installer/ConvertNow-Setup.exe'))
} finally {
    Pop-Location
}
