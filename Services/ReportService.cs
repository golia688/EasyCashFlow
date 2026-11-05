using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EasyCashFlow.Models;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace EasyCashFlow.Services;

/// <summary>Esito della generazione del report: il file scritto e quanti movimenti contiene, oppure l'errore.</summary>
public record EsitoReport(string? Percorso, int Movimenti, string? Errore);

/// <summary>
/// Analisi > Report > Genera report Excel: un file .xlsx con le analisi della pagina Analisi in quel momento
/// (periodo, conti, straordinari esclusi o no). Tre fogli:
/// - "Analisi": indicatori, tabelle per mese e per gruppo e quattro grafici nativi di Excel. Indicatori e
///   tabelle sono formule (SOMMA.PIÙ.SE) sul foglio Movimenti, così si vede come sono calcolati e cambiando
///   i dati i conti si aggiornano; il saldo, che viene dai saldi della banca e non dai movimenti, è un valore;
/// - "Movimenti": i movimenti del periodo e dei conti, come tabella Excel con i filtri. Ci sono anche
///   investimenti, giroconti e straordinari, con le colonne Tipo e "Nelle analisi" che le formule usano;
/// - "Saldo": il saldo giornaliero.
/// Il file è in chiaro: la pagina lo dice prima di salvarlo. Si scrive con Open XML SDK (licenza MIT), con le
/// stringhe in linea e le formule con il valore già calcolato, così si legge anche con programmi che non
/// ricalcolano. Le formule nel file sono in inglese (SUMIFS), come vuole il formato: Excel le mostra in
/// italiano.
/// </summary>
public class ReportService
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    // colori dei grafici: gli stessi del tema chiaro del programma (ColoreTema), senza #
    private static readonly string ColoreEntrate = ColoreTema.Entrate.Chiaro.TrimStart('#');
    private static readonly string ColoreUscite = ColoreTema.Uscite.Chiaro.TrimStart('#');
    private static readonly string ColoreSaldo = ColoreTema.Informazione.Chiaro.TrimStart('#');
    private static readonly string ColoreNetto = ColoreTema.Secondario.Chiaro.TrimStart('#');

    // indici degli stili (Stili())
    private const uint StileIntestazione = 1, StileEuro = 2, StileData = 3, StilePercentuale = 4,
                       StileTitolo = 5, StileGrassetto = 6, StileEuroGrassetto = 7, StileNota = 8, StileNumero = 9;

    private readonly AnalisiService _analisi;
    private readonly DatabaseService _database;
    private readonly LogService _log;

    public ReportService(AnalisiService analisi, DatabaseService database, LogService log)
    {
        _analisi = analisi;
        _database = database;
        _log = log;
    }

    /// <summary>Il nome proposto: EasyCashFlow-report-2026-11-04.xlsx.</summary>
    public static string NomeFile(DateTime quando) => $"EasyCashFlow-report-{quando:yyyy-MM-dd}.xlsx";

    public async Task<EsitoReport> GeneraAsync(string percorso, PeriodoAnalisi periodo, IReadOnlyCollection<string> conti,
                                               bool escludiStraordinari)
    {
        _log.Info($"Report Excel: {periodo.Nome}, {(conti.Count == 0 ? "tutti i conti" : $"{conti.Count} conti")}, " +
                  $"straordinari {(escludiStraordinari ? "esclusi" : "inclusi")} -> {Mascherato.Percorso(percorso)}");
        var parziale = percorso + ".parziale";
        try
        {
            var movimenti = _database.MovimentiPerAnalisi(periodo.Dal, periodo.Al, conti)
                                     .OrderBy(m => m.Data).ThenBy(m => m.Importo).ToList();
            var risultato = _analisi.Calcola(periodo, conti, escludiStraordinari);
            var nomiConti = _database.ElencoConti().ToDictionary(c => c.Codice, c => c.Nome);

            await Task.Run(() =>
            {
                Scrivi(parziale, periodo, conti, escludiStraordinari, movimenti, risultato, nomiConti);
                File.Move(parziale, percorso, overwrite: true);
            });
            _log.Info($"Report Excel scritto: {movimenti.Count} movimenti");
            return new EsitoReport(percorso, movimenti.Count, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OpenXmlPackageException)
        {
            _log.Errore("Report Excel non scritto", ex);
            try { File.Delete(parziale); } catch (IOException) { }
            return new EsitoReport(null, 0, ex.Message);
        }
    }

    // ---- il file ----

    private static void Scrivi(string percorso, PeriodoAnalisi periodo, IReadOnlyCollection<string> conti, bool escludi,
                               List<MovimentoAnalisi> movimenti, RisultatoAnalisi r, Dictionary<string, string> nomiConti)
    {
        using var documento = SpreadsheetDocument.Create(percorso, SpreadsheetDocumentType.Workbook);
        var libro = documento.AddWorkbookPart();
        libro.Workbook = new Workbook();
        var stili = libro.AddNewPart<WorkbookStylesPart>();
        stili.Stylesheet = Stili();

        var fogli = new Sheets();
        var analisi = libro.AddNewPart<WorksheetPart>();
        var foglioMovimenti = libro.AddNewPart<WorksheetPart>();
        var foglioSaldo = libro.AddNewPart<WorksheetPart>();
        fogli.Append(new Sheet { Name = "Analisi", SheetId = 1, Id = libro.GetIdOfPart(analisi) });
        fogli.Append(new Sheet { Name = "Movimenti", SheetId = 2, Id = libro.GetIdOfPart(foglioMovimenti) });
        fogli.Append(new Sheet { Name = "Saldo", SheetId = 3, Id = libro.GetIdOfPart(foglioSaldo) });
        libro.Workbook.Append(fogli);
        // Excel ricalcola le formule all'apertura (i valori già scritti servono a chi non ricalcola)
        libro.Workbook.Append(new CalculationProperties { CalculationId = 0, FullCalculationOnLoad = true });

        var ultimaRiga = ScriviMovimenti(foglioMovimenti, movimenti, escludi, nomiConti);
        var righeSaldo = ScriviSaldo(foglioSaldo, r.Saldo);
        ScriviAnalisi(analisi, periodo, conti, escludi, movimenti, r, nomiConti, ultimaRiga, righeSaldo);
    }

    // Foglio Movimenti: una riga per movimento, come tabella Excel. Restituisce l'ultima riga dei dati.
    private static int ScriviMovimenti(WorksheetPart parte, List<MovimentoAnalisi> movimenti, bool escludi,
                                       Dictionary<string, string> nomiConti)
    {
        string[] intestazioni = ["Data", "Mese", "Conto", "Causale", "Descrizione", "Dettaglio", "Gruppo", "Tipo",
                                 "Spesa", "Straordinario", "Nelle analisi", "Importo"];
        var dati = new SheetData();
        dati.Append(Riga(1, intestazioni.Select(t => Testo(t, StileIntestazione)).ToArray()));

        var numero = 2;
        foreach (var m in movimenti)
        {
            var nelleAnalisi = !(escludi && m.Straordinario);
            var spesa = m.Tipo == TipoGruppo.Ordinario && m.Importo < 0 ? (m.Ricorrente ? "Ricorrente" : "Variabile") : "";
            dati.Append(Riga(numero,
                Data(m.Data),
                Testo(EtichettaMese(m.Data)),
                Testo(nomiConti.GetValueOrDefault(m.Conto, m.Conto)),
                Testo(m.Causale),
                Testo(m.DescrizioneBreve),
                Testo(m.DescrizioneEstesa),
                Testo(m.Gruppo),
                Testo(m.Tipo.ToString()),
                Testo(spesa),
                Testo(m.Straordinario ? "Sì" : "No"),
                Testo(nelleAnalisi ? "Sì" : "No"),
                Numero(m.Importo, StileEuro)));
            numero++;
        }
        var ultima = Math.Max(numero - 1, 2);     // una tabella vuota ha comunque una riga di dati

        parte.Worksheet = new Worksheet(
            new SheetViews(new SheetView(
                new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen })
            { WorkbookViewId = 0 }),
            Colonne(12, 10, 22, 9, 34, 50, 24, 13, 11, 13, 13, 14),
            dati);

        var tabella = parte.AddNewPart<TableDefinitionPart>();
        var riferimento = $"A1:L{ultima}";
        tabella.Table = new Table(
            new AutoFilter { Reference = riferimento },
            new TableColumns(intestazioni.Select((t, i) => new TableColumn { Id = (uint)(i + 1), Name = t }))
            { Count = (uint)intestazioni.Length },
            new TableStyleInfo { Name = "TableStyleMedium2", ShowRowStripes = true, ShowColumnStripes = false,
                                 ShowFirstColumn = false, ShowLastColumn = false })
        { Id = 1, Name = "Movimenti", DisplayName = "Movimenti", Reference = riferimento, TotalsRowShown = false };
        parte.Worksheet.Append(new TableParts(new TablePart { Id = parte.GetIdOfPart(tabella) }) { Count = 1 });
        return ultima;
    }

    // Foglio Saldo: data e saldo a fine giornata. Restituisce il numero di righe dei dati.
    private static int ScriviSaldo(WorksheetPart parte, IReadOnlyList<PuntoSaldo> saldo)
    {
        var dati = new SheetData();
        dati.Append(Riga(1, Testo("Data", StileIntestazione), Testo("Saldo", StileIntestazione)));
        var numero = 2;
        foreach (var p in saldo)
            dati.Append(Riga(numero++, Data(p.Data), Numero(p.Saldo, StileEuro)));
        parte.Worksheet = new Worksheet(Colonne(12, 16), dati);
        return saldo.Count;
    }

    private static void ScriviAnalisi(WorksheetPart parte, PeriodoAnalisi periodo, IReadOnlyCollection<string> conti,
                                      bool escludi, List<MovimentoAnalisi> movimenti, RisultatoAnalisi r,
                                      Dictionary<string, string> nomiConti, int ultima, int righeSaldo)
    {
        var k = r.Indicatori;
        var dati = new SheetData();
        var riga = 1;

        // gli intervalli del foglio Movimenti usati dalle formule
        string Col(string lettera) => $"Movimenti!${lettera}$2:${lettera}${ultima}";
        var importo = Col("L");
        var ordinarieInAnalisi = $"{Col("H")},\"Ordinario\",{Col("K")},\"Sì\"";

        // ---- intestazione ----
        dati.Append(Riga(riga++, Testo("EasyCashFlow - Analisi", StileTitolo)));
        dati.Append(Riga(riga++, Testo("Periodo", StileGrassetto),
                         Testo($"{periodo.Nome}: dal {periodo.Dal:dd/MM/yyyy} al {periodo.Al:dd/MM/yyyy}")));
        dati.Append(Riga(riga++, Testo("Conti", StileGrassetto),
                         Testo(conti.Count == 0 ? "tutti" : string.Join(", ", conti.Select(c => nomiConti.GetValueOrDefault(c, c))))));
        dati.Append(Riga(riga++, Testo("Straordinari", StileGrassetto),
                         Testo(escludi ? "esclusi dalle analisi" : "compresi nelle analisi")));
        dati.Append(Riga(riga++, Testo("Generato il", StileGrassetto), Testo(DateTime.Now.ToString("dd/MM/yyyy 'alle' HH:mm"))));
        riga++;

        // ---- indicatori ----
        dati.Append(Riga(riga++, Testo("Indicatore", StileIntestazione), Testo("Valore", StileIntestazione),
                         Testo("Come si calcola", StileIntestazione)));
        var rEntrate = riga;
        dati.Append(Riga(riga++, Testo("Entrate"),
            Formula($"SUMIFS({importo},{ordinarieInAnalisi},{importo},\">0\")", k.Entrate, StileEuro),
            Testo("movimenti in entrata di tipo Ordinario (senza investimenti e giroconti)", StileNota)));
        var rUscite = riga;
        dati.Append(Riga(riga++, Testo("Uscite"),
            Formula($"-SUMIFS({importo},{ordinarieInAnalisi},{importo},\"<0\")", k.Uscite, StileEuro),
            Testo("movimenti in uscita di tipo Ordinario", StileNota)));
        var rNetto = riga;
        dati.Append(Riga(riga++, Testo("Flusso netto", StileGrassetto),
            Formula($"B{rEntrate}-B{rUscite}", k.Netto, StileEuroGrassetto),
            Testo("entrate meno uscite", StileNota)));
        dati.Append(Riga(riga++, Testo("Tasso di risparmio"),
            Formula($"IF(B{rEntrate}>0,B{rNetto}/B{rEntrate},\"\")", k.TassoRisparmio, StilePercentuale),
            Testo("flusso netto diviso le entrate", StileNota)));
        var rInvestito = riga;
        dati.Append(Riga(riga++, Testo("Investito netto"),
            Formula($"-SUMIFS({importo},{Col("H")},\"Investimento\",{Col("K")},\"Sì\")", k.InvestitoNetto, StileEuro),
            Testo("versato negli investimenti meno vendite e rimborsi", StileNota)));
        var rMesi = riga;
        dati.Append(Riga(riga++, Testo("Mesi della media"), Numero(k.Mesi, StileNumero),
            Testo("i mesi incompleti contano in proporzione ai giorni con movimenti", StileNota)));
        dati.Append(Riga(riga++, Testo("Entrate al mese"),
            Formula($"IF(B{rMesi}>0,B{rEntrate}/B{rMesi},0)", k.Mesi > 0 ? k.Entrate / k.Mesi : 0, StileEuro)));
        dati.Append(Riga(riga++, Testo("Uscite al mese"),
            Formula($"IF(B{rMesi}>0,B{rUscite}/B{rMesi},0)", k.UscitaMediaMensile, StileEuro)));
        dati.Append(Riga(riga++, Testo("Flusso netto al mese"),
            Formula($"IF(B{rMesi}>0,B{rNetto}/B{rMesi},0)", k.Mesi > 0 ? k.Netto / k.Mesi : 0, StileEuro)));
        dati.Append(Riga(riga++, Testo("Investito netto al mese"),
            Formula($"IF(B{rMesi}>0,B{rInvestito}/B{rMesi},0)", k.Mesi > 0 ? k.InvestitoNetto / k.Mesi : 0, StileEuro)));
        dati.Append(Riga(riga++, Testo("Saldo a fine periodo"),
            k.SaldoFinale is { } s ? Numero(s, StileEuro) : Testo("–"),
            Testo(k.DataSaldoFinale is { } d ? $"saldo della banca al {d:dd/MM/yyyy} (non si ricava dai movimenti)" : "nessun saldo salvato", StileNota)));
        riga++;

        // ---- per mese ----
        dati.Append(Riga(riga++, Testo("Entrate e uscite per mese", StileGrassetto)));
        dati.Append(Riga(riga++, Testo("Mese", StileIntestazione), Testo("Entrate", StileIntestazione),
                         Testo("Uscite", StileIntestazione), Testo("Flusso netto", StileIntestazione)));
        var primaMese = riga;
        foreach (var m in r.Mesi)
        {
            dati.Append(Riga(riga,
                Testo(EtichettaMese(m.Mese)),
                Formula($"SUMIFS({importo},{Col("B")},$A{riga},{ordinarieInAnalisi},{importo},\">0\")", m.Entrate, StileEuro),
                Formula($"-SUMIFS({importo},{Col("B")},$A{riga},{ordinarieInAnalisi},{importo},\"<0\")", m.Uscite, StileEuro),
                Formula($"B{riga}-C{riga}", m.Netto, StileEuro)));
            riga++;
        }
        var ultimaMese = Math.Max(riga - 1, primaMese);
        riga++;

        // ---- per gruppo: dai movimenti, come le formule (senza il raggruppamento in "Altro" della pagina) ----
        var inAnalisi = movimenti.Where(m => m.Tipo == TipoGruppo.Ordinario && !(escludi && m.Straordinario)).ToList();
        var uscitePerGruppo = inAnalisi.Where(m => m.Importo < 0).GroupBy(m => m.Gruppo)
            .Select(g => (Gruppo: g.Key, Totale: -g.Sum(m => m.Importo))).OrderByDescending(g => g.Totale).ToList();
        var entratePerGruppo = inAnalisi.Where(m => m.Importo > 0).GroupBy(m => m.Gruppo)
            .Select(g => (Gruppo: g.Key, Totale: g.Sum(m => m.Importo))).OrderByDescending(g => g.Totale).ToList();

        dati.Append(Riga(riga++, Testo("Uscite per gruppo", StileGrassetto)));
        dati.Append(Riga(riga++, Testo("Gruppo", StileIntestazione), Testo("Uscite", StileIntestazione)));
        var primaUscite = riga;
        foreach (var g in uscitePerGruppo)
        {
            dati.Append(Riga(riga, Testo(g.Gruppo),
                Formula($"-SUMIFS({importo},{Col("G")},$A{riga},{ordinarieInAnalisi},{importo},\"<0\")", g.Totale, StileEuro)));
            riga++;
        }
        var ultimaUscite = Math.Max(riga - 1, primaUscite);
        riga++;

        dati.Append(Riga(riga++, Testo("Entrate per gruppo", StileGrassetto)));
        dati.Append(Riga(riga++, Testo("Gruppo", StileIntestazione), Testo("Entrate", StileIntestazione)));
        var primaEntrate = riga;
        foreach (var g in entratePerGruppo)
        {
            dati.Append(Riga(riga, Testo(g.Gruppo),
                Formula($"SUMIFS({importo},{Col("G")},$A{riga},{ordinarieInAnalisi},{importo},\">0\")", g.Totale, StileEuro)));
            riga++;
        }
        var ultimaEntrate = Math.Max(riga - 1, primaEntrate);

        parte.Worksheet = new Worksheet(Colonne(26, 16, 16, 16), dati);

        // ---- grafici, a destra delle tabelle ----
        var disegni = parte.AddNewPart<DrawingsPart>();
        disegni.WorksheetDrawing = new Xdr.WorksheetDrawing();
        var colonnaGrafici = 5;     // colonna F (da 0)
        var rigaGrafico = 0;
        uint id = 1;

        if (r.Mesi.Count > 0)
        {
            AggiungiGrafico(disegni, id++, colonnaGrafici, rigaGrafico, 11, 20,
                GraficoMesi("Entrate e uscite per mese", primaMese, ultimaMese, r.Mesi));
            rigaGrafico += 21;
        }
        if (righeSaldo > 0)
        {
            AggiungiGrafico(disegni, id++, colonnaGrafici, rigaGrafico, 11, 18,
                GraficoSaldo("Andamento del saldo", righeSaldo, r.Saldo));
            rigaGrafico += 19;
        }
        if (uscitePerGruppo.Count > 0)
        {
            var altezza = Math.Max(14, uscitePerGruppo.Count + 6);
            AggiungiGrafico(disegni, id++, colonnaGrafici, rigaGrafico, 11, altezza,
                GraficoGruppi("Dove vanno i soldi (uscite per gruppo)", "Uscite", primaUscite, ultimaUscite,
                              uscitePerGruppo, ColoreUscite));
            rigaGrafico += altezza + 1;
        }
        if (entratePerGruppo.Count > 0)
        {
            var altezza = Math.Max(12, entratePerGruppo.Count + 6);
            AggiungiGrafico(disegni, id++, colonnaGrafici, rigaGrafico, 11, altezza,
                GraficoGruppi("Da dove arrivano (entrate per gruppo)", "Entrate", primaEntrate, ultimaEntrate,
                              entratePerGruppo, ColoreEntrate));
        }
        parte.Worksheet.Append(new Drawing { Id = parte.GetIdOfPart(disegni) });
    }

    // ---- grafici ----

    private static void AggiungiGrafico(DrawingsPart disegni, uint id, int colonna, int riga, int larghezza, int altezza,
                                        C.ChartSpace grafico)
    {
        var parte = disegni.AddNewPart<ChartPart>();
        parte.ChartSpace = grafico;

        disegni.WorksheetDrawing!.Append(new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(new Xdr.ColumnId(colonna.ToString()), new Xdr.ColumnOffset("0"),
                               new Xdr.RowId(riga.ToString()), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(new Xdr.ColumnId((colonna + larghezza).ToString()), new Xdr.ColumnOffset("0"),
                             new Xdr.RowId((riga + altezza).ToString()), new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = id + 1, Name = $"Grafico {id}" },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = 0, Cy = 0 }),
                new A.Graphic(new A.GraphicData(new C.ChartReference { Id = disegni.GetIdOfPart(parte) })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }))
            { Macro = "" },
            new Xdr.ClientData()));
    }

    // colonne di entrate e uscite affiancate, con la linea del flusso netto sopra
    private static C.ChartSpace GraficoMesi(string titolo, int prima, int ultima, IReadOnlyList<ValoriMese> mesi)
    {
        var categorie = $"Analisi!$A${prima}:$A${ultima}";
        var etichette = mesi.Select(m => EtichettaMese(m.Mese)).ToList();

        var colonne = new C.BarChart(
            new C.BarDirection { Val = C.BarDirectionValues.Column },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
            new C.VaryColors { Val = false },
            SerieBarre(0, "Entrate", ColoreEntrate, categorie, etichette, $"Analisi!$B${prima}:$B${ultima}", mesi.Select(m => m.Entrate)),
            SerieBarre(1, "Uscite", ColoreUscite, categorie, etichette, $"Analisi!$C${prima}:$C${ultima}", mesi.Select(m => m.Uscite)),
            new C.GapWidth { Val = 60 },
            new C.AxisId { Val = 1 }, new C.AxisId { Val = 2 });

        var linea = new C.LineChart(
            new C.Grouping { Val = C.GroupingValues.Standard },
            new C.VaryColors { Val = false },
            SerieLinea(2, "Flusso netto", ColoreNetto, categorie, etichette, $"Analisi!$D${prima}:$D${ultima}",
                       mesi.Select(m => m.Netto), conPunti: true),
            new C.ShowMarker { Val = true },
            new C.AxisId { Val = 1 }, new C.AxisId { Val = 2 });

        return Grafico(titolo, new C.PlotArea(new C.Layout(), colonne, linea, AsseCategorie(1, 2), AsseValori(2, 1)), legenda: true);
    }

    private static C.ChartSpace GraficoSaldo(string titolo, int righe, IReadOnlyList<PuntoSaldo> saldo)
    {
        var categorie = $"Saldo!$A$2:$A${righe + 1}";
        var linea = new C.LineChart(
            new C.Grouping { Val = C.GroupingValues.Standard },
            new C.VaryColors { Val = false },
            SerieLinea(0, "Saldo", ColoreSaldo, categorie, saldo.Select(p => p.Data.ToString("dd/MM/yyyy")).ToList(),
                       $"Saldo!$B$2:$B${righe + 1}", saldo.Select(p => p.Saldo), conPunti: false),
            new C.ShowMarker { Val = false },
            new C.AxisId { Val = 1 }, new C.AxisId { Val = 2 });
        return Grafico(titolo, new C.PlotArea(new C.Layout(), linea, AsseCategorie(1, 2, salto: Math.Max(1, righe / 12)), AsseValori(2, 1)),
                       legenda: false);
    }

    // barre orizzontali, dalla più grande in alto
    private static C.ChartSpace GraficoGruppi(string titolo, string serie, int prima, int ultima,
                                              List<(string Gruppo, decimal Totale)> gruppi, string colore)
    {
        var barre = new C.BarChart(
            new C.BarDirection { Val = C.BarDirectionValues.Bar },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
            new C.VaryColors { Val = false },
            SerieBarre(0, serie, colore, $"Analisi!$A${prima}:$A${ultima}", gruppi.Select(g => g.Gruppo).ToList(),
                       $"Analisi!$B${prima}:$B${ultima}", gruppi.Select(g => g.Totale)),
            new C.GapWidth { Val = 50 },
            new C.AxisId { Val = 1 }, new C.AxisId { Val = 2 });
        return Grafico(titolo, new C.PlotArea(new C.Layout(), barre,
                                              AsseCategorie(1, 2, posizione: C.AxisPositionValues.Left, invertito: true),
                                              AsseValori(2, 1, posizione: C.AxisPositionValues.Bottom)),
                       legenda: false);
    }

    private static C.ChartSpace Grafico(string titolo, C.PlotArea area, bool legenda)
    {
        var grafico = new C.Chart(
            new C.Title(
                new C.ChartText(new C.RichText(new A.BodyProperties(), new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.RunProperties { FontSize = 1200, Bold = true, Language = "it-IT" },
                                              new A.Text(titolo))))),
                new C.Overlay { Val = false }),
            new C.AutoTitleDeleted { Val = false },
            area);
        if (legenda)
            grafico.Append(new C.Legend(new C.LegendPosition { Val = C.LegendPositionValues.Bottom }, new C.Overlay { Val = false }));
        grafico.Append(new C.PlotVisibleOnly { Val = true });
        grafico.Append(new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap });

        return new C.ChartSpace(
            new C.EditingLanguage { Val = "it-IT" },
            new C.RoundedCorners { Val = false },
            grafico);
    }

    private static C.BarChartSeries SerieBarre(uint indice, string nome, string colore, string categorie,
                                               List<string> etichette, string valori, IEnumerable<decimal> numeri) =>
        new(
            new C.Index { Val = indice },
            new C.Order { Val = indice },
            new C.SeriesText(new C.NumericValue(nome)),
            Riempimento(colore),
            new C.InvertIfNegative { Val = false },
            Categorie(categorie, etichette),
            Valori(valori, numeri));

    private static C.LineChartSeries SerieLinea(uint indice, string nome, string colore, string categorie,
                                                List<string> etichette, string valori, IEnumerable<decimal> numeri, bool conPunti) =>
        new(
            new C.Index { Val = indice },
            new C.Order { Val = indice },
            new C.SeriesText(new C.NumericValue(nome)),
            new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = colore })) { Width = 22225 }),
            new C.Marker(new C.Symbol { Val = conPunti ? C.MarkerStyleValues.Circle : C.MarkerStyleValues.None },
                         new C.Size { Val = 5 }),
            Categorie(categorie, etichette),
            Valori(valori, numeri),
            new C.Smooth { Val = false });

    private static C.ChartShapeProperties Riempimento(string colore) =>
        new(new A.SolidFill(new A.RgbColorModelHex { Val = colore }));

    private static C.CategoryAxisData Categorie(string formula, List<string> etichette)
    {
        var cache = new C.StringCache(new C.PointCount { Val = (uint)etichette.Count });
        for (var i = 0; i < etichette.Count; i++)
            cache.Append(new C.StringPoint(new C.NumericValue(etichette[i])) { Index = (uint)i });
        return new C.CategoryAxisData(new C.StringReference(new C.Formula(formula), cache));
    }

    private static C.Values Valori(string formula, IEnumerable<decimal> numeri)
    {
        var elenco = numeri.ToList();
        var cache = new C.NumberingCache(new C.FormatCode("General"), new C.PointCount { Val = (uint)elenco.Count });
        for (var i = 0; i < elenco.Count; i++)
            cache.Append(new C.NumericPoint(new C.NumericValue(Numero(elenco[i]))) { Index = (uint)i });
        return new C.Values(new C.NumberReference(new C.Formula(formula), cache));
    }

    private static C.CategoryAxis AsseCategorie(uint id, uint incrocio, int salto = 1,
                                                C.AxisPositionValues? posizione = null, bool invertito = false)
    {
        var asse = new C.CategoryAxis(
            new C.AxisId { Val = id },
            new C.Scaling(new C.Orientation { Val = invertito ? C.OrientationValues.MaxMin : C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = posizione ?? C.AxisPositionValues.Bottom },
            new C.NumberingFormat { FormatCode = "General", SourceLinked = false },
            new C.MajorTickMark { Val = C.TickMarkValues.None },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.Low },
            new C.CrossingAxis { Val = incrocio },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.AutoLabeled { Val = true },
            new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
            new C.LabelOffset { Val = 100 });
        if (salto > 1)
        {
            asse.Append(new C.TickLabelSkip { Val = salto });
            asse.Append(new C.TickMarkSkip { Val = salto });
        }
        asse.Append(new C.NoMultiLevelLabels { Val = false });
        return asse;
    }

    private static C.ValueAxis AsseValori(uint id, uint incrocio, C.AxisPositionValues? posizione = null) =>
        new(
            new C.AxisId { Val = id },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = posizione ?? C.AxisPositionValues.Left },
            new C.MajorGridlines(new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "D9D9D9" })))),
            new C.NumberingFormat { FormatCode = "#,##0 \"€\"", SourceLinked = false },
            new C.MajorTickMark { Val = C.TickMarkValues.None },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = incrocio },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.CrossBetween { Val = C.CrossBetweenValues.Between });

    // ---- celle ----

    private static Row Riga(int numero, params Cell[] celle)
    {
        var riga = new Row { RowIndex = (uint)numero };
        for (var i = 0; i < celle.Length; i++)
        {
            celle[i].CellReference = $"{Lettera(i)}{numero}";
            riga.Append(celle[i]);
        }
        return riga;
    }

    private static string Lettera(int indice) => indice < 26
        ? ((char)('A' + indice)).ToString()
        : $"{(char)('A' + indice / 26 - 1)}{(char)('A' + indice % 26)}";

    private static Cell Testo(string testo, uint stile = 0) => new(new InlineString(new Text(testo) { Space = SpaceProcessingModeValues.Preserve }))
        { DataType = CellValues.InlineString, StyleIndex = stile };

    private static Cell Numero(decimal valore, uint stile) => new(new CellValue(Numero(valore))) { DataType = CellValues.Number, StyleIndex = stile };

    private static Cell Data(DateOnly data) =>
        new(new CellValue(data.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(Invariante))) { DataType = CellValues.Number, StyleIndex = StileData };

    // la formula con il valore già calcolato dal programma (null = cella vuota, come IF(...;""))
    private static Cell Formula(string formula, decimal? valore, uint stile)
    {
        var cella = new Cell(new CellFormula(formula)) { StyleIndex = stile };
        if (valore is { } v)
            cella.Append(new CellValue(Numero(v)));
        return cella;
    }

    private static string Numero(decimal valore) => valore.ToString(Invariante);

    // "ott 2026": la stessa etichetta nel foglio Movimenti e nella tabella per mese, che le confronta
    private static string EtichettaMese(DateOnly data) => data.ToString("MMM yyyy", Italiano);

    private static Columns Colonne(params double[] larghezze) =>
        new(larghezze.Select((l, i) => new Column { Min = (uint)(i + 1), Max = (uint)(i + 1), Width = l, CustomWidth = true }));

    private static Stylesheet Stili() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0.00 \"€\";[Red]-#,##0.00 \"€\"" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "dd/mm/yyyy" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "0.0%" },
            new NumberingFormat { NumberFormatId = 167, FormatCode = "0.00" })
        { Count = 4 },
        new Fonts(
            new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 16 }, new FontName { Val = "Calibri" }),
            new Font(new Italic(), new FontSize { Val = 10 }, new Color { Rgb = "FF7F7F7F" }, new FontName { Val = "Calibri" }),
            // intestazioni: il colore va scritto, altrimenti nella tabella Excel vale quello dello stile della
            // tabella (bianco, pensato per il fondo blu), che sul nostro fondo verde chiaro non si legge
            new Font(new Bold(), new FontSize { Val = 11 }, new Color { Rgb = "FF1F2A24" }, new FontName { Val = "Calibri" }))
        { Count = 5 },
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDDE8E1" }) { PatternType = PatternValues.Solid }))
        { Count = 3 },
        new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder())) { Count = 1 },
        new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
        new CellFormats(
            new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 },                                    // 0
            new CellFormat { NumberFormatId = 0, FontId = 4, FillId = 2, BorderId = 0, ApplyFont = true, ApplyFill = true }, // 1 intestazione
            new CellFormat { NumberFormatId = 164, FontId = 0, FillId = 0, BorderId = 0, ApplyNumberFormat = true },       // 2 euro
            new CellFormat { NumberFormatId = 165, FontId = 0, FillId = 0, BorderId = 0, ApplyNumberFormat = true },       // 3 data
            new CellFormat { NumberFormatId = 166, FontId = 0, FillId = 0, BorderId = 0, ApplyNumberFormat = true },       // 4 percentuale
            new CellFormat { NumberFormatId = 0, FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true },                 // 5 titolo
            new CellFormat { NumberFormatId = 0, FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true },                 // 6 grassetto
            new CellFormat { NumberFormatId = 164, FontId = 1, FillId = 0, BorderId = 0, ApplyNumberFormat = true, ApplyFont = true }, // 7 euro grassetto
            new CellFormat { NumberFormatId = 0, FontId = 3, FillId = 0, BorderId = 0, ApplyFont = true },                 // 8 nota
            new CellFormat { NumberFormatId = 167, FontId = 0, FillId = 0, BorderId = 0, ApplyNumberFormat = true })       // 9 numero
        { Count = 10 },
        new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
}
