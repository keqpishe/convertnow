using System.Runtime.InteropServices;

namespace ConvertNow.ShellExtension;

/// <summary>
/// Windows File Explorer <see cref="IExplorerCommand"/> implementation that adds
/// "Convert now" to the context menu for individual files and launches ConvertNow.exe.
/// </summary>
[ComVisible(true)]
[Guid("8F5C9E2A-4D7B-4A1E-9B3C-6E2D8A1F5C40")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class ConvertNowCommand : IExplorerCommand
{
    public const string CommandTitle = "Convert now";
    public const string CommandToolTip = "Convert file with Convert.Now";
    public static readonly Guid CommandClsid = new("8F5C9E2A-4D7B-4A1E-9B3C-6E2D8A1F5C40");

    /// <inheritdoc />
    public int GetTitle(IShellItemArray? psiItemArray, out string? ppszName)
    {
        ppszName = CommandTitle;
        return HResult.S_OK;
    }

    /// <inheritdoc />
    public int GetIcon(IShellItemArray? psiItemArray, out string? ppszIcon)
    {
        try
        {
            string? iconPath = ClassFactory.ResolveIconPath();
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                ppszIcon = iconPath;
                return HResult.S_OK;
            }
        }
        catch
        {
            // Never let managed exceptions escape across COM boundary into Explorer.
        }

        ppszIcon = null;
        return HResult.E_NOTIMPL;
    }

    /// <inheritdoc />
    public int GetToolTip(IShellItemArray? psiItemArray, out string? ppszInfotip)
    {
        ppszInfotip = CommandToolTip;
        return HResult.S_OK;
    }

    /// <inheritdoc />
    public int GetCanonicalName(out Guid pguidCommandName)
    {
        pguidCommandName = CommandClsid;
        return HResult.S_OK;
    }

    /// <inheritdoc />
    public int GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
    {
        try
        {
            pCmdState = TryGetSingleSelectedFilePath(psiItemArray, out _)
                ? EXPCMDSTATE.ECS_ENABLED
                : EXPCMDSTATE.ECS_HIDDEN;
        }
        catch
        {
            pCmdState = EXPCMDSTATE.ECS_HIDDEN;
        }

        return HResult.S_OK;
    }

    /// <inheritdoc />
    public int Invoke(IShellItemArray? psiItemArray, IntPtr pbc)
    {
        try
        {
            if (!TryGetSingleSelectedFilePath(psiItemArray, out string? filePath) ||
                string.IsNullOrWhiteSpace(filePath))
            {
                ApplicationLauncher.ReportFailure("Select one local file and try again.");
                return HResult.E_INVALIDARG;
            }

            string? exePath = ClassFactory.ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                ApplicationLauncher.ReportFailure("ConvertNow.exe could not be found. Reinstall Convert.Now and try again.");
                return HResult.E_FAIL;
            }

            ApplicationLauncher.Launch(exePath, filePath);
            return HResult.S_OK;
        }
        catch (Exception ex)
        {
            ApplicationLauncher.ReportFailure(ex.Message);
            return HResult.E_FAIL;
        }
    }

    /// <inheritdoc />
    public int GetFlags(out EXPCMDFLAGS pFlags)
    {
        pFlags = EXPCMDFLAGS.ECF_DEFAULT;
        return HResult.S_OK;
    }

    /// <inheritdoc />
    public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        ppEnum = null;
        return HResult.E_NOTIMPL;
    }

    private static bool TryGetSingleSelectedFilePath(IShellItemArray? psiItemArray, out string? filePath)
    {
        filePath = null;
        if (psiItemArray is null)
        {
            return false;
        }

        if (HResult.Failed(psiItemArray.GetCount(out uint count)) || count != 1)
        {
            return false;
        }

        if (HResult.Failed(psiItemArray.GetItemAt(0, out IShellItem? item)) || item is null)
        {
            return false;
        }

        try
        {
            int attrHr = item.GetAttributes(SFGAO.FILESYSTEM | SFGAO.FOLDER | SFGAO.STREAM, out SFGAO attribs);
            if (HResult.Failed(attrHr))
            {
                return false;
            }

            // Stream-backed items (e.g. .zip archives) report both FOLDER and STREAM; exclude real directories.
            if ((attribs & SFGAO.FOLDER) != 0 && (attribs & SFGAO.STREAM) == 0)
            {
                return false;
            }

            int nameHr = item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string? path);
            if (HResult.Failed(nameHr) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (Directory.Exists(path))
            {
                return false;
            }

            filePath = path;
            return true;
        }
        finally
        {
            if (Marshal.IsComObject(item))
            {
                Marshal.ReleaseComObject(item);
            }
        }
    }
}
