# Convert.Now

Windows desktop converter for video, audio and images, powered by FFmpeg.
Select a file in File Explorer, choose **Convert now**, select an output format,
and convert it. The app also opens directly and lets you pick a file.

## Requirements

- Windows 10 build 19041 or later / Windows 11, x64.
- .NET 10 SDK for development; .NET 10 runtime for the converter.
- Windows App Runtime 1.8, version `8000.994.2142.0` or newer in the 1.8 family.
- Local `ffmpeg.exe` and `ffprobe.exe` binaries in `tools/ffmpeg/`.
  These binaries, generated installers and signing certificates are excluded from Git.

## Build and test

Run in PowerShell from the repository root:

```powershell
dotnet build src/ConvertNow.App/ConvertNow.App.csproj -p:Platform=x64
dotnet build src/ConvertNow.ShellExtension/ConvertNow.ShellExtension.csproj -p:Platform=x64

dotnet test tests/ConvertNow.Core.Tests/ConvertNow.Core.Tests.csproj
dotnet test tests/ConvertNow.Installer.Tests/ConvertNow.Installer.Tests.csproj
dotnet test tests/ConvertNow.App.Tests/ConvertNow.App.Tests.csproj -p:Platform=x64
```

Core integration tests run real FFmpeg conversions and require both FFmpeg binaries.
Application tests target Windows. Application-control policies can block unsigned
test assemblies; treat that result as a blocked check, not a passing test.

## Build the installer

Create a local development signing certificate (no administrator access needed for
creation). Keep its private key out of source control:

```powershell
$certificate = & ./src/ConvertNow.Package/scripts/New-DevCertificate.ps1
./tools/Build-Installer.ps1 -CertificateThumbprint $certificate.Thumbprint
```

For an existing certificate, pass its thumbprint instead. Its subject must match
the publisher in `src/ConvertNow.Package/AppxManifest.xml`.

The build script builds the Release app and extension, signs the sparse MSIX,
creates a new embedded payload from those outputs, and publishes
`dist/installer/ConvertNow-Setup.exe`. It does not install or register the app.
The installer project depends on this generated payload; run the script before
building the full solution from a fresh checkout. Building the installer directly
with an old payload will not include newer app changes.

Installation requires administrator access. The current installer uses a local
development certificate; public release signing and clean-machine validation are
separate release tasks. FFmpeg is a separate dependency from this project's source.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/ConvertNow.Core` | Formats, FFmpeg runner, progress and output naming |
| `src/ConvertNow.App` | WinUI 3 interface |
| `src/ConvertNow.ShellExtension` | File Explorer COM command |
| `src/ConvertNow.Package` | Sparse MSIX manifest and signing scripts |
| `src/ConvertNow.Installer` | Windows installer and uninstaller |
| `tests` | Core, application and installer tests |

See [startup troubleshooting](docs/STARTUP.md) for the package-identity fix and
the installed shell launch check. [PLAN.md](docs/PLAN.md) is the original design
plan; its unchecked items and older version requirements do not describe current
implementation status.
