using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>Un gruppo di causale come sta nel file di esportazione.</summary>
public record GruppoEsportato(string Causale, string Gruppo, TipoGruppo Tipo, bool Ricorrente, string? DescrizioneBanca = null);

/// <summary>Cosa c'è in un file letto: i gruppi validi e il motivo di ogni riga scartata.</summary>
public record FileGruppiLetto(IReadOnlyList<GruppoEsportato> Gruppi, IReadOnlyList<string> Scarti, DateTime? EsportatoIl);

/// <summary>
/// L'esportazione e l'importazione dei gruppi di causali (dal 06/11/2026), per non doverli rifare a mano su
/// un'installazione nuova o su un altro computer. Il file è JSON, leggibile anche con il Blocco note:
///
///   { "formato": "EasyCashFlow.GruppiCausali", "versione": 2, "esportatoIl": "...",
///     "gruppi": [ { "causale": "26", "gruppo": "Bonifici effettuati", "tipo": "Ordinario",
///                   "ricorrente": false, "descrizioneBanca": "Vostra disposizione a favore di" }, ... ] }
///
/// Dalla versione 2 (10/11) i gruppi sono per causale CBI, le prime due cifre della causale. La versione 1
/// aveva le causali complete di 4 caratteri: si legge ancora, e dà a ogni causale CBI il gruppo più usato
/// dalle sue causali complete nel file.
/// Contiene solo codici delle causali, nomi dei gruppi e una descrizione (dello standard CBI o, nei file di
/// prima, della banca): nessun movimento né importo, quindi non è cifrato. La descrizione serve a chi legge
/// il file e, all'importazione, alla finestra di conferma: nel database non si scrive.
/// </summary>
public static class FileGruppiCausali
{
    public const string Formato = "EasyCashFlow.GruppiCausali";
    public const int Versione = 2;
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
        var scarti = new List<string>();
        void Scarta(string motivo) => scarti.Add(motivo);

        foreach (var g in contenuto.Gruppi ?? [])
        {
            // la causale sono 4 caratteri (record 62, posizioni 42-45): 2 della causale CBI, 2 della banca;
            // 2 caratteri soli sono la regola di una causale CBI
            var causale = g?.Causale?.Trim().ToUpperInvariant() ?? "";
            var gruppo = g?.Gruppo?.Trim() ?? "";
            if (g is null)
                Scarta("riga vuota");
            else if (causale.Length is not (2 or 4) || !causale.All(char.IsAsciiLetterOrDigit))
                Scarta($"causale \"{causale}\" non valida");
            else if (gruppo == "" || gruppo.Length > LunghezzaMassimaGruppo)
                Scarta($"causale {causale}: nome del gruppo vuoto o più lungo di {LunghezzaMassimaGruppo} caratteri");
            else if (!Enum.TryParse<TipoGruppo>(g.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo)
                     || int.TryParse(g.Tipo, out _))
                Scarta($"causale {causale}: tipo \"{g.Tipo}\" non riconosciuto");
            else
                validi[causale] = new GruppoEsportato(causale, gruppo, tipo, g.Ricorrente,    // un doppione vale l'ultimo
                                                      string.IsNullOrWhiteSpace(g.DescrizioneBanca) ? null : g.DescrizioneBanca.Trim());
        }

        // le causali complete (file di prima del 10/11) diventano il gruppo della loro causale CBI: quello più
        // usato tra le sue causali nel file, se il file non ha già la causale CBI
        var perCodice = validi.Values.Where(g => g.Causale.Length == 2).ToDictionary(g => g.Causale);
        foreach (var famiglia in validi.Values.Where(g => g.Causale.Length == 4).GroupBy(g => g.Causale[..2]))
        {
            if (perCodice.ContainsKey(famiglia.Key)) continue;
            var piuUsato = famiglia.GroupBy(g => (g.Gruppo, g.Tipo, g.Ricorrente))
                                   .OrderByDescending(v => v.Count()).ThenBy(v => v.Key.Gruppo, StringComparer.Ordinal).First();
            perCodice[famiglia.Key] = new GruppoEsportato(famiglia.Key, piuUsato.Key.Gruppo, piuUsato.Key.Tipo,
                                                          piuUsato.Key.Ricorrente, CausaliCbi.Descrizione(famiglia.Key));
        }

        return new FileGruppiLetto(perCodice.Values.ToList(), scarti, contenuto.EsportatoIl);
    }
}
