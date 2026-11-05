using System.Globalization;
using System.IO;
using System.Text;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

public enum LivelloLog
{
    Normale = 0,   // solo record scartati ed errori
    Trace = 1,     // + flusso dell'importazione
    Debug = 2      // + tutto il resto, con stack trace
}

public class LogService
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");
    private readonly object _blocco = new();
    private LivelloLog _livello = LivelloLog.Normale;

    public string Cartella { get; }

    /// <summary>Per quanti giorni si conservano i file di log, se l'utente non ha scelto altro
    /// (Impostazioni > Preferenze > Generali).</summary>
    public const int GiorniPredefiniti = 60;

    public LogService()
    {
        Cartella = Path.Combine(AppContext.BaseDirectory, "log");
        Directory.CreateDirectory(Cartella);
    }

    public string PercorsoFileCorrente =>
        Path.Combine(Cartella, $"easycashflow-{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

    public string NomeFileCorrente => Path.GetFileName(PercorsoFileCorrente);

    public bool TraceAttivo => _livello >= LivelloLog.Trace;
    public bool DebugAttivo => _livello >= LivelloLog.Debug;

    public LivelloLog Livello
    {
        get => _livello;
        set
        {
            if (value == _livello) return;
            var precedente = _livello;
            _livello = value;
            Scrivi("LIVELLO", $"Livello di log cambiato: {precedente} -> {value}");
            if (value == LivelloLog.Debug) ScriviInformazioniAmbiente();
        }
    }

    // ---- sempre scritti, qualunque sia il livello ----

    public void Info(string messaggio) => Scrivi("INFO", messaggio);

    public void Avviso(string messaggio) => Scrivi("AVVISO", messaggio);

    /// <summary>Un record non caricato nel database: motivo, posizione nel file e dati del movimento
    /// mascherati (Mascherato); della riga del file solo la forma, senza il contenuto.</summary>
    public void Scarto(RecordScartato s)
    {
        var sb = new StringBuilder();
        sb.Append(s.Motivo).Append(" - ").Append(Mascherato.Messaggio(s.Dettaglio));

        if (s.RigaFile is int riga)
            sb.Append($" | riga file: {riga}");

        if (s.Movimento is MovimentoCbi m)
            sb.Append(" | ").Append(Mascherato.Movimento(m));

        if (s.RigaOriginale is not null)
            sb.Append($" | forma della riga ({s.RigaOriginale.TrimEnd().Length} car.): {Mascherato.Forma(s.RigaOriginale.TrimEnd())}");

        Scrivi("SCARTO", sb.ToString());
    }

    /// <summary>Errore operativo. Lo stack trace completo compare solo in modalità debug.</summary>
    public void Errore(string messaggio, Exception? ex = null)
    {
        var sb = new StringBuilder(messaggio);
        if (ex is not null)
        {
            sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(Mascherato.Messaggio(ex.Message));
            if (_livello == LivelloLog.Debug)
                sb.AppendLine().Append(Mascherato.Messaggio(ex.ToString()));      // stack trace e eccezioni interne
        }
        Scrivi("ERRORE", sb.ToString());
    }

    /// <summary>Eccezione non gestita: scrive sempre lo stack trace completo.</summary>
    public void Fatale(string messaggio, Exception ex) =>
        Scrivi("FATALE", $"{messaggio}{Environment.NewLine}{Mascherato.Messaggio(ex.ToString())}");

    // ---- scritti solo se il livello lo richiede ----

    public void Trace(string messaggio)
    {
        if (_livello >= LivelloLog.Trace) Scrivi("TRACE", messaggio);
    }

    public void Debug(string messaggio)
    {
        if (_livello >= LivelloLog.Debug) Scrivi("DEBUG", messaggio);
    }

    /// <summary>Eccezione gestita nel normale flusso: stack trace solo in modalità debug.</summary>
    public void EccezioneGestita(string contesto, Exception ex)
    {
        if (_livello >= LivelloLog.Debug)
            Scrivi("DEBUG", $"Eccezione gestita ({contesto}){Environment.NewLine}{Mascherato.Messaggio(ex.ToString())}");
    }

    /// <summary>Cancella tutti i file di log, compreso quello di oggi, e ne riapre uno nuovo. Si usa quando
    /// nasce un database nuovo: i log che ci sono riguardano dati che non esistono più, e quelli delle
    /// versioni di prova fino alla 0.9.2 contengono i movimenti in chiaro (docs/07-sicurezza.md, 8.4).</summary>
    public void EliminaTutti()
    {
        int eliminati = 0, nonEliminati = 0;
        lock (_blocco)
        {
            foreach (var percorso in Directory.EnumerateFiles(Cartella, "easycashflow-*.log"))
            {
                try
                {
                    File.Delete(percorso);
                    eliminati++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    nonEliminati++;     // per esempio aperto in un editor
                }
            }
        }

        Info($"Nuovo database: log precedenti cancellati ({eliminati} file)");
        if (nonEliminati > 0)
            Avviso($"{nonEliminati} file di log non cancellati (forse aperti in un altro programma): vanno cancellati a mano");
        ScriviInformazioniAmbiente();
    }

    // ---- apertura dei file da menu ----

    public void ApriFileCorrente()
    {
        try
        {
            if (!File.Exists(PercorsoFileCorrente))
                Info("File di log creato su richiesta dell'utente");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{PercorsoFileCorrente}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Errore("Impossibile aprire il file di log", ex);
        }
    }

    public void ApriCartella()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Cartella,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Errore("Impossibile aprire la cartella dei log", ex);
        }
    }

    // ---- interno ----

    private void ScriviInformazioniAmbiente()
    {
        Scrivi("DEBUG",
            $"Ambiente: {Environment.OSVersion} | .NET {Environment.Version} | " +
            $"processo a {(Environment.Is64BitProcess ? 64 : 32)} bit (id {Environment.ProcessId}) | " +
            $"cultura: {CultureInfo.CurrentCulture.Name} | cartella app: {Mascherato.Percorso(AppContext.BaseDirectory)}");
    }

    private void Scrivi(string tipo, string messaggio)
    {
        var riga = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} " +
                   $"[{tipo,-7}] [T{Environment.CurrentManagedThreadId}] {messaggio}";
        try
        {
            lock (_blocco)
                File.AppendAllText(PercorsoFileCorrente, riga + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // il log non deve mai far fallire l'applicazione
            System.Diagnostics.Debug.WriteLine($"Scrittura del log non riuscita: {ex.Message}");
        }
    }

    /// <summary>Elimina i file easycashflow-AAAA-MM-GG.log più vecchi di quei giorni. La chiama
    /// ImpostazioniService all'avvio e quando l'utente cambia i giorni. L'età si legge dalla data nel nome
    /// del file; gli altri file della cartella non si toccano.</summary>
    public void EliminaLogVecchi(int giorni)
    {
        try
        {
            const string prefisso = "easycashflow-";
            var limite = DateTime.Today.AddDays(-giorni);

            foreach (var percorso in Directory.EnumerateFiles(Cartella, prefisso + "*.log"))
            {
                var nome = Path.GetFileNameWithoutExtension(percorso);
                var testoData = nome.Substring(prefisso.Length);

                if (!DateTime.TryParseExact(testoData, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var data))
                    continue;   // nome non riconosciuto: il file non viene toccato

                if (data >= limite) continue;

                try
                {
                    File.Delete(percorso);
                    Scrivi("INFO", $"Log eliminato perché più vecchio di {giorni} giorni: {Path.GetFileName(percorso)}");
                }
                catch (Exception ex)
                {
                    // per esempio il file è aperto in un editor: ci si riprova al prossimo avvio
                    Scrivi("ERRORE", $"Impossibile eliminare il log {Path.GetFileName(percorso)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Scrivi("ERRORE", $"Pulizia dei log non riuscita: {ex.Message}");
        }
    }
}