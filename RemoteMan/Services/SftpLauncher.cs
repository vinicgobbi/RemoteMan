using System.Diagnostics;
using Microsoft.Win32;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Detecta os clientes SFTP instalados (WinSCP, FileZilla, Bitvise) e abre o escolhido já conectado.</summary>
public static class SftpLauncher
{
    public static string DisplayName(SftpClientKind kind) => kind switch
    {
        SftpClientKind.WinScp => "WinSCP",
        SftpClientKind.FileZilla => "FileZilla",
        SftpClientKind.Bitvise => "Bitvise SSH Client",
        _ => kind.ToString(),
    };

    /// <summary>Página oficial de download de cada cliente.</summary>
    public static Uri DownloadUri(SftpClientKind kind) => new(kind switch
    {
        SftpClientKind.WinScp => "https://winscp.net/eng/download.php",
        SftpClientKind.FileZilla => "https://filezilla-project.org/download.php?type=client",
        SftpClientKind.Bitvise => "https://www.bitvise.com/ssh-client-download",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    });

    private static (string Exe, string Folder) Install(SftpClientKind kind) => kind switch
    {
        SftpClientKind.WinScp => ("WinSCP.exe", "WinSCP"),
        SftpClientKind.FileZilla => ("filezilla.exe", "FileZilla FTP Client"),
        SftpClientKind.Bitvise => ("BvSsh.exe", "Bitvise SSH Client"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Clientes instalados, com o caminho do executável, na ordem de preferência padrão.</summary>
    public static IReadOnlyDictionary<SftpClientKind, string> FindInstalled()
    {
        var found = new Dictionary<SftpClientKind, string>();
        foreach (var kind in Enum.GetValues<SftpClientKind>())
        {
            if (FindClient(kind) is { } path)
                found[kind] = path;
        }
        return found;
    }

    /// <summary>Caminho do executável do cliente, ou null se não estiver instalado.</summary>
    public static string? FindClient(SftpClientKind kind)
    {
        var (exe, folder) = Install(kind);

        // registro de "App Paths" (preenchido por vários instaladores, inclusive em pasta personalizada)
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                if (key?.GetValue(null) is string registered && File.Exists(registered.Trim('"')))
                    return registered.Trim('"');
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // sem acesso ao registro: segue para as pastas padrão
            }
        }

        // pastas padrão de instalação (64 e 32 bits, e instalação só para o usuário)
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };
        return roots
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(r => Path.Combine(r!, folder, exe))
            .FirstOrDefault(File.Exists);
    }

    /// <param name="username">Usuário efetivo (da conexão ou da credencial compartilhada).</param>
    /// <param name="exePath">Executável do cliente, vindo de <see cref="FindInstalled"/>.</param>
    public static SftpCommand Launch(Connection c, string? username, SftpClientKind kind, string exePath)
    {
        var keyFile = kind == SftpClientKind.WinScp ? PpkFor(c.KeyFile) : c.KeyFile;
        var command = SftpArguments.Build(kind, c.Host, (int)c.Port, username, keyFile, c.JumpHost);

        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
        };
        foreach (var a in command.Arguments)
            psi.ArgumentList.Add(a);

        Process.Start(psi);
        return command;
    }

    /// <summary>
    /// O WinSCP só aceita .ppk: se a chave for OpenSSH e existir uma versão convertida ao lado
    /// ("id_ed25519.ppk" ou "id_ed25519" → ".ppk"), usa ela.
    /// </summary>
    private static string PpkFor(string? keyFile)
    {
        if (string.IsNullOrWhiteSpace(keyFile))
            return string.Empty;

        var key = keyFile.Trim();
        if (key.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase))
            return key;

        return new[] { key + ".ppk", Path.ChangeExtension(key, ".ppk") }.FirstOrDefault(File.Exists) ?? key;
    }
}
