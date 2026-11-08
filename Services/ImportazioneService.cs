using System.Diagnostics;
using System.IO;
using System.Text;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

public class ImportazioneService
{
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

    /// <summary>Chiede il file e lo importa. Restituisce null se l'utente non sceglie niente (o se
    /// un'importazione è già in corso); fileScelto riceve il nome del file appena scelto, così la finestra di
    /// importazione può mostrarlo mentre lavora. L'esito va anche a chi è iscritto a ImportCompletato.</summary>
    public async Task<EsitoImportazione?> ImportaDaFileAsync(Action<string>? fileScelto = null)
    {
        if (InCorso) return null;
        InCorso = true;
        try
        {
            return await ScegliEImporta(fileScelto);
        }
        finally
        {
            InCorso = false;
        }
    }

    private async Task<EsitoImportazione?> ScegliEImporta(Action<string>? fileScelto)
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
            esito = await Task.Run(() => Importa(percorso, nomeFile));
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

    private EsitoImportazione Importa(string percorso, string nomeFile)
    {
        var cronometro = Stopwatch.StartNew();

        if (_log.TraceAttivo)
        {
            var info = new FileInfo(percorso);
            _log.Trace($"File: {Mascherato.Percorso(percorso)} ({info.Length:N0} byte, modificato il {info.LastWriteTime:dd/MM/yyyy HH:mm:ss})");
        }
        var formato = Formati.Riconosci(percorso);
        _log.Debug($"Formato del file: {formato}");

        RisultatoImportCbi letto;
        if (formato == FormatoExport.Xml)
        {
            using var flusso = File.OpenRead(percorso);
            letto = _parserXml.Leggi(flusso, _database.DescrizioniBreviDellaCausale);
        }
        else
        {
            _log.Debug("Codifica di lettura: Latin1");
            letto = _parser.Leggi(File.ReadLines(percorso, Encoding.Latin1));
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

        // la descrizione breve è un campo della banca solo nel file a 120 caratteri; nell'XML è ricavata
        var risultato = _database.InserisciMovimenti(letto.Movimenti, nomeFile, breveDellaBanca: formato == FormatoExport.Cbi);

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

        // le causali nuove senza gruppo predefinito ricevono quello della loro causale CBI
        _database.AssegnaGruppiDiRipiego();

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

        var esito = new EsitoImportazione(nomeFile, letto.Movimenti.Count, risultato.Inseriti,
            giaPresenti, scartati, avvisi, _log.NomeFileCorrente, null,
            Giornate: letto.Saldi.Count, Dal: dal, Al: al, Conti: letto.Conti.Count,
            DescrizioniAggiornate: risultato.DescrizioniAggiornate, ContenutoDiverso: divergenti,
            RigheNonValide: letto.Scartati.Count);

        _database.RegistraImportazione(esito, dal, al);

        return esito;
    }
}
