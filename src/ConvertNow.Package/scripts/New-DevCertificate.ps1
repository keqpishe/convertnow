<#
.SYNOPSIS
    Generates a self-signed code signing certificate for local Convert.Now Sparse MSIX development.

.DESCRIPTION
    Creates a self-signed Code Signing certificate in Cert:\CurrentUser\My with subject
    "CN=ConvertNowDev" (matching AppxManifest.xml Publisher="CN=ConvertNowDev") and exports
    both .cer (public key) and .pfx (private key) files.

    IMPORTANT:
    - Do NOT commit generated .cer or .pfx files to source control (ignored by .gitignore).
    - Installing a sparse MSIX signed with this certificate requires trusting the .cer file
      in Cert:\LocalMachine\TrustedPeople (or Cert:\LocalMachine\Root), which requires
      Administrator privileges. This script does NOT touch LocalMachine stores unless
      -InstallToLocalMachine is explicitly passed in an elevated PowerShell session.

.EXAMPLE
    .\New-DevCertificate.ps1
    Creates certs\ConvertNowDev.cer and certs\ConvertNowDev.pfx using the default dev password.

.EXAMPLE
    .\New-DevCertificate.ps1 -InstallToLocalMachine
    (Run as Administrator) Generates the certificate and imports the .cer into LocalMachine\TrustedPeople.
#>
[CmdletBinding()]
param(
    [string]$Subject = 'CN=ConvertNowDev',
    [string]$FriendlyName = 'Convert.Now Development Certificate',
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'certs'),
    [string]$FilePrefix = 'ConvertNowDev',
    [SecureString]$Password,
    [int]$ValidYears = 3,
    [switch]$InstallToLocalMachine
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $PSBoundParameters.ContainsKey('Password')) {
    # Non-production development password used purely for local test signing.
    $Password = ConvertTo-SecureString -String 'ConvertNowDev!' -AsPlainText -Force
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$cerPath = Join-Path $OutputDirectory "$FilePrefix.cer"
$pfxPath = Join-Path $OutputDirectory "$FilePrefix.pfx"

Write-Host "Creating self-signed code signing certificate ($Subject)..."
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -FriendlyName $FriendlyName `
    -KeyUsage DigitalSignature `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @(
        '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
        '2.5.29.19={text}'
    ) `
    -NotAfter (Get-Date).AddYears($ValidYears)

Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $Password -Force | Out-Null

Write-Host "Certificate created:"
Write-Host "  Thumbprint : $($cert.Thumbprint)"
Write-Host "  Public CER : $cerPath"
Write-Host "  Private PFX: $pfxPath"

if ($InstallToLocalMachine) {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Importing into Cert:\LocalMachine\TrustedPeople requires an elevated (Administrator) PowerShell session.'
    }

    Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    Write-Host 'Imported certificate into Cert:\LocalMachine\TrustedPeople.'
}
else {
    Write-Host ''
    Write-Host 'Next steps for local MSIX sideloading (run in elevated PowerShell when ready):'
    Write-Host "  Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'"
}

[pscustomobject]@{
    Thumbprint = $cert.Thumbprint
    Subject    = $cert.Subject
    CerPath    = $cerPath
    PfxPath    = $pfxPath
}
