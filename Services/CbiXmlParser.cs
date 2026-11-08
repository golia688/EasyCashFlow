using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// Legge la rendicontazione dei movimenti in XML del CBI ("Rendicontazioni XML", STRN-MO-001 v.00.01.02,
/// costruita su ISO 20022 camt.053; dal 08/11/2026, docs/05-formato-cbi.md sezione 14). È lo stesso servizio
/// "saldi e movimenti" del file a 120 caratteri, e il risultato è lo stesso di <see cref="CbiParser"/>: conti,
/// movimenti, saldi giornalieri, scarti e avvisi. Dal confronto di un export XML e di un export CBI degli
/// stessi tre mesi (stessa banca), chiave, causale, data valuta e saldi coincidono: lo stesso movimento,
/// importato nei due formati, è uno solo.
///
/// Ogni blocco Stmt è una giornata di un conto: saldi OPBD (iniziale) e CLBD (finale), i movimenti Ntry e il
/// riepilogo TxsSummry. Si legge solo la versione giornaliera (CBIDlyStmtReqLogMsg): la periodica è un estratto
/// conto, l'intraday ha movimenti che la banca può ancora annullare.
/// </summary>
public class CbiXmlParser
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    // la descrizione breve del file CBI a 120 caratteri è lunga al massimo 34 caratteri
    private const int LunghezzaBreve = 34;

    private readonly LogService _log;

    public CbiXmlParser(LogService log)
    {
        _log = log;
    }

    // Un movimento letto, con il testo completo della banca ancora da dividere in descrizione breve ed estesa
    // (si divide alla fine, quando si conoscono tutti i movimenti con la stessa causale).
    private record Letto(MovimentoCbi Movimento, string Testo);

    /// <summary>Legge il file. breviNote dà, per una causale, le descrizioni brevi già nel database: servono a
    /// dividere il testo del movimento come nel file CBI a 120 caratteri.</summary>
    public RisultatoImportCbi Leggi(Stream flusso, Func<string, IReadOnlyCollection<string>>? breviNote = null)
    {
        var conti = new Dictionary<string, ContoCorrente>();
        var letti = new List<Letto>();
        var giornate = new List<CbiParser.Giornata>();
        var scartati = new List<RecordScartato>();
        var avvisi = new List<string>();

        void Avvisa(string testo, string codiceConto, decimal? importo = null)
        {
            var completo = testo.Replace("{conto}", codiceConto);
            var mascherato = testo.Replace("{conto}", Mascherato.Conto(codiceConto));
            if (importo is { } i)
            {
                completo = completo.Replace("{importo}", i.ToString("N2", Italiano));
                mascherato = mascherato.Replace("{importo}", Mascherato.Importo(i));
            }
            avvisi.Add(completo);
            _log.Avviso(mascherato);
        }

        _log.Trace("Lettura del file XML avviata");
        var radice = Carica(flusso);

        var giornalieri = Discendenti(radice, "CBIDlyStmtReqLogMsg").ToList();
        if (giornalieri.Count == 0)
            throw new FileNonCbiException(TipoNonGestito(radice));

        int nonContabilizzati = 0, riferimentiLunghi = 0, senzaRiferimento = 0;

        foreach (var stmt in giornalieri.SelectMany(g => Figli(g, "Stmt")))
        {
            var rigaStmt = Riga(stmt);
            var conto = LeggiConto(stmt, rigaStmt, conti);

            var iniziale = Saldo(stmt, "OPBD") ?? throw Strutturale(rigaStmt, "manca il saldo iniziale (OPBD)");
            var finale = Saldo(stmt, "CLBD") ?? throw Strutturale(rigaStmt, "manca il saldo finale (CLBD)");
            var data = finale.Data;
            if (iniziale.Data != finale.Data)
                Avvisa($"Conto {{conto}}, {data:dd/MM/yyyy}: la data del saldo iniziale è diversa da quella del saldo finale.", conto);

            var giorno = new List<MovimentoCbi>();
            int ntryTotali = 0;
            decimal somma = 0m, netto = 0m;
            foreach (var ntry in Figli(stmt, "Ntry"))
            {
                ntryTotali++;
                var riga = Riga(ntry);
                try
                {
                    var importo = Importo(ntry);
                    somma += Math.Abs(importo);
                    netto += importo;

                    // solo i movimenti contabilizzati (BOOK): gli altri la banca li può ancora cambiare
                    var stato = Testo(ntry, "Sts") is { Length: > 0 } s ? s : Testo(ntry, "Sts", "Cd");
                    if (stato != "" && stato != "BOOK")
                    {
                        nonContabilizzati++;
                        _log.Trace($"Riga {riga}: movimento con stato {stato}, non importato");
                        continue;
                    }

                    var (movimento, testo, lungo, senza) = LeggiMovimento(ntry, conto, data, importo, riga);
                    if (lungo) riferimentiLunghi++;
                    if (senza) senzaRiferimento++;
                    giorno.Add(movimento);
                    letti.Add(new Letto(movimento, testo));
                    _log.Trace($"Riga {riga}: movimento del {movimento.DataContabile:dd/MM/yyyy}, " +
                               $"importo {Mascherato.Importo(movimento.Importo)}, causale {movimento.Causale}, " +
                               $"rif. {Mascherato.Testo(movimento.Riferimento)}, descrizione {Mascherato.Testo(testo)}");
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
                {
                    var scarto = new RecordScartato(MotivoScarto.RigaNonValida,
                        $"Movimento non interpretabile: {ex.Message}", riga, null);
                    scartati.Add(scarto);
                    _log.Scarto(scarto);
                    _log.EccezioneGestita($"lettura del movimento a riga {riga}", ex);
                }
            }

            ControllaRiepilogo(stmt, ntryTotali, somma, netto, conto, data, Avvisa);

            var quadra = iniziale.Importo + giorno.Sum(m => m.Importo) == finale.Importo;
            _log.Trace($"Riga {rigaStmt}: giornata {data:dd/MM/yyyy} del conto {Mascherato.Conto(conto)}, {giorno.Count} movimenti, " +
                       $"saldo iniziale {Mascherato.Importo(iniziale.Importo)}, finale {Mascherato.Importo(finale.Importo)}, " +
                       $"quadratura {(quadra ? "OK" : "NON OK")}");
            giornate.Add(new CbiParser.Giornata(conto, data, iniziale.Importo, finale.Importo, giorno.Count, quadra));
        }

        if (giornate.Count == 0)
            throw new FileNonCbiException("Il file XML non contiene nessuna giornata (blocco Stmt): non ci sono movimenti da importare.");

        CbiParser.ControllaSaldi(giornate, Avvisa);

        if (nonContabilizzati > 0)
            avvisi.Add($"{nonContabilizzati} movimenti non ancora contabilizzati dalla banca non sono stati importati: " +
                       "arriveranno con un export successivo.");
        if (riferimentiLunghi > 0)
            _log.Avviso($"{riferimentiLunghi} movimenti con il riferimento della banca più lungo di 15 caratteri: letto di 15, come nel file CBI");
        if (senzaRiferimento > 0)
            avvisi.Add($"{senzaRiferimento} movimenti non hanno il riferimento della banca: se importi anche il file CBI di testo degli " +
                       "stessi giorni, potrebbero comparire due volte.");

        var movimenti = Dividi(letti, breviNote);

        _log.Trace($"Lettura completata: {conti.Count} conti, {movimenti.Count} movimenti, {giornate.Count} giornate, " +
                   $"{scartati.Count} movimenti scartati, {avvisi.Count} avvisi");

        var saldi = giornate.Select(g => new SaldoGiornaliero(g.Conto, g.Data, g.Iniziale, g.Finale)).ToList();
        return new RisultatoImportCbi(conti.Values.ToList(), movimenti, saldi, scartati, avvisi);
    }

    // Il file si legge senza DTD e senza risorse esterne: un XML costruito apposta non può far leggere al
    // programma altri file del computer né scaricare niente.
    private XElement Carica(Stream flusso)
    {
        var impostazioni = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };
        try
        {
            using var lettore = XmlReader.Create(flusso, impostazioni);
            return XDocument.Load(lettore, LoadOptions.SetLineInfo).Root
                   ?? throw new FileNonCbiException("Il file XML è vuoto. " + CbiParser.NonCbi);
        }
        catch (XmlException ex)
        {
            _log.EccezioneGestita("lettura del file XML", ex);
            throw new FileNonCbiException("Il file XML non è leggibile" + (ex.LineNumber > 0 ? $" (riga {ex.LineNumber})" : "") +
                ": potrebbe essere rovinato o incompleto. Scaricalo di nuovo dalla banca.");
        }
    }

    // Perché un XML senza rendicontazione giornaliera non va bene, detto all'utente.
    private static string TipoNonGestito(XElement radice)
    {
        if (Discendenti(radice, "CBIIdyStmtReqLogMsg").Any())
            return ("Il file è la rendicontazione XML infragiornaliera (intraday): contiene movimenti " +
                "che la banca può ancora annullare, e il programma non la importa. In home banking scegli i movimenti " +
                "contabilizzati (rendicontazione giornaliera).");
        if (Discendenti(radice, "CBIPrdcStmtReqLogMsg").Any())
            return ("Il file è un estratto conto periodico in XML: il programma legge solo la " +
                "rendicontazione giornaliera \"saldi e movimenti\". In home banking scegli quella.");
        if (Discendenti(radice, "Stmt").Any())
            return ("Il file è un estratto conto XML, ma non secondo lo standard CBI (rendicontazione " +
                "giornaliera \"saldi e movimenti\"): il programma non lo sa leggere. " + CbiParser.NonCbi);
        return $"Il file è un XML, ma non una rendicontazione dei movimenti CBI (elemento principale {radice.Name.LocalName}). " +
               CbiParser.NonCbi;
    }

    // Il conto dall'IBAN: per un IBAN italiano, dopo paese, cifre di controllo e CIN vengono ABI, CAB e numero
    // di conto, cioè proprio il codice del conto del file CBI a 120 caratteri (record 61, posizioni 53-74).
    private string LeggiConto(XElement stmt, int riga, Dictionary<string, ContoCorrente> conti)
    {
        var iban = Testo(stmt, "Acct", "Id", "IBAN").Replace(" ", "").ToUpperInvariant();
        if (iban == "")
            throw Strutturale(riga, "manca l'IBAN del conto");

        var italiano = iban.StartsWith("IT") && iban.Length == 27;
        var codice = italiano ? iban[5..] : iban;
        var divisa = Testo(stmt, "Acct", "Ccy") is { Length: > 0 } d ? d : "EUR";

        if (!conti.ContainsKey(codice))
        {
            var valido = CbiParser.IbanValido(iban);
            var abi = italiano ? iban[5..10] : Testo(stmt, "Acct", "Svcr", "FinInstnId", "ClrSysMmbId", "MmbId");
            conti[codice] = new ContoCorrente(codice, valido ? iban : "", abi, divisa, "");
            _log.Trace($"Riga {riga}: conto {Mascherato.Conto(codice)} trovato nel file, IBAN " +
                       (valido ? Mascherato.Iban(iban) : "non valido"));
        }
        return codice;
    }

    private (MovimentoCbi Movimento, string Testo, bool RiferimentoLungo, bool SenzaRiferimento) LeggiMovimento(
        XElement ntry, string conto, DateOnly giornata, decimal importo, int riga)
    {
        var contabile = DataDi(Figlio(ntry, "BookgDt")) ?? giornata;
        var valuta = DataDi(Figlio(ntry, "ValDt")) ?? contabile;

        // La causale CBI è nel codice proprietario con emittente CBI: "48//00" è la causale CBI 48 con la
        // causale interna 00, cioè "4800" del file a 120 caratteri.
        var proprietario = Figlio(ntry, "BkTxCd", "Prtry");
        var causale = Testo(proprietario, "Cd").Replace("//", "").Replace(" ", "");
        if (causale == "")
            throw new FormatException("manca la causale CBI (codice proprietario dell'operazione)");
        if (Testo(proprietario, "Issr") is { Length: > 0 } emittente && emittente != "CBI")
            _log.Debug($"Riga {riga}: codice dell'operazione con emittente {emittente}, letto come causale CBI");

        // Il riferimento della banca è lo stesso del file a 120 caratteri, che ne legge 15 caratteri.
        // Se manca, se ne fa uno dalla data e dal numero del movimento nella giornata.
        var riferimento = Testo(ntry, "AcctSvcrRef");
        bool lungo = riferimento.Length > 15, senza = riferimento == "";
        if (lungo) riferimento = riferimento[..15];
        if (senza) riferimento = $"X{contabile:yyMMdd}{Testo(ntry, "NtryRef")}";

        var movimento = new MovimentoCbi(conto, contabile, valuta, importo, causale, riferimento, "", "", riga);
        return (movimento, TestoDelMovimento(ntry), lungo, senza);
    }

    // Il testo del movimento: le informazioni aggiuntive (una per transazione, se il movimento ne raggruppa più
    // di una), altrimenti quelle del movimento, altrimenti nomi delle parti e causale del pagamento.
    private static string TestoDelMovimento(XElement ntry)
    {
        var dettagli = Figli(Figlio(ntry, "NtryDtls"), "TxDtls").ToList();
        var testi = dettagli.Select(t => Testo(t, "AddtlTxInf")).Where(t => t != "").ToList();
        if (testi.Count == 0 && Testo(ntry, "AddtlNtryInf") is { Length: > 0 } infoMovimento)
            testi.Add(infoMovimento);
        if (testi.Count == 0)
            foreach (var t in dettagli)
                testi.Add(string.Join(" ", new[]
                {
                    Testo(t, "RltdPties", "Dbtr", "Nm"), Testo(t, "RltdPties", "Cdtr", "Nm"),
                    string.Join(" ", Figli(Figlio(t, "RmtInf"), "Ustrd").Select(u => u.Value.Trim()))
                }.Where(p => p != "")));
        return Pulisci(string.Join(" · ", testi.Where(t => t != "")));
    }

    // L'XML non ha la descrizione breve come campo a sé: il testo della banca comincia con quella che nel file
    // a 120 caratteri è la descrizione breve (al massimo 34 caratteri), e prosegue con quello che lì sta nei
    // record 63 (verificato su 74 movimenti). Per dividerlo, nell'ordine:
    // 1. una descrizione breve già nel database per la stessa causale, se il testo comincia così;
    // 2. il testo prima di " *" o " @", che nei testi della banca aprono la parte dei record 63 (il nome della
    //    controparte, un addebito della banca);
    // 3. la parte iniziale comune a tutti i movimenti del file con la stessa causale;
    // 4. i primi 34 caratteri, senza spezzare una parola.
    // Se poi si importa il file CBI a 120 caratteri degli stessi giorni, la descrizione breve diventa quella
    // della banca (DatabaseService.InserisciMovimenti).
    private static List<MovimentoCbi> Dividi(List<Letto> letti, Func<string, IReadOnlyCollection<string>>? breviNote)
    {
        // la parte comune conta solo tra testi diversi: tra testi tutti uguali (lo stesso addebito ogni mese)
        // sarebbe il testo intero
        var comuni = letti.GroupBy(l => l.Movimento.Causale)
                          .Select(g => (g.Key, Testi: g.Select(l => l.Testo).Distinct().ToList()))
                          .Where(g => g.Testi.Count > 1)
                          .ToDictionary(g => g.Key, g => PrefissoComune(g.Testi));
        var note = new Dictionary<string, IReadOnlyCollection<string>>();

        return letti.Select(l =>
        {
            var causale = l.Movimento.Causale;
            if (!note.TryGetValue(causale, out var brevi))
                note[causale] = brevi = breviNote?.Invoke(causale) ?? [];
            var breve = Breve(l.Testo, brevi, comuni.GetValueOrDefault(causale));
            var estesa = l.Testo.Length > breve.Length ? l.Testo[breve.Length..].Trim() : "";
            return l.Movimento with { DescrizioneBreve = breve, DescrizioneEstesa = estesa };
        }).ToList();
    }

    internal static string Breve(string testo, IReadOnlyCollection<string> brevi, string? comune)
    {
        static bool Inizia(string testo, string breve) =>
            breve != "" && testo.StartsWith(breve, StringComparison.Ordinal) &&
            (testo.Length == breve.Length || testo[breve.Length] == ' ');

        var nota = brevi.Where(b => Inizia(testo, b)).MaxBy(b => b.Length);
        if (nota is not null) return nota;

        var segni = new[] { testo.IndexOf(" *", StringComparison.Ordinal), testo.IndexOf(" @", StringComparison.Ordinal) }
            .Where(i => i > 0 && i <= LunghezzaBreve).ToList();
        if (segni.Count > 0) return testo[..segni.Min()].Trim();

        if (comune is { Length: > 0 })
            return comune.Length >= LunghezzaBreve ? comune[..LunghezzaBreve].TrimEnd() : SenzaParolaTroncata(comune, testo);

        return testo.Length <= LunghezzaBreve ? testo : SenzaParolaTroncata(testo[..LunghezzaBreve], testo);
    }

    // toglie l'ultima parola se nel testo completo continua oltre il taglio
    private static string SenzaParolaTroncata(string taglio, string testo)
    {
        if (taglio.Length >= testo.Length || testo[taglio.Length] == ' ') return taglio.TrimEnd();
        var spazio = taglio.LastIndexOf(' ');
        return spazio > 0 ? taglio[..spazio].TrimEnd() : taglio.TrimEnd();
    }

    private static string PrefissoComune(IEnumerable<string> testi)
    {
        string? comune = null;
        foreach (var t in testi)
        {
            if (comune is null) { comune = t; continue; }
            int i = 0;
            while (i < comune.Length && i < t.Length && comune[i] == t[i]) i++;
            comune = comune[..i];
        }
        return comune ?? "";
    }

    // Il riepilogo della giornata (TxsSummry): numero dei movimenti, somma dei valori assoluti e somma con il
    // segno. Come i conteggi del record EF: se non tornano, il file è stato tagliato o modificato.
    private static void ControllaRiepilogo(XElement stmt, int movimenti, decimal somma, decimal netto, string conto,
                                           DateOnly data, Action<string, string, decimal?> avvisa)
    {
        var totale = Figlio(stmt, "TxsSummry", "TtlNtries");
        if (totale is null)
        {
            if (movimenti > 0)
                avvisa($"Conto {{conto}}, {data:dd/MM/yyyy}: manca il riepilogo dei movimenti della giornata.", conto, null);
            return;
        }

        if (int.TryParse(Testo(totale, "NbOfNtries"), out var dichiarati) && dichiarati != movimenti)
            avvisa($"Conto {{conto}}, {data:dd/MM/yyyy}: il riepilogo dichiara {dichiarati} movimenti, il file ne contiene " +
                   $"{movimenti}: potrebbe essere incompleto.", conto, null);
        if (Decimale(Testo(totale, "Sum")) is { } dichiarata && dichiarata != somma)
            avvisa($"Conto {{conto}}, {data:dd/MM/yyyy}: la somma dei movimenti non coincide con quella del riepilogo.", conto, null);
        if (Decimale(Testo(totale, "TtlNetNtryAmt")) is { } nettoDichiarato &&
            (Testo(totale, "CdtDbtInd") == "DBIT" ? -nettoDichiarato : nettoDichiarato) != netto)
            avvisa($"Conto {{conto}}, {data:dd/MM/yyyy}: il saldo dei movimenti non coincide con quello del riepilogo.", conto, null);
    }

    private record SaldoLetto(DateOnly Data, decimal Importo);

    private static SaldoLetto? Saldo(XElement stmt, string tipo)
    {
        var saldo = Figli(stmt, "Bal").FirstOrDefault(b => Testo(b, "Tp", "CdOrPrtry", "Cd") == tipo);
        if (saldo is null) return null;
        var data = DataDi(Figlio(saldo, "Dt")) ?? throw Strutturale(Riga(saldo), $"il saldo {tipo} non ha la data");
        var importo = Decimale(Testo(saldo, "Amt")) ?? throw Strutturale(Riga(saldo), $"l'importo del saldo {tipo} non è valido");
        return new SaldoLetto(data, Testo(saldo, "CdtDbtInd") == "DBIT" ? -importo : importo);
    }

    // importo con segno: DBIT = addebito (negativo), CRDT = accredito (positivo)
    private static decimal Importo(XElement ntry)
    {
        var importo = Decimale(Testo(ntry, "Amt")) ?? throw new FormatException("l'importo non è valido");
        return Testo(ntry, "CdtDbtInd") switch
        {
            "DBIT" => -importo,
            "CRDT" => importo,
            var altro => throw new FormatException($"il segno dell'importo non è valido ({altro})")
        };
    }

    // Una data ISO, con o senza fuso ("2026-11-06+01:00") o con l'ora (DtTm): contano anno, mese e giorno.
    private static DateOnly? DataDi(XElement? contenitore)
    {
        var testo = Testo(contenitore, "Dt") is { Length: > 0 } d ? d : Testo(contenitore, "DtTm");
        if (testo.Length < 10) return null;
        return DateOnly.TryParseExact(testo[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data : throw new FormatException("data non valida");
    }

    private static decimal? Decimale(string testo) =>
        decimal.TryParse(testo, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valore) ? valore : null;

    private static FileNonCbiException Strutturale(int riga, string problema) =>
        new($"Alla riga {riga} del file XML {problema}: il file potrebbe essere rovinato o modificato. Scaricalo di nuovo dalla banca.");

    // Gli elementi si cercano per nome locale: i prefissi e le versioni degli spazi dei nomi cambiano tra le
    // versioni dello standard, i nomi degli elementi no.
    private static IEnumerable<XElement> Figli(XElement? e, string nome) =>
        e?.Elements().Where(x => x.Name.LocalName == nome) ?? [];

    private static XElement? Figlio(XElement? e, params string[] percorso)
    {
        foreach (var nome in percorso)
            e = Figli(e, nome).FirstOrDefault();
        return e;
    }

    private static string Testo(XElement? e, params string[] percorso) => Figlio(e, percorso)?.Value.Trim() ?? "";

    private static IEnumerable<XElement> Discendenti(XElement e, string nome) =>
        e.DescendantsAndSelf().Where(x => x.Name.LocalName == nome);

    private static int Riga(XElement e) => ((IXmlLineInfo)e).HasLineInfo() ? ((IXmlLineInfo)e).LineNumber : 0;

    private static string Pulisci(string testo) => Regex.Replace(testo, @"\s+", " ").Trim();
}
