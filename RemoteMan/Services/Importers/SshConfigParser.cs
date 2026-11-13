using System.Text.RegularExpressions;
using RemoteMan.Models;

namespace RemoteMan.Services.Importers;

/// <summary>
/// Lê um arquivo de configuração do OpenSSH (~/.ssh/config). Cada alias concreto de "Host" vira uma conexão;
/// padrões com curingas (ex.: "Host *") não viram conexões, mas fornecem valores padrão, como no ssh:
/// para cada opção vale o primeiro valor encontrado entre os blocos que casam com o alias.
/// </summary>
public static class SshConfigParser
{
    private sealed class Block(string[] patterns)
    {
        public string[] Patterns { get; } = patterns;
        public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="content">Conteúdo do arquivo.</param>
    /// <param name="homeDir">Pasta do usuário, para expandir "~" e "%d" em IdentityFile.</param>
    public static ImportResult Parse(string content, string homeDir)
    {
        var blocks = new List<Block>();
        var global = new Block(["*"]); // opções antes do primeiro Host valem para todos
        var current = global;
        var aliases = new List<string>();
        var errors = new List<string>();

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var (keyword, args) = SplitKeyword(line);
            if (keyword.Length == 0)
                continue;

            if (keyword.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                var patterns = Tokenize(args);
                current = new Block(patterns);
                blocks.Add(current);
                aliases.AddRange(patterns.Where(p => !IsPattern(p)));
            }
            else if (keyword.Equals("Match", StringComparison.OrdinalIgnoreCase))
            {
                current = new Block([]); // condições de Match não são avaliadas: o bloco é ignorado
            }
            else if (keyword.Equals("Include", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Include {args} foi ignorado: importe esse arquivo separadamente.");
            }
            else
            {
                // primeiro valor vale; o resto da linha é o argumento (IdentityFile pode ter aspas)
                current.Options.TryAdd(keyword, Unquote(args));
            }
        }

        // o bloco global (antes de qualquer Host) é avaliado primeiro, como no ssh
        blocks.Insert(0, global);

        var connections = new List<ImportedConnection>();
        foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var block in blocks.Where(b => Matches(b.Patterns, alias)))
                foreach (var (k, v) in block.Options)
                    options.TryAdd(k, v);

            var host = Get(options, "HostName") is { Length: > 0 } h ? h.Replace("%h", alias) : alias;
            var portText = Get(options, "Port");
            var port = ConnectionAddress.DefaultSshPort;
            if (portText is not null && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
            {
                errors.Add($"{alias}: porta inválida ({portText}).");
                continue;
            }

            var warning = Get(options, "ProxyJump") is { Length: > 0 } jump && !jump.Equals("none", StringComparison.OrdinalIgnoreCase)
                ? $"Usa ProxyJump ({jump}), que não é aplicado pelo RemoteMan."
                : Get(options, "ProxyCommand") is { Length: > 0 } && !Get(options, "ProxyCommand")!.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? "Usa ProxyCommand, que não é aplicado pelo RemoteMan."
                    : null;

            connections.Add(new ImportedConnection
            {
                Protocol = ConnectionProtocol.Ssh,
                Name = alias,
                Host = host,
                Port = port,
                Username = Get(options, "User") ?? string.Empty,
                KeyFile = ExpandPath(Get(options, "IdentityFile"), homeDir),
                Warning = warning,
            });
        }

        return new ImportResult(connections, errors);
    }

    private static string? Get(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var v) ? v : null;

    /// <summary>"Chave valor", "Chave=valor" ou "Chave = valor".</summary>
    private static (string Keyword, string Args) SplitKeyword(string line)
    {
        var i = 0;
        while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '=')
            i++;
        var keyword = line[..i];
        var rest = line[i..].TrimStart();
        if (rest.StartsWith('='))
            rest = rest[1..].TrimStart();
        return (keyword, rest.Trim());
    }

    private static string[] Tokenize(string args) =>
        Regex.Matches(args, "\"([^\"]*)\"|(\\S+)")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .Where(t => t.Length > 0)
            .ToArray();

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static bool IsPattern(string p) => p.IndexOfAny(['*', '?', '!']) >= 0;

    /// <summary>Casa o alias com a lista de padrões (com negação "!"), como o ssh faz.</summary>
    private static bool Matches(string[] patterns, string alias)
    {
        var matched = false;
        foreach (var pattern in patterns)
        {
            var negated = pattern.StartsWith('!');
            var glob = negated ? pattern[1..] : pattern;
            var regex = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            if (Regex.IsMatch(alias, regex, RegexOptions.IgnoreCase))
            {
                if (negated)
                    return false;
                matched = true;
            }
        }
        return matched;
    }

    private static string ExpandPath(string? path, string homeDir)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        if (path == "~")
            return homeDir;
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
            path = Path.Combine(homeDir, path[2..]);
        path = path.Replace("%d", homeDir);
        // caminhos no estilo Unix ("/" ) são aceitos pelo ssh do Windows, mas exibimos no formato nativo
        return path.Replace('/', Path.DirectorySeparatorChar);
    }
}
