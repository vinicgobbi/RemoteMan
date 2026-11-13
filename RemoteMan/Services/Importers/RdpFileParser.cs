using RemoteMan.Models;

namespace RemoteMan.Services.Importers;

/// <summary>
/// Lê arquivos .rdp (formato "chave:tipo:valor" do mstsc). Senhas salvas no arquivo não são importadas.
/// </summary>
public static class RdpFileParser
{
    /// <param name="name">Nome da conexão (normalmente o nome do arquivo, sem extensão).</param>
    /// <param name="content">Conteúdo do arquivo (já decodificado; o mstsc grava em UTF-16).</param>
    public static bool TryParse(string name, string content, out ImportedConnection result, out string error)
    {
        result = null!;
        error = string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            var first = line.IndexOf(':');
            var second = first < 0 ? -1 : line.IndexOf(':', first + 1);
            if (second < 0)
                continue;
            // a primeira ocorrência vale (como no mstsc)
            values.TryAdd(line[..first].Trim(), line[(second + 1)..].Trim());
        }

        if (!values.TryGetValue("full address", out var address) || string.IsNullOrWhiteSpace(address))
        {
            error = $"{name}: o arquivo não tem o endereço do servidor (full address).";
            return false;
        }
        if (!ConnectionAddress.TryParse(address, out var parsed, out var addressError))
        {
            error = $"{name}: {addressError}";
            return false;
        }

        var port = parsed.Port ?? Int("server port") ?? ConnectionAddress.DefaultRdpPort;
        var (domain, user) = ConnectionAddress.SplitDomainUser(Str("username"));

        var warnings = new List<string>();
        if (Int("gatewayusagemethod") is 1 or 2 && Str("gatewayhostname").Length > 0)
            warnings.Add("usa RD Gateway, que ainda não é suportado");
        if (Str("alternate shell").Length > 0 || Str("remoteapplicationprogram").Length > 0)
            warnings.Add("é um RemoteApp/programa remoto; será importado como área de trabalho completa");

        var screenMode = Int("screen mode id");
        result = new ImportedConnection
        {
            Protocol = ConnectionProtocol.Rdp,
            Name = name.Trim().Length > 0 ? name.Trim() : parsed.Host,
            Host = parsed.Host,
            Port = port,
            Username = user,
            Domain = domain,
            FullScreen = screenMode is null ? null : screenMode == 2,
            DesktopWidth = Int("desktopwidth"),
            DesktopHeight = Int("desktopheight"),
            MultiMonitor = Bool("use multimon"),
            RedirectClipboard = Bool("redirectclipboard"),
            RedirectDrives = values.TryGetValue("drivestoredirect", out var drives) ? drives.Length > 0 : null,
            AdminSession = Bool("administrative session") ?? Bool("connect to console"),
            Warning = warnings.Count > 0 ? $"Este arquivo {string.Join(" e ", warnings)}." : null,
        };
        return true;

        string Str(string key) => values.TryGetValue(key, out var v) ? v : string.Empty;
        int? Int(string key) => int.TryParse(Str(key), out var v) ? v : null;
        bool? Bool(string key) => Int(key) is { } v ? v != 0 : null;
    }
}
