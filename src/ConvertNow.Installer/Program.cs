namespace ConvertNow.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool uninstall = string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),
            "Uninstall", StringComparison.OrdinalIgnoreCase)
            || args.Any(arg => arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
        bool silent = args.Any(arg => arg.Equals("/silent", StringComparison.OrdinalIgnoreCase));

        if (silent)
        {
            if (!uninstall)
                return 1;

            try
            {
                InstallEngine.Uninstall((_, _) => { });
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        Application.Run(new InstallerForm(uninstall));
        return 0;
    }
}
