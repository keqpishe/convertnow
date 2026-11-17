using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace ConvertNow.Installer;

public static class InstallEngine
{
    public const string AppName = "Convert.Now";
    public const string AppId = "ConvertNow";
    public const string SparsePackageName = "ConvertNow.SparsePackage";
    public const string DefaultInstallDir = @"C:\Program Files\ConvertNow";
    private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ConvertNow";
    private const string ContextMenuRegKey = @"*\shell\ConvertNow";
    private const string BackgroundContextMenuRegKey = @"Directory\Background\shell\ConvertNow";

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    public static bool IsInstalled(out string? installPath)
    {
        installPath = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallRegKey);
            if (key != null)
            {
                installPath = key.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(installPath) && File.Exists(Path.Combine(installPath, "ConvertNow.exe")))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        if (Directory.Exists(DefaultInstallDir) && File.Exists(Path.Combine(DefaultInstallDir, "ConvertNow.exe")))
        {
            installPath = DefaultInstallDir;
            return true;
        }

        return false;
    }

    public static void Install(
        string targetDir,
        Action<int, string> progress,
        bool createDesktopShortcut = true,
        bool createStartMenuShortcut = true,
        bool restartExplorer = false,
        bool addContextMenu = true)
    {
        targetDir = ValidateInstallDirectory(targetDir);
        progress(5, "Preparing destination directory...");
        Directory.CreateDirectory(targetDir);

        // Terminate any active ConvertNow processes before overwriting
        try
        {
            foreach (var proc in Process.GetProcessesByName("ConvertNow"))
            {
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch
        {
        }

        // A loaded COM server keeps the old extension mapped during an upgrade.
        string extensionPath = Path.Combine(targetDir, "ConvertNow.ShellExtension.dll");
        foreach (var host in Process.GetProcessesByName("dllhost"))
        {
            using (host)
            {
                bool ownsExtension;
                try
                {
                    ownsExtension = host.Modules.Cast<ProcessModule>().Any(module =>
                        string.Equals(module.FileName, extensionPath, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    continue;
                }
                if (ownsExtension)
                {
                    host.Kill();
                    if (!host.WaitForExit(5000))
                        throw new TimeoutException("Close the Convert.Now context menu and try the update again.");
                }
            }
        }

        progress(15, "Extracting application payload...");
        ExtractPayload(targetDir, p => progress(15 + (int)(p * 0.40), "Extracting application files..."));

        if (addContextMenu)
        {
            progress(60, "Installing local security certificate...");
            InstallCertificate(targetDir);
            progress(70, "Registering Windows 11 Shell Extension...");
            RegisterSparsePackage(targetDir);
            progress(80, "Configuring File Explorer context menu...");
            RegisterContextMenu(targetDir);
        }
        else
        {
            progress(70, "Removing existing File Explorer integration...");
            UnregisterSparsePackage();
            UnregisterContextMenu();
        }

        progress(85, "Creating shortcuts...");
        CreateShortcuts(targetDir, createDesktopShortcut, createStartMenuShortcut);

        progress(90, "Registering uninstaller in Windows Settings...");
        RegisterUninstaller(targetDir);

        progress(95, "Refreshing Windows Shell...");
        RefreshShell();

        if (restartExplorer)
        {
            progress(98, "Restarting Windows Explorer to apply changes...");
            RestartExplorerProcess();
        }

        progress(100, "Installation completed successfully!");
    }

    public static void Uninstall(Action<int, string> progress)
    {
        // Resolve and validate before removing the registry entry that stores this path.
        if (!IsInstalled(out string? installedPath) || string.IsNullOrWhiteSpace(installedPath))
            throw new InvalidOperationException("Could not locate the installed application. No files were removed.");
        string installDir = ValidateInstallDirectory(installedPath);

        progress(10, "Closing running instances...");
        try
        {
            foreach (var proc in Process.GetProcessesByName("ConvertNow"))
            {
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch
        {
        }

        progress(25, "Unregistering Windows 11 Shell Extension...");
        UnregisterSparsePackage();

        progress(45, "Removing File Explorer context menu...");
        UnregisterContextMenu();

        progress(60, "Removing shortcuts...");
        RemoveShortcuts();

        progress(75, "Removing uninstaller registry entries...");
        Registry.LocalMachine.DeleteSubKeyTree(UninstallRegKey, throwOnMissingSubKey: false);

        progress(85, "Refreshing Windows Shell...");
        RefreshShell();

        progress(90, "Scheduling file cleanup...");
        ScheduleDirectoryDeletion(installDir);

        progress(100, "Convert.Now has been uninstalled successfully.");
    }

    private static void ExtractPayload(string targetDir, Action<double> progressUpdate)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ConvertNow.Installer.Resources.payload.zip");
        if (stream == null)
        {
            throw new InvalidOperationException("Embedded payload.zip resource not found in installer.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        int total = archive.Entries.Count;
        int current = 0;

        foreach (var entry in archive.Entries)
        {
            current++;
            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

            // Prevent zip-slip vulnerability
            if (!destinationPath.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir)) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The payload contains a path outside the installation directory.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
            progressUpdate((double)current / total);
        }

        string currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate the installer executable.");
        File.Copy(currentExe, Path.Combine(targetDir, "Uninstall.exe"), overwrite: true);
    }

    private static void InstallCertificate(string targetDir)
    {
        string cerPath = Path.Combine(targetDir, "ConvertNowDev.cer");
        if (!File.Exists(cerPath))
        {
            throw new FileNotFoundException("Shell extension certificate is missing.", cerPath);
        }

        try
        {
            using var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);

            using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
            }

            using (var rootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                rootStore.Open(OpenFlags.ReadWrite);
                rootStore.Add(cert);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not install the shell extension certificate.", ex);
        }
    }

    private static void RegisterSparsePackage(string targetDir)
    {
        string msixPath = Path.Combine(targetDir, "ConvertNow.msix");
        if (!File.Exists(msixPath))
        {
            throw new FileNotFoundException("Shell extension package is missing.", msixPath);
        }
        RunPowerShell($"Add-AppxPackage -Path {PowerShellLiteral(msixPath)} -ExternalLocation {PowerShellLiteral(targetDir)} -ErrorAction Stop");
    }

    private static void UnregisterSparsePackage()
    {
        RunPowerShell($"Get-AppxPackage -Name {PowerShellLiteral(SparsePackageName)} -ErrorAction Stop | Remove-AppxPackage -ErrorAction Stop");
    }

    private static void RegisterContextMenu(string targetDir)
    {
        string exePath = Path.Combine(targetDir, "ConvertNow.exe");
        string iconPath = Path.Combine(targetDir, "Assets", "context-menu-icon.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = exePath;
        }

        // 1. Register for all files: HKEY_CLASSES_ROOT\*\shell\ConvertNow
        try
        {
            using var fileKey = Registry.ClassesRoot.CreateSubKey(ContextMenuRegKey);
            if (fileKey != null)
            {
                fileKey.SetValue("", "Convert now");
                fileKey.SetValue("Icon", $"\"{iconPath}\"");
                using var cmdKey = fileKey.CreateSubKey("command");
                cmdKey?.SetValue("", $"\"{exePath}\" --file \"%1\"");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not register the file context menu.", ex);
        }

        // 2. Register for Directory Background (empty space in Explorer or desktop):
        try
        {
            using var bgKey = Registry.ClassesRoot.CreateSubKey(BackgroundContextMenuRegKey);
            if (bgKey != null)
            {
                bgKey.SetValue("", "Convert now");
                bgKey.SetValue("Icon", $"\"{iconPath}\"");
                using var cmdKey = bgKey.CreateSubKey("command");
                cmdKey?.SetValue("", $"\"{exePath}\"");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not register the background context menu.", ex);
        }
    }

    private static void UnregisterContextMenu()
    {
        Registry.ClassesRoot.DeleteSubKeyTree(ContextMenuRegKey, throwOnMissingSubKey: false);
        Registry.ClassesRoot.DeleteSubKeyTree(BackgroundContextMenuRegKey, throwOnMissingSubKey: false);
    }

    private static void CreateShortcuts(string targetDir, bool desktop, bool startMenu)
    {
        string exePath = Path.Combine(targetDir, "ConvertNow.exe");
        string iconPath = Path.Combine(targetDir, "Assets", "icon.ico");

        if (startMenu)
        {
            try
            {
                string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                string lnkPath = Path.Combine(programsDir, "Convert.Now.lnk");
                CreateShortcutFile(lnkPath, exePath, targetDir, iconPath, "Convert.Now — Universal File Converter");
            }
            catch
            {
            }
        }

        if (desktop)
        {
            try
            {
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string lnkPath = Path.Combine(desktopDir, "Convert.Now.lnk");
                CreateShortcutFile(lnkPath, exePath, targetDir, iconPath, "Convert.Now — Universal File Converter");
            }
            catch
            {
            }
        }
    }

    private static void RemoveShortcuts()
    {
        try
        {
            string startLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Convert.Now.lnk");
            if (File.Exists(startLnk)) File.Delete(startLnk);
        }
        catch { }

        try
        {
            string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Convert.Now.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
        }
        catch { }
    }

    private static void CreateShortcutFile(string lnkPath, string targetPath, string workDir, string iconPath, string description)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType != null)
        {
            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell != null)
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workDir;
                shortcut.IconLocation = File.Exists(iconPath) ? $"{iconPath},0" : $"{targetPath},0";
                shortcut.Description = description;
                shortcut.Save();
            }
        }
    }

    private static void RegisterUninstaller(string targetDir)
    {
        try
        {
            string uninstallExe = Path.Combine(targetDir, "Uninstall.exe");
            string iconPath = Path.Combine(targetDir, "Assets", "icon.ico");

            using var key = Registry.LocalMachine.CreateSubKey(UninstallRegKey);
            if (key != null)
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "Convert.Now Team");
                key.SetValue("DisplayIcon", File.Exists(iconPath) ? iconPath : uninstallExe);
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"\"{uninstallExe}\"");
                key.SetValue("QuietUninstallString", $"\"{uninstallExe}\" /silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 140 * 1024, RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not register the uninstaller.", ex);
        }
    }

    public static void RefreshShell()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }

    public static void RestartExplorerProcess()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                p.Kill();
                p.WaitForExit(3000);
            }
        }
        catch
        {
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    internal static string ValidateInstallDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException("Choose an absolute installation path.", nameof(directoryPath));

        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        string[] protectedPaths =
        [
            Path.GetPathRoot(fullPath)!,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        ];
        if (protectedPaths.Any(path => !string.IsNullOrEmpty(path)
                && string.Equals(fullPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a separate folder for Convert.Now.", nameof(directoryPath));

        return fullPath;
    }

    internal static string PowerShellLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    private static ProcessStartInfo PowerShellStartInfo(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-OutputFormat");
        startInfo.ArgumentList.Add("Text");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        return startInfo;
    }

    internal static void RunPowerShell(string command, int timeoutMilliseconds = 60000)
    {
        var startInfo = PowerShellStartInfo("$ErrorActionPreference = 'Stop'; " + command);
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Windows PowerShell.");
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Task.WhenAll(stderr, stdout).GetAwaiter().GetResult();
            throw new TimeoutException("Windows shell registration did not finish in time.");
        }

        Task.WhenAll(stderr, stdout).GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Windows shell registration failed (exit code {process.ExitCode}): {stderr.Result.Trim()}");
    }

    private static void ScheduleDirectoryDeletion(string directoryPath)
    {
        string fullPath = ValidateInstallDirectory(directoryPath);
        if (!File.Exists(Path.Combine(fullPath, "ConvertNow.exe")))
            throw new InvalidOperationException("The cleanup directory does not contain Convert.Now.");

        // Wait for this uninstaller to close, then retry while Windows releases file handles.
        string command = $"$target = {PowerShellLiteral(fullPath)}; " +
            $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; " +
            "for ($attempt = 0; $attempt -lt 10; $attempt++) { " +
            "try { Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop; exit 0 } " +
            "catch { Start-Sleep -Seconds 2 } }; exit 1";
        using var process = Process.Start(PowerShellStartInfo(command))
            ?? throw new InvalidOperationException("Could not schedule application file cleanup.");
    }
}
