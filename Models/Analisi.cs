namespace EasyCashFlow.Models;

/// <summary>Come una causale entra nelle analisi (tabella GruppiCausali).</summary>
public enum TipoGruppo
{
    Ordinario,      // entrata o uscita vera e propria, secondo il segno
    Investimento,   // soldi spostati verso o dagli investimenti: né spesa né entrata
    Giroconto       // spostamento tra conti propri: escluso dalle analisi
}

/// <summary>Un movimento letto per le analisi, con il gruppo della sua causale.</summary>
public record MovimentoAnalisi(
    string Conto,
    DateOnly Data,              // data contabile
    decimal Importo,
    string Causale,
    string DescrizioneBreve,
    string DescrizioneEstesa,
    string Riferimento,
    string Gruppo,
    TipoGruppo Tipo,            // tipo effettivo: quello scelto per il movimento, altrimenti quello del gruppo
    bool Ricorrente,
    bool Straordinario = false, // segnato dall'utente: non si ripeterà
    bool TipoForzato = false,   // il tipo è stato scelto per questo movimento, non viene dal gruppo
    TipoGruppo TipoDelGruppo = TipoGruppo.Ordinario)
{
    public ChiaveMovimento Chiave => new(Conto, Data, Riferimento, Importo);
}

/// <summary>Una causale presente nei movimenti, con il suo gruppo (null = senza gruppo, finisce in "Altro").</summary>
public record GruppoCausale(
    string Causale,
    string Descrizione,         // descrizione della banca, troncata a 34 caratteri dal formato CBI
    int Movimenti,
    decimal Totale,
    string? Gruppo,
    TipoGruppo Tipo,
    bool Ricorrente);

/// <summary>Il gruppo di una causale CBI (dal 10/11): vale per tutte le causali che cominciano con quel codice.
/// Gruppo null = nessun gruppo. Movimenti e Totale: dei movimenti con quella causale CBI.</summary>
public record RegolaCausaleCbi(string Codice, string Descrizione, string? Gruppo, TipoGruppo Tipo, bool Ricorrente,
                               int Movimenti, decimal Totale);

/// <summary>Gli indicatori della pagina Analisi, ognuno con la sua pagina di dettaglio.</summary>
public enum Indicatore
{
    Entrate,
    Uscite,
    FlussoNetto,
    TassoRisparmio,
    InvestitoNetto,
    Saldo
}

public record MovimentoEscluso(MovimentoAnalisi Movimento, string Motivo);

/// <summary>I dati della pagina di dettaglio di un indicatore: il calcolo completo del periodo,
/// i movimenti che concorrono all'indicatore e quelli del periodo che ne restano fuori.</summary>
public record DettaglioIndicatore(
    Indicatore Indicatore,
    RisultatoAnalisi Risultato,
    IReadOnlyList<MovimentoAnalisi> Inclusi,
    IReadOnlyList<MovimentoEscluso> Esclusi);

/// <summary>Un gruppo presente nel periodo di base della simulazione, che si può escludere dalla media.
/// Totale con il segno (uscite negative); Tipo per distinguere gli investimenti.</summary>
public record GruppoSimulazione(string Gruppo, TipoGruppo Tipo, decimal Totale, int Movimenti);

/// <summary>Il punto di partenza della simulazione del saldo: medie mensili del periodo scelto
/// (senza i gruppi esclusi) e saldo reale più recente.</summary>
public record BaseSimulazione(
    PeriodoAnalisi Periodo,
    decimal Mesi,                       // mesi su cui si fa la media, i mesi incompleti in proporzione ai giorni
    IReadOnlyList<string> MesiIncompleti,   // per l'avviso: "ottobre 2026 (20 giorni su 31)"
    decimal EntrateMedie,               // al mese
    decimal UsciteMedie,                // al mese, positivo
    decimal InvestitoMedio,             // al mese, positivo = verso gli investimenti
    PuntoSaldo? SaldoIniziale,          // saldo reale più recente (null se non ci sono saldi)
    IReadOnlyList<PuntoSaldo> Storico,  // saldo reale degli ultimi 12 mesi, per il grafico
    IReadOnlyList<GruppoSimulazione> Gruppi,
    int StraordinariEsclusi,            // movimenti straordinari lasciati fuori dalla media
    decimal TotaleStraordinariEsclusi)
{
    public decimal NettoMedio => EntrateMedie - UsciteMedie;

    /// <summary>Tasso di risparmio di un flusso netto mensile, con le entrate medie della base.</summary>
    public decimal? Tasso(decimal netto) => EntrateMedie > 0 ? netto / EntrateMedie : null;
}

/// <summary>Un mese futuro della simulazione.</summary>
public record MeseSimulato(
    DateOnly Mese,                      // primo giorno del mese
    decimal Netto,
    bool NettoModificato,               // diverso dal valore unico: scelto per questo mese
    decimal Investito,
    decimal Saldo)                      // a fine mese
{
    public decimal Variazione => Netto - Investito;
}

/// <summary>Un intervallo di date selezionabile nella pagina Analisi.</summary>
public record PeriodoAnalisi(string Nome, DateOnly Dal, DateOnly Al);

public record IndicatoriPeriodo(
    decimal Entrate,
    decimal Uscite,                 // positivo
    decimal Netto,                  // entrate - uscite
    decimal? TassoRisparmio,        // netto / entrate (null senza entrate)
    decimal InvestitoNetto,         // positivo = soldi spostati verso gli investimenti
    decimal VersatoInvestimenti,    // uscite verso gli investimenti (positivo)
    decimal RientratoInvestimenti,  // vendite e rimborsi rientrati sul conto
    decimal Giroconti,              // somma con segno dei giroconti: esclusi da tutto, ma cambiano il saldo
    decimal StraordinariEsclusi,    // somma con segno degli straordinari esclusi (0 se non si escludono)
    int NumeroStraordinariEsclusi,
    decimal? SaldoFinale,           // null se non ci sono saldi salvati
    DateOnly? DataSaldoFinale,
    decimal? SaldoIniziale,         // saldo alla vigilia del periodo, se i saldi la coprono
    DateOnly? DataSaldoIniziale,
    decimal? VariazioneSaldo,
    decimal UscitaMediaMensile,
    decimal Mesi,                   // mesi su cui si fa la media, i mesi incompleti in proporzione ai giorni
    IReadOnlyList<string> MesiIncompleti);

public record ValoriMese(
    DateOnly Mese,                  // primo giorno del mese
    decimal Entrate,
    decimal Uscite,                 // positivo
    decimal UsciteRicorrenti,
    decimal UsciteVariabili)
{
    public decimal Netto => Entrate - Uscite;
}

/// <summary>Il totale di un gruppo. Per la voce "Altro" che raccoglie più gruppi, Contiene li elenca.</summary>
public record TotaleGruppo(string Gruppo, decimal Totale, int Movimenti, IReadOnlyList<string>? Contiene = null);

public record PuntoSaldo(DateOnly Data, decimal Saldo);

/// <summary>Flusso netto cumulato mese per mese di un anno (12 valori; null per i mesi non ancora trascorsi).</summary>
public record CumulatoAnno(int Anno, IReadOnlyList<decimal?> Mesi);

public record RisultatoAnalisi(
    PeriodoAnalisi Periodo,
    IndicatoriPeriodo Indicatori,
    IReadOnlyList<ValoriMese> Mesi,
    IReadOnlyList<TotaleGruppo> UscitePerGruppo,
    IReadOnlyList<TotaleGruppo> EntratePerGruppo,
    IReadOnlyList<PuntoSaldo> Saldo,
    IReadOnlyList<CumulatoAnno> Cumulati,
    IReadOnlyList<MovimentoAnalisi> MovimentiMaggiori);
