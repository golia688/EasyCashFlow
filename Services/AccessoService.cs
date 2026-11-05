using Microsoft.Data.Sqlite;

namespace EasyCashFlow.Services;

/// <summary>A che punto è l'accesso ai dati (docs/07-sicurezza.md, sezione 6).</summary>
public enum FaseAccesso
{
    CreaPassword,   // primo avvio: nessun database e nessuna chiave
    Sblocca,        // c'è dati\chiave.json: serve la password (o il codice di recupero)
    Aperto          // database aperto: si vedono le pagine
}

public enum EsitoSblocco
{
    Riuscito,
    Sbagliato,              // password o codice di recupero sbagliati
    InAttesa,               // troppi errori di fila: va aspettato AttesaRimanente
    DatabaseNonApribile,    // il segreto è giusto, ma il database non si apre con quella chiave
    FileChiaveRovinato      // dati\chiave.json illeggibile
}

/// <summary>
/// Porta il programma dall'avvio all'apertura del database: decide se chiedere di creare la password o
/// di sbloccare, sblocca (in un thread a parte: Argon2 dura circa mezzo secondo) e apre il database.
/// La pagina di accesso (PaginaAccesso.razor) usa solo questo servizio.
/// </summary>
public class AccessoService
{
    // dopo questi errori di fila si aspetta prima di riprovare: non ferma chi ha copiato il file (e prova
    // fuori dal programma), ma scoraggia chi prova a mano sul computer dell'utente
    private const int ErroriSenzaAttesa = 5;

    private readonly DatabaseService _database;
    private readonly ChiaviService _chiavi;
    private readonly LogService _log;

    private int _erroriDiFila;
    private DateTime _attesaFino = DateTime.MinValue;

    public AccessoService(DatabaseService database, ChiaviService chiavi, LogService log)
    {
        _database = database;
        _chiavi = chiavi;
        _log = log;
    }

    public FaseAccesso Fase { get; private set; } = FaseAccesso.CreaPassword;

    public bool Aperto => Fase == FaseAccesso.Aperto;

    /// <summary>Vero dopo <see cref="Blocca"/>, finché non si sblocca: la pagina di accesso lo dice.</summary>
    public bool Bloccato { get; private set; }

    /// <summary>Scatta quando il database è aperto e si possono mostrare le pagine, e quando si blocca.</summary>
    public event Action? Cambiato;

    /// <summary>Quanto manca prima di poter riprovare dopo troppi errori (zero se si può subito).</summary>
    public TimeSpan AttesaRimanente =>
        _attesaFino > DateTime.Now ? _attesaFino - DateTime.Now : TimeSpan.Zero;

    /// <summary>All'avvio, prima dell'interfaccia. Un database che non si può aprire (cifrato senza
    /// chiave.json, oppure in chiaro di una versione di prova precedente) non arriva qui: lo ferma
    /// MainWindow, perché non c'è niente che l'utente possa fare dall'interfaccia.</summary>
    public void Prepara()
    {
        Fase = _chiavi.FileEsiste ? FaseAccesso.Sblocca : FaseAccesso.CreaPassword;
        _log.Info($"Accesso ai dati: {Fase}");
    }

    /// <summary>Primo avvio: crea la chiave, protetta dalla password, e il database cifrato.
    /// Restituisce il codice di recupero da mostrare; le pagine compaiono con <see cref="Completa"/>,
    /// dopo che l'utente ha confermato di averlo salvato. I log che ci sono già riguardano un database
    /// che non c'è più (e quelli delle versioni di prova sono in chiaro): si cancellano.</summary>
    public async Task<string> CreaAsync(string password)
    {
        _log.EliminaTutti();
        var codice = await Task.Run(() => _chiavi.Crea(password));
        _database.Inizializza(_chiavi.ChiaveDati);
        return codice;
    }

    public Task<EsitoSblocco> SbloccaAsync(string password) =>
        ProvaASbloccare(() => _chiavi.Sblocca(password), "password");

    /// <summary>Password dimenticata. Se riesce, il database è aperto ma le pagine no: prima l'utente
    /// sceglie una password nuova (<see cref="NuovaPasswordDopoRecuperoAsync"/>).</summary>
    public Task<EsitoSblocco> SbloccaConCodiceAsync(string codice) =>
        ProvaASbloccare(() => _chiavi.SbloccaConCodice(codice), "codice di recupero");

    /// <summary>Dopo lo sblocco con il codice di recupero: password nuova e codice nuovo (quello usato non
    /// vale più). Restituisce il codice nuovo da mostrare.</summary>
    public async Task<string> NuovaPasswordDopoRecuperoAsync(string password)
    {
        await Task.Run(() => _chiavi.CambiaPassword(password));
        return await Task.Run(_chiavi.NuovoCodiceRecupero);
    }

    /// <summary>Mostra le pagine (il database è già aperto).</summary>
    public void Completa()
    {
        Fase = FaseAccesso.Aperto;
        Bloccato = false;
        Cambiato?.Invoke();
    }

    /// <summary>Blocco per inattività o "Blocca adesso": come all'avvio, la chiave esce dalla memoria e il
    /// database si chiude; per continuare serve di nuovo la password (o il codice di recupero).</summary>
    public void Blocca(string motivo)
    {
        if (!Aperto) return;
        _chiavi.Blocca();
        _database.Chiudi();
        Fase = FaseAccesso.Sblocca;
        Bloccato = true;
        _log.Info($"Programma bloccato ({motivo})");
        Cambiato?.Invoke();
    }

    private async Task<EsitoSblocco> ProvaASbloccare(Func<bool> sblocca, string con)
    {
        if (AttesaRimanente > TimeSpan.Zero) return EsitoSblocco.InAttesa;

        bool riuscito;
        try
        {
            riuscito = await Task.Run(sblocca);
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException
                                       or System.Text.Json.JsonException or FormatException)
        {
            _log.Errore("File della chiave del database non leggibile", ex);
            return EsitoSblocco.FileChiaveRovinato;
        }

        if (!riuscito)
        {
            _erroriDiFila++;
            if (_erroriDiFila >= ErroriSenzaAttesa)
            {
                var secondi = (_erroriDiFila - ErroriSenzaAttesa) switch { 0 => 5, 1 => 10, _ => 30 };
                _attesaFino = DateTime.Now.AddSeconds(secondi);
                _log.Avviso($"{_erroriDiFila} tentativi sbagliati di fila: attesa di {secondi} secondi");
            }
            return EsitoSblocco.Sbagliato;
        }

        _erroriDiFila = 0;
        try
        {
            _database.Inizializza(_chiavi.ChiaveDati);
        }
        catch (SqliteException ex)
        {
            // per esempio un chiave.json copiato da un'altra cartella dati
            _log.Errore($"Il {con} è giusto, ma il database non si apre con la sua chiave", ex);
            _chiavi.Blocca();
            return EsitoSblocco.DatabaseNonApribile;
        }
        return EsitoSblocco.Riuscito;
    }
}
