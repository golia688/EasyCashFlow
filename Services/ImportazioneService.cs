using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

public class ImportazioneService
{
    /// <summary>Il nome del pulsante e della voce di menu che importano un file (dal 09/11 "Importa
    /// movimenti", prima "Importa file CBI o XML"), e il tooltip con i formati che accetta.</summary>
    public const string NomePulsante = "Importa movimenti";
    public const string FormatiAccettati = "Formato CBI (testo o XML) o formato Excel";

    private readonly CbiParser _parser;
    private readonly CbiXmlParser _parserXml;
    private readonly DatabaseService _database;
    private readonly SelettoreFile _selettore;
    private readonly LogService _log;

    /// <summary>Scatta al termine di ogni importazione (riuscita o no).</summary>
    public event Action<EsitoImportazione>? ImportCompletato;

    /// <summary>Vero dalla scelta del file alla fine dell'importazione: intanto il programma non si
    /// blocca per inattività (BloccoService).</summary>
    public bool InCorso { get; private set; }

    public ImportazioneService(CbiParser parser, CbiXmlParser parserXml, DatabaseService database, SelettoreFile selettore, LogService log)
    {
        _parser = parser;
        _parserXml = parserXml;
        _database = database;
        _selettore = selettore;
        _log = log;
    }

    /// <summary>Chiede il file e lo importa. Restituisce null se l'utente non sceglie niente, se annulla
    /// l'abbinamento delle colonne di un file Excel o se un'importazione è già in corso; fileScelto riceve il
    /// nome del file appena scelto, così la finestra di importazione può mostrarlo mentre lavora. Per un file
    /// Excel chiediAbbinamento apre la finestra in cui l'utente abbina le colonne e sceglie il conto (null se
    /// annulla). L'esito va anche a chi è iscritto a ImportCompletato.</summary>
    public async Task<EsitoImportazione?> ImportaDaFileAsync(Action<string>? fileScelto = null,
        Func<string, TabellaExcel, Task<RichiestaExcel?>>? chiediAbbinamento = null)
    {
        if (InCorso) return null;
        InCorso = true;
        try
        {
            return await ScegliEImporta(fileScelto, chiediAbbinamento);
        }
        finally
        {
            InCorso = false;
        }
    }

    private async Task<EsitoImportazione?> ScegliEImporta(Action<string>? fileScelto,
        Func<string, TabellaExcel, Task<RichiestaExcel?>>? chiediAbbinamento)
    {
        _log.Debug("Importazione: apertura della finestra di selezione del file");

        var percorso = await _selettore.ScegliFileCbiAsync();
        if (percorso is null)
        {
            _log.Trace("Importazione annullata dall'utente (nessun file scelto)");
            return null;
        }

        var nomeFile = Path.GetFileName(percorso);
        _log.Info($"Importazione avviata: {nomeFile}");
        fileScelto?.Invoke(nomeFile);

        EsitoImportazione esito;
        try
        {
            // un file Excel si legge prima, per mostrare le sue colonne nella finestra di abbinamento
            RichiestaExcel? richiesta = null;
            var formato = await Task.Run(() => Formati.Riconosci(percorso));
            _log.Debug($"Formato del file: {formato}");
            if (formato == FormatoExport.Excel)
            {
                var tabella = await Task.Run(() => LettoreTabelle.Leggi(percorso));
                _log.Debug($"File Excel letto come {tabella.Tipo}: {tabella.Righe.Count} righe, {tabella.Colonne} colonne");
                richiesta = chiediAbbinamento is null ? null : await chiediAbbinamento(nomeFile, tabella);
                if (richiesta is null)
                {
                    _log.Info($"Importazione di {nomeFile} annullata dall'utente nell'abbinamento delle colonne");
                    return null;
                }
            }
            esito = await Task.Run(() => Importa(percorso, nomeFile, formato, richiesta));
        }
        catch (FileNonCbiException ex)
        {
            // il controllo formale del lettore ha fermato tutto prima del database: il messaggio è già per l'utente
            _log.Errore($"Importazione di {nomeFile} rifiutata, nessun movimento caricato: {ex.Message}");
            esito = new EsitoImportazione(nomeFile, 0, 0, 0, 0, Array.Empty<string>(), _log.NomeFileCorrente, ex.Message);
        }
        catch (Exception ex)
        {
            _log.Errore($"Importazione di {nomeFile} non riuscita: nessun movimento è stato caricato", ex);
            esito = new EsitoImportazione(nomeFile, 0, 0, 0, 0, Array.Empty<string>(),
                _log.NomeFileCorrente, $"Importazione non riuscita: {ex.Message}");     // per l'utente: completo
        }

        ImportCompletato?.Invoke(esito);
        return esito;
    }

    private EsitoImportazione Importa(string percorso, string nomeFile, FormatoExport formato, RichiestaExcel? richiesta)
    {
        var cronometro = Stopwatch.StartNew();

        if (_log.TraceAttivo)
        {
            var info = new FileInfo(percorso);
            _log.Trace($"File: {Mascherato.Percorso(percorso)} ({info.Length:N0} byte, modificato il {info.LastWriteTime:dd/MM/yyyy HH:mm:ss})");
        }
        RisultatoImportCbi letto;
        if (richiesta is not null)
        {
            letto = ExcelParser.Leggi(richiesta);
            _log.Debug($"Excel: {letto.Movimenti.Count} movimenti, {letto.Scartati.Count} righe non valide, {letto.Saldi.Count} saldi giornalieri");
        }
        else if (formato == FormatoExport.Xml)
        {
            using var flusso = File.OpenRead(percorso);
            letto = _parserXml.Leggi(flusso, _database.DescrizioniBreviDellaCausale);
        }
        else
        {
            _log.Debug("Codifica di lettura: Latin1");
            letto = _parser.Leggi(File.ReadLines(percorso, Encoding.Latin1));
        }

        if (letto.Saldi.Count == 0 && letto.Movimenti.Count == 0 && richiesta is not null)
        {
            _log.Errore($"Importazione di {nomeFile}: nessun movimento riconosciuto con le colonne scelte");
            return new EsitoImportazione(nomeFile, 0, 0, 0, letto.Scartati.Count, letto.Avvisi, _log.NomeFileCorrente,
                "Con le colonne scelte non si è riconosciuto nessun movimento: controlla la riga delle intestazioni e le colonne di data e importo.",
                RigheNonValide: letto.Scartati.Count);
        }

        if (letto.Saldi.Count == 0 && letto.Movimenti.Count == 0)
        {
            _log.Errore($"Importazione di {nomeFile}: nessuna giornata riconosciuta, il file non sembra un export CBI valido");
            return new EsitoImportazione(nomeFile, 0, 0, 0, letto.Scartati.Count, letto.Avvisi,
                _log.NomeFileCorrente, "Nessuna giornata riconosciuta: il file non sembra un export CBI valido, di testo o XML.");
        }

        var avvisi = letto.Avvisi.ToList();

        // prima dei movimenti, così ogni movimento salvato ha il suo conto registrato
        _database.RegistraConti(letto.Conti);
        if (richiesta is { ContoNuovo: true })
            _database.RinominaConto(richiesta.Conto.Codice, richiesta.Conto.Nome);

        // la descrizione breve è un campo della banca solo nel file a 120 caratteri; nell'XML è ricavata
        var risultato = _database.InserisciMovimenti(letto.Movimenti, nomeFile, breveDellaBanca: formato == FormatoExport.Cbi);
        if (richiesta is not null)
            SalvaAbbinamento(richiesta);

        // i saldi servono all'andamento del saldo nelle analisi; rimportare un file vecchio li aggiunge
        var saldiSalvati = _database.SalvaSaldi(letto.Saldi);
        _log.Trace($"Saldi giornalieri salvati: {saldiSalvati}");

        var divergenti = risultato.Scartati.Count(s => s.Motivo == MotivoScarto.ContenutoDiverso);
        if (divergenti > 0)
        {
            // per l'utente è una riga del riepilogo (finestra di importazione), con la spiegazione
            _log.Avviso($"{divergenti} movimenti hanno la stessa chiave di movimenti già salvati ma un contenuto diverso (data valuta, causale o descrizione): registrati nella tabella MovimentiScartati con il nome del file ({nomeFile})");
        }

        if (risultato.DescrizioniAggiornate > 0)
        {
            _log.Info($"{risultato.DescrizioniAggiornate} movimenti erano già presenti: aggiornata la loro descrizione, ora più completa");
        }

        // il gruppo delle causali nuove viene dalla regola della loro causale CBI (GruppiEffettivi, dal 10/11)

        var giaPresenti = risultato.Scartati.Count(s => s.Motivo == MotivoScarto.GiaPresente);
        var scartati = letto.Scartati.Count + risultato.Scartati.Count;

        cronometro.Stop();
        _log.Info($"Importazione terminata: {nomeFile} - {letto.Movimenti.Count} movimenti letti, " +
                  $"{risultato.Inseriti} inseriti, {scartati} non caricati ({giaPresenti} già presenti) " +
                  $"in {cronometro.ElapsedMilliseconds} ms");

        // il periodo coperto dal file sono le sue giornate (anche quelle senza movimenti)
        var giornate = letto.Saldi.Select(s => s.Data).Concat(letto.Movimenti.Select(m => m.DataContabile)).ToList();
        DateOnly? dal = giornate.Count > 0 ? giornate.Min() : null;
        DateOnly? al = giornate.Count > 0 ? giornate.Max() : null;

        // un file Excel senza saldi ha comunque i suoi giorni di movimenti
        var numeroGiornate = richiesta is not null && letto.Saldi.Count == 0
            ? letto.Movimenti.Select(m => m.DataContabile).Distinct().Count()
            : letto.Saldi.Count;

        var esito = new EsitoImportazione(nomeFile, letto.Movimenti.Count, risultato.Inseriti,
            giaPresenti, scartati, avvisi, _log.NomeFileCorrente, null,
            Giornate: numeroGiornate, Dal: dal, Al: al, Conti: letto.Conti.Count,
            DescrizioniAggiornate: risultato.DescrizioniAggiornate, ContenutoDiverso: divergenti,
            RigheNonValide: letto.Scartati.Count);

        _database.RegistraImportazione(esito, dal, al);

        return esito;
    }

    // ---- abbinamenti delle colonne dei file Excel ----

    // Un abbinamento salvato: le colonne di ogni casella, la riga delle intestazioni e il conto. Si ritrova
    // dalle intestazioni del file, così la volta dopo un file della stessa banca si importa senza rifarlo.
    private record AbbinamentoSalvatoJson(int Riga, Dictionary<string, int[]> Colonne, string Conto);

    private const string PrefissoAbbinamento = "ExcelAbbinamento:";

    private static string ChiaveAbbinamento(string firma) =>
        PrefissoAbbinamento + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firma)))[..16];

    /// <summary>L'abbinamento usato l'ultima volta per un file con le stesse intestazioni, con il codice del
    /// conto; null se non c'è. Le intestazioni si cercano alla riga salvata.</summary>
    public (AbbinamentoExcel Abbinamento, string Conto)? AbbinamentoSalvato(TabellaExcel tabella)
    {
        var impostazioni = _database.LeggiImpostazioni();
        foreach (var (chiave, valore) in impostazioni.Where(i => i.Key.StartsWith(PrefissoAbbinamento)))
        {
            try
            {
                var salvato = JsonSerializer.Deserialize<AbbinamentoSalvatoJson>(valore);
                if (salvato is null || salvato.Riga >= tabella.Righe.Count) continue;
                if (ChiaveAbbinamento(ExcelParser.Firma(tabella, salvato.Riga)) != chiave) continue;

                var colonne = salvato.Colonne
                    .Where(c => Enum.TryParse<CampoExcel>(c.Key, out _))
                    .ToDictionary(c => Enum.Parse<CampoExcel>(c.Key), c => (IReadOnlyList<int>)c.Value.Where(i => i < tabella.Colonne).ToList());
                return (new AbbinamentoExcel(salvato.Riga, colonne), salvato.Conto);
            }
            catch (JsonException ex)
            {
                _log.Avviso($"Abbinamento Excel salvato non leggibile ({chiave}): {ex.Message}");
            }
        }
        return null;
    }

    private void SalvaAbbinamento(RichiestaExcel richiesta)
    {
        var a = richiesta.Abbinamento;
        var json = JsonSerializer.Serialize(new AbbinamentoSalvatoJson(a.RigaIntestazioni,
            a.Colonne.Where(c => c.Value.Count > 0).ToDictionary(c => c.Key.ToString(), c => c.Value.ToArray()),
            richiesta.Conto.Codice));
        _database.SalvaImpostazione(ChiaveAbbinamento(ExcelParser.Firma(richiesta.Tabella, a.RigaIntestazioni)), json);
        _log.Debug("Abbinamento delle colonne salvato per i prossimi file con le stesse intestazioni");
    }
}
