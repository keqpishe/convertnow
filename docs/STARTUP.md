# Launching from File Explorer

The Windows 11 context menu runs the COM extension with the sparse package identity.
The converter inherits that identity. A bootstrapper configured only for unpackaged
startup used to exit with `0x80070032` before WinUI created a window.

Keep both parts of the fix together:

- `ConvertNow.App.csproj` enables the bootstrapper's `OnPackageIdentity_NoOp` option.
  Direct EXE launches still initialize the runtime normally.
- `AppxManifest.xml` declares the Windows App Runtime 1.8 framework dependency, so
  launches carrying the package identity already have the required runtime graph.

Microsoft documents the bootstrapper option in
[Use the Windows App SDK runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/use-windows-app-sdk-run-time).

The extension uses shell activation, observes startup without blocking Explorer,
and reports launch failures rather than silently returning an HRESULT.
Its log is `shell-launch.log` under the package's local cache:

`%LOCALAPPDATA%\Packages\ConvertNow.SparsePackage_qqpkce0dfcczj\LocalCache\Local\ConvertNow\logs`

Run `tools/Test-ShellLaunch.ps1` to activate the registered COM extension with a real
Shell item and verify a fresh successful window startup in the log. It opens a sample
image without converting or changing it. Also verify the actual Windows 11 context
menu and a direct EXE launch when changing startup or packaging settings.

After rebuilding the Release application and shell extension and signing the sparse
package, `tools/Update-InstalledShellExtension.ps1` can repair an existing installation.
Run it elevated with Convert.Now closed. It saves backups, replaces the startup
binaries, refreshes the package registration, and stops only the COM surrogate that
loaded this extension. The installer payload must contain the same updated binaries
and signed package.
