namespace RemoteMan.Services;

public sealed record ReleaseAsset(string Name, string DownloadUrl);

/// <summary>Release publicado no GitHub.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, IReadOnlyList<ReleaseAsset> Assets);

/// <summary>Regras de comparação de versões e escolha do arquivo de atualização.</summary>
/// <remarks>Classe pura (sem WinUI/rede) para poder ser testada isoladamente.</remarks>
public static class UpdateRelease
{
    /// <summary>"v1.2.3", "1.2.3-beta+abc" ou "1.2" → 1.2.3 / 1.2.3 / 1.2.0. Null se inválida.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var s = tag.Trim().TrimStart('v', 'V');
        var end = s.IndexOfAny(['-', '+']);
        if (end >= 0)
            s = s[..end];

        return Version.TryParse(s, out var v) ? Normalize(v) : null;
    }

    public static bool IsNewer(Version current, Version latest) => Normalize(latest) > Normalize(current);

    /// <summary>
    /// Escolhe o arquivo para baixar: o instalador quando o app foi instalado, o zip portátil caso contrário.
    /// Se o preferido não existir no release, usa o outro.
    /// </summary>
    /// <param name="arch">"x64" ou "arm64".</param>
    public static ReleaseAsset? PickAsset(IEnumerable<ReleaseAsset> assets, string arch, bool installed)
    {
        var list = assets.ToList();
        var installer = list.FirstOrDefault(a =>
            a.Name.StartsWith("RemoteMan-Setup-", StringComparison.OrdinalIgnoreCase)
            && a.Name.EndsWith($"-{arch}.exe", StringComparison.OrdinalIgnoreCase));
        var portable = list.FirstOrDefault(a =>
            a.Name.EndsWith($"-{arch}-portable.zip", StringComparison.OrdinalIgnoreCase));

        return installed ? installer ?? portable : portable ?? installer;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
