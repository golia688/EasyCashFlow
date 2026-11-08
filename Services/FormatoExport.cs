using System.IO;
using System.Text;

namespace EasyCashFlow.Services;

/// <summary>I formati di export che il programma importa: il file CBI "saldi e movimenti" a 120 caratteri
/// e la stessa rendicontazione in XML (dal 08/11/2026).</summary>
public enum FormatoExport { Cbi, Xml }

public static class Formati
{
    // Il formato si riconosce dal contenuto, non dall'estensione (dal 08/11/2026): un XML comincia con "<",
    // dopo l'eventuale BOM e gli spazi. L'export "Excel" della banca è testo con le colonne separate da
    // tabulazioni: lo si riconosce per dire perché non si può usare, invece di un generico "non è un file CBI".
    public static FormatoExport Riconosci(string percorso)
    {
        var inizio = new byte[512];
        int letti;
        using (var flusso = File.OpenRead(percorso))
            letti = flusso.Read(inizio, 0, inizio.Length);

        // in Latin1 il BOM UTF-8 (EF BB BF) diventa "ï»¿"
        var testo = Encoding.Latin1.GetString(inizio, 0, letti).TrimStart('\u00EF', '\u00BB', '\u00BF', ' ', '\t', '\r', '\n');
        if (testo.StartsWith('<'))
            return FormatoExport.Xml;

        if (testo.StartsWith("Data contabile\t", StringComparison.OrdinalIgnoreCase) ||
            testo.StartsWith("Data operazione\t", StringComparison.OrdinalIgnoreCase))
            throw new FileNonCbiException("Il file è l'elenco dei movimenti in formato Excel della banca: ha solo date, " +
                "importo e descrizione, senza causale, riferimento e saldi, e il programma non può usarlo (non saprebbe " +
                "riconoscere i movimenti già importati né raggrupparli). In home banking esporta i movimenti in formato " +
                "CBI, di testo o XML.");

        return FormatoExport.Cbi;
    }
}
