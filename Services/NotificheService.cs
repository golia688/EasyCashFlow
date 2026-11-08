using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

public enum GravitaNotifica { Informazione, Avviso, Errore }

/// <summary>Dove porta il pulsante di una notifica (lo esegue Main.razor).</summary>
public enum AzioneNotifica { Importa, RiepilogoImportazione, GruppiCausali, Scarti, Conti, Backup }

/// <summary>Una notifica della campanella. Id è stabile per lo stesso fatto ("scarti", "backup"…), così
/// una notifica nascosta resta nascosta finché il fatto non cambia.</summary>
public record Notifica(string Id, GravitaNotifica Gravita, string Titolo, string Testo, string Pulsante, AzioneNotifica Azione);

/// <summary>
/// La sezione notifiche (campanella nella barra in alto, dal 05/11/2026): le cose da fare o da guardare,
/// raccolte in un posto solo invece che come notifiche che compaiono e spariscono.
/// La maggior parte si ricava dallo stato del database ogni volta che si aggiorna (dopo lo sblocco, a ogni
/// importazione, a ogni cambio di pagina): una notifica sparisce da sola quando il fatto è risolto, per
/// esempio quando tutte le causali hanno un gruppo. L'esito dell'ultima importazione, se ha avvisi o non è
/// riuscita, resta finché il programma è aperto e riapre il riepilogo.
/// "Nascondi" vale fino alla chiusura del programma: niente si salva.
/// </summary>
public class NotificheService
{
    // oltre questi giorni dall'ultimo movimento si propone un export nuovo (come il promemoria della Home)
    public const int GiorniSenzaImportare = 7;
    // oltre questi giorni dall'ultimo backup lo si ricorda
    public const int GiorniSenzaBackup = 30;

    private readonly DatabaseService _database;
    private readonly PreferenzeService _preferenze;
    private readonly AccessoService _accesso;
    private readonly LogService _log;

    private readonly HashSet<string> _nascoste = [];
    private List<Notifica> _elenco = [];

    public NotificheService(DatabaseService database, PreferenzeService preferenze, AccessoService accesso,
                            ImportazioneService importazione, LogService log)
    {
        _database = database;
        _preferenze = preferenze;
        _accesso = accesso;
        _log = log;
        importazione.ImportCompletato += esito =>
        {
            UltimaImportazione = esito;
            _nascoste.Remove(IdImportazione);
            Aggiorna();
        };
        _accesso.Cambiato += Aggiorna;
    }

    private const string IdImportazione = "importazione";

    /// <summary>L'esito dell'ultima importazione fatta da quando il programma è aperto.</summary>
    public EsitoImportazione? UltimaImportazione { get; private set; }

    /// <summary>Le notifiche da mostrare, le più gravi per prime.</summary>
    public IReadOnlyList<Notifica> Elenco => _elenco;

    public event Action? Cambiate;

    public void Nascondi(string id)
    {
        _nascoste.Add(id);
        Aggiorna();
    }

    /// <summary>Ricalcola le notifiche dallo stato del database (pochi conteggi, qualche millisecondo).</summary>
    public void Aggiorna()
    {
        var nuove = new List<Notifica>();
        try
        {
            if (_accesso.Aperto && _database.Pronto)
                Raccogli(nuove);
        }
        catch (Exception ex)
        {
            // le notifiche non devono mai impedire di usare il programma
            _log.Errore("Aggiornamento delle notifiche non riuscito", ex);
        }

        _elenco = nuove.Where(n => !_nascoste.Contains(n.Id))
                       .OrderByDescending(n => n.Gravita)
                       .ToList();
        Cambiate?.Invoke();
    }

    private void Raccogli(List<Notifica> elenco)
    {
        if (UltimaImportazione is { } esito)
        {
            if (esito.Errore is not null)
                elenco.Add(new(IdImportazione, GravitaNotifica.Errore, "Importazione non riuscita",
                    $"{esito.NomeFile}: non è stato caricato nessun movimento.", "Vedi il riepilogo", AzioneNotifica.RiepilogoImportazione));
            else if (esito.Avvisi.Count > 0 || esito.ContenutoDiverso > 0 || esito.RigheNonValide > 0)
                elenco.Add(new(IdImportazione, GravitaNotifica.Avviso, "Ultima importazione con avvisi",
                    $"{esito.NomeFile}: {esito.Inseriti:N0} movimenti nuovi" +
                    (esito.Avvisi.Count > 0 ? $", {esito.Avvisi.Count:N0} avvisi sul file" : "") +
                    (esito.ContenutoDiverso + esito.RigheNonValide > 0 ? $", {esito.ContenutoDiverso + esito.RigheNonValide:N0} non caricati" : "") + ".",
                    "Vedi il riepilogo", AzioneNotifica.RiepilogoImportazione));
        }

        var stat = _database.Statistiche();
        if (stat.Movimenti == 0)
        {
            elenco.Add(new("primo-file", GravitaNotifica.Informazione, "Importa il primo file",
                "Non ci sono ancora movimenti: scarica dalla banca l'export CBI, di testo o XML, e importalo.", "Importa file CBI o XML", AzioneNotifica.Importa));
            return;
        }

        if (stat.Scartati > 0)
            elenco.Add(new("scarti", GravitaNotifica.Avviso, "Movimenti scartati da controllare",
                $"{stat.Scartati:N0} movimenti hanno la stessa chiave di movimenti già salvati ma un contenuto diverso.",
                "Vedi gli scarti", AzioneNotifica.Scarti));

        if (stat.CausaliSenzaGruppo > 0)
            elenco.Add(new("causali", GravitaNotifica.Avviso, "Causali senza gruppo",
                $"{stat.CausaliSenzaGruppo:N0} causali non hanno un gruppo: nei grafici finiscono in \"Altro\".",
                "Assegna i gruppi", AzioneNotifica.GruppiCausali));

        var provvisori = _database.ElencoConti().Count(c => c.Nome == ContoCorrente.NomeProvvisorio(c.Codice));
        if (provvisori > 0)
            elenco.Add(new("conti", GravitaNotifica.Informazione, "Dai un nome ai conti",
                provvisori == 1 ? "Un conto ha ancora il nome provvisorio." : $"{provvisori} conti hanno ancora il nome provvisorio.",
                "Conti correnti", AzioneNotifica.Conti));

        var oggi = DateOnly.FromDateTime(DateTime.Today);
        if (stat.Intervallo is { } intervallo && oggi.DayNumber - intervallo.Ultima.DayNumber > GiorniSenzaImportare)
            elenco.Add(new("export", GravitaNotifica.Informazione, "Importa un export recente",
                $"L'ultimo movimento è del {intervallo.Ultima:dd/MM/yyyy}, {oggi.DayNumber - intervallo.Ultima.DayNumber} giorni fa.",
                "Importa file CBI o XML", AzioneNotifica.Importa));

        var ultimoBackup = _preferenze.UltimoBackup;
        if (ultimoBackup is null || (DateTime.Now - ultimoBackup.Value).TotalDays > GiorniSenzaBackup)
            elenco.Add(new("backup", GravitaNotifica.Informazione, "Fai un backup",
                ultimoBackup is { } quando
                    ? $"L'ultimo backup è del {quando:dd/MM/yyyy}: più di {GiorniSenzaBackup} giorni fa."
                    : "Non hai ancora fatto un backup dei tuoi dati.",
                "Backup", AzioneNotifica.Backup));
    }
}
