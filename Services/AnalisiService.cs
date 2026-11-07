using System.Globalization;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>Calcola le analisi del cash flow. I movimenti si leggono una volta per periodo e si
/// aggregano qui in memoria: anche anni di dati sono poche migliaia di righe.
/// Regole: i movimenti di tipo Ordinario sono entrate o uscite secondo il segno; gli Investimenti
/// restano fuori da entrate e uscite e si sommano a parte; i Giroconti si ignorano.
/// I mesi si contano sulla data contabile.</summary>
public class AnalisiService
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    private readonly DatabaseService _database;
    private readonly LogService _log;
    private readonly PreferenzeService _preferenze;

    public AnalisiService(DatabaseService database, LogService log, PreferenzeService preferenze)
    {
        _database = database;
        _preferenze = preferenze;
        _log = log;
    }

    /// <summary>Il periodo proposto all'apertura della pagina, se l'utente non ne ha scelto un altro in
    /// Impostazioni > Preferenze > Generali.</summary>
    public const string PeriodoPredefinito = "Ultimi 12 mesi completi";

    // Le scelte fatte nella pagina Analisi, ricordate finché l'applicazione resta aperta: la pagina
    // viene ricreata a ogni navigazione, e tornando dal dettaglio di un indicatore deve ritrovarle.
    // Finché l'utente non le cambia valgono il periodo e il conto predefiniti (PreferenzeService).
    public string? PeriodoScelto { get; set; }

    /// <summary>Nome del periodo scelto con le date "Data contabile da" e "a" di Analisi.</summary>
    public const string NomePersonalizzato = "Personalizzato";

    /// <summary>Le date scelte a mano in Analisi (Nome = <see cref="NomePersonalizzato"/>); valgono anche
    /// nel dettaglio degli indicatori e nella simulazione. Null finché non si cambiano le date.</summary>
    public PeriodoAnalisi? PeriodoPersonalizzato { get; private set; }

    /// <summary>Il periodo per quelle date: uno dell'elenco se le date coincidono, altrimenti il periodo
    /// personalizzato, che diventa anche quello scelto.</summary>
    public PeriodoAnalisi ScegliDate(DateOnly dal, DateOnly al)
    {
        var periodo = Periodi().FirstOrDefault(p => p.Dal == dal && p.Al == al);
        if (periodo is null)
        {
            periodo = new PeriodoAnalisi(NomePersonalizzato, dal, al);
            PeriodoPersonalizzato = periodo;
        }
        PeriodoScelto = periodo.Nome;
        _log.Debug($"Periodo scelto con le date: {periodo.Nome}, dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}");
        return periodo;
    }

    /// <summary>I periodi dell'elenco più, in fondo, quello personalizzato se c'è (per la simulazione).</summary>
    public List<PeriodoAnalisi> PeriodiConPersonalizzato()
    {
        var periodi = Periodi();
        if (PeriodoPersonalizzato is { } personalizzato && periodi.Count > 0)
            periodi.Add(personalizzato);
        return periodi;
    }

    private IReadOnlyCollection<string>? _contiScelti;
    public IReadOnlyCollection<string> ContiScelti
    {
        get => _contiScelti ??= ContiPredefiniti();
        set => _contiScelti = value;
    }

    /// <summary>Il conto predefinito, se c'è ancora; altrimenti nessuno (cioè tutti).</summary>
    public IReadOnlyCollection<string> ContiPredefiniti() =>
        _preferenze.ContoPredefinito is { } conto && _database.ElencoConti().Any(c => c.Codice == conto)
            ? [conto]
            : [];
    public bool EscludiStraordinari { get; set; }

    /// <summary>Quanti movimenti del periodo sono segnati come straordinari (per il pulsante che li elenca).</summary>
    public int ContaStraordinari(PeriodoAnalisi periodo, IReadOnlyCollection<string> conti) =>
        _database.MovimentiPerAnalisi(periodo.Dal, periodo.Al, conti).Count(m => m.Straordinario);

    /// <summary>Il mese che la Home confronta con la media, dei conti indicati o di tutti: dal primo del mese
    /// fino al giorno a cui sono aggiornati <b>tutti</b> quei conti, cioè l'ultima giornata importata del conto
    /// aggiornato meno di recente (la giornata del saldo, anche senza movimenti). I conti si aggiornano con export separati: con un conto fermo al 31/10 e
    /// uno al 05/11, novembre avrebbe i movimenti di un conto solo, e il totale dei conti sarebbe uguale a
    /// quel conto (segnalato il 07/11/2026). Con un conto solo è il mese del suo ultimo movimento.
    /// Null senza movimenti.</summary>
    public PeriodoAnalisi? MeseUltimoMovimento(IReadOnlyCollection<string>? conti = null) =>
        _database.AggiornatoFinoAPerConto(conti) is { Count: > 0 } ultimi
            ? MeseFinoA(ultimi.Values.Min())
            : null;

    private static PeriodoAnalisi MeseFinoA(DateOnly ultima) =>
        new("Mese dell'ultimo movimento", new DateOnly(ultima.Year, ultima.Month, 1), ultima);

    /// <summary>Vero se i conti indicati (o tutti) sono aggiornati fino a giorni diversi: la Home lo dice,
    /// perché il mese mostrato si ferma al conto aggiornato meno di recente.</summary>
    public bool ContiAggiornatiADateDiverse(IReadOnlyCollection<string>? conti = null) =>
        _database.AggiornatoFinoAPerConto(conti).Values.Distinct().Count() > 1;

    /// <summary>L'ultimo saldo salvato di un conto fino a quella data (per la Home, un conto per riga).</summary>
    public PuntoSaldo? UltimoSaldo(string conto, DateOnly finoAl) =>
        _database.SaldiFinoAl(finoAl, [conto]) is { Count: > 0 } saldi
            ? new PuntoSaldo(saldi[^1].Data, saldi[^1].SaldoFinale)
            : null;

    /// <summary>Il periodo con quel nome, o quello predefinito se il nome non c'è (più).</summary>
    public PeriodoAnalisi? TrovaPeriodo(string? nome)
    {
        if (nome == NomePersonalizzato && PeriodoPersonalizzato is { } personalizzato)
            return personalizzato;
        var periodi = Periodi();
        var predefinito = _preferenze.PeriodoPredefinito;
        return periodi.FirstOrDefault(p => p.Nome == nome)
               ?? (predefinito == PreferenzeService.AnnoPiuRecente
                   ? periodi.FirstOrDefault(p => p.Nome.StartsWith("Anno "))
                   : periodi.FirstOrDefault(p => p.Nome == predefinito))
               ?? periodi.FirstOrDefault(p => p.Nome == PeriodoPredefinito)
               ?? periodi.FirstOrDefault();
    }

    /// <summary>I movimenti del periodo che concorrono a un indicatore e quelli che ne restano fuori,
    /// ognuno con il motivo. Fuori si elencano solo i movimenti che potrebbero sembrare "mancanti":
    /// per le entrate, i movimenti positivi di altro tipo; non tutte le uscite.</summary>
    public DettaglioIndicatore Dettaglio(Indicatore indicatore, PeriodoAnalisi periodo, IReadOnlyCollection<string> conti,
                                         bool escludiStraordinari = false)
    {
        var risultato = Calcola(periodo, conti, escludiStraordinari);
        var movimenti = _database.MovimentiPerAnalisi(periodo.Dal, periodo.Al, conti);

        static string Motivo(MovimentoAnalisi m) => m.Tipo switch
        {
            TipoGruppo.Investimento => "Investimento: conta in Investito netto, non in entrate e uscite",
            TipoGruppo.Giroconto => "Giroconto: escluso dalle analisi",
            _ => "Ordinario: conta in entrate e uscite"
        } + (m.TipoForzato ? " (tipo scelto per questo movimento)" : "");

        (Func<MovimentoAnalisi, bool> incluso, Func<MovimentoAnalisi, bool> escluso) regole = indicatore switch
        {
            Indicatore.Entrate => (m => m.Tipo == TipoGruppo.Ordinario && m.Importo > 0,
                                   m => m.Tipo != TipoGruppo.Ordinario && m.Importo > 0),
            Indicatore.Uscite => (m => m.Tipo == TipoGruppo.Ordinario && m.Importo < 0,
                                  m => m.Tipo != TipoGruppo.Ordinario && m.Importo < 0),
            Indicatore.FlussoNetto or Indicatore.TassoRisparmio =>
                (m => m.Tipo == TipoGruppo.Ordinario, m => m.Tipo != TipoGruppo.Ordinario),
            Indicatore.InvestitoNetto => (m => m.Tipo == TipoGruppo.Investimento, m => m.Tipo == TipoGruppo.Giroconto),
            _ => (m => true, m => false)    // il saldo cambia con tutti i movimenti
        };

        // Con "Escludi straordinari", un movimento straordinario che entrerebbe nell'indicatore va tra gli
        // esclusi con il suo motivo. Il saldo no: la riconciliazione usa tutti i movimenti, perché il saldo
        // della banca li comprende tutti.
        bool Fuori(MovimentoAnalisi m) => escludiStraordinari && m.Straordinario && indicatore != Indicatore.Saldo;

        var inclusi = movimenti.Where(m => regole.incluso(m) && !Fuori(m)).OrderByDescending(m => m.Data).ToList();
        var esclusi = movimenti
            .Where(m => regole.escluso(m) || (regole.incluso(m) && Fuori(m)))
            .OrderByDescending(m => m.Data)
            .Select(m => new MovimentoEscluso(m, Fuori(m) && regole.incluso(m)
                ? "Straordinario: escluso perché è attivo \"Escludi straordinari\""
                : Motivo(m)))
            .ToList();

        _log.Debug($"Dettaglio {indicatore}, {periodo.Nome}: {inclusi.Count} movimenti inclusi, {esclusi.Count} esclusi");
        return new DettaglioIndicatore(indicatore, risultato, inclusi, esclusi);
    }

    /// <summary>I periodi selezionabili: ultimo mese, ultimi 3, 6 e 12 mesi completi, un anno per ogni
    /// anno con dati, tutto. Si contano a partire dall'ultimo movimento importato, non da oggi: i dati
    /// arrivano in ritardo. Dal 04/11 "ultimi N mesi completi" finiscono con l'ultimo mese finito: con
    /// movimenti fino al 3 novembre, "Ultimo mese completo" è ottobre. Prima comprendevano il mese
    /// dell'ultimo movimento, e nei primi giorni del mese "Ultimo mese" erano pochi giorni (medie senza
    /// senso, docs/06-analisi.md). Il mese dell'ultimo movimento si vede comunque con l'anno in corso,
    /// "Tutto" o le date a mano. L'anno in corso resta da gennaio a dicembre e prende quello che c'è.
    /// Senza nessun mese finito (movimenti tutti nel mese in corso) i periodi a mesi completi mancano.</summary>
    public List<PeriodoAnalisi> Periodi()
    {
        var intervallo = _database.IntervalloMovimenti();
        if (intervallo is not { } date) return [];
        var (prima, ultima) = date;

        // l'ultimo giorno dell'ultimo mese finito: il mese dell'ultimo movimento è finito solo se il
        // movimento è dell'ultimo giorno del mese
        var meseUltimo = new DateOnly(ultima.Year, ultima.Month, 1);
        var fineCompleti = ultima == meseUltimo.AddMonths(1).AddDays(-1) ? ultima : meseUltimo.AddDays(-1);
        var meseCompleto = new DateOnly(fineCompleti.Year, fineCompleti.Month, 1);

        var periodi = new List<PeriodoAnalisi>();
        if (fineCompleti >= prima)
        {
            periodi.Add(new("Ultimo mese completo", meseCompleto, fineCompleti));
            periodi.Add(new("Ultimi 3 mesi completi", meseCompleto.AddMonths(-2), fineCompleti));
            periodi.Add(new("Ultimi 6 mesi completi", meseCompleto.AddMonths(-5), fineCompleti));
            periodi.Add(new(PeriodoPredefinito, meseCompleto.AddMonths(-11), fineCompleti));
        }
        for (var anno = ultima.Year; anno >= prima.Year; anno--)
            periodi.Add(new($"Anno {anno}", new DateOnly(anno, 1, 1), new DateOnly(anno, 12, 31)));
        periodi.Add(new("Tutto", prima, ultima));
        return periodi;
    }

    /// <summary>Con escludiStraordinari i movimenti segnati come straordinari non contano in nessun
    /// indicatore né grafico; il loro totale resta in Indicatori.StraordinariEsclusi, perché il saldo li
    /// comprende comunque.</summary>
    public RisultatoAnalisi Calcola(PeriodoAnalisi periodo, IReadOnlyCollection<string> conti, bool escludiStraordinari = false)
    {
        var tutti = _database.MovimentiPerAnalisi(periodo.Dal, periodo.Al, conti);
        var straordinariEsclusi = escludiStraordinari ? tutti.Where(m => m.Straordinario).ToList() : [];
        var movimenti = escludiStraordinari ? tutti.Where(m => !m.Straordinario).ToList() : tutti;
        var ordinari = movimenti.Where(m => m.Tipo == TipoGruppo.Ordinario).ToList();

        var entrate = ordinari.Where(m => m.Importo > 0).Sum(m => m.Importo);
        var uscite = -ordinari.Where(m => m.Importo < 0).Sum(m => m.Importo);
        var investimenti = movimenti.Where(m => m.Tipo == TipoGruppo.Investimento).ToList();
        var versato = -investimenti.Where(m => m.Importo < 0).Sum(m => m.Importo);
        var rientrato = investimenti.Where(m => m.Importo > 0).Sum(m => m.Importo);
        var giroconti = movimenti.Where(m => m.Tipo == TipoGruppo.Giroconto).Sum(m => m.Importo);

        var mesi = ValoriPerMese(periodo, ordinari);
        var durata = Durata(periodo);
        var saldo = AndamentoSaldo(periodo, conti);

        // la variazione del saldo solo se i saldi coprono l'inizio del periodo: altrimenti sarebbe la
        // variazione di un pezzo del periodo
        var inizio = saldo.Count > 1 && saldo[0].Data < periodo.Dal ? saldo[0] : null;

        var indicatori = new IndicatoriPeriodo(
            Entrate: entrate,
            Uscite: uscite,
            Netto: entrate - uscite,
            TassoRisparmio: entrate > 0 ? (entrate - uscite) / entrate : null,
            InvestitoNetto: versato - rientrato,
            VersatoInvestimenti: versato,
            RientratoInvestimenti: rientrato,
            Giroconti: giroconti,
            StraordinariEsclusi: straordinariEsclusi.Sum(m => m.Importo),
            NumeroStraordinariEsclusi: straordinariEsclusi.Count,
            SaldoFinale: saldo.Count > 0 ? saldo[^1].Saldo : null,
            DataSaldoFinale: saldo.Count > 0 ? saldo[^1].Data : null,
            SaldoIniziale: inizio?.Saldo,
            DataSaldoIniziale: inizio?.Data,
            VariazioneSaldo: inizio is not null ? saldo[^1].Saldo - inizio.Saldo : null,
            UscitaMediaMensile: durata.Mesi > 0 ? uscite / durata.Mesi : 0,
            Mesi: durata.Mesi,
            MesiIncompleti: durata.Incompleti);

        var risultato = new RisultatoAnalisi(
            periodo,
            indicatori,
            mesi,
            UscitePerGruppo: PerGruppo(ordinari.Where(m => m.Importo < 0)),
            EntratePerGruppo: PerGruppo(ordinari.Where(m => m.Importo > 0)),
            saldo,
            Cumulati(periodo.Al.Year, conti, escludiStraordinari),
            MovimentiMaggiori: ordinari.OrderByDescending(m => Math.Abs(m.Importo)).Take(10).ToList());

        // nel log niente totali: solo quanti movimenti e mesi (docs/07-sicurezza.md, sezione 8)
        _log.Debug($"Analisi {periodo.Nome}: {movimenti.Count} movimenti ({ordinari.Count} ordinari), " +
                   $"{mesi.Count} mesi, {saldo.Count} punti di saldo");
        return risultato;
    }

    // Un elemento per ogni mese del periodo, anche senza movimenti (il grafico non deve saltare mesi).
    /// <summary>
    /// Su quanti mesi si fanno le medie: la parte del periodo coperta dai movimenti (un "Anno 2026" con
    /// movimenti fino al 20/10 finisce il 20/10), con i mesi incompleti in proporzione ai giorni
    /// (20 giorni di ottobre = 20/31 = 0,65 mesi). Incompleti elenca quei mesi per l'avviso nelle pagine:
    /// la proporzione suppone i soldi distribuiti in modo uniforme nel mese, e uno stipendio che arriva
    /// nei giorni mancanti la smentisce.
    /// </summary>
    public (decimal Mesi, IReadOnlyList<string> Incompleti) Durata(PeriodoAnalisi periodo) =>
        _database.IntervalloMovimenti() is { } dati ? Durata(periodo, dati.Prima, dati.Ultima) : (0, []);

    public static (decimal Mesi, IReadOnlyList<string> Incompleti) Durata(PeriodoAnalisi periodo, DateOnly prima, DateOnly ultima)
    {
        var dal = periodo.Dal > prima ? periodo.Dal : prima;
        var al = periodo.Al < ultima ? periodo.Al : ultima;
        var incompleti = new List<string>();
        var mesi = 0m;

        for (var mese = new DateOnly(dal.Year, dal.Month, 1); mese <= al; mese = mese.AddMonths(1))
        {
            var giorniDelMese = DateTime.DaysInMonth(mese.Year, mese.Month);
            var inizio = dal > mese ? dal : mese;
            var fine = al < mese.AddDays(giorniDelMese - 1) ? al : mese.AddDays(giorniDelMese - 1);
            var giorni = fine.DayNumber - inizio.DayNumber + 1;

            mesi += (decimal)giorni / giorniDelMese;
            if (giorni < giorniDelMese)
                incompleti.Add($"{mese.ToString("MMMM yyyy", Italiano)} ({giorni} {(giorni == 1 ? "giorno" : "giorni")} su {giorniDelMese})");
        }
        return (Math.Round(mesi, 2), incompleti);
    }

    /// <summary>Grafico personalizzato: entrate e uscite per mese di un insieme di movimenti scelti a mano,
    /// dal mese del primo al mese dell'ultimo, compresi i mesi senza movimenti (a zero). Qui i movimenti
    /// contano tutti, qualunque sia il loro tipo: li ha scelti l'utente.</summary>
    public static List<ValoriMese> PerMese(IEnumerable<MovimentoCbi> movimenti)
    {
        var elenco = movimenti.ToList();
        if (elenco.Count == 0) return [];
        var perMese = elenco.ToLookup(m => new DateOnly(m.DataContabile.Year, m.DataContabile.Month, 1));
        var primo = perMese.Min(g => g.Key);
        var ultimo = perMese.Max(g => g.Key);

        var mesi = new List<ValoriMese>();
        for (var mese = primo; mese <= ultimo; mese = mese.AddMonths(1))
        {
            var delMese = perMese[mese].ToList();
            mesi.Add(new ValoriMese(mese,
                Entrate: delMese.Where(m => m.Importo > 0).Sum(m => m.Importo),
                Uscite: -delMese.Where(m => m.Importo < 0).Sum(m => m.Importo),
                UsciteRicorrenti: 0,
                UsciteVariabili: 0));
        }
        return mesi;
    }

    /// <summary>"3 mesi", "2,65 mesi", "1 mese".</summary>
    public static string TestoMesi(decimal mesi) =>
        mesi == 1 ? "1 mese" : $"{mesi.ToString("0.##", Italiano)} mesi";

    private static List<ValoriMese> ValoriPerMese(PeriodoAnalisi periodo, List<MovimentoAnalisi> ordinari)
    {
        var perMese = ordinari.ToLookup(m => new DateOnly(m.Data.Year, m.Data.Month, 1));
        var elenco = new List<ValoriMese>();

        for (var mese = new DateOnly(periodo.Dal.Year, periodo.Dal.Month, 1); mese <= periodo.Al; mese = mese.AddMonths(1))
        {
            var delMese = perMese[mese].ToList();
            var usciteMese = delMese.Where(m => m.Importo < 0).ToList();
            elenco.Add(new ValoriMese(
                mese,
                Entrate: delMese.Where(m => m.Importo > 0).Sum(m => m.Importo),
                Uscite: -usciteMese.Sum(m => m.Importo),
                UsciteRicorrenti: -usciteMese.Where(m => m.Ricorrente).Sum(m => m.Importo),
                UsciteVariabili: -usciteMese.Where(m => !m.Ricorrente).Sum(m => m.Importo)));
        }
        return elenco;
    }

    // Totali per gruppo in valore assoluto, dal più grande, al massimo 8 barre: oltre non si leggono più.
    // Se i gruppi sono di più, i primi 7 restano e gli altri confluiscono in "Altro", che ricorda quali
    // gruppi contiene (per il tooltip). Se sono 8 o meno compaiono tutti con il loro nome: raccogliere
    // in "Altro" un gruppo solo nasconderebbe il nome senza risparmiare spazio.
    private static List<TotaleGruppo> PerGruppo(IEnumerable<MovimentoAnalisi> movimenti)
    {
        const int Barre = 8;

        var gruppi = movimenti
            .GroupBy(m => m.Gruppo)
            .Select(g => new TotaleGruppo(g.Key, Math.Abs(g.Sum(m => m.Importo)), g.Count()))
            .OrderByDescending(g => g.Totale)
            .ToList();

        // le causali senza gruppo ("Altro") vanno sempre in fondo, anche se il totale è grande
        var senzaGruppo = gruppi.Where(g => g.Gruppo == "Altro").ToList();
        var principali = gruppi.Except(senzaGruppo).ToList();

        if (principali.Count + senzaGruppo.Count <= Barre)
            return [.. principali, .. senzaGruppo];

        var raccolti = principali.Skip(Barre - 1).ToList();
        var contenuto = raccolti.Select(g => g.Gruppo).ToList();
        if (senzaGruppo.Count > 0)
            contenuto.Add("causali senza gruppo");

        var altro = new TotaleGruppo("Altro",
            raccolti.Sum(g => g.Totale) + senzaGruppo.Sum(g => g.Totale),
            raccolti.Sum(g => g.Movimenti) + senzaGruppo.Sum(g => g.Movimenti),
            contenuto);
        return [.. principali.Take(Barre - 1), altro];
    }

    /// <summary>La base della simulazione: medie mensili di entrate, uscite e investito nel periodo,
    /// senza i gruppi esclusi (per esempio spese straordinarie che non si ripeteranno), più il saldo
    /// reale più recente e quello degli ultimi 12 mesi per il grafico. I mesi sono quelli del periodo,
    /// contati come nei grafici mensili (anche quelli senza movimenti).</summary>
    public BaseSimulazione BaseSimulazione(PeriodoAnalisi periodo, IReadOnlyCollection<string> conti,
                                           IReadOnlyCollection<string> gruppiEsclusi, bool escludiStraordinari)
    {
        var tutti = _database.MovimentiPerAnalisi(periodo.Dal, periodo.Al, conti)
            .Where(m => m.Tipo != TipoGruppo.Giroconto)
            .ToList();
        // gli straordinari non si ripeteranno: di solito restano fuori dalla media
        var straordinari = escludiStraordinari ? tutti.Where(m => m.Straordinario).ToList() : [];
        var movimenti = escludiStraordinari ? tutti.Where(m => !m.Straordinario).ToList() : tutti;

        var gruppi = movimenti
            .GroupBy(m => (m.Gruppo, m.Tipo))
            .Select(g => new GruppoSimulazione(g.Key.Gruppo, g.Key.Tipo, g.Sum(m => m.Importo), g.Count()))
            .OrderBy(g => g.Totale)     // le uscite più grandi per prime
            .ToList();

        var usati = movimenti.Where(m => !gruppiEsclusi.Contains(m.Gruppo)).ToList();
        var ordinari = usati.Where(m => m.Tipo == TipoGruppo.Ordinario).ToList();
        var durata = Durata(periodo);
        var mesi = durata.Mesi;

        // il saldo fino a oggi, non all'ultimo movimento: i saldi arrivano anche nei giorni senza movimenti
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        var storico = AndamentoSaldo(new PeriodoAnalisi("Ultimi 12 mesi", oggi.AddMonths(-12), oggi), conti);

        var risultato = new BaseSimulazione(
            periodo,
            mesi,
            durata.Incompleti,
            EntrateMedie: mesi > 0 ? ordinari.Where(m => m.Importo > 0).Sum(m => m.Importo) / mesi : 0,
            UsciteMedie: mesi > 0 ? -ordinari.Where(m => m.Importo < 0).Sum(m => m.Importo) / mesi : 0,
            InvestitoMedio: mesi > 0 ? -usati.Where(m => m.Tipo == TipoGruppo.Investimento).Sum(m => m.Importo) / mesi : 0,
            SaldoIniziale: storico.Count > 0 ? storico[^1] : null,
            Storico: storico,
            Gruppi: gruppi,
            StraordinariEsclusi: straordinari.Count,
            TotaleStraordinariEsclusi: straordinari.Sum(m => m.Importo));

        _log.Debug($"Base simulazione {periodo.Nome}, {gruppiEsclusi.Count} gruppi esclusi, " +
                   $"{risultato.Mesi} mesi, {straordinari.Count} straordinari esclusi");
        return risultato;
    }

    /// <summary>Simula il saldo a fine mese per i mesi successivi al saldo iniziale: ogni mese
    /// saldo = saldo del mese prima + flusso netto - investito netto. Il flusso netto è il valore unico,
    /// salvo i mesi indicati in nettoPerMese (chiave: primo giorno del mese).</summary>
    public static List<MeseSimulato> Simula(BaseSimulazione base_, int mesi, decimal nettoUnico, decimal investito,
                                            IReadOnlyDictionary<DateOnly, decimal> nettoPerMese)
    {
        var elenco = new List<MeseSimulato>();
        if (base_.SaldoIniziale is not { } inizio) return elenco;

        var saldo = inizio.Saldo;
        var primo = new DateOnly(inizio.Data.Year, inizio.Data.Month, 1).AddMonths(1);
        for (var i = 0; i < mesi; i++)
        {
            var mese = primo.AddMonths(i);
            var modificato = nettoPerMese.TryGetValue(mese, out var netto);
            if (!modificato) netto = nettoUnico;
            saldo += netto - investito;
            elenco.Add(new MeseSimulato(mese, netto, modificato, investito, saldo));
        }
        return elenco;
    }

    // Saldo di fine giornata, sommando i conti scelti. Un conto senza saldo in una data (giorno
    // festivo, o conto non presente nel file) conserva l'ultimo saldo noto. Il primo punto è il saldo
    // alla vigilia del periodo, così la variazione copre l'intero periodo.
    private List<PuntoSaldo> AndamentoSaldo(PeriodoAnalisi periodo, IReadOnlyCollection<string> conti)
    {
        var saldi = _database.SaldiFinoAl(periodo.Al, conti);
        var ultimoPerConto = new Dictionary<string, decimal>();
        var punti = new List<PuntoSaldo>();
        PuntoSaldo? vigilia = null;

        foreach (var giorno in saldi.GroupBy(s => s.Data))
        {
            foreach (var s in giorno)
                ultimoPerConto[s.Conto] = s.SaldoFinale;

            var punto = new PuntoSaldo(giorno.Key, ultimoPerConto.Values.Sum());
            if (giorno.Key < periodo.Dal)
                vigilia = punto;
            else
                punti.Add(punto);
        }

        if (vigilia is not null && punti.Count > 0)
            punti.Insert(0, vigilia with { Data = periodo.Dal.AddDays(-1) });
        return punti;
    }

    // Flusso netto cumulato mese per mese dell'anno indicato e del precedente, per il confronto.
    private List<CumulatoAnno> Cumulati(int anno, IReadOnlyCollection<string> conti, bool escludiStraordinari)
    {
        var movimenti = _database.MovimentiPerAnalisi(new DateOnly(anno - 1, 1, 1), new DateOnly(anno, 12, 31), conti)
            .Where(m => m.Tipo == TipoGruppo.Ordinario && !(escludiStraordinari && m.Straordinario))
            .ToList();
        var primaData = movimenti.Count > 0 ? movimenti.Min(m => m.Data) : DateOnly.MaxValue;
        var ultimaData = movimenti.Count > 0 ? movimenti.Max(m => m.Data) : DateOnly.MinValue;
        var primoMese = new DateOnly(primaData.Year, primaData.Month, 1);

        var elenco = new List<CumulatoAnno>();
        foreach (var a in new[] { anno - 1, anno })
        {
            var valori = new decimal?[12];
            decimal cumulato = 0;
            for (var mese = 1; mese <= 12; mese++)
            {
                // i mesi prima del primo e dopo l'ultimo movimento importato restano vuoti:
                // uno zero farebbe credere a un mese senza entrate né uscite
                if (new DateOnly(a, mese, 1) < primoMese) continue;
                if (new DateOnly(a, mese, 1) > ultimaData) break;
                cumulato += movimenti.Where(m => m.Data.Year == a && m.Data.Month == mese).Sum(m => m.Importo);
                valori[mese - 1] = cumulato;
            }
            if (valori.Any(v => v is not null))
                elenco.Add(new CumulatoAnno(a, valori));
        }
        return elenco;
    }
}
