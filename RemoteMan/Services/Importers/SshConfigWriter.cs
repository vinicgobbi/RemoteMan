using System.Text;
using RemoteMan.Models;

namespace RemoteMan.Services.Importers;

/// <summary>Gera um arquivo no formato do ~/.ssh/config a partir das conexões SSH.</summary>
public static class SshConfigWriter
{
    public static string Write(IEnumerable<ImportedConnection> connections, DateTime exportedAt)
    {
        var sb = new StringBuilder();
        sb.Append("# Exportado do RemoteMan em ").Append(exportedAt.ToString("yyyy-MM-dd HH:mm")).Append('\n');
        sb.Append("# Para usar, copie para ~/.ssh/config ou inclua com: Include <caminho deste arquivo>\n");

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in connections.Where(c => c.Protocol == ConnectionProtocol.Ssh))
        {
            var alias = UniqueAlias(c.Name, c.Host, used);
            sb.Append('\n').Append("Host ").Append(alias).Append('\n');
            sb.Append("    HostName ").Append(c.Host.Trim()).Append('\n');
            if (c.Username.Trim().Length > 0)
                sb.Append("    User ").Append(c.Username.Trim()).Append('\n');
            if (c.Port != ConnectionAddress.DefaultSshPort)
                sb.Append("    Port ").Append(c.Port).Append('\n');
            if (c.KeyFile.Trim().Length > 0)
                sb.Append("    IdentityFile ").Append(Quote(c.KeyFile.Trim())).Append('\n');
            if (SshArguments.NormalizeJumpHost(c.JumpHost) is { Length: > 0 } jump)
                sb.Append("    ProxyJump ").Append(jump).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Alias válido no ssh (sem espaços nem curingas) e único no arquivo.</summary>
    public static string UniqueAlias(string name, string host, ISet<string> used)
    {
        var chars = name.Trim()
            .Select(ch => char.IsWhiteSpace(ch) ? '-' : ch)
            .Where(ch => ch is not ('*' or '?' or '!' or '#' or '"' or ','))
            .ToArray();
        var alias = new string(chars).Trim('-');
        if (alias.Length == 0)
            alias = host.Trim();

        var candidate = alias;
        for (var i = 2; !used.Add(candidate); i++)
            candidate = $"{alias}-{i}";
        return candidate;
    }

    private static string Quote(string path) => path.Any(char.IsWhiteSpace) ? $"\"{path}\"" : path;
}
