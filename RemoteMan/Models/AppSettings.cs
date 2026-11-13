namespace RemoteMan.Models;

/// <summary>Material de fundo da janela (efeitos nativos do Windows 11).</summary>
public enum BackdropKind
{
    /// <summary>Mica: tingido sutil com as cores do papel de parede.</summary>
    Mica,

    /// <summary>Mica Alt: variante mais escura/contrastada do Mica.</summary>
    MicaAlt,

    /// <summary>Acrílico: desfoque translúcido que deixa ver o que está atrás da janela.</summary>
    Acrylic,
}

/// <summary>Preferências do app, salvas em %LOCALAPPDATA%\RemoteMan\settings.json.</summary>
public sealed class AppSettings
{
    public BackdropKind Backdrop { get; set; } = BackdropKind.Mica;

    /// <summary>Verifica no GitHub, ao abrir o app (no máximo uma vez por dia), se há versão nova.</summary>
    public bool CheckForUpdates { get; set; } = true;

    public DateTime? LastUpdateCheck { get; set; }
}
