using System.Runtime.InteropServices;
using System.Text;

namespace LuxTicker;

/// <summary>
/// Per-user Start Menu shortcut so Windows will show toasts for this unpackaged exe.
/// The link lives under the roaming profile. It is not an installer and does not need an admin password.
/// </summary>
static class ToastShortcut
{
    public const string AppId = "Lux.LuxTicker";

    public static void Identify()
    {
        SetCurrentProcessExplicitAppUserModelID(AppId);
    }

    public static void Ensure(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException(LuxWireEdition.ProductName + " needs its exe path before it can show toasts.");

        Identify();
        var linkPath = AppPaths.StartMenuShortcut();
        var dir = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exePath);
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? exePath);
        link.SetIconLocation(exePath, 0);
        link.SetDescription(LuxWireEdition.ProductName);

        var store = (IPropertyStore)link;
        // Do not P/Invoke propsys!InitPropVariantFromString. It is an inline helper in the
        // Windows header, not an export, and CharSet.Unicode also looks for
        // InitPropVariantFromStringW. Stock Windows 11 raises EntryPointNotFoundException.
        InitPropVariantFromString(AppId, out var variant);
        try
        {
            var key = PkeyAppUserModelId;
            store.SetValue(ref key, ref variant);
            store.Commit();
        }
        finally
        {
            PropVariantClear(ref variant);
        }

        ((IPersistFile)link).Save(linkPath, true);
    }

    static readonly PropertyKey PkeyAppUserModelId = new()
    {
        FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        PropertyId = 5
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "SetCurrentProcessExplicitAppUserModelID")]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    const ushort VtLpWstr = 31;

    static void InitPropVariantFromString(string value, out PropVariant variant)
    {
        variant = new PropVariant
        {
            VariantType = VtLpWstr,
            Pointer = Marshal.StringToCoTaskMemUni(value ?? "")
        };
    }

    [DllImport("ole32.dll", ExactSpelling = true, EntryPoint = "PropVariantClear")]
    static extern int PropVariantClear(ref PropVariant variant);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, out Win32FindData data, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRel, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Win32FindData
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string AlternateFileName;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
