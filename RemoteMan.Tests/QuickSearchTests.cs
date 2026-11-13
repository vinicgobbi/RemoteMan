using RemoteMan.Services;
using Xunit;

namespace RemoteMan.Tests;

public class QuickSearchTests
{
    private static QuickSearchEntry E(string name, string host = "", string group = "", string user = "",
        bool favorite = false, int? daysAgo = null) =>
        new(name, host, group, user, favorite, daysAgo is { } d ? new DateTime(2026, 1, 1).AddDays(-d) : null);

    private static List<string> Names(IEnumerable<QuickSearchEntry> items, string? query, int max = QuickSearch.MaxResults) =>
        QuickSearch.Search(items, e => e, query, max).Select(e => e.Name).ToList();

    [Theory]
    [InlineData("web", 0)]   // começo do nome
    [InlineData("01", 1)]    // meio do nome
    [InlineData("10.0", 2)]  // host
    [InlineData("prod", 3)]  // grupo
    [InlineData("deploy", 3)] // usuário
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Tier(string? query, int expected) =>
        Assert.Equal(expected, QuickSearch.Tier(E("Web01", "10.0.0.1", "Produção", "deploy"), query));

    [Fact]
    public void Tier_NaoCasa() =>
        Assert.Null(QuickSearch.Tier(E("Web01", "10.0.0.1"), "banco"));

    [Fact]
    public void Tier_VariosTermosPrecisamCasarTodos()
    {
        var e = E("Web01", "10.0.0.1", "Produção");
        Assert.Equal(3, QuickSearch.Tier(e, "web prod"));
        Assert.Null(QuickSearch.Tier(e, "web homolog"));
    }

    [Fact]
    public void Ordena_PorRelevanciaFavoritoERecente()
    {
        var items = new[]
        {
            E("Banco web", host: "db"),                  // nome contém
            E("web-antigo", daysAgo: 30),                // começa com, usado há 30 dias
            E("web-recente", daysAgo: 1),                // começa com, usado ontem
            E("web-favorito", favorite: true),           // começa com, favorito
            E("Proxy", host: "web.empresa.com"),         // host
        };

        Assert.Equal(["web-favorito", "web-recente", "web-antigo", "Banco web", "Proxy"], Names(items, "web"));
    }

    [Fact]
    public void SemTermo_FavoritosERecentesPrimeiroDepoisPorNome()
    {
        var items = new[] { E("Zeta"), E("Alfa"), E("Recente", daysAgo: 2), E("Favorito", favorite: true) };
        Assert.Equal(["Favorito", "Recente", "Alfa", "Zeta"], Names(items, ""));
    }

    [Fact]
    public void LimitaQuantidade()
    {
        var items = Enumerable.Range(1, 20).Select(i => E($"srv{i:00}")).ToList();
        Assert.Equal(QuickSearch.MaxResults, Names(items, "srv").Count);
        Assert.Equal(3, Names(items, "srv", max: 3).Count);
    }
}
