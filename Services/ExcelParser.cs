using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>Le caselle del modello in cui l'utente trascina le colonne del file Excel.</summary>
public enum CampoExcel { DataContabile, DataValuta, Descrizione, Importo, Uscite, Entrate, Causale, Saldo }

/// <summary>Quali colonne del file vanno in quali caselle, e in che riga sono le intestazioni (da 0).
/// La descrizione può avere più colonne (per esempio Descrizione e Note): si uniscono.</summary>
public record AbbinamentoExcel(int RigaIntestazioni, IReadOnlyDictionary<CampoExcel, IReadOnlyList<int>> Colonne)
{
    public IReadOnlyList<int> Di(CampoExcel campo) => Colonne.TryGetValue(campo, out var c) ? c : [];

    /// <summary>Le caselle necessarie: data contabile, descrizione e un importo (con segno, o uscite/entrate).</summary>
    public bool Completo => Di(CampoExcel.DataContabile).Count > 0 && Di(CampoExcel.Descrizione).Count > 0 &&
                            (Di(CampoExcel.Importo).Count > 0 || Di(CampoExcel.Uscite).Count > 0 || Di(CampoExcel.Entrate).Count > 0);
}

/// <summary>Cosa ha scelto l'utente nella finestra di abbinamento: le colonne, il conto (già registrato o
/// nuovo) e, se il file non ha i saldi, il saldo del conto dopo l'ultimo movimento.</summary>
public record RichiestaExcel(TabellaExcel Tabella, AbbinamentoExcel Abbinamento, ContoCorrente Conto, bool ContoNuovo, decimal? SaldoFinale);

/// <summary>Una riga del file letta con l'abbinamento: per l'anteprima e per l'importazione.</summary>
public record RigaExcel(int Riga, DateOnly? DataContabile, DateOnly? DataValuta, decimal? Importo, string Descrizione,
                        string Causale, decimal? Saldo, string? Errore, bool CausaleImparata = false)
{
    /// <summary>Una riga senza data e senza importo (vuota, un titolo, un totale): non è un movimento.</summary>
    public bool Ignorata => Errore is null && DataContabile is null;
}

/// <summary>
/// Trasforma le righe di un file Excel della banca in movimenti, con l'abbinamento delle colonne scelto
/// dall'utente (dal 09/11/2026). Rispetto al file CBI mancano dei dati, e si ricavano così:
/// - la causale, se il file non ne ha una colonna con i codici CBI, si deduce dal testo con poche regole
///   (POS, bonifico, prelievo, stipendio...): le prime due cifre sono la causale CBI, le altre due "XL",
///   così il gruppo arriva dalla causale CBI (Gruppi di causali) e si distingue dalle causali delle banche; quello
///   che non si riconosce ha la causale "00XL", senza gruppo, e lo classifica l'utente;
/// - il riferimento, che con conto, data e importo fa la chiave del movimento, è "XL" più un'impronta di
///   data valuta, importo e descrizione: reimportare un periodo già importato non duplica i movimenti;
/// - i saldi si ricavano dalla colonna del saldo, se c'è, oppure dal saldo finale indicato dall'utente.
/// </summary>
public static class ExcelParser
{
    public const string SuffissoCausale = "XL";
    public const string CausaleSconosciuta = CausaliCbi.Fittizia;

    // ---- proposta dell'abbinamento ----

    /// <summary>La riga delle intestazioni proposta all'utente (che la conferma o la cambia): la prima con
    /// almeno tre celle di testo, seguita entro due righe piene da una riga che ha una data. Se non si trova,
    /// la prima riga.</summary>
    public static int TrovaIntestazioni(TabellaExcel tabella)
    {
        for (var r = 0; r < Math.Min(tabella.Righe.Count - 1, 80); r++)
        {
            var celle = tabella.Righe[r].Where(c => c != "").ToList();
            if (celle.Count < 3 || celle.Any(c => LeggiData(c) is not null || LeggiImporto(c) is not null)) continue;
            var sotto = tabella.Righe.Skip(r + 1).Where(riga => riga.Any(c => c != "")).Take(2);
            if (sotto.Any(riga => riga.Any(c => LeggiData(c) is not null))) return r;
        }
        return 0;
    }

    /// <summary>Un abbinamento proposto dai nomi delle colonne; l'utente lo corregge.</summary>
    public static AbbinamentoExcel Proponi(TabellaExcel tabella, int rigaIntestazioni)
    {
        var colonne = new Dictionary<CampoExcel, IReadOnlyList<int>>();
        var intestazioni = Enumerable.Range(0, tabella.Colonne)
            .Select(i => (Indice: i, Nome: tabella.Cella(rigaIntestazioni, i).ToLowerInvariant())).ToList();

        void Prova(CampoExcel campo, params string[] parole)
        {
            if (colonne.ContainsKey(campo)) return;
            foreach (var parola in parole)
            {
                var trovata = intestazioni.FirstOrDefault(c => c.Nome.Contains(parola) && !colonne.Values.Any(v => v.Contains(c.Indice)));
                if (trovata.Nome is null) continue;
                colonne[campo] = [trovata.Indice];
                return;
            }
        }

        Prova(CampoExcel.DataValuta, "valuta");
        Prova(CampoExcel.DataContabile, "data contabile", "contabile", "data operazione", "data registrazione", "data");
        Prova(CampoExcel.Saldo, "saldo");
        Prova(CampoExcel.Uscite, "uscite", "dare", "addebit");
        Prova(CampoExcel.Entrate, "entrate", "avere", "accredit");
        if (!colonne.ContainsKey(CampoExcel.Uscite) && !colonne.ContainsKey(CampoExcel.Entrate))
            Prova(CampoExcel.Importo, "importo", "ammontare", "euro", "eur");
        Prova(CampoExcel.Causale, "causale abi", "codice causale", "causale", "tipo operazione", "tipologia", "categoria");
        Prova(CampoExcel.Descrizione, "descrizione", "dettagli", "operazione", "motivazione");

        // se nessuna colonna si chiama "descrizione", il testo del movimento è quasi sempre nella colonna
        // "causale" (molte banche la chiamano così): va nella descrizione, da cui si deduce la causale
        if (!colonne.ContainsKey(CampoExcel.Descrizione) && colonne.Remove(CampoExcel.Causale, out var causale))
            colonne[CampoExcel.Descrizione] = causale;

        // le note completano la descrizione, come le righe 63 del file CBI
        var note = intestazioni.Where(c => c.Nome.Contains("note") && !colonne.Values.Any(v => v.Contains(c.Indice))).Select(c => c.Indice);
        if (colonne.TryGetValue(CampoExcel.Descrizione, out var descrizione))
            colonne[CampoExcel.Descrizione] = descrizione.Concat(note).ToList();

        return new AbbinamentoExcel(rigaIntestazioni, colonne);
    }

    /// <summary>Un'impronta delle intestazioni, per riconoscere lo stesso tipo di file la volta dopo.</summary>
    public static string Firma(TabellaExcel tabella, int rigaIntestazioni) =>
        string.Join("|", tabella.Righe[rigaIntestazioni].Select(c => c.ToLowerInvariant()));

    // ---- lettura delle righe ----

    public static IEnumerable<RigaExcel> Righe(TabellaExcel tabella, AbbinamentoExcel abbinamento, CausaliImparate? imparate = null)
    {
        for (var r = abbinamento.RigaIntestazioni + 1; r < tabella.Righe.Count; r++)
            yield return Riga(tabella, abbinamento, r, imparate);
    }

    public static RigaExcel Riga(TabellaExcel tabella, AbbinamentoExcel a, int r, CausaliImparate? imparate = null)
    {
        string Testo(CampoExcel campo) => string.Join(" ", a.Di(campo).Select(c => tabella.Cella(r, c)).Where(t => t != ""));

        var testoData = Testo(CampoExcel.DataContabile);
        var descrizione = Testo(CampoExcel.Descrizione);
        var testoImporto = Testo(CampoExcel.Importo);
        var testoUscite = Testo(CampoExcel.Uscite);
        var testoEntrate = Testo(CampoExcel.Entrate);
        var testoCausale = Testo(CampoExcel.Causale);
        var testoSaldo = Testo(CampoExcel.Saldo);

        var data = LeggiData(testoData);
        var valuta = LeggiData(Testo(CampoExcel.DataValuta)) ?? data;

        decimal? importo = null;
        string? errore = null;
        var haImporto = testoImporto != "" || testoUscite != "" || testoEntrate != "";
        if (a.Di(CampoExcel.Importo).Count > 0)
            importo = LeggiImporto(testoImporto);
        else if (haImporto)
        {
            var uscite = testoUscite == "" ? 0 : LeggiImporto(testoUscite);
            var entrate = testoEntrate == "" ? 0 : LeggiImporto(testoEntrate);
            if (uscite is not null && entrate is not null)
                importo = Math.Abs(entrate.Value) - Math.Abs(uscite.Value);
        }

        // una riga senza data, o con un testo al posto della data ("Saldo finale", "Totale"), è vuota, un
        // titolo o un totale: non è un movimento e non è un errore
        if (data is null && (!haImporto || testoData == "" || testoData.Any(char.IsLetter)))
            return new RigaExcel(r, null, null, null, descrizione, "", null, null);

        if (data is null)
            errore = testoData == "" ? "manca la data" : $"la data \"{testoData}\" non si riconosce";
        else if (!haImporto)
            errore = "manca l'importo";
        else if (importo is null)
            errore = $"l'importo \"{(testoImporto != "" ? testoImporto : testoUscite + " " + testoEntrate).Trim()}\" non si riconosce";

        // la causale: il codice CBI della colonna causale, se c'è; poi quella scelta dall'utente per un movimento
        // con la stessa descrizione (CausaliImparate); poi le regole sul testo
        var causale = "";
        var imparata = false;
        if (importo is { } i)
        {
            if (CodiceDellaColonna(testoCausale) is { } codice) causale = codice;
            else if (imparate?.Cerca(descrizione) is { } scelta) { causale = scelta; imparata = true; }
            else causale = Causale(testoCausale, descrizione, i);
        }
        return new RigaExcel(r, data, valuta, importo, descrizione, causale, LeggiImporto(testoSaldo), errore, imparata);
    }

    /// <summary>I movimenti del file per il conto scelto, con i saldi giornalieri che si possono ricavare.</summary>
    public static RisultatoImportCbi Leggi(RichiestaExcel richiesta, CausaliImparate? imparate = null)
    {
        var conto = richiesta.Conto.Codice;
        var righe = Righe(richiesta.Tabella, richiesta.Abbinamento, imparate).ToList();
        var scartati = new List<RecordScartato>();
        var avvisi = new List<string>();

        foreach (var r in righe.Where(r => r.Errore is not null))
            scartati.Add(new RecordScartato(MotivoScarto.RigaNonValida, $"Riga {r.Riga + 1}: {r.Errore}", r.Riga + 1,
                string.Join(" | ", richiesta.Tabella.Righe[r.Riga])));

        var valide = righe.Where(r => r.Errore is null && !r.Ignorata).ToList();

        // in ordine di tempo: molte banche mettono in cima il movimento più recente
        if (InOrdineDecrescente(valide)) valide.Reverse();

        // il riferimento: impronta del movimento più un contatore per i movimenti identici dello stesso giorno
        var visti = new Dictionary<string, int>();
        var movimenti = new List<MovimentoCbi>();
        foreach (var r in valide)
        {
            var estesa = r.Descrizione;
            var chiave = $"{conto}|{r.DataContabile:yyyyMMdd}|{r.DataValuta:yyyyMMdd}|{r.Importo:0.00}|{Normalizza(estesa)}";
            var n = visti[chiave] = visti.GetValueOrDefault(chiave) + 1;
            movimenti.Add(new MovimentoCbi(conto, r.DataContabile!.Value, r.DataValuta!.Value, r.Importo!.Value, r.Causale,
                Riferimento(chiave, n), Breve(estesa), estesa, r.Riga + 1));
        }

        var saldi = richiesta.Abbinamento.Di(CampoExcel.Saldo).Count > 0
            ? SaldiDallaColonna(conto, valide, avvisi)
            : SaldiDalFinale(conto, valide, richiesta.SaldoFinale);

        if (saldi.Count == 0 && movimenti.Count > 0)
            avvisi.Add("Il file non ha la colonna del saldo e non hai indicato il saldo del conto: per questo periodo " +
                       "il programma non conosce il saldo, e l'andamento del saldo di questo conto resta vuoto.");

        var riprese = valide.Count(r => r.CausaleImparata);
        if (riprese > 0)
            avvisi.Add($"{riprese} movimenti hanno ripreso la causale che avevi scelto per movimenti con la stessa descrizione.");

        var sconosciute = movimenti.Count(m => m.Causale == CausaleSconosciuta);
        if (sconosciute > 0)
            avvisi.Add($"{sconosciute} movimenti non hanno un tipo riconoscibile dalla descrizione: hanno la causale " +
                       $"fittizia {CausaleSconosciuta}, senza gruppo. In Movimenti li trovi con il filtro Etichette, \"Causale fittizia\": " +
                       "dal menu di ogni movimento (o selezionandone più di uno) puoi scegliere la causale giusta, e il gruppo arriva con lei.");

        return new RisultatoImportCbi(richiesta.ContoNuovo ? [richiesta.Conto] : [], movimenti, saldi, scartati, avvisi);
    }

    private static bool InOrdineDecrescente(List<RigaExcel> righe) =>
        righe.Count > 1 && righe[0].DataContabile > righe[^1].DataContabile;

    private static string Riferimento(string chiave, int n)
    {
        var impronta = SHA256.HashData(Encoding.UTF8.GetBytes($"{chiave}|{n}"));
        return SuffissoCausale + Convert.ToHexString(impronta)[..13];
    }

    private static string Normalizza(string testo) => Regex.Replace(testo, @"\s+", " ").Trim().ToUpperInvariant();

    // come il campo di 34 caratteri del file CBI, senza tagliare una parola a metà se si può
    private static string Breve(string testo)
    {
        testo = Regex.Replace(testo, @"\s+", " ").Trim();
        if (testo.Length <= 34) return testo;
        var spazio = testo.LastIndexOf(' ', 34);
        return (spazio > 15 ? testo[..spazio] : testo[..34]).TrimEnd();
    }

    // ---- saldi ----

    // Il saldo di una riga è quello dopo il movimento: il saldo finale della giornata è quello dell'ultimo
    // movimento del giorno, l'iniziale si ottiene togliendo i movimenti del giorno.
    private static List<SaldoGiornaliero> SaldiDallaColonna(string conto, List<RigaExcel> righe, List<string> avvisi)
    {
        var conSaldo = righe.Where(r => r.Saldo is not null).ToList();
        if (conSaldo.Count == 0) return [];

        // dentro una giornata l'ordine del file può essere al contrario di quello tra i giorni: si sceglie quello
        // in cui ogni saldo torna con il precedente più l'importo
        var inAvanti = ConcordanzeSaldo(conSaldo);
        var conSaldoRovesciato = conSaldo.GroupBy(r => r.DataContabile).SelectMany(g => g.Reverse()).ToList();
        if (ConcordanzeSaldo(conSaldoRovesciato) > inAvanti) conSaldo = conSaldoRovesciato;

        var saldi = new List<SaldoGiornaliero>();
        foreach (var giorno in conSaldo.GroupBy(r => r.DataContabile!.Value))
        {
            var finale = giorno.Last().Saldo!.Value;
            var movimentiDelGiorno = righe.Where(r => r.DataContabile == giorno.Key).Sum(r => r.Importo!.Value);
            saldi.Add(new SaldoGiornaliero(conto, giorno.Key, finale - movimentiDelGiorno, finale));
        }

        for (var i = 1; i < saldi.Count; i++)
            if (saldi[i].SaldoIniziale != saldi[i - 1].SaldoFinale)
                avvisi.Add($"Conto {conto}, {saldi[i].Data:dd/MM/yyyy}: il saldo iniziale non coincide con il saldo finale " +
                           $"della giornata precedente ({saldi[i - 1].Data:dd/MM/yyyy}): potrebbero mancare dei movimenti");
        return saldi;
    }

    private static int ConcordanzeSaldo(List<RigaExcel> righe)
    {
        var n = 0;
        for (var i = 1; i < righe.Count; i++)
            if (righe[i - 1].Saldo + righe[i].Importo == righe[i].Saldo) n++;
        return n;
    }

    // dal saldo dopo l'ultimo movimento, all'indietro giorno per giorno
    private static List<SaldoGiornaliero> SaldiDalFinale(string conto, List<RigaExcel> righe, decimal? saldoFinale)
    {
        if (saldoFinale is not { } saldo || righe.Count == 0) return [];

        var saldi = new List<SaldoGiornaliero>();
        foreach (var giorno in righe.GroupBy(r => r.DataContabile!.Value).OrderByDescending(g => g.Key))
        {
            var iniziale = saldo - giorno.Sum(r => r.Importo!.Value);
            saldi.Add(new SaldoGiornaliero(conto, giorno.Key, iniziale, saldo));
            saldo = iniziale;
        }
        saldi.Reverse();
        return saldi;
    }

    // ---- causale ----

    // Le regole, nell'ordine: la prima che corrisponde decide. Il segno distingue entrate e uscite dello
    // stesso tipo (bonifico ricevuto o fatto).
    private static readonly (Regex Testo, string? Entrata, string? Uscita)[] Regole =
    [
        (R(@"STIPENDI|EMOLUMENT|PENSIONE|RETRIBUZ|CEDOLINO"), "27", null),
        (R(@"GIRO ?CONTO|GIROFONDI|TRASFERIMENTO TRA (I )?(MIEI )?CONTI"), "34", "34"),
        (R(@"\bPOS\b|PAGO ?BANCOMAT|PAGAMENT[IO] (CON |TRAMITE )?BANCOMAT|PAGAMENTO (CON |TRAMITE )?CARTA|PAG\.? ?CARTA|CARTA DI DEBITO|CONTACTLESS|APPLE ?PAY|GOOGLE ?PAY"), "43", "43"),
        (R(@"PRELIEV|PRELEVAMENTO|\bATM\b"), null, "05"),
        // "BANCOMAT" senza "prelievo" è un pagamento con la carta (la categoria di BPER per gli acquisti)
        (R(@"BANCOMAT"), "43", "43"),
        (R(@"CARTA DI CREDITO|ESTRATTO CONTO CARTA|SALDO CARTA"), null, "45"),
        (R(@"BONIFICO.*ESTERO|ESTERO.*BONIFICO"), "ZI", "ZL"),
        (R(@"BONIFIC|\bBON\b|\bSCT\b|ISTANTANEO|INSTANT"), "48", "26"),
        // le commissioni di un addebito (per esempio di un'utenza) sono spese bancarie, non l'utenza
        (R(@"COMMISSION"), null, "16"),
        // solo parole generiche, niente nomi di aziende: un elenco di fornitori non sarebbe mai completo
        (R(@"\bLUCE\b|\bGAS\b|ACQUA|ENERGIA|ELETTRIC|TELEFON|UTENZ"), null, "11"),
        (R(@"MUTUO|RATA|FINANZIAMENT|PRESTITO"), "ZS", "15"),
        (R(@"\bF24\b|IMPOST|\bBOLLO\b|TASSA|TRIBUT|AGENZIA DELLE ENTRATE"), "99", "19"),
        (R(@"ASSICURAZ|POLIZZA|AFFITTO|CANONE DI LOCAZIONE"), null, "17"),
        (R(@"INTERESS|CEDOL|DIVIDEND"), "18", "18"),
        (R(@"COMMISSION|CANONE|SPESE|COMPETENZE|COSTO"), null, "66"),
        (R(@"VERSAMENTO"), "78", null),
        (R(@"ASSEGN"), "Z2", "13"),
        (R(@"\bSDD\b|ADDEBITO DIRETTO|ADDEBITO SEPA|\bRID\b|DOMICILIAZ"), null, "50")
    ];

    private static Regex R(string modello) => new(modello, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>La causale di un movimento: il codice della colonna causale se è un codice CBI, altrimenti
    /// quella dedotta dal testo della colonna causale e della descrizione.</summary>
    public static string Causale(string testoCausale, string descrizione, decimal importo)
    {
        if (CodiceDellaColonna(testoCausale) is { } codice) return codice;

        var testo = $"{testoCausale} {descrizione}";
        foreach (var (regola, entrata, uscita) in Regole)
        {
            if (!regola.IsMatch(testo)) continue;
            var cbi = importo >= 0 ? entrata : uscita;
            if (cbi is not null) return cbi + SuffissoCausale;
        }
        return CausaleSconosciuta;
    }

    // la colonna causale contiene un codice CBI (due o quattro caratteri della tabella)
    private static string? CodiceDellaColonna(string testoCausale)
    {
        var codice = testoCausale.Trim().ToUpperInvariant();
        if (Regex.IsMatch(codice, @"^[0-9A-Z]{4}$") && CausaliCbi.Descrizione(codice) is not null) return codice;
        if (Regex.IsMatch(codice, @"^[0-9A-Z]{2}$") && CausaliCbi.Descrizione(codice) is not null) return codice + SuffissoCausale;
        return null;
    }

    /// <summary>La descrizione di un movimento ridotta a parole: maiuscole, senza cifre e segni (date, numeri
    /// di carta, importi e codici cambiano da un movimento all'altro). Vuota se restano meno di 3 lettere.</summary>
    public static string ChiaveDescrizione(string descrizione)
    {
        var parole = Regex.Replace(descrizione.ToUpperInvariant(), @"[^\p{L}]+", " ").Trim();
        return parole.Count(char.IsLetter) < 3 ? "" : parole;
    }

    // ---- date e importi ----

    private static readonly string[] FormatiData =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "dd-MM-yyyy", "d-M-yyyy", "dd-MM-yy",
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd"
    ];

    // le date con il mese in lettere, come le scrive BPER ("07 novembre 2026"), anche abbreviato ("7 nov 2026")
    private static readonly string[] FormatiDataInLettere =
    [
        "d MMMM yyyy", "dd MMMM yyyy", "d MMM yyyy", "dd MMM yyyy", "d-MMM-yyyy", "dd-MMM-yyyy", "dd-MMM-yy", "d MMMM yy"
    ];

    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public static DateOnly? LeggiData(string testo)
    {
        testo = Regex.Replace(testo.Trim().TrimEnd('.'), @"\s+", " ");
        if (testo.Length < 6) return null;

        DateOnly data;
        var parte = testo.Split(' ', 'T')[0];
        if (DateOnly.TryParseExact(parte, FormatiData, CultureInfo.InvariantCulture, DateTimeStyles.None, out data) ||
            DateOnly.TryParseExact(testo.ToLowerInvariant(), FormatiDataInLettere, Italiano, DateTimeStyles.None, out data))
            return data.Year is >= 1990 and <= 2100 ? data : null;
        return null;
    }

    /// <summary>Un importo scritto all'italiana o all'inglese: "1.234,56", "-1234.56", "€ 45,20", "45,20-".</summary>
    public static decimal? LeggiImporto(string testo)
    {
        var t = Regex.Replace(testo, @"EUR|€|\s|'", "", RegexOptions.IgnoreCase);
        if (t == "") return null;

        var negativo = false;
        if (t.StartsWith('(') && t.EndsWith(')')) { negativo = true; t = t[1..^1]; }
        if (t.EndsWith('-')) { negativo = true; t = t[..^1]; }
        else if (t.EndsWith('+')) t = t[..^1];
        if (t.StartsWith('-')) { negativo = true; t = t[1..]; }
        else if (t.StartsWith('+')) t = t[1..];

        if (!Regex.IsMatch(t, @"^\d[\d.,]*$")) return null;

        var punto = t.LastIndexOf('.');
        var virgola = t.LastIndexOf(',');
        char? decimali;
        if (punto >= 0 && virgola >= 0) decimali = punto > virgola ? '.' : ',';
        else if (virgola >= 0) decimali = t.Count(c => c == ',') == 1 ? ',' : null;
        else if (punto >= 0) decimali = t.Count(c => c == '.') == 1 && !Regex.IsMatch(t, @"^\d{1,3}\.\d{3}$") ? '.' : null;
        else decimali = null;

        string cifre;
        if (decimali is { } d)
        {
            var i = t.LastIndexOf(d);
            var intera = t[..i].Replace(".", "").Replace(",", "");
            cifre = intera + "." + t[(i + 1)..];
            if (!Regex.IsMatch(cifre, @"^\d*\.\d+$")) return null;
        }
        else
            cifre = t.Replace(".", "").Replace(",", "");

        if (!decimal.TryParse(cifre, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valore)) return null;
        return negativo ? -valore : valore;
    }
}

/// <summary>
/// Le causali scelte dall'utente per i movimenti dei file Excel (dal 10/11), per descrizione: quando arriva un
/// movimento con la stessa descrizione, prende la stessa causale invece di quella dedotta (o della fittizia).
/// La descrizione si confronta ridotta a parole (ExcelParser.ChiaveDescrizione): prima uguale, poi con le stesse
/// prime quattro parole, la causale scelta più spesso. Non troverà sempre il movimento giusto, ma spesso sì.
/// </summary>
public sealed class CausaliImparate
{
    private const int ParoleIniziali = 4;
    private readonly Dictionary<string, string> _esatte;
    private readonly Dictionary<string, string> _perInizio;

    public static CausaliImparate Nessuna { get; } = new(new Dictionary<string, string>());

    public CausaliImparate(IReadOnlyDictionary<string, string> perChiave)
    {
        _esatte = perChiave.Where(c => c.Key != "").ToDictionary(c => c.Key, c => c.Value);
        _perInizio = _esatte
            .Select(c => (Inizio: Inizio(c.Key), Causale: c.Value))
            .Where(c => c.Inizio is not null)
            .GroupBy(c => c.Inizio!)
            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.Causale).OrderByDescending(v => v.Count()).ThenBy(v => v.Key).First().Key);
    }

    public int Numero => _esatte.Count;

    /// <summary>La causale scelta per un movimento con questa descrizione, o null.</summary>
    public string? Cerca(string descrizione)
    {
        var chiave = ExcelParser.ChiaveDescrizione(descrizione);
        if (chiave == "") return null;
        if (_esatte.TryGetValue(chiave, out var causale)) return causale;
        return Inizio(chiave) is { } inizio && _perInizio.TryGetValue(inizio, out var simile) ? simile : null;
    }

    // le prime quattro parole, se ce ne sono almeno quattro
    private static string? Inizio(string chiave)
    {
        var parole = chiave.Split(' ');
        return parole.Length >= ParoleIniziali ? string.Join(' ', parole.Take(ParoleIniziali)) : null;
    }
}
