using RemoteMan.Models;

namespace RemoteMan.Services.Importers;

/// <summary>
/// Converte uma sessão salva do PuTTY (valores da chave do registro) em conexão. Só sessões SSH são importadas.
/// </summary>
public static class PuttySessionParser
{
    /// <param name="keyName">Nome da subchave (o PuTTY codifica espaços e símbolos como %XX).</param>
    /// <param name="values">Valores da sessão (HostName, PortNumber, Protocol, UserName, PublicKeyFile…).</param>
    /// <returns>A conexão, ou null se a sessão não for SSH ou não tiver host.</returns>
    public static ImportedConnection? Parse(string keyName, IReadOnlyDictionary<string, object?> values)
    {
        var protocol = Str("Protocol");
        if (!protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            return null;

        var hostText = Str("HostName");
        if (hostText.Length == 0 || !ConnectionAddress.TryParse(hostText, out var address, out _))
            return null;

        var port = values.TryGetValue("PortNumber", out var p) && p is int n and > 0 and <= 65535
            ? n
            : address.Port ?? ConnectionAddress.DefaultSshPort;

        var user = Str("UserName");
        if (user.Length == 0 && address.User is not null)
            user = address.User;

        var name = Uri.UnescapeDataString(keyName);
        var key = Str("PublicKeyFile");

        return new ImportedConnection
        {
            Protocol = ConnectionProtocol.Ssh,
            Name = name.Trim().Length > 0 ? name.Trim() : address.Host,
            Host = address.Host,
            Port = port,
            Username = user,
            // o PuTTY usa chaves .ppk, que o OpenSSH não lê
            Warning = key.Length > 0
                ? "A chave .ppk do PuTTY não foi importada: converta-a no PuTTYgen (Conversions > Export OpenSSH key) e informe em Editar."
                : null,
        };

        string Str(string k) => values.TryGetValue(k, out var v) && v is string s ? s.Trim() : string.Empty;
    }
}
