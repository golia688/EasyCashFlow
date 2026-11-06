using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>Un gruppo di causale come sta nel file di esportazione.</summary>
public record GruppoEsportato(string Causale, string Gruppo, TipoGruppo Tipo, bool Ricorrente, string? DescrizioneBanca = null);

/// <summary>Cosa c'è in un file letto: i gruppi validi e quante righe sono state scartate (con il motivo della prima).</summary>
public record FileGruppiLetto(IReadOnlyList<GruppoEsportato> Gruppi, int Scartate, string? PrimoScarto, DateTime? EsportatoIl);

/// <summary>
/// L'esportazione e l'importazione dei gruppi di causali (dal 06/11/2026), per non doverli rifare a mano su
/// un'installazione nuova o su un altro computer. Il file è JSON, leggibile anche con il Blocco note:
///
///   { "formato": "EasyCashFlow.GruppiCausali", "versione": 1, "esportatoIl": "...",
///     "gruppi": [ { "causale": "2611", "gruppo": "Bonifici effettuati", "tipo": "Ordinario",
///                   "ricorrente": false, "descrizioneBanca": "..." }, ... ] }
///
/// Contiene solo codici delle causali, nomi dei gruppi e la descrizione della banca (un testo generico come
/// "Bonifico tramite Internet Banking"): nessun movimento né importo, quindi non è cifrato. La descrizione
/// serve a chi legge il file e all'importazione si ignora.
/// </summary>
public static class FileGruppiCausali
{
    public const string Formato = "EasyCashFlow.GruppiCausali";
    public const int Versione = 1;
    public const int LunghezzaMassimaGruppo = 60;

    public static string NomeFile(DateTime quando) => $"EasyCashFlow-gruppi-causali-{quando:yyyy-MM-dd}.json";

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // lettere accentate leggibili
    };

    // nel file il tipo è testo ("Ordinario"...): letto come testo, un valore sbagliato scarta solo la sua riga
    private record Riga(string? Causale, string? Gruppo, string? Tipo, bool Ricorrente, string? DescrizioneBanca);
    private record Contenuto(string Formato, int Versione, DateTime? EsportatoIl, List<Riga?>? Gruppi);

    public static void Scrivi(string percorso, IEnumerable<GruppoEsportato> gruppi)
    {
        var adesso = DateTime.Now;
        var contenuto = new Contenuto(Formato, Versione,
            new DateTime(adesso.Year, adesso.Month, adesso.Day, adesso.Hour, adesso.Minute, adesso.Second),
            gruppi.OrderBy(g => g.Causale, StringComparer.Ordinal)
                  .Select(g => (Riga?)new Riga(g.Causale, g.Gruppo, g.Tipo.ToString(), g.Ricorrente, g.DescrizioneBanca))
                  .ToList());
        PercorsiDati.ScriviAtomico(percorso, JsonSerializer.Serialize(contenuto, Opzioni));
    }

    /// <summary>Legge e controlla un file esportato. Lancia InvalidDataException con un messaggio per l'utente
    /// se il file non è un'esportazione dei gruppi di causali; le singole righe sbagliate si scartano.</summary>
    public static FileGruppiLetto Leggi(string percorso)
    {
        Contenuto? contenuto;
        try
        {
            contenuto = JsonSerializer.Deserialize<Contenuto>(File.ReadAllText(percorso), Opzioni);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Il file non è un'esportazione dei gruppi di causali di EasyCashFlow (non è un file JSON valido).");
        }

        if (contenuto is null || contenuto.Formato != Formato)
            throw new InvalidDataException("Il file non è un'esportazione dei gruppi di causali di EasyCashFlow.");
        if (contenuto.Versione > Versione)
            throw new InvalidDataException($"Il file è stato esportato da una versione più recente di EasyCashFlow (formato {contenuto.Versione}): aggiorna il programma.");

        var validi = new Dictionary<string, GruppoEsportato>();
        int scartate = 0;
        string? primoScarto = null;

        void Scarta(string motivo)
        {
            scartate++;
            primoScarto ??= motivo;
        }

        foreach (var g in contenuto.Gruppi ?? [])
        {
            // la causale CBI sono 4 caratteri (record 62, posizioni 42-45): 2 della causale CBI, 2 della banca
            var causale = g?.Causale?.Trim() ?? "";
            var gruppo = g?.Gruppo?.Trim() ?? "";
            if (g is null || causale.Length != 4 || !causale.All(char.IsAsciiLetterOrDigit))
                Scarta($"causale \"{causale}\" non valida");
            else if (gruppo == "" || gruppo.Length > LunghezzaMassimaGruppo)
                Scarta($"causale {causale}: nome del gruppo vuoto o più lungo di {LunghezzaMassimaGruppo} caratteri");
            else if (!Enum.TryParse<TipoGruppo>(g.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo)
                     || int.TryParse(g.Tipo, out _))
                Scarta($"causale {causale}: tipo \"{g.Tipo}\" non riconosciuto");
            else
                validi[causale] = new GruppoEsportato(causale, gruppo, tipo, g.Ricorrente);   // un doppione vale l'ultimo
        }

        return new FileGruppiLetto(validi.Values.ToList(), scartate, primoScarto, contenuto.EsportatoIl);
    }
}
