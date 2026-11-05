using Microsoft.JSInterop;

namespace EasyCashFlow.Services;

/// <summary>
/// Blocco per inattività (docs/07-sicurezza.md, fase 7): se per i minuti scelti in Impostazioni > Sicurezza
/// nessuno muove il mouse, clicca, scorre o scrive nella finestra, il programma si blocca come all'avvio
/// (AccessoService.Blocca: la chiave esce dalla memoria e il database si chiude) e chiede di nuovo la
/// password. L'attività arriva dalla pagina (wwwroot/js/attivita.js), al massimo ogni 15 secondi.
/// Il tempo si misura con l'orologio di sistema, che corre anche quando il computer è sospeso: al risveglio
/// il programma si blocca subito.
/// </summary>
public class BloccoService : IDisposable
{
    private static readonly TimeSpan Controllo = TimeSpan.FromSeconds(15);

    private readonly AccessoService _accesso;
    private readonly PreferenzeService _preferenze;
    private readonly ImportazioneService _importazione;
    private readonly LogService _log;
    private readonly Timer _orologio;
    private long _ultimaAttivita = Environment.TickCount64;

    /// <summary>Scatta (fuori dal thread dell'interfaccia) quando è ora di bloccare: il blocco lo fa
    /// Main.razor nel suo thread, così non capita a metà di un'operazione dell'interfaccia.</summary>
    public event Action? BloccoRichiesto;

    public BloccoService(AccessoService accesso, PreferenzeService preferenze, ImportazioneService importazione,
                         LogService log)
    {
        _accesso = accesso;
        _preferenze = preferenze;
        _importazione = importazione;
        _log = log;
        // dopo lo sblocco il conto riparte da zero
        _accesso.Cambiato += Attivita;
        _orologio = new Timer(_ => Controlla(), null, Controllo, Controllo);
    }

    /// <summary>L'utente ha fatto qualcosa nella finestra.</summary>
    [JSInvokable]
    public void Attivita() => Interlocked.Exchange(ref _ultimaAttivita, Environment.TickCount64);

    private TimeSpan Inattivo => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _ultimaAttivita));

    private void Controlla()
    {
        try
        {
            // durante un'importazione (anche con la finestra di scelta del file aperta) non si blocca:
            // l'importazione scrive nel database
            if (!_accesso.Aperto || _importazione.InCorso) return;

            var minuti = _preferenze.MinutiBlocco;
            if (minuti == 0 || Inattivo < TimeSpan.FromMinutes(minuti)) return;

            _log.Info($"Nessuna attività da {(int)Inattivo.TotalMinutes} minuti: blocco del programma");
            Attivita();     // un solo avviso, anche se il blocco tarda
            BloccoRichiesto?.Invoke();
        }
        catch (Exception ex)
        {
            // l'orologio non deve mai far cadere il programma
            _log.Errore("Controllo del blocco per inattività non riuscito", ex);
        }
    }

    public void Dispose()
    {
        _accesso.Cambiato -= Attivita;
        _orologio.Dispose();
    }
}
