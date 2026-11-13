using System.Diagnostics;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Abre o OpenSSH numa aba do Windows Terminal (ou num console, se o WT não existir).</summary>
public static class SshLauncher
{
    /// <param name="username">Usuário efetivo (da conexão ou da credencial compartilhada).</param>
    public static void Launch(Connection c, string? username)
    {
        var ssh = FindSsh();
        var args = BuildSshArgs(c, username);
        var wt = FindWindowsTerminal();

        ProcessStartInfo psi;
        if (wt is not null)
        {
            psi = new ProcessStartInfo(wt) { UseShellExecute = false };
            // "-w 0" = abre como nova aba na janela mais recente do Terminal
            foreach (var a in new[] { "-w", "0", "new-tab", "--title", SafeTitle(c.Name), ssh })
                psi.ArgumentList.Add(a);
        }
        else
        {
            // App GUI sem console: o ssh.exe ganha uma janela de console própria
            psi = new ProcessStartInfo(ssh) { UseShellExecute = false };
        }

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        Process.Start(psi);
    }

    private static List<string> BuildSshArgs(Connection c, string? username) =>
        SshArguments.Build(c.Host, (int)c.Port, username, c.KeyFile, c.JumpHost, SshTunnels.Parse(c.Tunnels).Tunnels);

    private static string FindSsh() => FindSshPath() ?? "ssh.exe";

    /// <summary>Caminho do OpenSSH do Windows, ou null se não estiver instalado.</summary>
    public static string? FindSshPath()
    {
        // processo 32 bits enxerga System32 redirecionado; "Sysnative" aponta para o real
        var sysDir = Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem
            ? Environment.SystemDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative");

        var path = Path.Combine(sysDir, "OpenSSH", "ssh.exe");
        return File.Exists(path) ? path : null;
    }

    public static string? FindWindowsTerminal()
    {
        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return File.Exists(alias) ? alias : null;
    }

    // ';' separa subcomandos no wt.exe
    private static string SafeTitle(string name) =>
        string.IsNullOrWhiteSpace(name) ? "SSH" : name.Replace(';', ',');
}
