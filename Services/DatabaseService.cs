using EasyCashFlow.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace EasyCashFlow.Services;

/// <summary>Com'è il file del database prima di aprirlo.</summary>
public enum StatoDatabase
{
    Assente,    // primo avvio
    InChiaro,   // di una versione di prova senza cifratura (fino alla 0.9.2): non si apre
    Cifrato     // formato SQLCipher 4: serve la chiave
}

public class DatabaseService
{
    private readonly string _stringaConnessione;
    private readonly LogService _log;

    // Chiave grezza del database, in esadecimale per PRAGMA key; null finché non è aperto.
    // Si imposta in Inizializza e vale per tutte le connessioni.
    private string? _chiaveHex;

    public string Percorso { get; }

    /// <summary>Vero dopo <see cref="Inizializza"/>: prima nessun metodo può leggere o scrivere.</summary>
    public bool Pronto { get; private set; }

    // Il costruttore non apre il database: con la cifratura serve prima la chiave, che arriva dalla
    // password (ChiaviService). Lo apre Inizializza.
    public DatabaseService(LogService log)
    {
        _log = log;
        Directory.CreateDirectory(PercorsiDati.Cartella);
        Percorso = PercorsiDati.Database;
        _stringaConnessione = $"Data Source={Percorso}";
    }

    /// <summary>Riconosce il file dall'intestazione: un database SQLite in chiaro inizia con
    /// "SQLite format 3"; uno cifrato no (al suo posto c'è il salt di SQLCipher).</summary>
    public StatoDatabase Stato()
    {
        if (!File.Exists(Percorso) || new FileInfo(Percorso).Length == 0) return StatoDatabase.Assente;

        var intestazione = new byte[16];
        using (var flusso = new FileStream(Percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            flusso.ReadExactly(intestazione);
        return intestazione.AsSpan().SequenceEqual("SQLite format 3\0"u8) ? StatoDatabase.InChiaro : StatoDatabase.Cifrato;
    }

    /// <summary>Apre il database cifrato con la chiave dei dati (e lo crea se manca). Con una chiave
    /// sbagliata lancia SqliteException ("file is not a database").</summary>
    public void Inizializza(byte[] chiave)
    {
        _chiaveHex = Convert.ToHexString(chiave);
        try
        {
            CreaSchema();
        }
        catch
        {
            _chiaveHex = null;
            SqliteConnection.ClearAllPools();
            throw;
        }
        Pronto = true;
        Mascherato.ImpostaChiave(ChiaveImpronta());
        _log.Info($"Database aperto: {Mascherato.Percorso(Percorso)}");
    }

    /// <summary>Chiude tutte le connessioni (quelle nel pool tengono il file aperto) e dimentica la chiave.
    /// Va chiamato prima di copiare, sostituire o cancellare il file, e al blocco.</summary>
    public void Chiudi()
    {
        SqliteConnection.ClearAllPools();
        _chiaveHex = null;
        Pronto = false;
    }

    /// <summary>Per il backup: chiude le connessioni del pool, che tengono il file aperto, ma senza
    /// dimenticare la chiave come Chiudi. Senza registro WAL, a connessioni chiuse il file è completo.</summary>
    public void RilasciaFile() => SqliteConnection.ClearAllPools();

    /// <summary>Per la verifica del backup: apre la copia di un database con la chiave attuale e conta i
    /// movimenti. Lancia SqliteException se la copia non si apre con questa chiave.</summary>
    public int ContaMovimentiNellaCopia(string percorso)
    {
        using var connessione = new SqliteConnection($"Data Source={percorso};Mode=ReadOnly;Pooling=False");
        connessione.Open();
        using var comando = connessione.CreateCommand();
        comando.CommandText = $"PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4; PRAGMA key = \"x'{_chiaveHex}'\";";
        comando.ExecuteNonQuery();
        comando.CommandText = "SELECT COUNT(*) FROM Movimenti";
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    private const string ChiaveImprontaLog = "Log.ChiaveImpronta";

    // Chiave delle impronte nel log (Mascherato.Impronta): casuale, creata al primo avvio e conservata
    // nel database, così lo stesso testo ha la stessa impronta in tutti i log. Chi riceve un log non ce l'ha.
    private byte[] ChiaveImpronta()
    {
        if (LeggiImpostazioni().TryGetValue(ChiaveImprontaLog, out var salvata))
        {
            try { return Convert.FromBase64String(salvata); }
            catch (FormatException) { }     // rovinata: se ne crea una nuova
        }
        var nuova = RandomNumberGenerator.GetBytes(32);
        SalvaImpostazione(ChiaveImprontaLog, Convert.ToBase64String(nuova));
        return nuova;
    }

    // Ogni connessione, appena aperta e prima di qualunque altra istruzione, riceve la chiave grezza
    // (x'…'): con la chiave grezza SQLCipher non rifà la sua derivazione, e l'apertura resta sotto il
    // millisecondo (docs/07-sicurezza.md, sezione 2). Non si usa "Password" nella stringa di connessione,
    // che passerebbe la chiave come frase.
    private SqliteConnection Apri()
    {
        var connessione = new SqliteConnection(_stringaConnessione);
        connessione.Open();
        if (_chiaveHex is not null)
        {
            using var comando = connessione.CreateCommand();
            comando.CommandText = $"PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4; PRAGMA key = \"x'{_chiaveHex}'\";";
            comando.ExecuteNonQuery();
        }
        return connessione;
    }

    private void CreaSchema()
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS Movimenti (
                Conto             TEXT    NOT NULL,
                DataContabile     TEXT    NOT NULL,   -- yyyy-MM-dd
                DataValuta        TEXT    NOT NULL,
                ImportoCentesimi  INTEGER NOT NULL,   -- negativo = addebito
                Causale           TEXT    NOT NULL,
                Riferimento       TEXT    NOT NULL,
                DescrizioneBreve  TEXT    NOT NULL,
                DescrizioneEstesa TEXT    NOT NULL,
                ImportatoIl       TEXT    NOT NULL,
                Straordinario     INTEGER NOT NULL DEFAULT 0,   -- 1 = non si ripeterà (escludibile da analisi e simulazione)
                TipoForzato       TEXT,                         -- tipo scelto al posto di quello del gruppo (NULL = del gruppo)
                PRIMARY KEY (Conto, DataContabile, Riferimento, ImportoCentesimi)
            );
            CREATE INDEX IF NOT EXISTS IX_Movimenti_Data ON Movimenti (DataContabile);

            CREATE TABLE IF NOT EXISTS MovimentiScartati (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                Motivo            TEXT    NOT NULL,
                NomeFile          TEXT    NOT NULL,
                RigaFile          INTEGER NOT NULL,
                ScartatoIl        TEXT    NOT NULL,
                Conto             TEXT    NOT NULL,
                DataContabile     TEXT    NOT NULL,
                DataValuta        TEXT    NOT NULL,
                ImportoCentesimi  INTEGER NOT NULL,
                Causale           TEXT    NOT NULL,
                Riferimento       TEXT    NOT NULL,
                DescrizioneBreve  TEXT    NOT NULL,
                DescrizioneEstesa TEXT    NOT NULL,
                UNIQUE (NomeFile, Conto, DataContabile, DataValuta, ImportoCentesimi,
                        Causale, Riferimento, DescrizioneBreve, DescrizioneEstesa)
            );

            CREATE TABLE IF NOT EXISTS Conti (
                Conto        TEXT NOT NULL PRIMARY KEY,   -- ABI + CAB + numero, come in Movimenti
                Iban         TEXT NOT NULL,               -- vuoto se non ricavabile dal file
                Abi          TEXT NOT NULL,
                Divisa       TEXT NOT NULL,
                Nome         TEXT NOT NULL,               -- scelto dall'utente
                RegistratoIl TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Impostazioni (
                Chiave TEXT NOT NULL PRIMARY KEY,
                Valore TEXT NOT NULL
            );

            -- saldi contabili di ogni giornata presente nei file CBI (record 61 e 64)
            CREATE TABLE IF NOT EXISTS SaldiGiornalieri (
                Conto                 TEXT    NOT NULL,
                Data                  TEXT    NOT NULL,   -- yyyy-MM-dd
                SaldoInizialeCentesimi INTEGER NOT NULL,
                SaldoFinaleCentesimi   INTEGER NOT NULL,
                PRIMARY KEY (Conto, Data)
            );

            -- un registro delle importazioni riuscite
            CREATE TABLE IF NOT EXISTS Importazioni (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                NomeFile     TEXT    NOT NULL,
                ImportatoIl  TEXT    NOT NULL,   -- yyyy-MM-dd HH:mm:ss
                Letti        INTEGER NOT NULL,
                Inseriti     INTEGER NOT NULL,
                GiaPresenti  INTEGER NOT NULL,
                Scartati     INTEGER NOT NULL,   -- tutti i non caricati, compresi i già presenti
                Dal          TEXT,               -- prima e ultima giornata del file (yyyy-MM-dd)
                Al           TEXT,
                Avvisi       INTEGER NOT NULL
            );

            -- fino al 09/11 il gruppo di ogni causale completa; dal 10/11 non si usa più (il gruppo viene dalla
            -- causale CBI, GruppiCausaliCbi), e resta solo perché i database di prima la hanno
            CREATE TABLE IF NOT EXISTS GruppiCausali (
                Causale    TEXT    NOT NULL PRIMARY KEY,
                Gruppo     TEXT    NOT NULL,
                Tipo       TEXT    NOT NULL,   -- Ordinario, Investimento, Giroconto (enum TipoGruppo)
                Ricorrente INTEGER NOT NULL    -- 1 = spesa fissa (mutuo, utenze, ...), 0 = variabile
            );

            -- come trattare i movimenti nelle analisi (dal 10/11): gruppo, tipo e se è una spesa ricorrente, per
            -- ogni causale CBI, le prime due cifre della causale, uguali per tutte le banche. Vale per tutte le
            -- causali che cominciano con quel codice. Una causale CBI senza riga non ha gruppo
            -- le causali scelte dall'utente per i movimenti dei file Excel, per descrizione ridotta a parole
            -- (dal 10/11): un movimento con la stessa descrizione, importato dopo, prende la stessa causale.
            -- Restano anche se si elimina il conto
            CREATE TABLE IF NOT EXISTS CausaliImparate (
                Chiave     TEXT NOT NULL PRIMARY KEY,   -- ExcelParser.ChiaveDescrizione
                Causale    TEXT NOT NULL,
                ScelteIl   TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS GruppiCausaliCbi (
                Codice     TEXT    NOT NULL PRIMARY KEY,
                Gruppo     TEXT    NOT NULL,
                Tipo       TEXT    NOT NULL,
                Ricorrente INTEGER NOT NULL
            );

            -- il gruppo di ogni causale dei movimenti, dalla sua causale CBI; le causali senza gruppo non
            -- compaiono (nelle analisi: "Altro")
            DROP VIEW IF EXISTS GruppiEffettivi;
            CREATE VIEW GruppiEffettivi AS
            SELECT c.Causale, r.Gruppo, r.Tipo, r.Ricorrente
            FROM (SELECT DISTINCT Causale FROM Movimenti) c
            JOIN GruppiCausaliCbi r ON r.Codice = substr(c.Causale, 1, 2);
            """;
        comando.ExecuteNonQuery();

        PreparaGruppi(connessione);
        RicordaCausaliGiaScelte(connessione);
    }

    private const string ChiaveCausaliImparate = "CausaliImparate.Versione";

    // Una volta sola (dal 10/11): le causali scelte dall'utente per i movimenti Excel prima che il programma le
    // ricordasse. Sono i movimenti Excel con una causale diversa sia dalla fittizia sia da quella che le regole
    // ricavano dalla descrizione: la scelta finisce in CausaliImparate, come se fosse fatta adesso.
    private void RicordaCausaliGiaScelte(SqliteConnection connessione)
    {
        using (var lettura = connessione.CreateCommand())
        {
            lettura.CommandText = "SELECT Valore FROM Impostazioni WHERE Chiave = $chiave";
            lettura.Parameters.AddWithValue("$chiave", ChiaveCausaliImparate);
            if (lettura.ExecuteScalar() is string versione && versione != "") return;
        }

        var scelte = new Dictionary<string, string>();
        using (var cerca = connessione.CreateCommand())
        {
            cerca.CommandText = """
                SELECT DescrizioneEstesa, Causale, ImportoCentesimi FROM Movimenti
                WHERE Riferimento LIKE 'XL%' AND Causale <> $fittizia
                """;
            cerca.Parameters.AddWithValue("$fittizia", CausaliCbi.Fittizia);
            using var lettore = cerca.ExecuteReader();
            while (lettore.Read())
            {
                var descrizione = lettore.GetString(0);
                var causale = lettore.GetString(1);
                var chiave = ExcelParser.ChiaveDescrizione(descrizione);
                if (chiave != "" && causale != ExcelParser.Causale("", descrizione, lettore.GetInt64(2) / 100m))
                    scelte[chiave] = causale;
            }
        }

        using var transazione = connessione.BeginTransaction();
        using (var ricorda = connessione.CreateCommand())
        {
            ricorda.Transaction = transazione;
            ricorda.CommandText = """
                INSERT INTO CausaliImparate (Chiave, Causale, ScelteIl) VALUES ($chiave, $causale, $quando)
                ON CONFLICT DO NOTHING
                """;
            var pChiave = ricorda.Parameters.Add("$chiave", SqliteType.Text);
            var pCausale = ricorda.Parameters.Add("$causale", SqliteType.Text);
            ricorda.Parameters.AddWithValue("$quando", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            foreach (var (chiave, causale) in scelte)
            {
                pChiave.Value = chiave;
                pCausale.Value = causale;
                ricorda.ExecuteNonQuery();
            }
        }
        using (var versione = connessione.CreateCommand())
        {
            versione.Transaction = transazione;
            versione.CommandText = """
                INSERT INTO Impostazioni (Chiave, Valore) VALUES ($chiave, '1')
                ON CONFLICT (Chiave) DO UPDATE SET Valore = excluded.Valore
                """;
            versione.Parameters.AddWithValue("$chiave", ChiaveCausaliImparate);
            versione.ExecuteNonQuery();
        }
        transazione.Commit();
        if (scelte.Count > 0)
            _log.Info($"Causali scelte prima per i movimenti Excel ricordate: {scelte.Count} descrizioni");
    }

    private const string ChiaveVersioneGruppi = "GruppiCausali.Versione";

    // Una volta sola (dal 10/11): i gruppi di partenza delle causali CBI (CausaliCbi.GruppiDiPartenza). Da lì
    // in poi li sceglie l'utente; una regola tolta non torna. I gruppi scelti fino al 09/11 per le causali
    // complete (tabella GruppiCausali) non contano più.
    private void PreparaGruppi(SqliteConnection connessione)
    {
        using (var lettura = connessione.CreateCommand())
        {
            lettura.CommandText = "SELECT Valore FROM Impostazioni WHERE Chiave = $chiave";
            lettura.Parameters.AddWithValue("$chiave", ChiaveVersioneGruppi);
            if (lettura.ExecuteScalar() is string versione && versione != "") return;
        }

        using var transazione = connessione.BeginTransaction();
        using (var regola = connessione.CreateCommand())
        {
            regola.Transaction = transazione;
            regola.CommandText = """
                INSERT INTO GruppiCausaliCbi (Codice, Gruppo, Tipo, Ricorrente)
                VALUES ($codice, $gruppo, $tipo, $ricorrente)
                ON CONFLICT DO NOTHING
                """;
            var pCodice = regola.Parameters.Add("$codice", SqliteType.Text);
            var pGruppo = regola.Parameters.Add("$gruppo", SqliteType.Text);
            var pTipo = regola.Parameters.Add("$tipo", SqliteType.Text);
            var pRicorrente = regola.Parameters.Add("$ricorrente", SqliteType.Integer);
            foreach (var g in CausaliCbi.GruppiDiPartenza)
            {
                pCodice.Value = g.Codice;
                pGruppo.Value = g.Gruppo;
                pTipo.Value = g.Tipo.ToString();
                pRicorrente.Value = g.Ricorrente ? 1 : 0;
                regola.ExecuteNonQuery();
            }
        }
        using (var versione = connessione.CreateCommand())
        {
            versione.Transaction = transazione;
            versione.CommandText = """
                INSERT INTO Impostazioni (Chiave, Valore) VALUES ($chiave, '3')
                ON CONFLICT (Chiave) DO UPDATE SET Valore = excluded.Valore
                """;
            versione.Parameters.AddWithValue("$chiave", ChiaveVersioneGruppi);
            versione.ExecuteNonQuery();
        }
        transazione.Commit();
        _log.Info("Gruppi delle causali CBI di partenza preparati");
    }

    /// <summary>Segna (o smette di segnare) dei movimenti come straordinari.</summary>
    public int ImpostaStraordinario(IEnumerable<ChiaveMovimento> movimenti, bool straordinario)
    {
        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        using var comando = ComandoSuMovimento(connessione, transazione,
            "UPDATE Movimenti SET Straordinario = $valore");
        comando.Parameters.AddWithValue("$valore", straordinario ? 1 : 0);

        int modificati = 0;
        foreach (var m in movimenti)
            modificati += EseguiSuMovimento(comando, m);
        transazione.Commit();

        _log.Info($"Movimenti {(straordinario ? "segnati come" : "non più")} straordinari: {modificati}");
        return modificati;
    }

    /// <summary>Sceglie il tipo di un movimento al posto di quello del suo gruppo (null = torna al gruppo).</summary>
    public void ImpostaTipoForzato(ChiaveMovimento movimento, TipoGruppo? tipo)
    {
        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        using var comando = ComandoSuMovimento(connessione, transazione,
            "UPDATE Movimenti SET TipoForzato = $tipo");
        comando.Parameters.AddWithValue("$tipo", tipo is { } t ? t.ToString() : DBNull.Value);
        EseguiSuMovimento(comando, movimento);
        transazione.Commit();

        _log.Info($"Movimento {Mascherato.Chiave(movimento)}: tipo {(tipo is { } scelto ? scelto.ToString() : "del gruppo")}");
    }

    // un UPDATE sulla riga con questa chiave primaria; i parametri della chiave si impostano per ogni movimento
    private static SqliteCommand ComandoSuMovimento(SqliteConnection connessione, SqliteTransaction transazione, string update)
    {
        var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = update + """
             WHERE Conto = $conto AND DataContabile = $data AND Riferimento = $riferimento
               AND ImportoCentesimi = $importo
            """;
        comando.Parameters.Add("$conto", SqliteType.Text);
        comando.Parameters.Add("$data", SqliteType.Text);
        comando.Parameters.Add("$riferimento", SqliteType.Text);
        comando.Parameters.Add("$importo", SqliteType.Integer);
        return comando;
    }

    private static int EseguiSuMovimento(SqliteCommand comando, ChiaveMovimento m)
    {
        comando.Parameters["$conto"].Value = m.Conto;
        comando.Parameters["$data"].Value = ScriviData(m.DataContabile);
        comando.Parameters["$riferimento"].Value = m.Riferimento;
        comando.Parameters["$importo"].Value = Centesimi(m.Importo);
        return comando.ExecuteNonQuery();
    }

    /// <summary>Salva i saldi delle giornate lette da un file. Una giornata già salvata viene
    /// sovrascritta: un export più recente può averla completata (per esempio la giornata in corso).</summary>
    public int SalvaSaldi(IEnumerable<SaldoGiornaliero> saldi)
    {
        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        using var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = """
            INSERT INTO SaldiGiornalieri (Conto, Data, SaldoInizialeCentesimi, SaldoFinaleCentesimi)
            VALUES ($conto, $data, $iniziale, $finale)
            ON CONFLICT (Conto, Data) DO UPDATE SET
                SaldoInizialeCentesimi = excluded.SaldoInizialeCentesimi,
                SaldoFinaleCentesimi   = excluded.SaldoFinaleCentesimi
            """;
        var pConto = comando.Parameters.Add("$conto", SqliteType.Text);
        var pData = comando.Parameters.Add("$data", SqliteType.Text);
        var pIniziale = comando.Parameters.Add("$iniziale", SqliteType.Integer);
        var pFinale = comando.Parameters.Add("$finale", SqliteType.Integer);

        int salvati = 0;
        foreach (var s in saldi)
        {
            pConto.Value = s.Conto;
            pData.Value = ScriviData(s.Data);
            pIniziale.Value = Centesimi(s.SaldoIniziale);
            pFinale.Value = Centesimi(s.SaldoFinale);
            salvati += comando.ExecuteNonQuery();
        }
        transazione.Commit();
        return salvati;
    }

    /// <summary>I movimenti dell'intervallo (estremi compresi) con il gruppo della loro causale.
    /// Conti vuoto = tutti i conti.</summary>
    public List<MovimentoAnalisi> MovimentiPerAnalisi(DateOnly dal, DateOnly al, IReadOnlyCollection<string> conti)
    {
        var cronometro = Stopwatch.StartNew();
        var elenco = new List<MovimentoAnalisi>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        var filtroConti = conti.Count > 0 ? "AND " + CondizioneIn(comando, "m.Conto", "$conto", conti) : "";
        comando.CommandText = $"""
            SELECT m.Conto, m.DataContabile, m.ImportoCentesimi, m.Causale, m.DescrizioneBreve,
                   m.DescrizioneEstesa, m.Riferimento,
                   COALESCE(g.Gruppo, 'Altro'), COALESCE(m.TipoForzato, g.Tipo, 'Ordinario'), COALESCE(g.Ricorrente, 0),
                   m.Straordinario, m.TipoForzato IS NOT NULL, COALESCE(g.Tipo, 'Ordinario')
            FROM Movimenti m
            LEFT JOIN GruppiEffettivi g ON g.Causale = m.Causale
            WHERE m.DataContabile BETWEEN $dal AND $al {filtroConti}
            ORDER BY m.DataContabile
            """;
        comando.Parameters.AddWithValue("$dal", ScriviData(dal));
        comando.Parameters.AddWithValue("$al", ScriviData(al));

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
        {
            elenco.Add(new MovimentoAnalisi(
                Conto: lettore.GetString(0),
                Data: LeggiData(lettore.GetString(1)),
                Importo: lettore.GetInt64(2) / 100m,
                Causale: lettore.GetString(3),
                DescrizioneBreve: lettore.GetString(4),
                DescrizioneEstesa: lettore.GetString(5),
                Riferimento: lettore.GetString(6),
                Gruppo: lettore.GetString(7),
                Tipo: Enum.TryParse<TipoGruppo>(lettore.GetString(8), out var tipo) ? tipo : TipoGruppo.Ordinario,
                Ricorrente: lettore.GetInt64(9) == 1,
                Straordinario: lettore.GetInt64(10) == 1,
                TipoForzato: lettore.GetInt64(11) == 1,
                TipoDelGruppo: Enum.TryParse<TipoGruppo>(lettore.GetString(12), out var delGruppo) ? delGruppo : TipoGruppo.Ordinario));
        }

        _log.Debug($"Analisi: {elenco.Count} movimenti dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}, " +
                   $"{cronometro.ElapsedMilliseconds} ms");
        return elenco;
    }

    /// <summary>Vero se tra tutti i movimenti (di qualunque periodo) dei conti indicati ce n'è almeno uno di
    /// tipo Investimento, quello del gruppo o quello scelto per il movimento. Conti vuoto = tutti i conti.
    /// Senza investimenti la pagina Analisi non mostra la scheda "Investito netto" (dal 06/11/2026).</summary>
    public bool CiSonoInvestimenti(IReadOnlyCollection<string> conti)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        var filtroConti = conti.Count > 0 ? "AND " + CondizioneIn(comando, "m.Conto", "$conto", conti) : "";
        comando.CommandText = $"""
            SELECT EXISTS (
                SELECT 1 FROM Movimenti m
                LEFT JOIN GruppiEffettivi g ON g.Causale = m.Causale
                WHERE COALESCE(m.TipoForzato, g.Tipo, 'Ordinario') = 'Investimento' {filtroConti})
            """;
        return Convert.ToInt64(comando.ExecuteScalar()) == 1;
    }

    /// <summary>I saldi salvati fino alla data indicata (compresa), in ordine di data.
    /// Conti vuoto = tutti i conti.</summary>
    public List<SaldoGiornaliero> SaldiFinoAl(DateOnly al, IReadOnlyCollection<string> conti)
    {
        var elenco = new List<SaldoGiornaliero>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        var filtroConti = conti.Count > 0 ? "AND " + CondizioneIn(comando, "Conto", "$conto", conti) : "";
        comando.CommandText = $"""
            SELECT Conto, Data, SaldoInizialeCentesimi, SaldoFinaleCentesimi
            FROM SaldiGiornalieri
            WHERE Data <= $al {filtroConti}
            ORDER BY Data
            """;
        comando.Parameters.AddWithValue("$al", ScriviData(al));

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            elenco.Add(new SaldoGiornaliero(lettore.GetString(0), LeggiData(lettore.GetString(1)),
                                            lettore.GetInt64(2) / 100m, lettore.GetInt64(3) / 100m));
        return elenco;
    }

    /// <summary>Prima e ultima data contabile, di tutti i conti o solo di quelli indicati
    /// (null se non ci sono movimenti).</summary>
    public (DateOnly Prima, DateOnly Ultima)? IntervalloMovimenti(IReadOnlyCollection<string>? conti = null)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        var filtroConti = conti is { Count: > 0 } ? "WHERE " + CondizioneIn(comando, "Conto", "$conto", conti) : "";
        comando.CommandText = $"SELECT MIN(DataContabile), MAX(DataContabile) FROM Movimenti {filtroConti}";
        using var lettore = comando.ExecuteReader();
        if (!lettore.Read() || lettore.IsDBNull(0)) return null;
        return (LeggiData(lettore.GetString(0)), LeggiData(lettore.GetString(1)));
    }

    /// <summary>Fino a che giorno è aggiornato ogni conto (di tutti o solo di quelli indicati): l'ultima giornata
    /// importata, quella del saldo, che il file CBI ha anche nei giorni senza movimenti; senza saldi, l'ultimo
    /// movimento. I conti si aggiornano con export separati, quindi possono arrivare a date diverse.</summary>
    public Dictionary<string, DateOnly> AggiornatoFinoAPerConto(IReadOnlyCollection<string>? conti = null)
    {
        var elenco = new Dictionary<string, DateOnly>();
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        var filtroConti = conti is { Count: > 0 } ? "WHERE " + CondizioneIn(comando, "Conto", "$conto", conti) : "";
        comando.CommandText = $"""
            SELECT Conto, MAX(Giorno) FROM (
                SELECT Conto, DataContabile AS Giorno FROM Movimenti
                UNION ALL
                SELECT Conto, Data FROM SaldiGiornalieri)
            {filtroConti}
            GROUP BY Conto
            """;
        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            elenco[lettore.GetString(0)] = LeggiData(lettore.GetString(1));
        return elenco;
    }

    /// <summary>Registra un'importazione appena fatta.</summary>
    public void RegistraImportazione(EsitoImportazione esito, DateOnly? dal, DateOnly? al)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = """
            INSERT INTO Importazioni (NomeFile, ImportatoIl, Letti, Inseriti, GiaPresenti, Scartati, Dal, Al, Avvisi)
            VALUES ($file, $quando, $letti, $inseriti, $gia, $scartati, $dal, $al, $avvisi)
            """;
        comando.Parameters.AddWithValue("$file", esito.NomeFile);
        comando.Parameters.AddWithValue("$quando", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        comando.Parameters.AddWithValue("$letti", esito.Letti);
        comando.Parameters.AddWithValue("$inseriti", esito.Inseriti);
        comando.Parameters.AddWithValue("$gia", esito.GiaPresenti);
        comando.Parameters.AddWithValue("$scartati", esito.Scartati);
        comando.Parameters.AddWithValue("$dal", dal is { } d ? ScriviData(d) : DBNull.Value);
        comando.Parameters.AddWithValue("$al", al is { } a ? ScriviData(a) : DBNull.Value);
        comando.Parameters.AddWithValue("$avvisi", esito.Avvisi.Count);
        comando.ExecuteNonQuery();
    }

    /// <summary>I numeri del database per la Home, letti con una sola connessione.</summary>
    public StatisticheDatabase Statistiche()
    {
        using var connessione = Apri();

        long Numero(string sql)
        {
            using var comando = connessione.CreateCommand();
            comando.CommandText = sql;
            return Convert.ToInt64(comando.ExecuteScalar());
        }

        ImportazioneRegistrata? ultima = null;
        using (var comando = connessione.CreateCommand())
        {
            comando.CommandText = """
                SELECT NomeFile, ImportatoIl, Letti, Inseriti, GiaPresenti, Scartati, Dal, Al, Avvisi
                FROM Importazioni ORDER BY ImportatoIl DESC, Id DESC LIMIT 1
                """;
            using var lettore = comando.ExecuteReader();
            if (lettore.Read())
                ultima = new ImportazioneRegistrata(
                    NomeFile: lettore.GetString(0),
                    ImportatoIl: DateTime.ParseExact(lettore.GetString(1), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Letti: lettore.GetInt32(2),
                    Inseriti: lettore.GetInt32(3),
                    GiaPresenti: lettore.GetInt32(4),
                    Scartati: lettore.GetInt32(5),
                    Dal: lettore.IsDBNull(6) ? null : LeggiData(lettore.GetString(6)),
                    Al: lettore.IsDBNull(7) ? null : LeggiData(lettore.GetString(7)),
                    Avvisi: lettore.GetInt32(8));
        }

        return new StatisticheDatabase(
            Movimenti: (int)Numero("SELECT COUNT(*) FROM Movimenti"),
            Intervallo: IntervalloMovimenti(),
            Conti: (int)Numero("SELECT COUNT(*) FROM Conti"),
            Causali: (int)Numero("SELECT COUNT(DISTINCT Causale) FROM Movimenti"),
            CausaliSenzaGruppo: (int)Numero(
                "SELECT COUNT(DISTINCT Causale) FROM Movimenti WHERE Causale NOT IN (SELECT Causale FROM GruppiEffettivi)"),
            // i gruppi effettivamente usati dai movimenti, non tutti quelli predefiniti
            Gruppi: (int)Numero("SELECT COUNT(DISTINCT Gruppo) FROM GruppiEffettivi"),
            GruppiInvestimento: (int)Numero("SELECT COUNT(DISTINCT Gruppo) FROM GruppiEffettivi WHERE Tipo = 'Investimento'"),
            GiornateSaldo: (int)Numero("SELECT COUNT(DISTINCT Data) FROM SaldiGiornalieri"),
            Scartati: (int)Numero("SELECT COUNT(*) FROM MovimentiScartati"),
            Importazioni: (int)Numero("SELECT COUNT(*) FROM Importazioni"),
            FileImportati: (int)Numero("SELECT COUNT(DISTINCT NomeFile) FROM Importazioni"),
            UltimaImportazione: ultima);
    }

    /// <summary>Gli ultimi movimenti per data contabile, i più recenti per primi.</summary>
    public List<MovimentoCbi> UltimiMovimenti(int quanti, IReadOnlyCollection<string>? conti = null) =>
        CercaMovimenti(new FiltriMovimenti { Conti = conti ?? [] }, 0, quanti).Righe;

    /// <summary>Le causali presenti nei movimenti, con quanti movimenti hanno, il loro totale e il
    /// gruppo assegnato (Gruppo null se la causale non ne ha uno e nelle analisi finisce in "Altro").</summary>
    public List<GruppoCausale> ElencoGruppiCausali()
    {
        var elenco = new List<GruppoCausale>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = """
            SELECT m.Causale, MIN(m.DescrizioneBreve), COUNT(*), SUM(m.ImportoCentesimi),
                   g.Gruppo, g.Tipo, g.Ricorrente
            FROM Movimenti m
            LEFT JOIN GruppiEffettivi g ON g.Causale = m.Causale
            GROUP BY m.Causale
            """;

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
        {
            var senzaGruppo = lettore.IsDBNull(4);
            elenco.Add(new GruppoCausale(
                Causale: lettore.GetString(0),
                Descrizione: lettore.GetString(1).Trim().TrimEnd('-', ' '),
                Movimenti: lettore.GetInt32(2),
                Totale: lettore.GetInt64(3) / 100m,
                Gruppo: senzaGruppo ? null : lettore.GetString(4),
                Tipo: !senzaGruppo && Enum.TryParse<TipoGruppo>(lettore.GetString(5), out var tipo) ? tipo : TipoGruppo.Ordinario,
                Ricorrente: !senzaGruppo && lettore.GetInt64(6) == 1));
        }
        return elenco;
    }

    /// <summary>I gruppi delle causali CBI (dal 10/11): tutte le causali dello standard, con il gruppo scelto
    /// (null se non ne hanno), quanti movimenti hanno e il loro totale.</summary>
    public List<RegolaCausaleCbi> RegoleCausaliCbi()
    {
        var regole = new Dictionary<string, (string Gruppo, TipoGruppo Tipo, bool Ricorrente)>();
        var movimenti = new Dictionary<string, (int Numero, decimal Totale)>();
        using var connessione = Apri();
        using (var comando = connessione.CreateCommand())
        {
            comando.CommandText = "SELECT Codice, Gruppo, Tipo, Ricorrente FROM GruppiCausaliCbi";
            using var lettore = comando.ExecuteReader();
            while (lettore.Read())
                regole[lettore.GetString(0)] = (lettore.GetString(1),
                    Enum.TryParse<TipoGruppo>(lettore.GetString(2), out var tipo) ? tipo : TipoGruppo.Ordinario,
                    lettore.GetInt64(3) == 1);
        }
        using (var comando = connessione.CreateCommand())
        {
            comando.CommandText = """
                SELECT substr(Causale, 1, 2), COUNT(*), SUM(ImportoCentesimi)
                FROM Movimenti GROUP BY substr(Causale, 1, 2)
                """;
            using var lettore = comando.ExecuteReader();
            while (lettore.Read())
                movimenti[lettore.GetString(0)] = (lettore.GetInt32(1), lettore.GetInt64(2) / 100m);
        }

        return CausaliCbi.Scegliibili
            .Select(c =>
            {
                var (numero, totale) = movimenti.GetValueOrDefault(c.Codice);
                return regole.TryGetValue(c.Codice, out var r)
                    ? new RegolaCausaleCbi(c.Codice, c.Descrizione, r.Gruppo, r.Tipo, r.Ricorrente, numero, totale)
                    : new RegolaCausaleCbi(c.Codice, c.Descrizione, null, TipoGruppo.Ordinario, false, numero, totale);
            })
            .ToList();
    }

    /// <summary>Il gruppo di una causale CBI; gruppo null o vuoto lo toglie (le sue causali restano senza
    /// gruppo, e nelle analisi finiscono in "Altro").</summary>
    public void SalvaRegolaCbi(string codice, string? gruppo, TipoGruppo tipo, bool ricorrente)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        if (string.IsNullOrWhiteSpace(gruppo))
        {
            comando.CommandText = "DELETE FROM GruppiCausaliCbi WHERE Codice = $codice";
            comando.Parameters.AddWithValue("$codice", codice);
            comando.ExecuteNonQuery();
            _log.Info($"Causale CBI {codice}: nessun gruppo");
            return;
        }
        comando.CommandText = """
            INSERT INTO GruppiCausaliCbi (Codice, Gruppo, Tipo, Ricorrente)
            VALUES ($codice, $gruppo, $tipo, $ricorrente)
            ON CONFLICT (Codice) DO UPDATE SET
                Gruppo = excluded.Gruppo, Tipo = excluded.Tipo, Ricorrente = excluded.Ricorrente
            """;
        comando.Parameters.AddWithValue("$codice", codice);
        comando.Parameters.AddWithValue("$gruppo", gruppo.Trim());
        comando.Parameters.AddWithValue("$tipo", tipo.ToString());
        comando.Parameters.AddWithValue("$ricorrente", ricorrente ? 1 : 0);
        comando.ExecuteNonQuery();
        _log.Info($"Causale CBI {codice}: gruppo {Mascherato.Nome("gruppo", gruppo)}, tipo {tipo}, {(ricorrente ? "ricorrente" : "variabile")}");
    }

    /// <summary>Tutti i nomi di gruppo usati, per proporli nella tendina.</summary>
    public List<string> NomiGruppi()
    {
        var nomi = new List<string>();
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT DISTINCT Gruppo FROM GruppiCausaliCbi";
        using var lettore = comando.ExecuteReader();
        while (lettore.Read()) nomi.Add(lettore.GetString(0));
        var italiano = StringComparer.Create(CultureInfo.GetCultureInfo("it-IT"), ignoreCase: true);
        return nomi.Distinct(italiano).Order(italiano).ToList();
    }

    /// <summary>I gruppi delle causali CBI, con la descrizione dello standard, per l'esportazione.</summary>
    public List<GruppoEsportato> TuttiGruppiCausali() =>
        RegoleCausaliCbi().Where(r => r.Gruppo is not null)
            .Select(r => new GruppoEsportato(r.Codice, r.Gruppo!, r.Tipo, r.Ricorrente, r.Descrizione))
            .ToList();

    /// <summary>Scrive i gruppi delle causali CBI letti da un file esportato. Restituisce quante causali erano
    /// senza gruppo, quante sono cambiate e quante erano già uguali.</summary>
    public (int Nuove, int Cambiate, int Uguali) ImportaGruppiCausali(IReadOnlyList<GruppoEsportato> gruppi)
    {
        int nuove = 0, cambiate = 0, uguali = 0;
        var attuali = RegoleCausaliCbi().Where(r => r.Gruppo is not null).ToDictionary(r => r.Codice);
        foreach (var g in gruppi)
        {
            if (!attuali.TryGetValue(g.Causale, out var a)) nuove++;
            else if (a.Gruppo == g.Gruppo && a.Tipo == g.Tipo && a.Ricorrente == g.Ricorrente) { uguali++; continue; }
            else cambiate++;
            SalvaRegolaCbi(g.Causale, g.Gruppo, g.Tipo, g.Ricorrente);
        }
        _log.Info($"Gruppi delle causali CBI importati da file: {nuove} nuovi, {cambiate} cambiati, {uguali} già uguali");
        return (nuove, cambiate, uguali);
    }

    private static int InserisciConto(SqliteConnection connessione, SqliteTransaction transazione, ContoCorrente c)
    {
        using var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = """
            INSERT INTO Conti (Conto, Iban, Abi, Divisa, Nome, RegistratoIl)
            VALUES ($conto, $iban, $abi, $divisa, $nome, $registratoIl)
            ON CONFLICT DO NOTHING
            """;
        comando.Parameters.AddWithValue("$conto", c.Codice);
        comando.Parameters.AddWithValue("$iban", c.Iban);
        comando.Parameters.AddWithValue("$abi", c.Abi);
        comando.Parameters.AddWithValue("$divisa", c.Divisa);
        comando.Parameters.AddWithValue("$nome", c.Nome);
        comando.Parameters.AddWithValue("$registratoIl",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        return comando.ExecuteNonQuery();
    }

    /// <summary>Registra i conti letti da un file. Un conto nuovo riceve un nome provvisorio;
    /// per uno già noto si completano IBAN e divisa se mancavano, senza toccare il nome.</summary>
    public void RegistraConti(IEnumerable<ContoCorrente> conti)
    {
        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();

        foreach (var c in conti)
        {
            if (InserisciConto(connessione, transazione, c with { Nome = ContoCorrente.NomeProvvisorio(c.Codice) }) == 1)
            {
                _log.Info($"Nuovo conto registrato: {Mascherato.Conto(c.Codice)} (IBAN {Mascherato.Iban(c.Iban)}, divisa {c.Divisa})");
                continue;
            }

            using var aggiorna = connessione.CreateCommand();
            aggiorna.Transaction = transazione;
            aggiorna.CommandText = """
                UPDATE Conti
                SET Iban   = CASE WHEN Iban = ''   THEN $iban   ELSE Iban   END,
                    Divisa = CASE WHEN Divisa = '' THEN $divisa ELSE Divisa END
                WHERE Conto = $conto AND ((Iban = '' AND $iban <> '') OR (Divisa = '' AND $divisa <> ''))
                """;
            aggiorna.Parameters.AddWithValue("$conto", c.Codice);
            aggiorna.Parameters.AddWithValue("$iban", c.Iban);
            aggiorna.Parameters.AddWithValue("$divisa", c.Divisa);

            if (aggiorna.ExecuteNonQuery() == 1)
                _log.Info($"Conto {Mascherato.Conto(c.Codice)}: completati IBAN e divisa dal file");
            else
                _log.Trace($"Conto {Mascherato.Conto(c.Codice)} già registrato");
        }

        transazione.Commit();
    }

    /// <summary>Tutte le impostazioni salvate, come coppie chiave-valore.</summary>
    public Dictionary<string, string> LeggiImpostazioni()
    {
        var impostazioni = new Dictionary<string, string>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT Chiave, Valore FROM Impostazioni";

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            impostazioni[lettore.GetString(0)] = lettore.GetString(1);

        return impostazioni;
    }

    public void SalvaImpostazione(string chiave, string valore)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = """
            INSERT INTO Impostazioni (Chiave, Valore) VALUES ($chiave, $valore)
            ON CONFLICT (Chiave) DO UPDATE SET Valore = excluded.Valore
            """;
        comando.Parameters.AddWithValue("$chiave", chiave);
        comando.Parameters.AddWithValue("$valore", valore);
        comando.ExecuteNonQuery();
    }

    /// <summary>Tutti i conti registrati, in ordine di nome.</summary>
    public List<ContoCorrente> ElencoConti()
    {
        var elenco = new List<ContoCorrente>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT Conto, Iban, Abi, Divisa, Nome FROM Conti ORDER BY Nome COLLATE NOCASE";

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            elenco.Add(new ContoCorrente(lettore.GetString(0), lettore.GetString(1), lettore.GetString(2),
                                         lettore.GetString(3), lettore.GetString(4)));

        return elenco;
    }

    /// <summary>Quanti dati ha un conto, per la conferma prima di eliminarlo.</summary>
    public DatiConto DatiDelConto(string codice)
    {
        using var connessione = Apri();

        long Numero(string sql)
        {
            using var comando = connessione.CreateCommand();
            comando.CommandText = sql;
            comando.Parameters.AddWithValue("$conto", codice);
            return Convert.ToInt64(comando.ExecuteScalar());
        }

        (DateOnly, DateOnly)? intervallo = null;
        using (var comando = connessione.CreateCommand())
        {
            comando.CommandText = "SELECT MIN(DataContabile), MAX(DataContabile) FROM Movimenti WHERE Conto = $conto";
            comando.Parameters.AddWithValue("$conto", codice);
            using var lettore = comando.ExecuteReader();
            if (lettore.Read() && !lettore.IsDBNull(0))
                intervallo = (LeggiData(lettore.GetString(0)), LeggiData(lettore.GetString(1)));
        }

        return new DatiConto(
            Movimenti: (int)Numero("SELECT COUNT(*) FROM Movimenti WHERE Conto = $conto"),
            Intervallo: intervallo,
            Giornate: (int)Numero("SELECT COUNT(*) FROM SaldiGiornalieri WHERE Conto = $conto"),
            Scartati: (int)Numero("SELECT COUNT(*) FROM MovimentiScartati WHERE Conto = $conto"),
            Straordinari: (int)Numero("SELECT COUNT(*) FROM Movimenti WHERE Conto = $conto AND (Straordinario = 1 OR TipoForzato IS NOT NULL)"));
    }

    /// <summary>Elimina un conto con tutti i suoi dati, in un'unica transazione: movimenti (con le scelte
    /// fatte sui singoli movimenti), saldi giornalieri, movimenti scartati e il conto stesso. Restano i
    /// gruppi di causali, che valgono per tutti i conti, e il registro delle importazioni, che riguarda i file.
    /// Restituisce quanti dati ha eliminato.</summary>
    public DatiConto EliminaConto(string codice)
    {
        var prima = DatiDelConto(codice);

        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        foreach (var tabella in new[] { "Movimenti", "SaldiGiornalieri", "MovimentiScartati", "Conti" })
        {
            using var comando = connessione.CreateCommand();
            comando.Transaction = transazione;
            comando.CommandText = $"DELETE FROM {tabella} WHERE Conto = $conto";   // nome di tabella da un elenco fisso
            comando.Parameters.AddWithValue("$conto", codice);
            comando.ExecuteNonQuery();
        }
        transazione.Commit();

        _log.Info($"Conto {Mascherato.Conto(codice)} eliminato: {prima.Movimenti} movimenti, {prima.Giornate} giornate di saldo, " +
                  $"{prima.Scartati} movimenti scartati");
        return prima;
    }

    public void RinominaConto(string codice, string nome)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "UPDATE Conti SET Nome = $nome WHERE Conto = $conto";
        comando.Parameters.AddWithValue("$conto", codice);
        comando.Parameters.AddWithValue("$nome", nome);
        comando.ExecuteNonQuery();
        _log.Info($"Conto {Mascherato.Conto(codice)} rinominato in {Mascherato.Nome("nome", nome)}");
    }

    /// <summary>Inserisce i movimenti in un'unica transazione. Ogni movimento non inserito
    /// viene restituito (e scritto nel log) con il motivo dello scarto. breveDellaBanca è falso per i movimenti
    /// letti dall'XML, dove la descrizione breve è ricavata dal testo: non sostituisce quella già salvata.</summary>
    public RisultatoInserimento InserisciMovimenti(IEnumerable<MovimentoCbi> movimenti, string nomeFile, bool breveDellaBanca = true)
    {
        var scartati = new List<RecordScartato>();

        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        using var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = """
            INSERT INTO Movimenti
                (Conto, DataContabile, DataValuta, ImportoCentesimi, Causale, Riferimento,
                 DescrizioneBreve, DescrizioneEstesa, ImportatoIl)
            VALUES
                ($conto, $dataContabile, $dataValuta, $importo, $causale, $riferimento,
                 $descBreve, $descEstesa, $importatoIl)
            ON CONFLICT DO NOTHING
            """;

        var pConto = comando.Parameters.Add("$conto", SqliteType.Text);
        var pDataContabile = comando.Parameters.Add("$dataContabile", SqliteType.Text);
        var pDataValuta = comando.Parameters.Add("$dataValuta", SqliteType.Text);
        var pImporto = comando.Parameters.Add("$importo", SqliteType.Integer);
        var pCausale = comando.Parameters.Add("$causale", SqliteType.Text);
        var pRiferimento = comando.Parameters.Add("$riferimento", SqliteType.Text);
        var pDescBreve = comando.Parameters.Add("$descBreve", SqliteType.Text);
        var pDescEstesa = comando.Parameters.Add("$descEstesa", SqliteType.Text);
        var pImportatoIl = comando.Parameters.Add("$importatoIl", SqliteType.Text);

        pImportatoIl.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        _log.Debug($"SQL inserimento: {Compatta(comando.CommandText)}");

        int inseriti = 0, aggiornati = 0;
        foreach (var m in movimenti)
        {
            pConto.Value = m.Conto;
            pDataContabile.Value = ScriviData(m.DataContabile);
            pDataValuta.Value = ScriviData(m.DataValuta);
            pImporto.Value = Centesimi(m.Importo);
            pCausale.Value = m.Causale;
            pRiferimento.Value = m.Riferimento;
            pDescBreve.Value = m.DescrizioneBreve;
            pDescEstesa.Value = m.DescrizioneEstesa;

            try
            {
                if (_log.DebugAttivo)
                    _log.Debug($"SQL parametri: {ElencaParametri(comando)}");

                if (comando.ExecuteNonQuery() == 1)
                {
                    inseriti++;
                    _log.Trace($"Riga {m.RigaFile}: INSERITO {Sintesi(m)}");
                }
                else
                {
                    RecordScartato scarto;

                    CorreggiCausaleFittizia(connessione, transazione, m);

                    if (AggiornaDescrizioni(connessione, transazione, m, breveDellaBanca))
                    {
                        aggiornati++;
                        scarto = new RecordScartato(MotivoScarto.GiaPresente,
                            "Movimento già presente nel database: aggiornata la descrizione, letta in modo più completo.",
                            m.RigaFile, null, m);
                    }
                    else if (HaContenutoDiverso(connessione, transazione, m))
                    {
                        scarto = new RecordScartato(MotivoScarto.ContenutoDiverso,
                            "Esiste già un movimento con la stessa chiave ma con data valuta o causale diverse: il movimento del file non è stato importato.",
                            m.RigaFile, null, m);
                    }
                    else
                    {
                        scarto = new RecordScartato(MotivoScarto.GiaPresente,
                            "Movimento già presente nel database (stessa chiave e stesso contenuto).",
                            m.RigaFile, null, m);
                    }

                    scartati.Add(scarto);
                    _log.Scarto(scarto);

                    if (scarto.Motivo == MotivoScarto.ContenutoDiverso)
                        SalvaInTabellaScarti(connessione, transazione, m, nomeFile, scarto.Motivo);
                }
            }
            catch (SqliteException ex)
            {
                var scarto = new RecordScartato(MotivoScarto.ErroreDatabase,
                    $"Errore del database: {Mascherato.Messaggio(ex.Message)}", m.RigaFile, null, m);
                scartati.Add(scarto);
                _log.Scarto(scarto);
                _log.EccezioneGestita($"inserimento del movimento di riga {m.RigaFile}", ex);
            }
        }

        transazione.Commit();
        return new RisultatoInserimento(inseriti, scartati, aggiornati);
    }

    // Stessa chiave, stessa data valuta e stessa causale: è lo stesso movimento, che può arrivare con
    // descrizioni diverse. Se ne tiene la più completa, così l'ordine delle importazioni non conta:
    // - la descrizione estesa più lunga (spazi a parte). Dal 05/11/2026 il lettore CBI unisce tutti i record 63
    //   e prima ne teneva uno solo; dal 08/11 l'XML ha anche le informazioni che il file a 120 caratteri non ha
    //   (ordinante, IBAN del beneficiario, causale del bonifico). Un file letto meglio o l'XML arricchiscono il
    //   movimento; reimportare dopo un file meno completo non lo impoverisce;
    // - la descrizione breve del file a 120 caratteri, che è un campo della banca (breveDellaBanca); quella
    //   ricavata dal testo dell'XML non sostituisce quella già salvata.
    // Vero se ha aggiornato qualcosa.
    private static bool AggiornaDescrizioni(SqliteConnection connessione, SqliteTransaction transazione, MovimentoCbi m,
                                            bool breveDellaBanca)
    {
        using var lettura = connessione.CreateCommand();
        lettura.Transaction = transazione;
        lettura.CommandText = """
            SELECT DescrizioneBreve, DescrizioneEstesa FROM Movimenti
            WHERE Conto = $conto AND DataContabile = $dataContabile
              AND Riferimento = $riferimento AND ImportoCentesimi = $importo
              AND DataValuta = $dataValuta AND Causale = $causale
            """;
        lettura.Parameters.AddWithValue("$conto", m.Conto);
        lettura.Parameters.AddWithValue("$dataContabile", ScriviData(m.DataContabile));
        lettura.Parameters.AddWithValue("$riferimento", m.Riferimento);
        lettura.Parameters.AddWithValue("$importo", Centesimi(m.Importo));
        lettura.Parameters.AddWithValue("$dataValuta", ScriviData(m.DataValuta));
        lettura.Parameters.AddWithValue("$causale", m.Causale);

        string breveSalvata, estesaSalvata;
        using (var lettore = lettura.ExecuteReader())
        {
            if (!lettore.Read()) return false;
            breveSalvata = lettore.GetString(0);
            estesaSalvata = lettore.GetString(1);
        }

        static int Lunghezza(string testo) => string.Join(' ', testo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Length;

        var breve = breveDellaBanca && m.DescrizioneBreve != "" ? m.DescrizioneBreve : breveSalvata;
        var estesa = Lunghezza(m.DescrizioneEstesa) > Lunghezza(estesaSalvata) ? m.DescrizioneEstesa : estesaSalvata;
        if (breve == breveSalvata && estesa == estesaSalvata) return false;

        using var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = """
            UPDATE Movimenti SET DescrizioneBreve = $descBreve, DescrizioneEstesa = $descEstesa
            WHERE Conto = $conto AND DataContabile = $dataContabile
              AND Riferimento = $riferimento AND ImportoCentesimi = $importo
            """;
        comando.Parameters.AddWithValue("$conto", m.Conto);
        comando.Parameters.AddWithValue("$dataContabile", ScriviData(m.DataContabile));
        comando.Parameters.AddWithValue("$riferimento", m.Riferimento);
        comando.Parameters.AddWithValue("$importo", Centesimi(m.Importo));
        comando.Parameters.AddWithValue("$descBreve", breve);
        comando.Parameters.AddWithValue("$descEstesa", estesa);
        return comando.ExecuteNonQuery() == 1;
    }

    /// <summary>Le descrizioni brevi già salvate per una causale: servono a dividere il testo dei movimenti
    /// letti dall'XML come nel file CBI a 120 caratteri (CbiXmlParser).</summary>
    public IReadOnlyCollection<string> DescrizioniBreviDellaCausale(string causale)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT DISTINCT DescrizioneBreve FROM Movimenti WHERE Causale = $causale AND DescrizioneBreve <> ''";
        comando.Parameters.AddWithValue("$causale", causale);

        var brevi = new List<string>();
        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            brevi.Add(lettore.GetString(0));
        return brevi;
    }

    // Le colonne per cui si può ordinare una griglia (OrdineGriglia.Campo -> SQL): un elenco fisso, così
    // nella query non entra mai testo arrivato dall'interfaccia. Il conto si ordina per nome, non per codice.
    private static readonly Dictionary<string, string> OrdiniMovimenti = new()
    {
        ["Conto"] = "(SELECT Nome FROM Conti c WHERE c.Conto = Movimenti.Conto) COLLATE NOCASE",
        ["DataContabile"] = "DataContabile",
        ["DataValuta"] = "DataValuta",
        ["Importo"] = "ImportoCentesimi",
        ["Causale"] = "Causale",
        ["DescrizioneBreve"] = "DescrizioneBreve COLLATE NOCASE",
        ["DescrizioneEstesa"] = "DescrizioneEstesa COLLATE NOCASE"
    };

    private static readonly Dictionary<string, string> OrdiniScarti = new()
    {
        ["ScartatoIl"] = "s.ScartatoIl",
        ["Conto"] = "(SELECT Nome FROM Conti c WHERE c.Conto = s.Conto) COLLATE NOCASE",
        ["NomeFile"] = "s.NomeFile COLLATE NOCASE",
        ["RigaFile"] = "s.RigaFile",
        ["DataContabile"] = "s.DataContabile",
        ["Importo"] = "s.ImportoCentesimi",
        ["Riferimento"] = "s.Riferimento",
        ["DescrizioneBreve"] = "s.DescrizioneBreve COLLATE NOCASE"
    };

    // l'ordine scelto, poi quello predefinito per le righe uguali; un campo sconosciuto lascia il predefinito
    private string Ordine(Dictionary<string, string> colonne, OrdineGriglia? ordine, string predefinito)
    {
        if (ordine is null) return predefinito;
        if (!colonne.TryGetValue(ordine.Campo, out var colonna))
        {
            _log.Debug($"Ordinamento per una colonna non prevista: {ordine.Campo}");
            return predefinito;
        }
        return $"{colonna} {(ordine.Decrescente ? "DESC" : "ASC")}, {predefinito}";
    }

    /// <summary>Restituisce una pagina di movimenti (pagina da 0) e il totale che rispetta i filtri,
    /// nell'ordine scelto nella griglia (null = dal più recente).</summary>
    public (List<MovimentoCbi> Righe, int Totale) CercaMovimenti(FiltriMovimenti filtri, int pagina, int righePerPagina,
                                                                 OrdineGriglia? ordine = null)
    {
        var cronometro = Stopwatch.StartNew();
        using var connessione = Apri();

        int totale;
        using (var conteggio = connessione.CreateCommand())
        {
            var filtro = CostruisciFiltro(conteggio, filtri);
            conteggio.CommandText = $"SELECT COUNT(*) FROM Movimenti {filtro}";
            totale = Convert.ToInt32(conteggio.ExecuteScalar());
        }

        var righe = new List<MovimentoCbi>();
        using (var lettura = connessione.CreateCommand())
        {
            // "filtro" contiene solo frammenti SQL fissi: i valori passano sempre come parametri
            var filtro = CostruisciFiltro(lettura, filtri);
            lettura.CommandText = $"""
                SELECT Conto, DataContabile, DataValuta, ImportoCentesimi, Causale, Riferimento,
                       DescrizioneBreve, DescrizioneEstesa, Straordinario, TipoForzato
                FROM Movimenti
                {filtro}
                ORDER BY {Ordine(OrdiniMovimenti, ordine, "DataContabile DESC, Riferimento DESC")}
                LIMIT $limite OFFSET $offset
                """;
            lettura.Parameters.AddWithValue("$limite", righePerPagina);
            lettura.Parameters.AddWithValue("$offset", pagina * righePerPagina);

            if (_log.DebugAttivo)
                _log.Debug($"SQL lettura: {Compatta(lettura.CommandText)} | parametri: {ElencaParametri(lettura)}");

            using var lettore = lettura.ExecuteReader();
            while (lettore.Read())
            {
                righe.Add(new MovimentoCbi(
                    Conto: lettore.GetString(0),
                    DataContabile: LeggiData(lettore.GetString(1)),
                    DataValuta: LeggiData(lettore.GetString(2)),
                    Importo: lettore.GetInt64(3) / 100m,
                    Causale: lettore.GetString(4),
                    Riferimento: lettore.GetString(5),
                    DescrizioneBreve: lettore.GetString(6),
                    DescrizioneEstesa: lettore.GetString(7),
                    Straordinario: lettore.GetInt64(8) == 1,
                    TipoForzato: lettore.IsDBNull(9) ? null
                        : Enum.TryParse<TipoGruppo>(lettore.GetString(9), out var forzato) ? forzato : null));
            }
        }

        _log.Debug($"Griglia: pagina {pagina + 1}, {righePerPagina} righe per pagina, " +
           $"{righe.Count} righe lette su {totale}, filtri: {filtri}, {cronometro.ElapsedMilliseconds} ms");

        return (righe, totale);
    }

    /// <summary>Tutti i movimenti che rispettano i filtri, senza paginazione.</summary>
    public List<MovimentoCbi> TuttiMovimenti(FiltriMovimenti filtri) =>
        CercaMovimenti(filtri, 0, -1).Righe;      // in SQLite LIMIT -1 significa "nessun limite"

    /// <summary>Elenco delle causali presenti, con una descrizione di esempio (per il filtro),
    /// in ordine alfabetico di descrizione.</summary>
    public List<(string Causale, string Descrizione)> ElencoCausali()
    {
        var elenco = new List<(string Causale, string Descrizione)>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText =
            "SELECT Causale, MIN(DescrizioneBreve) FROM Movimenti GROUP BY Causale";

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
        {
            // il campo CBI è troncato a 34 caratteri e a volte finisce con " -": nel menu si toglie,
            // altrimenti si sommerebbe al trattino che separa descrizione e codice
            var descrizione = lettore.GetString(1).Trim().TrimEnd('-', ' ');
            elenco.Add((lettore.GetString(0), descrizione == "" ? "(senza descrizione)" : descrizione));
        }

        // l'ordinamento si fa qui e non in SQL: il NOCASE di SQLite ignora le lettere accentate
        var italiano = StringComparer.Create(CultureInfo.GetCultureInfo("it-IT"), ignoreCase: true);
        return elenco.OrderBy(c => c.Descrizione, italiano).ThenBy(c => c.Causale).ToList();
    }

    /// <summary>Cambia la causale di movimenti importati da un file Excel (riferimento "XL...", dal 09/11): la
    /// causale l'ha dedotta il programma, e l'utente sceglie quella giusta, di solito per i movimenti con la
    /// causale fittizia. I movimenti dei file CBI e XML hanno la causale della banca e non si toccano. Le
    /// causali nuove ricevono il gruppo proposto dalla causale CBI. Restituisce quanti movimenti sono cambiati.
    /// Reimportare lo stesso file non riporta la causale dedotta: per questi movimenti una causale diversa non
    /// è un contenuto diverso (HaContenutoDiverso).</summary>
    ///
    /// La scelta si ricorda per la descrizione (tabella CausaliImparate, dal 10/11): i movimenti Excel con la
    /// stessa descrizione importati dopo prendono questa causale, e quelli già nel database con la causale
    /// fittizia la prendono subito. Restituisce quanti movimenti scelti sono cambiati e quanti altri, ancora
    /// con la causale fittizia, hanno preso la stessa causale per la descrizione.
    public (int Scelti, int Altri) ImpostaCausaleExcel(IEnumerable<MovimentoCbi> movimenti, string causale)
    {
        var elenco = movimenti.ToList();
        int scelti = 0, altri = 0;
        using var connessione = Apri();
        using var transazione = connessione.BeginTransaction();
        using (var comando = ComandoSuMovimento(connessione, transazione, "UPDATE Movimenti SET Causale = $causale"))
        {
            comando.CommandText += " AND Riferimento LIKE 'XL%'";
            comando.Parameters.AddWithValue("$causale", causale);
            foreach (var m in elenco)
                scelti += EseguiSuMovimento(comando, m.Chiave);
        }

        // la scelta, per descrizione
        var chiavi = elenco.Where(m => m.Riferimento.StartsWith("XL"))
                           .Select(m => ExcelParser.ChiaveDescrizione(m.DescrizioneEstesa))
                           .Where(c => c != "").Distinct().ToList();
        using (var ricorda = connessione.CreateCommand())
        {
            ricorda.Transaction = transazione;
            ricorda.CommandText = """
                INSERT INTO CausaliImparate (Chiave, Causale, ScelteIl) VALUES ($chiave, $causale, $quando)
                ON CONFLICT (Chiave) DO UPDATE SET Causale = excluded.Causale, ScelteIl = excluded.ScelteIl
                """;
            var pChiave = ricorda.Parameters.Add("$chiave", SqliteType.Text);
            ricorda.Parameters.AddWithValue("$causale", causale);
            ricorda.Parameters.AddWithValue("$quando", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            foreach (var chiave in chiavi)
            {
                pChiave.Value = chiave;
                ricorda.ExecuteNonQuery();
            }
        }

        // gli altri movimenti ancora con la causale fittizia e la stessa descrizione
        if (chiavi.Count > 0)
        {
            var imparate = new CausaliImparate(chiavi.ToDictionary(c => c, _ => causale));
            var daSistemare = new List<ChiaveMovimento>();
            using (var cerca = connessione.CreateCommand())
            {
                cerca.Transaction = transazione;
                cerca.CommandText = """
                    SELECT Conto, DataContabile, Riferimento, ImportoCentesimi, DescrizioneEstesa FROM Movimenti
                    WHERE Causale = $fittizia AND Riferimento LIKE 'XL%'
                    """;
                cerca.Parameters.AddWithValue("$fittizia", CausaliCbi.Fittizia);
                using var lettore = cerca.ExecuteReader();
                while (lettore.Read())
                    if (imparate.Cerca(lettore.GetString(4)) is not null)
                        daSistemare.Add(new ChiaveMovimento(lettore.GetString(0), LeggiData(lettore.GetString(1)),
                                                            lettore.GetString(2), lettore.GetInt64(3) / 100m));
            }
            using var sistema = ComandoSuMovimento(connessione, transazione, "UPDATE Movimenti SET Causale = $causale");
            sistema.CommandText += " AND Causale = '" + CausaliCbi.Fittizia + "'";
            sistema.Parameters.AddWithValue("$causale", causale);
            foreach (var m in daSistemare)
                altri += EseguiSuMovimento(sistema, m);
        }

        transazione.Commit();
        _log.Info($"Causale {causale} scelta per {scelti} movimenti importati da file Excel, ricordata per {chiavi.Count} descrizioni; " +
                  $"{altri} altri movimenti con la causale fittizia e la stessa descrizione l'hanno presa");
        return (scelti, altri);
    }

    /// <summary>Le causali scelte per descrizione, per l'importazione dei file Excel.</summary>
    public Dictionary<string, string> CausaliImparate()
    {
        var elenco = new Dictionary<string, string>();
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT Chiave, Causale FROM CausaliImparate";
        using var lettore = comando.ExecuteReader();
        while (lettore.Read()) elenco[lettore.GetString(0)] = lettore.GetString(1);
        return elenco;
    }

    // Un movimento Excel già presente con la causale fittizia prende quella del file reimportato, se ora il
    // programma la riconosce (per esempio perché nel frattempo l'utente l'ha scelta per la stessa descrizione).
    // La causale scelta dall'utente per quel movimento non si tocca: si cambia solo la fittizia.
    private void CorreggiCausaleFittizia(SqliteConnection connessione, SqliteTransaction transazione, MovimentoCbi m)
    {
        if (!m.Riferimento.StartsWith("XL") || m.Causale == CausaliCbi.Fittizia) return;
        using var comando = ComandoSuMovimento(connessione, transazione, "UPDATE Movimenti SET Causale = $causale");
        comando.CommandText += " AND Causale = '" + CausaliCbi.Fittizia + "'";
        comando.Parameters.AddWithValue("$causale", m.Causale);
        if (EseguiSuMovimento(comando, m.Chiave) == 1)
            _log.Debug($"Riga {m.RigaFile}: la causale fittizia del movimento già presente diventa {m.Causale}");
    }

    /// <summary>Quanti movimenti di un conto vengono da file CBI o XML e non da un file Excel (il cui
    /// riferimento comincia con "XL", ExcelParser): servono ad avvisare che i due tipi di file per lo stesso
    /// conto duplicano i movimenti.</summary>
    public int ContaMovimentiNonExcel(string conto)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM Movimenti WHERE Conto = $conto AND Riferimento NOT LIKE 'XL%'";
        comando.Parameters.AddWithValue("$conto", conto);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    /// <summary>Quanti movimenti di un conto vengono da un file Excel.</summary>
    public int ContaMovimentiExcel(string conto)
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM Movimenti WHERE Conto = $conto AND Riferimento LIKE 'XL%'";
        comando.Parameters.AddWithValue("$conto", conto);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    /// <summary>Numero di righe nella tabella degli scarti.</summary>
    public int ContaScarti()
    {
        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM MovimentiScartati";
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    /// <summary>Nomi dei file da cui provengono gli scarti (per il filtro).</summary>
    public List<string> ElencoFileScarti()
    {
        var elenco = new List<string>();

        using var connessione = Apri();
        using var comando = connessione.CreateCommand();
        comando.CommandText = "SELECT DISTINCT NomeFile FROM MovimentiScartati ORDER BY NomeFile";

        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
            elenco.Add(lettore.GetString(0));

        return elenco;
    }

    /// <summary>Restituisce una pagina di scarti (pagina da 0) e il totale che rispetta i filtri.
    /// Ogni scarto è affiancato dal movimento con la stessa chiave già presente in Movimenti.</summary>
    public (List<MovimentoScartato> Righe, int Totale) CercaScarti(FiltriScarti filtri, int pagina, int righePerPagina,
                                                                   OrdineGriglia? ordine = null)
    {
        var cronometro = Stopwatch.StartNew();
        using var connessione = Apri();

        int totale;
        using (var conteggio = connessione.CreateCommand())
        {
            var filtro = CostruisciFiltroScarti(conteggio, filtri);
            conteggio.CommandText = $"SELECT COUNT(*) FROM MovimentiScartati s {filtro}";
            totale = Convert.ToInt32(conteggio.ExecuteScalar());
        }

        var righe = new List<MovimentoScartato>();
        using (var lettura = connessione.CreateCommand())
        {
            // il LEFT JOIN usa la chiave primaria di Movimenti: al massimo una riga per scarto
            var filtro = CostruisciFiltroScarti(lettura, filtri);
            lettura.CommandText = $"""
                SELECT s.Id, s.Motivo, s.NomeFile, s.RigaFile, s.ScartatoIl,
                       s.Conto, s.DataContabile, s.DataValuta, s.ImportoCentesimi, s.Causale, s.Riferimento,
                       s.DescrizioneBreve, s.DescrizioneEstesa,
                       m.DataValuta, m.Causale, m.DescrizioneBreve, m.DescrizioneEstesa
                FROM MovimentiScartati s
                LEFT JOIN Movimenti m
                       ON m.Conto = s.Conto AND m.DataContabile = s.DataContabile
                      AND m.Riferimento = s.Riferimento AND m.ImportoCentesimi = s.ImportoCentesimi
                {filtro}
                ORDER BY {Ordine(OrdiniScarti, ordine, "s.ScartatoIl DESC, s.Id DESC")}
                LIMIT $limite OFFSET $offset
                """;
            lettura.Parameters.AddWithValue("$limite", righePerPagina);
            lettura.Parameters.AddWithValue("$offset", pagina * righePerPagina);

            if (_log.DebugAttivo)
                _log.Debug($"SQL lettura scarti: {Compatta(lettura.CommandText)} | parametri: {ElencaParametri(lettura)}");

            using var lettore = lettura.ExecuteReader();
            while (lettore.Read())
            {
                var scartato = new MovimentoCbi(
                    Conto: lettore.GetString(5),
                    DataContabile: LeggiData(lettore.GetString(6)),
                    DataValuta: LeggiData(lettore.GetString(7)),
                    Importo: lettore.GetInt64(8) / 100m,
                    Causale: lettore.GetString(9),
                    Riferimento: lettore.GetString(10),
                    DescrizioneBreve: lettore.GetString(11),
                    DescrizioneEstesa: lettore.GetString(12),
                    RigaFile: lettore.GetInt32(3));

                // i campi della chiave coincidono per definizione: dal database servono solo gli altri
                MovimentoCbi? nelDatabase = lettore.IsDBNull(13) ? null : scartato with
                {
                    DataValuta = LeggiData(lettore.GetString(13)),
                    Causale = lettore.GetString(14),
                    DescrizioneBreve = lettore.GetString(15),
                    DescrizioneEstesa = lettore.GetString(16),
                    RigaFile = 0
                };

                righe.Add(new MovimentoScartato(
                    Id: lettore.GetInt64(0),
                    Motivo: lettore.GetString(1),
                    NomeFile: lettore.GetString(2),
                    RigaFile: lettore.GetInt32(3),
                    ScartatoIl: DateTime.ParseExact(lettore.GetString(4), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Scartato: scartato,
                    NelDatabase: nelDatabase));
            }
        }

        _log.Debug($"Griglia scarti: pagina {pagina + 1}, {righePerPagina} righe per pagina, " +
           $"{righe.Count} righe lette su {totale}, filtri: {filtri}, {cronometro.ElapsedMilliseconds} ms");

        return (righe, totale);
    }

    /// <summary>Tutti gli scarti che rispettano i filtri, senza paginazione.</summary>
    public List<MovimentoScartato> TuttiScarti(FiltriScarti filtri) =>
        CercaScarti(filtri, 0, -1).Righe;         // in SQLite LIMIT -1 significa "nessun limite"

    // Come CostruisciFiltro, per la tabella degli scarti (alias "s").
    private static string CostruisciFiltroScarti(SqliteCommand comando, FiltriScarti f)
    {
        var condizioni = new List<string>();

        if (f.Conti.Any())
            condizioni.Add(CondizioneIn(comando, "s.Conto", "$conto", f.Conti));

        if (f.DataDa is DateTime da)
        {
            condizioni.Add("s.DataContabile >= $dataDa");
            comando.Parameters.AddWithValue("$dataDa", da.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (f.DataA is DateTime a)
        {
            condizioni.Add("s.DataContabile <= $dataA");
            comando.Parameters.AddWithValue("$dataA", a.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(f.NomeFile))
        {
            condizioni.Add("s.NomeFile = $nomeFile");
            comando.Parameters.AddWithValue("$nomeFile", f.NomeFile);
        }

        // gli importi dei filtri si confrontano in valore assoluto
        if (f.ImportoDa is decimal minimo)
        {
            condizioni.Add("ABS(s.ImportoCentesimi) >= $importoDa");
            comando.Parameters.AddWithValue("$importoDa", Centesimi(minimo));
        }

        if (f.ImportoA is decimal massimo)
        {
            condizioni.Add("ABS(s.ImportoCentesimi) <= $importoA");
            comando.Parameters.AddWithValue("$importoA", Centesimi(massimo));
        }

        if (!string.IsNullOrWhiteSpace(f.Testo))
        {
            var modello = "%" + f.Testo.Trim()
                .Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            condizioni.Add(@"(s.DescrizioneBreve LIKE $testo ESCAPE '\' OR s.DescrizioneEstesa LIKE $testo ESCAPE '\' OR s.Riferimento LIKE $testo ESCAPE '\')");
            comando.Parameters.AddWithValue("$testo", modello);
        }

        return condizioni.Count == 0 ? "" : "WHERE " + string.Join(" AND ", condizioni);
    }

    // Aggiunge ai parametri del comando i valori dei filtri e restituisce la clausola WHERE.
    private static string CostruisciFiltro(SqliteCommand comando, FiltriMovimenti f)
    {
        var condizioni = new List<string>();

        if (f.Conti.Any())
            condizioni.Add(CondizioneIn(comando, "Conto", "$conto", f.Conti));

        if (f.DataDa is DateTime da)
        {
            condizioni.Add("DataContabile >= $dataDa");
            comando.Parameters.AddWithValue("$dataDa", da.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (f.DataA is DateTime a)
        {
            condizioni.Add("DataContabile <= $dataA");
            comando.Parameters.AddWithValue("$dataA", a.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (f.Tipo == TipoFiltro.Entrate) condizioni.Add("ImportoCentesimi > 0");
        else if (f.Tipo == TipoFiltro.Uscite) condizioni.Add("ImportoCentesimi < 0");

        if (f.Etichette == FiltroEtichette.Straordinari) condizioni.Add("Straordinario = 1");
        else if (f.Etichette == FiltroEtichette.NonStraordinari) condizioni.Add("Straordinario = 0");
        else if (f.Etichette == FiltroEtichette.TipoScelto) condizioni.Add("TipoForzato IS NOT NULL");

        // gli importi dei filtri si confrontano in valore assoluto
        if (f.ImportoDa is decimal minimo)
        {
            condizioni.Add("ABS(ImportoCentesimi) >= $importoDa");
            comando.Parameters.AddWithValue("$importoDa", Centesimi(minimo));
        }

        if (f.ImportoA is decimal massimo)
        {
            condizioni.Add("ABS(ImportoCentesimi) <= $importoA");
            comando.Parameters.AddWithValue("$importoA", Centesimi(massimo));
        }

        if (!string.IsNullOrWhiteSpace(f.Testo))
        {
            var modello = "%" + f.Testo.Trim()
                .Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            condizioni.Add(@"(DescrizioneBreve LIKE $testo ESCAPE '\' OR DescrizioneEstesa LIKE $testo ESCAPE '\')");
            comando.Parameters.AddWithValue("$testo", modello);
        }

        if (f.Causali.Any())
            condizioni.Add(CondizioneIn(comando, "Causale", "$causale", f.Causali));

        return condizioni.Count == 0 ? "" : "WHERE " + string.Join(" AND ", condizioni);
    }

    // "colonna IN ($p0, $p1, ...)" con un parametro per ogni valore: i valori non entrano mai nel testo SQL.
    private static string CondizioneIn(SqliteCommand comando, string colonna, string prefisso, IEnumerable<string> valori)
    {
        var nomi = new List<string>();
        foreach (var valore in valori.Distinct())
        {
            var nome = $"{prefisso}{nomi.Count}";
            comando.Parameters.AddWithValue(nome, valore);
            nomi.Add(nome);
        }
        return $"{colonna} IN ({string.Join(", ", nomi)})";
    }

    // Vero se esiste già una riga con la stessa chiave ma con data valuta o causale diverse. Le descrizioni
    // non contano più (dal 08/11/2026): lo stesso movimento ha descrizioni diverse nel file CBI a 120 caratteri
    // e nell'XML, e se ne tiene la più completa (AggiornaDescrizioni).
    private static bool HaContenutoDiverso(SqliteConnection connessione, SqliteTransaction transazione, MovimentoCbi m)
    {
        using var comando = connessione.CreateCommand();
        comando.Transaction = transazione;
        comando.CommandText = """
            SELECT 1 FROM Movimenti
            WHERE Conto = $conto AND DataContabile = $dataContabile
              AND Riferimento = $riferimento AND ImportoCentesimi = $importo
              AND (DataValuta <> $dataValuta
                   OR (Causale <> $causale AND Riferimento NOT LIKE 'XL%'))
            LIMIT 1
            """;
        comando.Parameters.AddWithValue("$conto", m.Conto);
        comando.Parameters.AddWithValue("$dataContabile", ScriviData(m.DataContabile));
        comando.Parameters.AddWithValue("$riferimento", m.Riferimento);
        comando.Parameters.AddWithValue("$importo", Centesimi(m.Importo));
        comando.Parameters.AddWithValue("$dataValuta", ScriviData(m.DataValuta));
        comando.Parameters.AddWithValue("$causale", m.Causale);

        return comando.ExecuteScalar() is not null;
    }

    // Registra nella tabella degli scarti un movimento con chiave già presente ma contenuto diverso.
    // Un errore qui non deve far perdere la segnalazione dello scarto: viene solo scritto nel log.
    private void SalvaInTabellaScarti(SqliteConnection connessione, SqliteTransaction transazione,
                                      MovimentoCbi m, string nomeFile, MotivoScarto motivo)
    {
        try
        {
            using var comando = connessione.CreateCommand();
            comando.Transaction = transazione;
            comando.CommandText = """
                INSERT INTO MovimentiScartati
                    (Motivo, NomeFile, RigaFile, ScartatoIl, Conto, DataContabile, DataValuta,
                     ImportoCentesimi, Causale, Riferimento, DescrizioneBreve, DescrizioneEstesa)
                VALUES
                    ($motivo, $nomeFile, $rigaFile, $scartatoIl, $conto, $dataContabile, $dataValuta,
                     $importo, $causale, $riferimento, $descBreve, $descEstesa)
                ON CONFLICT DO NOTHING
                """;
            comando.Parameters.AddWithValue("$motivo", motivo.ToString());
            comando.Parameters.AddWithValue("$nomeFile", nomeFile);
            comando.Parameters.AddWithValue("$rigaFile", m.RigaFile);
            comando.Parameters.AddWithValue("$scartatoIl",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            comando.Parameters.AddWithValue("$conto", m.Conto);
            comando.Parameters.AddWithValue("$dataContabile", ScriviData(m.DataContabile));
            comando.Parameters.AddWithValue("$dataValuta", ScriviData(m.DataValuta));
            comando.Parameters.AddWithValue("$importo", Centesimi(m.Importo));
            comando.Parameters.AddWithValue("$causale", m.Causale);
            comando.Parameters.AddWithValue("$riferimento", m.Riferimento);
            comando.Parameters.AddWithValue("$descBreve", m.DescrizioneBreve);
            comando.Parameters.AddWithValue("$descEstesa", m.DescrizioneEstesa);

            var salvati = comando.ExecuteNonQuery();
            _log.Info(salvati == 1
                ? $"Riga {m.RigaFile}: scarto registrato nella tabella MovimentiScartati (file {nomeFile})"
                : $"Riga {m.RigaFile}: scarto già presente nella tabella MovimentiScartati (file {nomeFile})");
        }
        catch (SqliteException ex)
        {
            _log.Errore($"Riga {m.RigaFile}: impossibile registrare lo scarto nella tabella MovimentiScartati", ex);
        }
    }

    private static long Centesimi(decimal importo) =>
        decimal.ToInt64(Math.Round(importo * 100m));

    private static string ScriviData(DateOnly data) =>
        data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly LeggiData(string testo) =>
        DateOnly.ParseExact(testo, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Compatta(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    // per il log: i valori dei parametri passano da Mascherato, come tutti i dati dei movimenti
    private static string ElencaParametri(SqliteCommand comando) =>
        string.Join(", ", comando.Parameters.Cast<SqliteParameter>()
            .Select(p => $"{p.ParameterName}={Mascherato.Parametro(p.ParameterName, p.Value)}"));

    private static string Sintesi(MovimentoCbi m) => Mascherato.Movimento(m);
}