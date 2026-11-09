using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace EasyCashFlow.Services;

/// <summary>Un foglio letto da un file "Excel" della banca: tutte le celle come testo, riga per riga.
/// Le date di un .xlsx diventano gg/mm/aaaa e i numeri hanno il punto come separatore dei decimali.</summary>
public record TabellaExcel(IReadOnlyList<string[]> Righe, string Tipo)
{
    public int Colonne => Righe.Count == 0 ? 0 : Righe.Max(r => r.Length);

    public string Cella(int riga, int colonna) =>
        riga < Righe.Count && colonna < Righe[riga].Length ? Righe[riga][colonna] : "";
}

/// <summary>
/// Legge l'elenco dei movimenti che le banche chiamano "Excel" (dal 09/11/2026). Dietro lo stesso nome ci
/// sono file diversi, e il tipo si riconosce dal contenuto, non dall'estensione:
/// - un vero .xlsx (un archivio zip): letto con DocumentFormat.OpenXml, il primo foglio;
/// - testo con le colonne separate da tabulazioni, punti e virgola o virgole, spesso con estensione .xls
///   (così fa Relax Banking), o un .csv;
/// - una pagina HTML con una tabella, salvata come .xls;
/// - il vecchio .xls binario di Excel 97-2003: non si legge, e il messaggio spiega come salvarlo in .xlsx.
/// </summary>
public static class LettoreTabelle
{
    private const int MassimoRighe = 100_000;

    /// <summary>Vero se il file sembra una tabella (xlsx, xls binario, testo separato o HTML) e non un file
    /// CBI. inizio sono i primi byte del file.</summary>
    public static bool ETabella(byte[] inizio, int letti, string estensione)
    {
        if (letti >= 4 && inizio[0] == 'P' && inizio[1] == 'K' && inizio[2] == 3 && inizio[3] == 4) return true;    // zip (xlsx)
        if (letti >= 4 && inizio[0] == 0xD0 && inizio[1] == 0xCF && inizio[2] == 0x11 && inizio[3] == 0xE0) return true;   // xls binario
        if (estensione is ".xlsx" or ".xls" or ".csv") return true;

        var testo = Encoding.Latin1.GetString(inizio, 0, letti);
        if (EHtml(testo)) return true;

        // la prima riga di un file CBI non ha separatori; quella di un elenco di colonne sì
        var prima = testo.Split('\n')[0];
        return prima.Count(c => c == '\t') >= 2 || prima.Count(c => c == ';') >= 2;
    }

    public static bool EHtml(string inizio) =>
        Regex.IsMatch(inizio, @"<\s*(html|table|!doctype\s+html)", RegexOptions.IgnoreCase);

    public static TabellaExcel Leggi(string percorso)
    {
        var inizio = new byte[1024];
        int letti;
        using (var flusso = Apri(percorso))
            letti = flusso.Read(inizio, 0, inizio.Length);

        if (letti >= 4 && inizio[0] == 'P' && inizio[1] == 'K')
            return Pulisci(LeggiXlsx(percorso), "Excel (.xlsx)");

        if (letti >= 4 && inizio[0] == 0xD0 && inizio[1] == 0xCF && inizio[2] == 0x11 && inizio[3] == 0xE0)
            throw new FileNonCbiException("Il file è nel vecchio formato di Excel 97-2003 (.xls), che il programma non sa " +
                "leggere. Aprilo con Excel (o con LibreOffice) e salvalo come \"Cartella di lavoro di Excel (.xlsx)\", " +
                "poi importa il file .xlsx.");

        var testo = LeggiTesto(percorso);
        if (EHtml(testo.Length > 4096 ? testo[..4096] : testo))
            return Pulisci(LeggiHtml(testo), "pagina HTML");

        return Pulisci(LeggiSeparato(testo), "testo a colonne");
    }

    /// <summary>Apre il file in lettura anche se è aperto in Excel: Excel lo tiene bloccato in scrittura, e
    /// File.OpenRead fallirebbe con "il file è usato da un altro processo".</summary>
    public static FileStream Apri(string percorso) =>
        new(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    // Toglie le righe vuote in fondo e le colonne vuote a destra; tiene le righe vuote in mezzo, che contano
    // per il numero della riga delle intestazioni.
    private static TabellaExcel Pulisci(List<string[]> righe, string tipo)
    {
        var pulite = righe.Select(r => r.Select(c => Spazi(c)).ToArray()).ToList();
        while (pulite.Count > 0 && pulite[^1].All(c => c == "")) pulite.RemoveAt(pulite.Count - 1);
        if (pulite.Count == 0)
            throw new FileNonCbiException("Il file non contiene una tabella di movimenti.");

        var colonne = pulite.Max(r => Array.FindLastIndex(r, c => c != "") + 1);
        var tabella = pulite.Select(r => Enumerable.Range(0, colonne).Select(i => i < r.Length ? r[i] : "").ToArray()).ToList();
        return new TabellaExcel(tabella, tipo);
    }

    private static string Spazi(string cella) =>
        Regex.Replace(cella.Replace(' ', ' '), @"\s+", " ").Trim();

    // UTF-8 se il file lo è davvero (con o senza BOM), altrimenti Latin1, la codifica dei file di Relax
    private static string LeggiTesto(string percorso)
    {
        byte[] byte_;
        using (var flusso = Apri(percorso))
        using (var memoria = new MemoryStream())
        {
            flusso.CopyTo(memoria);
            byte_ = memoria.ToArray();
        }
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(byte_).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(byte_);
        }
    }

    // ---- testo separato ----

    private static List<string[]> LeggiSeparato(string testo)
    {
        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        // il separatore è quello che compare più spesso, con lo stesso numero di volte, nelle prime righe piene
        var campione = righe.Where(r => r.Trim() != "").Take(20).ToList();
        var separatore = new[] { '\t', ';', ',', '|' }
            .OrderByDescending(s => campione.Count(r => r.Contains(s)))
            .ThenByDescending(s => campione.Sum(r => r.Count(c => c == s)))
            .First();

        var tabella = new List<string[]>();
        foreach (var riga in righe.Take(MassimoRighe))
            tabella.Add(DividiRiga(riga, separatore));
        return tabella;
    }

    // una riga di CSV, con i campi tra virgolette che possono contenere il separatore
    private static string[] DividiRiga(string riga, char separatore)
    {
        var campi = new List<string>();
        var campo = new StringBuilder();
        var traVirgolette = false;
        for (var i = 0; i < riga.Length; i++)
        {
            var c = riga[i];
            if (traVirgolette)
            {
                if (c == '"' && i + 1 < riga.Length && riga[i + 1] == '"') { campo.Append('"'); i++; }
                else if (c == '"') traVirgolette = false;
                else campo.Append(c);
            }
            else if (c == '"' && campo.ToString().Trim() == "") { campo.Clear(); traVirgolette = true; }
            else if (c == separatore) { campi.Add(campo.ToString()); campo.Clear(); }
            else campo.Append(c);
        }
        campi.Add(campo.ToString());
        return campi.ToArray();
    }

    // ---- HTML ----

    private static List<string[]> LeggiHtml(string testo)
    {
        var tabella = new List<string[]>();
        foreach (Match riga in Regex.Matches(testo, @"<tr\b[^>]*>(.*?)</tr\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var celle = new List<string>();
            foreach (Match cella in Regex.Matches(riga.Groups[1].Value, @"<t([dh])\b([^>]*)>(.*?)</t\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                var contenuto = Regex.Replace(cella.Groups[3].Value, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
                contenuto = WebUtility.HtmlDecode(Regex.Replace(contenuto, "<[^>]+>", ""));
                celle.Add(contenuto);
                var unite = Regex.Match(cella.Groups[2].Value, @"colspan\s*=\s*[""']?(\d+)", RegexOptions.IgnoreCase);
                if (unite.Success && int.TryParse(unite.Groups[1].Value, out var n))
                    for (var k = 1; k < Math.Min(n, 50); k++) celle.Add("");
            }
            tabella.Add(celle.ToArray());
            if (tabella.Count >= MassimoRighe) break;
        }
        return tabella;
    }

    // ---- xlsx ----

    private static List<string[]> LeggiXlsx(string percorso)
    {
        try
        {
            using var flusso = Apri(percorso);
            using var documento = SpreadsheetDocument.Open(flusso, false);
            var cartella = documento.WorkbookPart ?? throw new FileNonCbiException("Il file Excel non contiene fogli.");
            var primo = cartella.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
                        ?? throw new FileNonCbiException("Il file Excel non contiene fogli.");
            var foglio = (WorksheetPart)cartella.GetPartById(primo.Id!);

            var condivise = cartella.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>()
                .Select(s => s.InnerText).ToList() ?? [];
            var stiliData = StiliData(cartella);

            var righe = new List<string[]>();
            foreach (var riga in foglio.Worksheet.Descendants<Row>())
            {
                var indice = (int)(riga.RowIndex?.Value ?? (uint)(righe.Count + 1)) - 1;
                if (indice >= MassimoRighe) break;
                while (righe.Count < indice) righe.Add([]);

                var celle = new List<string>();
                foreach (var cella in riga.Elements<Cell>())
                {
                    var colonna = cella.CellReference?.Value is { } rif ? Colonna(rif) : celle.Count;
                    while (celle.Count < colonna) celle.Add("");
                    celle.Add(Valore(cella, condivise, stiliData));
                }
                righe.Add(celle.ToArray());
            }
            return righe;
        }
        catch (FileNonCbiException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OpenXmlPackageException or FormatException)
        {
            throw new FileNonCbiException($"Il file Excel non si può leggere: {ex.Message}");
        }
    }

    // "BC12" -> 54
    private static int Colonna(string riferimento)
    {
        var n = 0;
        foreach (var c in riferimento.TakeWhile(char.IsLetter))
            n = n * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        return n - 1;
    }

    private static string Valore(Cell cella, List<string> condivise, HashSet<uint> stiliData)
    {
        var testo = cella.CellValue?.Text ?? cella.InnerText;
        var tipo = cella.DataType?.Value;

        if (tipo == CellValues.SharedString)
            return int.TryParse(testo, out var i) && i >= 0 && i < condivise.Count ? condivise[i] : "";
        if (tipo == CellValues.InlineString)
            return cella.InlineString?.InnerText ?? testo;
        if (tipo == CellValues.Boolean || tipo == CellValues.String || tipo == CellValues.Error)
            return testo;
        if (tipo == CellValues.Date)
            return DateTime.TryParse(testo, CultureInfo.InvariantCulture, out var d) ? d.ToString("dd/MM/yyyy") : testo;

        if (!double.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero))
            return testo;

        // un numero con un formato di data è un giorno contato dal 30/12/1899
        if (cella.StyleIndex?.Value is { } stile && stiliData.Contains(stile) && numero is > 0 and < 2_958_466)
            return DateTime.FromOADate(numero).ToString("dd/MM/yyyy");

        // il punto come separatore dei decimali, e almeno due decimali: "1500.00" non si confonde con 1.500
        return ((decimal)numero).ToString("0.00##########", CultureInfo.InvariantCulture);
    }

    // gli stili delle celle che mostrano una data: i formati predefiniti di Excel per le date e quelli
    // personalizzati che contengono giorno, mese o anno (e non sono solo ore)
    private static HashSet<uint> StiliData(WorkbookPart cartella)
    {
        var stili = cartella.WorkbookStylesPart?.Stylesheet;
        var risultato = new HashSet<uint>();
        if (stili?.CellFormats is null) return risultato;

        var personalizzati = stili.NumberingFormats?.Elements<NumberingFormat>()
            .Where(f => f.NumberFormatId?.Value is not null)
            .ToDictionary(f => f.NumberFormatId!.Value, f => f.FormatCode?.Value ?? "") ?? [];

        uint indice = 0;
        foreach (var formato in stili.CellFormats.Elements<CellFormat>())
        {
            var id = formato.NumberFormatId?.Value ?? 0;
            var eData = id is >= 14 and <= 17 or 22 or (>= 27 and <= 36) or (>= 50 and <= 58)
                        || (personalizzati.TryGetValue(id, out var codice) && FormatoData(codice));
            if (eData) risultato.Add(indice);
            indice++;
        }
        return risultato;
    }

    private static bool FormatoData(string codice)
    {
        // senza le parti tra virgolette e tra parentesi quadre ([$-410], [Red]...)
        var pulito = Regex.Replace(codice, "\"[^\"]*\"|\\[[^\\]]*\\]", "").ToLowerInvariant();
        return pulito.Contains('d') || pulito.Contains('y');
    }
}
