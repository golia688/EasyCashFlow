using System.IO;
using System.Text.RegularExpressions;
using Markdig;

namespace EasyCashFlow.Services;

/// <summary>Un capitolo del manuale: Id per i collegamenti ("cap:importare"), titolo e testo in HTML.</summary>
public record CapitoloManuale(string Id, string Titolo, string Html);

/// <summary>Il manuale utente. Il testo è Manuale/manuale.md, incluso nell'eseguibile come risorsa:
/// ogni titolo "## Titolo {#id}" apre un capitolo. Nel testo i collegamenti "cap:id" portano a un altro
/// capitolo e "app:pagina" a una pagina del programma (vedi PaginaManuale e Main.razor).
/// Il servizio gestisce anche le richieste di aprire il manuale da un'altra pagina (icona "?").</summary>
public class ManualeService
{
    private static readonly Regex Titolo = new(@"^##\s+(.+?)\s*\{#([a-z0-9-]+)\}\s*$");

    private readonly LogService _log;
    private List<CapitoloManuale>? _capitoli;

    /// <summary>Qualcuno chiede di aprire il manuale, al capitolo indicato (null = il primo).</summary>
    public event Action<string?>? AperturaRichiesta;

    public ManualeService(LogService log)
    {
        _log = log;
    }

    public void Apri(string? capitolo) => AperturaRichiesta?.Invoke(capitolo);

    /// <summary>I capitoli, letti e convertiti la prima volta che servono.</summary>
    public IReadOnlyList<CapitoloManuale> Capitoli => _capitoli ??= Carica();

    private List<CapitoloManuale> Carica()
    {
        using var flusso = typeof(ManualeService).Assembly.GetManifestResourceStream("EasyCashFlow.manuale.md")
            ?? throw new InvalidOperationException("Il manuale non è incluso nell'eseguibile (risorsa EasyCashFlow.manuale.md)");
        using var lettore = new StreamReader(flusso);

        var capitoli = new List<CapitoloManuale>();
        string? id = null, titolo = null;
        var testo = new List<string>();

        void Chiudi()
        {
            if (id is not null)
                capitoli.Add(new CapitoloManuale(id, titolo!, Markdown.ToHtml(string.Join("\n", testo))));
            testo.Clear();
        }

        while (lettore.ReadLine() is { } riga)
        {
            if (Titolo.Match(riga) is { Success: true } m)
            {
                Chiudi();
                titolo = m.Groups[1].Value;
                id = m.Groups[2].Value;
            }
            else
            {
                testo.Add(riga);
            }
        }
        Chiudi();

        _log.Debug($"Manuale caricato: {capitoli.Count} capitoli");
        return capitoli;
    }
}
