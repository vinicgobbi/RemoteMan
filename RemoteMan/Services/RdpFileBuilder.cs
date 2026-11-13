using System.Text;

namespace RemoteMan.Services;

/// <summary>Opções usadas para gerar o arquivo .rdp.</summary>
public sealed record RdpFileOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 3389;
    public string? Username { get; init; }
    public bool FullScreen { get; init; } = true;
    public int DesktopWidth { get; init; } = 1920;
    public int DesktopHeight { get; init; } = 1080;
    public bool MultiMonitor { get; init; }
    public bool RedirectClipboard { get; init; } = true;
    public bool RedirectDrives { get; init; }

    /// <summary>Servidor RD Gateway (vazio = sem gateway).</summary>
    public string GatewayHost { get; init; } = string.Empty;

    /// <summary>Não usa o gateway para endereços da rede local.</summary>
    public bool GatewayBypassLocal { get; init; }

    /// <summary>Usa as mesmas credenciais no gateway e no computador remoto.</summary>
    public bool GatewayUseSameCredentials { get; init; } = true;
}

/// <summary>Monta o conteúdo do arquivo .rdp lido pelo mstsc.</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public static class RdpFileBuilder
{
    // valores de gatewayusagemethod do mstsc
    private const int GatewayDirect = 0;
    private const int GatewayAlways = 1;
    private const int GatewayBypassLocalAddresses = 2;

    public static string Build(RdpFileOptions o)
    {
        var host = o.Host.Trim();
        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{(o.Port is > 0 and not 3389 ? $"{host}:{o.Port}" : host)}");
        if (!string.IsNullOrWhiteSpace(o.Username))
            sb.AppendLine($"username:s:{o.Username}");
        sb.AppendLine($"screen mode id:i:{(o.FullScreen ? 2 : 1)}");
        if (!o.FullScreen)
        {
            sb.AppendLine($"desktopwidth:i:{o.DesktopWidth}");
            sb.AppendLine($"desktopheight:i:{o.DesktopHeight}");
        }
        sb.AppendLine($"use multimon:i:{(o.MultiMonitor ? 1 : 0)}");
        sb.AppendLine($"redirectclipboard:i:{(o.RedirectClipboard ? 1 : 0)}");
        if (o.RedirectDrives)
            sb.AppendLine("drivestoredirect:s:*");
        sb.AppendLine("prompt for credentials:i:0");
        sb.AppendLine("authentication level:i:2");
        sb.AppendLine("enablecredsspsupport:i:1");
        sb.AppendLine("smart sizing:i:1");
        sb.AppendLine("dynamic resolution:i:1");

        var gateway = o.GatewayHost.Trim();
        if (gateway.Length > 0)
        {
            sb.AppendLine($"gatewayhostname:s:{gateway}");
            sb.AppendLine($"gatewayusagemethod:i:{(o.GatewayBypassLocal ? GatewayBypassLocalAddresses : GatewayAlways)}");
            sb.AppendLine("gatewayprofileusagemethod:i:1"); // usa as opções deste arquivo
            sb.AppendLine("gatewaycredentialssource:i:4");  // o usuário escolhe na hora, se pedido
            sb.AppendLine($"promptcredentialonce:i:{(o.GatewayUseSameCredentials ? 1 : 0)}");
        }
        else
        {
            sb.AppendLine($"gatewayusagemethod:i:{GatewayDirect}");
            sb.AppendLine("gatewayprofileusagemethod:i:1");
        }

        return sb.ToString();
    }
}
