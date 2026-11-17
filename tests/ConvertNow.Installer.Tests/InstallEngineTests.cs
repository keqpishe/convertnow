namespace ConvertNow.Installer.Tests;

public sealed class InstallEngineTests
{
    [Theory]
    [InlineData("")]
    [InlineData("ConvertNow")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Temp\..\")]
    public void ValidateInstallDirectory_RejectsEmptyRelativeAndRootPaths(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => InstallEngine.ValidateInstallDirectory(path));
    }

    [Fact]
    public void ValidateInstallDirectory_RejectsSharedSystemAndUserFolders()
    {
        Environment.SpecialFolder[] folders =
        [
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
        ];
        foreach (var folder in folders)
        {
            string path = Environment.GetFolderPath(folder);
            if (path.Length > 0)
                Assert.Throws<ArgumentException>(() => InstallEngine.ValidateInstallDirectory(path));
        }
    }

    [Fact]
    public void ValidateInstallDirectory_PreservesCustomFolderAndNormalizesIt()
    {
        string custom = Path.Combine(Path.GetTempPath(), "ConvertNow test 日本語 ' & $ folder");
        Assert.Equal(custom, InstallEngine.ValidateInstallDirectory(custom + @"\child\..\"));
    }

    [Fact]
    public void RunPowerShell_PreservesLiteralPathsAndDrainsBothStreams()
    {
        const string value = @"C:\Test's folder\$name & 日本語";
        string encodedValue = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(value));
        InstallEngine.RunPowerShell(
            $"$value = {InstallEngine.PowerShellLiteral(value)}; " +
            $"if ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($value)) -cne '{encodedValue}') {{ throw 'Path was altered' }}; " +
            "1..5000 | ForEach-Object { [Console]::Out.WriteLine('output'); [Console]::Error.WriteLine('diagnostic') }");
    }

    [Fact]
    public void RunPowerShell_ReportsNonTerminatingErrors()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            InstallEngine.RunPowerShell("Write-Error 'registration-test-error'"));
        Assert.Contains("registration-test-error", error.Message);
    }

    [Fact]
    public void RunPowerShell_ReportsNonzeroExitCodes()
    {
        var error = Assert.Throws<InvalidOperationException>(() => InstallEngine.RunPowerShell("exit 7"));
        Assert.Contains("exit code 7", error.Message);
    }

    [Fact]
    public void RunPowerShell_TerminatesTimedOutProcesses()
    {
        Assert.Throws<TimeoutException>(() => InstallEngine.RunPowerShell("Start-Sleep -Seconds 30", 2000));
    }
}
