namespace RemoteMan.Services.Importers;

/// <summary>Nomes de arquivo seguros e únicos para exportação.</summary>
public static class ExportNames
{
    // caracteres proibidos em nomes de arquivo no Windows (fixos, para não depender do SO em que roda)
    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>"Servidor: web/01" → "Servidor_ web_01.rdp"; repete com " (2)", " (3)"… se já usado.</summary>
    public static string FileName(string name, string extension, ISet<string> used)
    {
        var chars = name.Trim().Select(ch => char.IsControl(ch) || Invalid.Contains(ch) ? '_' : ch).ToArray();
        var baseName = new string(chars).Trim().TrimEnd('.');
        if (baseName.Length == 0)
            baseName = "conexao";

        var candidate = baseName + extension;
        for (var i = 2; !used.Add(candidate); i++)
            candidate = $"{baseName} ({i}){extension}";
        return candidate;
    }
}
