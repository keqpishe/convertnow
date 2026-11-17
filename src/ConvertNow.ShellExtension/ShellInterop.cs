using System.Runtime.InteropServices;

namespace ConvertNow.ShellExtension;

/// <summary>
/// Common COM HRESULT constants and helpers used by Windows Shell interop.
/// </summary>
public static class HResult
{
    public const int S_OK = 0x00000000;
    public const int S_FALSE = 0x00000001;
    public const int E_NOTIMPL = unchecked((int)0x80004001);
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_POINTER = unchecked((int)0x80004003);
    public const int E_ABORT = unchecked((int)0x80004004);
    public const int E_FAIL = unchecked((int)0x80004005);
    public const int E_INVALIDARG = unchecked((int)0x80070057);
    public const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    public const int CLASS_E_CLASSNOTAVAILABLE = unchecked((int)0x80040111);

    public static bool Succeeded(int hr) => hr >= 0;

    public static bool Failed(int hr) => hr < 0;
}

/// <summary>
/// Specifies the state of an Explorer command item returned by <see cref="IExplorerCommand.GetState"/>.
/// </summary>
[Flags]
public enum EXPCMDSTATE : uint
{
    ECS_ENABLED = 0x00000000,
    ECS_DISABLED = 0x00000001,
    ECS_HIDDEN = 0x00000002,
    ECS_CHECKBOX = 0x00000004,
    ECS_CHECKED = 0x00000008,
    ECS_RADIOCHECK = 0x00000010,
}

/// <summary>
/// Specifies flags defining Explorer command behavior returned by <see cref="IExplorerCommand.GetFlags"/>.
/// </summary>
[Flags]
public enum EXPCMDFLAGS : uint
{
    ECF_DEFAULT = 0x00000000,
    ECF_HASSUBCOMMANDS = 0x00000001,
    ECF_HASSPLITBUTTON = 0x00000002,
    ECF_HIDELABEL = 0x00000004,
    ECF_ISSEPARATOR = 0x00000008,
    ECF_HASLUASHIELD = 0x00000010,
    ECF_SEPARATORBEFORE = 0x00000020,
    ECF_SEPARATORAFTER = 0x00000040,
    ECF_ISDROPDOWN = 0x00000080,
    ECF_TOGGLEABLE = 0x00000100,
    ECF_AUTOMENUICONS = 0x00000200,
}

/// <summary>
/// Requests the form of an item's display name to retrieve through <see cref="IShellItem.GetDisplayName"/>.
/// </summary>
public enum SIGDN : uint
{
    SIGDN_NORMALDISPLAY = 0x00000000,
    SIGDN_PARENTRELATIVEPARSING = 0x80018001,
    SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
    SIGDN_PARENTRELATIVEEDITING = 0x80038001,
    SIGDN_DESKTOPABSOLUTEEDITING = 0x80048000,
    SIGDN_FILESYSPATH = 0x80058000,
    SIGDN_URL = 0x80068000,
    SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007C001,
    SIGDN_PARENTRELATIVE = 0x80080001,
    SIGDN_PARENTRELATIVEFORUI = 0x80094001,
}

/// <summary>
/// Attributes that can be retrieved on a Shell item or Shell item array.
/// </summary>
[Flags]
public enum SFGAO : uint
{
    CANCOPY = 0x00000001,
    CANMOVE = 0x00000002,
    CANLINK = 0x00000004,
    STORAGE = 0x00000008,
    CANRENAME = 0x00000010,
    CANDELETE = 0x00000020,
    HASPROPSHEET = 0x00000040,
    DROPTARGET = 0x00000100,
    LINK = 0x00010000,
    SHARE = 0x00020000,
    READONLY = 0x00040000,
    GHOSTED = 0x00080000,
    HIDDEN = 0x00080000,
    STREAM = 0x00400000,
    STORAGEANCESTOR = 0x00800000,
    FILESYSANCESTOR = 0x10000000,
    FOLDER = 0x20000000,
    FILESYSTEM = 0x40000000,
    HASSUBFOLDER = 0x80000000,
}

/// <summary>
/// Specifies how attributes are combined in <see cref="IShellItemArray.GetAttributes"/>.
/// </summary>
public enum SIATTRIBFLAGS : uint
{
    SIATTRIBFLAGS_AND = 0x00000001,
    SIATTRIBFLAGS_OR = 0x00000002,
    SIATTRIBFLAGS_APPCOMPAT = 0x00000003,
    SIATTRIBFLAGS_MASK = 0x00000003,
    SIATTRIBFLAGS_ALLITEMS = 0x00004000,
}

/// <summary>
/// Specifies comparison options for <see cref="IShellItem.Compare"/>.
/// </summary>
[Flags]
public enum SICHINTF : uint
{
    SICHINT_DISPLAY = 0x00000000,
    SICHINT_CANONICAL = 0x10000000,
    SICHINT_TEST_FILESYSPATH_IF_NOT_EQUAL = 0x20000000,
    SICHINT_ALLFIELDS = 0x80000000,
}

/// <summary>
/// Identifies a Shell property key.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
}

/// <summary>
/// Exposes methods that allow a Windows Explorer command to control presentation, state, and invocation.
/// </summary>
[ComImport]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItemArray? psiItemArray,
        [Out, MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);

    [PreserveSig]
    int GetIcon(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItemArray? psiItemArray,
        [Out, MarshalAs(UnmanagedType.LPWStr)] out string? ppszIcon);

    [PreserveSig]
    int GetToolTip(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItemArray? psiItemArray,
        [Out, MarshalAs(UnmanagedType.LPWStr)] out string? ppszInfotip);

    [PreserveSig]
    int GetCanonicalName(
        [Out] out Guid pguidCommandName);

    [PreserveSig]
    int GetState(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItemArray? psiItemArray,
        [In, MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow,
        [Out] out EXPCMDSTATE pCmdState);

    [PreserveSig]
    int Invoke(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItemArray? psiItemArray,
        [In] IntPtr pbc);

    [PreserveSig]
    int GetFlags(
        [Out] out EXPCMDFLAGS pFlags);

    [PreserveSig]
    int EnumSubCommands(
        [Out, MarshalAs(UnmanagedType.Interface)] out IEnumExplorerCommand? ppEnum);
}

/// <summary>
/// Enumerates subcommands exposed by an <see cref="IExplorerCommand"/>.
/// </summary>
[ComImport]
[Guid("a88826f8-186f-4987-aade-ea4cef8fbfe8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IEnumExplorerCommand
{
    [PreserveSig]
    int Next(
        [In] uint celt,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IExplorerCommand[] pUICommand,
        [Out] out uint pceltFetched);

    [PreserveSig]
    int Skip(
        [In] uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(
        [Out, MarshalAs(UnmanagedType.Interface)] out IEnumExplorerCommand? ppenum);
}

/// <summary>
/// Exposes methods that create and manipulate an array of Shell items.
/// </summary>
[ComImport]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(
        [In] IntPtr pbc,
        [In] ref Guid bhid,
        [In] ref Guid riid,
        [Out] out IntPtr ppvOut);

    [PreserveSig]
    int GetPropertyStore(
        [In] int flags,
        [In] ref Guid riid,
        [Out] out IntPtr ppv);

    [PreserveSig]
    int GetPropertyDescriptionList(
        [In] ref PROPERTYKEY keyType,
        [In] ref Guid riid,
        [Out] out IntPtr ppv);

    [PreserveSig]
    int GetAttributes(
        [In] SIATTRIBFLAGS AttribFlags,
        [In] SFGAO sfgaoMask,
        [Out] out SFGAO psfgaoAttribs);

    [PreserveSig]
    int GetCount(
        [Out] out uint pdwNumItems);

    [PreserveSig]
    int GetItemAt(
        [In] uint dwIndex,
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItem? ppsi);

    [PreserveSig]
    int EnumItems(
        [Out] out IntPtr ppenumShellItems);
}

/// <summary>
/// Represents an item in the Windows Shell namespace.
/// </summary>
[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    [PreserveSig]
    int BindToHandler(
        [In] IntPtr pbc,
        [In] ref Guid bhid,
        [In] ref Guid riid,
        [Out] out IntPtr ppv);

    [PreserveSig]
    int GetParent(
        [Out, MarshalAs(UnmanagedType.Interface)] out IShellItem? ppsi);

    [PreserveSig]
    int GetDisplayName(
        [In] SIGDN sigdnName,
        [Out, MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);

    [PreserveSig]
    int GetAttributes(
        [In] SFGAO sfgaoMask,
        [Out] out SFGAO psfgaoAttribs);

    [PreserveSig]
    int Compare(
        [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
        [In] SICHINTF hint,
        [Out] out int piOrder);
}

/// <summary>
/// Enables creation of COM class instances for registered Shell extension classes.
/// </summary>
[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(
        [In] IntPtr pUnkOuter,
        [In] ref Guid riid,
        [Out] out IntPtr ppvObject);

    [PreserveSig]
    int LockServer(
        [In, MarshalAs(UnmanagedType.Bool)] bool fLock);
}
