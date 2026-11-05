using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// Come i dati dei movimenti si scrivono nel log (docs/07-sicurezza.md, sezione 8). Restano in chiaro
/// solo date, codici delle causali, numeri di riga, lunghezze e conteggi; importi, testi, IBAN e conti
/// si mascherano. Tutto il codice che scrive dati nel log passa da qui, così le regole sono uguali ovunque.
/// </summary>
public static class Mascherato
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");
    private static readonly Regex DataIso = new(@"^\d{4}-\d{2}-\d{2}( \d{2}:\d{2}(:\d{2})?)?$");     // date, anche con l'ora
    private static readonly Regex TraApici = new(@"'([^']*)'");

    // Chiave dell'impronta: finché il database non fornisce la sua (ImpostaChiave), una casuale per
    // questa esecuzione. Prima di aprire il database nel log non finiscono dati dei movimenti.
    private static byte[] _chiave = RandomNumberGenerator.GetBytes(32);

    /// <summary>La chiave salvata nel database: le impronte restano confrontabili tra un avvio e l'altro.</summary>
    public static void ImpostaChiave(byte[] chiave) => _chiave = chiave;

    /// <summary>Impronta breve di un testo: due testi uguali hanno la stessa, ma non si risale al testo.</summary>
    public static string Impronta(string testo) =>
        Convert.ToHexString(HMACSHA256.HashData(_chiave, Encoding.UTF8.GetBytes(testo)), 0, 2).ToLowerInvariant();

    /// <summary>Solo segno e forma delle cifre: "-1.234,56" diventa "-#.###,##".</summary>
    public static string Importo(decimal importo)
    {
        var testo = Math.Abs(importo).ToString("N2", Italiano);
        return (importo < 0 ? "-" : importo > 0 ? "+" : "") + Regex.Replace(testo, @"\d", "#");
    }

    /// <summary>Descrizioni, riferimenti, testo cercato: lunghezza e impronta.</summary>
    public static string Testo(string? testo) =>
        string.IsNullOrEmpty(testo) ? "[vuoto]" : $"[{testo.Length} car. #{Impronta(testo)}]";

    /// <summary>Nomi scritti dall'utente (conti, gruppi): il tipo di nome e l'impronta.</summary>
    public static string Nome(string cosa, string? nome) =>
        string.IsNullOrEmpty(nome) ? $"[{cosa} vuoto]" : $"[{cosa} #{Impronta(nome)}]";

    /// <summary>Codice del conto (ABI + CAB + numero): solo le ultime 4 cifre.</summary>
    public static string Conto(string? codice) =>
        string.IsNullOrEmpty(codice) ? "[conto vuoto]" : "…" + codice.Trim()[^Math.Min(4, codice.Trim().Length)..];

    /// <summary>IBAN: paese e ultime 4 cifre.</summary>
    public static string Iban(string? iban) =>
        string.IsNullOrEmpty(iban) || iban.Length < 6 ? "non disponibile" : $"{iban[..2]}…{iban[^4..]}";

    /// <summary>Forma di un testo senza il contenuto: cifre → 9, lettere → X, il resto com'è.
    /// Per le righe del file CBI: si vede dove un campo ha il formato sbagliato.</summary>
    public static string Forma(string? testo)
    {
        if (string.IsNullOrEmpty(testo)) return "";
        var sb = new StringBuilder(testo.Length);
        foreach (var c in testo)
            sb.Append(char.IsDigit(c) ? '9' : char.IsLetter(c) ? 'X' : c);
        return sb.ToString();
    }

    /// <summary>Messaggi delle eccezioni: .NET mette tra apici il valore che non ha saputo leggere
    /// ("The input string '12A' was not in a correct format"), che può essere un pezzo di importo o di testo.</summary>
    public static string Messaggio(string? messaggio) =>
        messaggio is null ? "" : TraApici.Replace(messaggio, m => $"'{Forma(m.Groups[1].Value)}'");

    /// <summary>Percorsi: il nome dell'utente di Windows non finisce nel log.</summary>
    public static string Percorso(string? percorso)
    {
        if (string.IsNullOrEmpty(percorso)) return "";
        var utente = Environment.UserName;
        return utente.Length == 0 ? percorso : percorso.Replace(utente, "<utente>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Un movimento in una riga: date e causale in chiaro, il resto mascherato.</summary>
    public static string Movimento(MovimentoCbi m) =>
        $"conto {Conto(m.Conto)}, data contabile {m.DataContabile:dd/MM/yyyy}, data valuta {m.DataValuta:dd/MM/yyyy}, " +
        $"importo {Importo(m.Importo)}, causale {m.Causale}, rif. {Testo(m.Riferimento)}, " +
        $"descrizione {Testo(m.DescrizioneBreve)}, dettaglio {Testo(m.DescrizioneEstesa)}";

    /// <summary>La chiave di un movimento: la data in chiaro e l'impronta di tutta la chiave.</summary>
    public static string Chiave(ChiaveMovimento k) =>
        $"del {k.DataContabile:dd/MM/yyyy} #{Impronta($"{k.Conto}|{k.DataContabile:yyyy-MM-dd}|{k.Riferimento}|{k.Importo}")}";

    /// <summary>Un parametro di una query: date, numeri piccoli e codici delle causali in chiaro,
    /// importi (in centesimi) e testi mascherati.</summary>
    public static string Parametro(string nome, object? valore)
    {
        var n = nome.ToLowerInvariant();
        return valore switch
        {
            null or DBNull => "NULL",
            string s when DataIso.IsMatch(s) => s,
            string s when n.Contains("causal") || n.Contains("tipo") => s,
            string s when n.Contains("conto") => Conto(s),
            string s => Testo(s),
            long or int when n.Contains("importo") => Importo(Convert.ToDecimal(valore) / 100m),
            decimal d when n.Contains("importo") => Importo(d),
            _ => Convert.ToString(valore, CultureInfo.InvariantCulture) ?? ""
        };
    }
}
