namespace EasyCashFlow.Models;

/// <summary>Un colore principale selezionabile, con una tonalità per il tema chiaro
/// e una più luminosa per il tema scuro (sullo sfondo scuro un colore pieno risulta spento).</summary>
public record ColoreTema(string Chiave, string Nome, string Chiaro, string Scuro)
{
    // Palette pastello. Nel tema scuro sono pastelli veri; nel tema chiaro le stesse tinte
    // più "polverose", perché un pastello chiaro su fondo chiaro non si leggerebbe.
    // Ogni tonalità ha almeno 4,5:1 di contrasto WCAG sulle superfici del suo tema, comprese
    // le righe alternate delle griglie (nel tema chiaro le superfici sono avorio, vedi Main.razor).
    // Niente rosso né verde: sono già i colori di uscite ed entrate negli importi.
    public static readonly IReadOnlyList<ColoreTema> Tavolozza =
    [
        new("Lavanda", "Lavanda", "#6A5FC1", "#B3ACF2"),
        new("Azzurro", "Azzurro", "#3D6FB6", "#93B8EE"),
        new("Salvia", "Salvia", "#38746A", "#8FCBBF"),
        new("Malva", "Malva", "#8A5A9E", "#D2AEE3"),
        new("Pesca", "Pesca", "#A0552A", "#F2B98F"),
        new("Ardesia", "Ardesia", "#56657A", "#AEBBCB")
    ];

    // il colore al primo avvio, scelto il 31/10/2026 (insieme al tema scuro, alle icone piene e ai pulsanti classici)
    public static ColoreTema Predefinito => Tavolozza.First(c => c.Chiave == "Salvia");

    // Colori legati a un significato: fissi, non selezionabili dall'utente.
    public static readonly ColoreTema Entrate = new("Entrate", "Entrate", "#2F7D4F", "#86C99B");
    public static readonly ColoreTema Uscite = new("Uscite", "Uscite", "#B4463F", "#EE9A94");
    public static readonly ColoreTema Avviso = new("Avviso", "Avviso", "#8A5F00", "#F0CD7A");
    public static readonly ColoreTema Informazione = new("Informazione", "Informazione", "#3D6FB6", "#93B8EE");
    public static readonly ColoreTema Secondario = new("Secondario", "Secondario", "#6B6560", "#B9B4AE");

    // Chiavi della tavolozza precedente (colori accesi), salvate nel database prima del 30/10/2026:
    // ognuna passa al pastello della stessa famiglia.
    private static readonly Dictionary<string, string> ChiaviPrecedenti = new()
    {
        ["Indaco"] = "Lavanda",
        ["Blu"] = "Azzurro",
        ["Petrolio"] = "Salvia",
        ["Viola"] = "Malva",
        ["Arancio"] = "Pesca",
        ["Grafite"] = "Ardesia"
    };

    /// <summary>Il colore del testo sopra uno sfondo pieno di questo colore (pulsanti, chip):
    /// bianco o quasi nero, quello con il contrasto maggiore secondo la formula WCAG.</summary>
    public static string TestoSu(string sfondo)
    {
        var luminanza = Luminanza(sfondo);
        var contrastoBianco = 1.05 / (luminanza + 0.05);
        var contrastoNero = (luminanza + 0.05) / (Luminanza(QuasiNero) + 0.05);
        return contrastoBianco >= contrastoNero ? "#FFFFFF" : QuasiNero;
    }

    private const string QuasiNero = "#1A1A1A";

    // luminanza relativa WCAG di un colore "#RRGGBB"
    private static double Luminanza(string esadecimale)
    {
        double Canale(int inizio)
        {
            var c = Convert.ToInt32(esadecimale.Substring(inizio, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Canale(1) + 0.7152 * Canale(3) + 0.0722 * Canale(5);
    }

    public static ColoreTema DaChiave(string? chiave)
    {
        if (chiave is not null && ChiaviPrecedenti.TryGetValue(chiave, out var nuova))
            chiave = nuova;
        return Tavolozza.FirstOrDefault(c => c.Chiave == chiave) ?? Predefinito;
    }
}
