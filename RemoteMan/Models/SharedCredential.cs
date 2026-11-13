namespace RemoteMan.Models;

/// <summary>
/// Credencial reutilizada por várias conexões (ex.: administrador do domínio). A senha fica no
/// Gerenciador de Credenciais do Windows (ver <see cref="Services.CredentialStore.KeyForShared"/>).
/// </summary>
public sealed class SharedCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;

    /// <summary>Usuário no formato DOMINIO\usuario quando houver domínio.</summary>
    public string FullUsername =>
        string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain.Trim()}\\{Username}";

    /// <summary>Texto da lista: "Admin do domínio (EMPRESA\admin)".</summary>
    public string DisplayText =>
        string.IsNullOrWhiteSpace(FullUsername) ? Name : $"{Name} ({FullUsername})";
}
