using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RemoteMan.Services;

/// <summary>Consulta o último release do RemoteMan na API pública do GitHub.</summary>
public static class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/vinicgobbi/RemoteMan/releases/latest";

    // criado no primeiro uso: depende de CurrentVersion (User-Agent), e a ordem de inicialização
    // de campos estáticos não pode decidir isso
    private static readonly Lazy<HttpClient> Http = new(CreateClient);

    public static Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : new Version(0, 0, 0);

    /// <summary>"x64" ou "arm64", no padrão dos nomes dos arquivos do release.</summary>
    public static string Architecture =>
        RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";

    /// <summary>True quando o app roda da pasta do instalador (e não de um zip portátil).</summary>
    public static bool IsInstalled
    {
        get
        {
            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RemoteMan");
            return Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)
                .Equals(installDir, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Último release publicado, ou null se ainda não houver nenhum.</summary>
    public static async Task<ReleaseInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        using var response = await Http.Value.GetAsync(LatestReleaseUrl, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = json.RootElement;

        var tag = root.GetProperty("tag_name").GetString();
        if (UpdateRelease.ParseVersion(tag) is not { } version)
            return null;

        var assets = root.TryGetProperty("assets", out var list)
            ? list.EnumerateArray()
                .Select(a => new ReleaseAsset(
                    a.GetProperty("name").GetString() ?? string.Empty,
                    a.GetProperty("browser_download_url").GetString() ?? string.Empty))
                .ToList()
            : [];

        return new ReleaseInfo(version, tag!, root.GetProperty("html_url").GetString() ?? string.Empty, assets);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // a API do GitHub exige User-Agent; identifica só o app e a versão
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RemoteMan", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
