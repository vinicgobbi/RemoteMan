using System.Diagnostics;
using System.Text;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Gera um arquivo .rdp temporário e abre o mstsc.exe.</summary>
public static class RdpLauncher
{
    public static void Launch(Connection c, string? password)
    {
        var host = c.Host.Trim();
        var port = (int)c.Port;

        string? username;
        if (c.UseWindowsCredentials)
        {
            // Usuário logado no Windows. Remove a credencial genérica TERMSRV/host que o próprio app
            // possa ter gravado antes; senão o mstsc a usaria no lugar do usuário do Windows.
            // (Credenciais do "Lembrar de mim" do mstsc são de outro tipo e não são tocadas.)
            username = Connection.CurrentWindowsUser;
            CredentialStore.Delete(CredentialStore.RdpKeyFor(host));
        }
        else
        {
            username = string.IsNullOrWhiteSpace(c.Username) ? null : c.FullUsername;

            // Grava a credencial onde o mstsc procura (TERMSRV/host) para conectar sem pedir senha.
            if (password is not null && username is not null)
                CredentialStore.Write(CredentialStore.RdpKeyFor(host), username, password);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{(port is > 0 and not 3389 ? $"{host}:{port}" : host)}");
        if (username is not null)
            sb.AppendLine($"username:s:{username}");
        sb.AppendLine($"screen mode id:i:{(c.FullScreen ? 2 : 1)}");
        if (!c.FullScreen)
        {
            sb.AppendLine($"desktopwidth:i:{(int)c.DesktopWidth}");
            sb.AppendLine($"desktopheight:i:{(int)c.DesktopHeight}");
        }
        sb.AppendLine($"use multimon:i:{(c.MultiMonitor ? 1 : 0)}");
        sb.AppendLine($"redirectclipboard:i:{(c.RedirectClipboard ? 1 : 0)}");
        if (c.RedirectDrives)
            sb.AppendLine("drivestoredirect:s:*");
        sb.AppendLine("prompt for credentials:i:0");
        sb.AppendLine("authentication level:i:2");
        sb.AppendLine("enablecredsspsupport:i:1");
        sb.AppendLine("smart sizing:i:1");
        sb.AppendLine("dynamic resolution:i:1");

        var dir = Path.Combine(Path.GetTempPath(), "RemoteMan");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{c.Id}.rdp");
        File.WriteAllText(file, sb.ToString(), Encoding.Unicode);

        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mstsc.exe"))
        {
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(file);
        if (c.AdminSession)
            psi.ArgumentList.Add("/admin");

        Process.Start(psi);
    }
}
