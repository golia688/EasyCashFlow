using System.IO;
using System.Text;

namespace EasyCashFlow.Services;

/// <summary>I formati di export che il programma importa: il file CBI "saldi e movimenti" a 120 caratteri,
/// la stessa rendicontazione in XML (dal 08/11/2026) e l'elenco dei movimenti "Excel" della banca, con le
/// colonne abbinate dall'utente (dal 09/11/2026).</summary>
public enum FormatoExport { Cbi, Xml, Excel }

public static class Formati
{
    // Il formato si riconosce dal contenuto, non dall'estensione: un XML comincia con "<", dopo l'eventuale
    // BOM e gli spazi, ma una pagina HTML salvata come .xls è un Excel; un .xlsx è un archivio zip; un elenco
    // di colonne separate da tabulazioni o punti e virgola è un Excel (come l'export "Excel" di Relax Banking).
    public static FormatoExport Riconosci(string percorso)
    {
        var inizio = new byte[1024];
        int letti;
        using (var flusso = LettoreTabelle.Apri(percorso))
            letti = flusso.Read(inizio, 0, inizio.Length);

        // in Latin1 il BOM UTF-8 (EF BB BF) diventa "ï»¿"
        var testo = Encoding.Latin1.GetString(inizio, 0, letti).TrimStart('\u00EF', '\u00BB', '\u00BF', ' ', '\t', '\r', '\n');
        if (testo.StartsWith('<') && !LettoreTabelle.EHtml(testo))
            return FormatoExport.Xml;

        var estensione = Path.GetExtension(percorso).ToLowerInvariant();
        if (LettoreTabelle.ETabella(inizio, letti, estensione))
            return FormatoExport.Excel;

        return FormatoExport.Cbi;
    }
}
