using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LuxTicker;

/// <summary>
/// Session tokens live in account.bin, protected for the current Windows user.
/// CryptProtectData is called with the UI-forbidden flag only, which keeps the default user scope.
/// </summary>
static class AccountVault
{
    const uint UiForbidden = 0x1;

    public static void Save(AccountSession session)
    {
        var json = JsonSerializer.Serialize(session, JsonFile.Options);
        var guarded = Protect(Encoding.UTF8.GetBytes(json));
        var path = AppPaths.AccountPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, guarded);
        File.Move(tmp, path, overwrite: true);
    }

    public static AccountSession? TryLoad()
    {
        var path = AppPaths.AccountPath();
        if (!File.Exists(path))
            return null;
        try
        {
            var plain = Unprotect(File.ReadAllBytes(path));
            return JsonSerializer.Deserialize<AccountSession>(plain, JsonFile.Options);
        }
        catch (Exception ex)
        {
            Log.Error("account vault " + ex.Message);
            return null;
        }
    }

    public static void Clear()
    {
        var path = AppPaths.AccountPath();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Error("account vault clear " + ex.Message);
        }
    }

    static byte[] Protect(byte[] plain)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        try
        {
            input.cbData = plain.Length;
            input.pbData = Marshal.AllocHGlobal(plain.Length);
            Marshal.Copy(plain, 0, input.pbData, plain.Length);
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output))
                throw new InvalidOperationException("Couldn't protect the account session. Win32 " + Marshal.GetLastWin32Error());
            var guarded = new byte[output.cbData];
            Marshal.Copy(output.pbData, guarded, 0, output.cbData);
            return guarded;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero)
                LocalFree(output.pbData);
        }
    }

    static byte[] Unprotect(byte[] guarded)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        try
        {
            input.cbData = guarded.Length;
            input.pbData = Marshal.AllocHGlobal(guarded.Length);
            Marshal.Copy(guarded, 0, input.pbData, guarded.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output))
                throw new InvalidOperationException("Couldn't read the account session. Win32 " + Marshal.GetLastWin32Error());
            var plain = new byte[output.cbData];
            Marshal.Copy(output.pbData, plain, 0, output.cbData);
            return plain;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero)
                LocalFree(output.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr hMem);
}
