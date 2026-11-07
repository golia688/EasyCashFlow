using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// Elimina un conto con tutti i suoi dati (Impostazioni > Dati > Conti correnti, dal 07/11/2026). Nel database
/// lo fa <see cref="DatabaseService.EliminaConto"/>, in una transazione; qui si tolgono anche i riferimenti al
/// conto che resterebbero altrove e punterebbero a un conto che non c'è più:
/// - le preferenze salvate: il conto proposto nei filtri (Generali) e i conti scelti nella Home;
/// - le scelte che durano finché il programma è aperto: i conti di Analisi, i filtri di Movimenti, i filtri e
///   la selezione del grafico personalizzato.
/// Il conto si può riavere importando di nuovo i suoi file CBI, ma con il nome provvisorio e senza le scelte
/// fatte a mano sui singoli movimenti.
/// </summary>
public class EliminazioneContoService
{
    private readonly DatabaseService _database;
    private readonly PreferenzeService _preferenze;
    private readonly AnalisiService _analisi;
    private readonly StatoMovimenti _statoMovimenti;
    private readonly StatoGraficoPersonalizzato _statoGrafico;
    private readonly NotificheService _notifiche;

    public EliminazioneContoService(DatabaseService database, PreferenzeService preferenze, AnalisiService analisi,
                                    StatoMovimenti statoMovimenti, StatoGraficoPersonalizzato statoGrafico,
                                    NotificheService notifiche)
    {
        _database = database;
        _preferenze = preferenze;
        _analisi = analisi;
        _statoMovimenti = statoMovimenti;
        _statoGrafico = statoGrafico;
        _notifiche = notifiche;
    }

    /// <summary>Elimina il conto e restituisce quanti dati c'erano.</summary>
    public DatiConto Elimina(string codice)
    {
        var eliminati = _database.EliminaConto(codice);

        // preferenze salvate
        if (_preferenze.ContoPredefinito == codice)
            _preferenze.ImpostaContoPredefinito(null);
        if (_preferenze.ContiHome is { } contiHome && contiHome.Contains(codice))
            _preferenze.ImpostaContiHome(contiHome.Where(c => c != codice).ToList());   // vuoto = tutti

        // scelte del programma aperto
        if (_analisi.ContiScelti.Contains(codice))
            _analisi.ContiScelti = _analisi.ContiScelti.Where(c => c != codice).ToList();
        foreach (var filtri in new[] { _statoMovimenti.Filtri, _statoMovimenti.FiltriAttivi, _statoGrafico.Filtri, _statoGrafico.FiltriAttivi })
            if (filtri is not null && filtri.Conti.Contains(codice))
                filtri.Conti = filtri.Conti.Where(c => c != codice).ToList();
        _statoGrafico.Selezione.RemoveWhere(m => m.Conto == codice);

        _notifiche.Aggiorna();
        return eliminati;
    }
}
