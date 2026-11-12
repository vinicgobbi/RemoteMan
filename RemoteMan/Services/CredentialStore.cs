using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>
/// Acesso ao Windows Credential Manager (advapi32 CredWrite/CredRead/CredDelete).
/// As senhas ficam protegidas pelo DPAPI do usuário do Windows.
/// </summary>
public static class CredentialStore
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;
    private const int ERROR_NOT_FOUND = 1168;

    /// <summary>Chave onde o app guarda a senha de uma conexão.</summary>
    public static string KeyFor(Connection c) => $"RemoteMan/{c.Id}";

    /// <summary>Chave que o mstsc.exe consulta para logar automaticamente.</summary>
    public static string RdpKeyFor(string host) => $"TERMSRV/{host}";

    public static void Write(string target, string? userName, string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password);
        var blob = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = string.IsNullOrEmpty(userName) ? null : userName,
                CredentialBlob = blob,
                CredentialBlobSize = bytes.Length,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
            };

            if (!CredWrite(ref cred, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            // zera a cópia não gerenciada antes de liberar
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Array.Clear(bytes);
        }
    }

    public static string? ReadPassword(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            return cred.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(cred.CredentialBlob, cred.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public static bool Exists(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr))
            return false;
        CredFree(ptr);
        return true;
    }

    public static void Delete(string target)
    {
        if (!CredDelete(target, CRED_TYPE_GENERIC, 0))
        {
            var err = Marshal.GetLastWin32Error();
            if (err != ERROR_NOT_FOUND)
                throw new Win32Exception(err);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
