namespace EasyCashFlow.Services;

/// <summary>La pagina che si vede dopo aver inserito la password.</summary>
public enum PaginaIniziale
{
    Home,
    Movimenti,
    Analisi
}

/// <summary>
/// Preferenze generali (Impostazioni > Preferenze > Generali), minuti del blocco per inattività
/// (Impostazioni > Preferenze > Sicurezza) e periodi della Home (Dashboard > Scegli periodo). Stanno nella tabella Impostazioni del database cifrato, non in
/// preferenze.json come quelle d'aspetto: servono solo dopo la password, il conto predefinito è un dato
/// personale e il blocco non deve potersi spegnere modificando un file in chiaro. Si leggono la prima
/// volta che servono, a database aperto.
/// </summary>
public class PreferenzeService
{
    private const string ChiavePagina = "Generali.PaginaIniziale";
    private const string ChiaveConto = "Generali.ContoPredefinito";
    private const string ChiavePeriodo = "Generali.PeriodoPredefinito";
    private const string ChiaveBlocco = "Sicurezza.MinutiBlocco";
    private const string ChiaveGiorniSaldo = "Home.GiorniSaldo";
    private const string ChiaveMediaHome = "Home.MediaConfronto";
    private const string ChiaveContiHome = "Home.Conti";
    private const string ChiaveCartellaBackup = "Backup.Cartella";
    private const string ChiaveUltimoBackup = "Backup.Ultimo";
    private const string ChiaveImportiNascosti = "Privacy.ImportiNascosti";

    /// <summary>Quanti giorni mostra il grafico "Il conto oggi" della Home: 30, 90, 6 mesi (180), 12 mesi (365).</summary>
    public static readonly IReadOnlyList<int> GiorniSaldoPossibili = [30, 90, 180, 365];

    /// <summary>Con la media di quale periodo la Home confronta il mese dell'ultimo movimento.</summary>
    public static readonly IReadOnlyList<string> MedieHomePossibili =
        ["Ultimi 3 mesi completi", "Ultimi 6 mesi completi", AnalisiService.PeriodoPredefinito];

    /// <summary>Periodo predefinito che segue l'anno dell'ultimo movimento ("Anno 2026", poi "Anno 2027"…).</summary>
    public const string AnnoPiuRecente = "Anno più recente";

    /// <summary>I periodi che si possono scegliere come predefiniti: gli stessi della pagina Analisi, con
    /// l'anno più recente al posto dei singoli anni.</summary>
    public static readonly IReadOnlyList<string> PeriodiPossibili =
        ["Ultimo mese completo", "Ultimi 3 mesi completi", "Ultimi 6 mesi completi", AnalisiService.PeriodoPredefinito,
         AnnoPiuRecente, "Tutto"];

    // i nomi di prima del 04/11, quando i periodi comprendevano il mese in corso: un valore salvato allora
    // vale come il periodo a mesi completi corrispondente
    private static readonly Dictionary<string, string> NomiVecchi = new()
    {
        ["Ultimo mese"] = "Ultimo mese completo",
        ["Ultimi 3 mesi"] = "Ultimi 3 mesi completi",
        ["Ultimi 6 mesi"] = "Ultimi 6 mesi completi",
        ["Ultimi 12 mesi"] = AnalisiService.PeriodoPredefinito
    };

    /// <summary>Dopo quanti minuti senza attività il programma si blocca; 0 = mai.</summary>
    public static readonly IReadOnlyList<int> MinutiPossibili = [0, 5, 10, 15, 30, 60];

    public const int MinutiBloccoPredefiniti = 15;

    private readonly DatabaseService _database;
    private readonly LogService _log;
    private readonly object _blocco = new();
    private bool _caricate;

    private PaginaIniziale _pagina = PaginaIniziale.Home;
    private string? _conto;
    private string _periodo = AnalisiService.PeriodoPredefinito;
    private int _minutiBlocco = MinutiBloccoPredefiniti;
    private int _giorniSaldo = 90;
    private string _mediaHome = AnalisiService.PeriodoPredefinito;
    private IReadOnlyList<string>? _contiHome;
    private string? _cartellaBackup;
    private DateTime? _ultimoBackup;
    private bool _importiNascosti;

    /// <summary>Scatta quando si nascondono o si mostrano gli importi (l'occhio accanto al "?").</summary>
    public event Action? ImportiNascostiCambiati;

    public PreferenzeService(DatabaseService database, LogService log)
    {
        _database = database;
        _log = log;
    }

    public PaginaIniziale PaginaIniziale { get { Carica(); return _pagina; } }

    /// <summary>Codice del conto proposto nei filtri di Movimenti e Analisi; null = tutti i conti.</summary>
    public string? ContoPredefinito { get { Carica(); return _conto; } }

    /// <summary>Uno dei <see cref="PeriodiPossibili"/>: il periodo proposto in Analisi e nella simulazione.</summary>
    public string PeriodoPredefinito { get { Carica(); return _periodo; } }

    /// <summary>Uno dei <see cref="MinutiPossibili"/>; 0 = il programma non si blocca da solo.</summary>
    public int MinutiBlocco { get { Carica(); return _minutiBlocco; } }

    /// <summary>Uno dei <see cref="GiorniSaldoPossibili"/>.</summary>
    public int GiorniSaldoHome { get { Carica(); return _giorniSaldo; } }

    /// <summary>Uno dei <see cref="MedieHomePossibili"/>.</summary>
    public string MediaHome { get { Carica(); return _mediaHome; } }

    /// <summary>I conti scelti nella Home (codici); vuoto = tutti; null = mai scelti (la Home propone il
    /// conto predefinito). Può contenere conti che non ci sono più: li scarta la Home.</summary>
    public IReadOnlyList<string>? ContiHome { get { Carica(); return _contiHome; } }

    /// <summary>La cartella dell'ultimo backup, proposta per il successivo.</summary>
    public string? CartellaBackup { get { Carica(); return _cartellaBackup; } }

    /// <summary>Quando è stato fatto l'ultimo backup riuscito; null se mai.</summary>
    public DateTime? UltimoBackup { get { Carica(); return _ultimoBackup; } }

    /// <summary>Importi coperti da •••• in tutte le pagine, per chi si avvicina allo schermo. Si ricorda
    /// anche alla prossima apertura.</summary>
    public bool ImportiNascosti { get { Carica(); return _importiNascosti; } }

    // un valore non riconosciuto (database di una versione futura) lascia il predefinito
    private void Carica()
    {
        lock (_blocco)
        {
            if (_caricate) return;
            var salvate = _database.LeggiImpostazioni();

            if (salvate.TryGetValue(ChiavePagina, out var pagina) && Enum.TryParse<PaginaIniziale>(pagina, out var p)
                && Enum.IsDefined(p))
                _pagina = p;
            _conto = salvate.GetValueOrDefault(ChiaveConto) is { Length: > 0 } conto ? conto : null;
            if (salvate.TryGetValue(ChiavePeriodo, out var periodo))
            {
                periodo = NomiVecchi.GetValueOrDefault(periodo, periodo);
                if (PeriodiPossibili.Contains(periodo))
                    _periodo = periodo;
            }
            if (salvate.TryGetValue(ChiaveBlocco, out var minuti) && int.TryParse(minuti, out var m)
                && MinutiPossibili.Contains(m))
                _minutiBlocco = m;
            if (salvate.TryGetValue(ChiaveGiorniSaldo, out var giorni) && int.TryParse(giorni, out var g)
                && GiorniSaldoPossibili.Contains(g))
                _giorniSaldo = g;
            if (salvate.TryGetValue(ChiaveMediaHome, out var media) && MedieHomePossibili.Contains(media))
                _mediaHome = media;
            _cartellaBackup = salvate.GetValueOrDefault(ChiaveCartellaBackup);
            _importiNascosti = salvate.GetValueOrDefault(ChiaveImportiNascosti) == "1";
            if (salvate.TryGetValue(ChiaveUltimoBackup, out var ultimo)
                && DateTime.TryParse(ultimo, System.Globalization.CultureInfo.InvariantCulture,
                                     System.Globalization.DateTimeStyles.RoundtripKind, out var quando))
                _ultimoBackup = quando;
            if (salvate.TryGetValue(ChiaveContiHome, out var contiHome))
                _contiHome = contiHome.Split('|', StringSplitOptions.RemoveEmptyEntries);

            _caricate = true;
            _log.Debug($"Preferenze generali caricate: pagina iniziale {_pagina}, conto predefinito " +
                       $"{(_conto is null ? "tutti" : Mascherato.Conto(_conto))}, periodo {_periodo}, blocco dopo {_minutiBlocco} minuti, " +
                       $"Home: saldo di {_giorniSaldo} giorni, media {_mediaHome}");
        }
    }

    public void ImpostaPaginaIniziale(PaginaIniziale pagina)
    {
        if (pagina == PaginaIniziale) return;
        _pagina = pagina;
        _database.SalvaImpostazione(ChiavePagina, pagina.ToString());
        _log.Info($"Pagina iniziale cambiata: {pagina}");
    }

    public void ImpostaContoPredefinito(string? conto)
    {
        if (conto == ContoPredefinito) return;
        _conto = conto;
        _database.SalvaImpostazione(ChiaveConto, conto ?? "");
        _log.Info($"Conto predefinito cambiato: {(conto is null ? "tutti" : Mascherato.Conto(conto))}");
    }

    public void ImpostaPeriodoPredefinito(string periodo)
    {
        if (periodo == PeriodoPredefinito || !PeriodiPossibili.Contains(periodo)) return;
        _periodo = periodo;
        _database.SalvaImpostazione(ChiavePeriodo, periodo);
        _log.Info($"Periodo predefinito cambiato: {periodo}");
    }

    public void ImpostaMinutiBlocco(int minuti)
    {
        if (minuti == MinutiBlocco || !MinutiPossibili.Contains(minuti)) return;
        _minutiBlocco = minuti;
        _database.SalvaImpostazione(ChiaveBlocco, minuti.ToString());
        _log.Info(minuti == 0 ? "Blocco per inattività spento" : $"Blocco per inattività: dopo {minuti} minuti");
    }

    public void ImpostaGiorniSaldoHome(int giorni)
    {
        if (giorni == GiorniSaldoHome || !GiorniSaldoPossibili.Contains(giorni)) return;
        _giorniSaldo = giorni;
        _database.SalvaImpostazione(ChiaveGiorniSaldo, giorni.ToString());
        _log.Info($"Home: andamento del saldo di {giorni} giorni");
    }

    public void ImpostaImportiNascosti(bool nascosti)
    {
        if (nascosti == ImportiNascosti) return;
        _importiNascosti = nascosti;
        _database.SalvaImpostazione(ChiaveImportiNascosti, nascosti ? "1" : "0");
        _log.Info(nascosti ? "Importi nascosti" : "Importi di nuovo visibili");
        ImportiNascostiCambiati?.Invoke();
    }

    /// <summary>Dopo un backup riuscito: dove e quando.</summary>
    public void RegistraBackup(string cartella, DateTime quando)
    {
        _cartellaBackup = cartella;
        _ultimoBackup = quando;
        _database.SalvaImpostazione(ChiaveCartellaBackup, cartella);
        _database.SalvaImpostazione(ChiaveUltimoBackup, quando.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
    }

    public void ImpostaContiHome(IReadOnlyCollection<string> conti)
    {
        _contiHome = conti.ToList();
        _database.SalvaImpostazione(ChiaveContiHome, string.Join('|', conti));
        _log.Info($"Home: conti scelti {(conti.Count == 0 ? "tutti" : string.Join(", ", conti.Select(Mascherato.Conto)))}");
    }

    public void ImpostaMediaHome(string media)
    {
        if (media == MediaHome || !MedieHomePossibili.Contains(media)) return;
        _mediaHome = media;
        _database.SalvaImpostazione(ChiaveMediaHome, media);
        _log.Info($"Home: confronto con la media di {media}");
    }
}
