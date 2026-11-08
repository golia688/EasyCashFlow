using System.Globalization;
using System.Text.RegularExpressions;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// Legge un flusso CBI "Rendicontazione saldi e movimenti di conti correnti" (CBI-RND-001, capitolo 2;
/// il tracciato è descritto in docs/05-formato-cbi.md). Ogni rendicontazione è una giornata di un conto:
/// 61 saldo iniziale, 62 movimento seguito da 0-5 record 63 di informazioni, 64 saldo finale, 65 liquidità
/// future (ignorato); il flusso è racchiuso tra RH (testa) ed EF (coda).
/// </summary>
/// <summary>Il file non è un flusso CBI "saldi e movimenti" che il programma sa leggere: il messaggio è per
/// l'utente. La lettura si ferma e non si salva niente.</summary>
public class FileNonCbiException(string messaggio) : Exception(messaggio);

public class CbiParser
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    // i soli record della funzione "saldi e movimenti" (CBI-RND-001, capitolo 2)
    private static readonly HashSet<string> RecordPrevisti = ["RH", "61", "62", "63", "64", "65", "EF"];

    internal const string NonCbi = "Il file non è un export CBI \"saldi e movimenti\": in home banking scegli il formato CBI (testo o XML), non PDF, Excel o CSV.";
    private readonly LogService _log;

    public CbiParser(LogService log)
    {
        _log = log;
    }

    // Una giornata chiusa dal record 64. I controlli sui saldi si fanno alla fine, conto per conto e in
    // ordine di data, perché le banche non scrivono tutte le giornate nello stesso ordine: c'è chi va
    // dalla più vecchia alla più recente e chi al contrario (export di un tester, 05/11/2026).
    // Serve anche al lettore XML (CbiXmlParser), che fa gli stessi controlli sui saldi.
    internal record Giornata(string Conto, DateOnly Data, decimal Iniziale, decimal Finale, int Movimenti, bool Quadra);

    public RisultatoImportCbi Leggi(IEnumerable<string> righe)
    {
        var conti = new Dictionary<string, ContoCorrente>();
        var movimenti = new List<MovimentoCbi>();
        var giornate = new List<Giornata>();
        var scartati = new List<RecordScartato>();
        var avvisi = new List<string>();

        // stato della giornata (rendicontazione) in lettura
        var giorno = new List<MovimentoCbi>();
        string? numeroUltimo = null;        // numero del 62 a cui si attaccano i 63 che lo seguono
        bool ultimaParteStrutturata = false;
        DateOnly dataGiorno = default;
        decimal saldoIniziale = 0m;
        string conto = "";
        string divisa = "";

        // controlli del record di coda EF: rendicontazioni (record 61) e record dalla testa alla coda
        bool trovatoEf = false;
        int rendicontazioni = 0, recordFlusso = 0;
        int ritrasmissioni = 0, riferimentiLunghi = 0;
        int numeroRiga = 0;
        bool giaLetto = false;      // letto almeno un record: il primo deve essere la testa RH

        // L'avviso per l'utente (riepilogo dell'importazione) è completo; nel log conto e importi sono mascherati.
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

        // Se una giornata si interrompe senza il record 64 (file troncato), tiene comunque
        // i movimenti letti e segnala il problema.
        void ChiudiGiornataIncompleta()
        {
            numeroUltimo = null;
            if (giorno.Count == 0) return;
            movimenti.AddRange(giorno);
            Avvisa($"Conto {{conto}}, {dataGiorno:dd/MM/yyyy}: giornata incompleta nel file (manca il saldo finale).", conto);
            giorno.Clear();
        }

        _log.Trace("Lettura del file CBI avviata");

        foreach (var riga in righe)
        {
            numeroRiga++;

            if (riga.Length < 3)
            {
                if (_log.DebugAttivo)
                    _log.Debug($"Riga {numeroRiga}: troppo corta ({riga.Length} caratteri), ignorata");
                continue;
            }

            var tipo = riga.Substring(1, 2);
            recordFlusso++;
            if (_log.DebugAttivo)
                _log.Debug($"Riga {numeroRiga}: tipo {tipo}, lunghezza {riga.Length}");
            ControllaForma(riga, tipo, numeroRiga, primoRecord: !giaLetto, dopoLaCoda: trovatoEf);
            giaLetto = true;

            try
            {
                switch (tipo)
                {
                    case "RH":
                        // un nuovo flusso: i conteggi dell'EF valgono da qui
                        trovatoEf = false;
                        rendicontazioni = 0;
                        recordFlusso = 1;
                        _log.Trace($"Riga {numeroRiga}: record di testa RH, banca mittente {Campo(riga, 4, 8)}, creato il {Campo(riga, 14, 19)}");
                        break;

                    case "61": // saldo iniziale: apre la rendicontazione di un conto per una giornata
                        ChiudiGiornataIncompleta();
                        rendicontazioni++;
                        conto = Campo(riga, 53, 74);   // ABI + CAB + numero di conto
                        divisa = Campo(riga, 75, 77).Trim();
                        dataGiorno = Data(Campo(riga, 78, 83));
                        saldoIniziale = Importo(Campo(riga, 84, 84), Campo(riga, 85, 99));
                        if (Campo(riga, 29, 33) == "93011") ritrasmissioni++;
                        _log.Trace($"Riga {numeroRiga}: conto {Mascherato.Conto(conto)}, giornata {dataGiorno:dd/MM/yyyy}, " +
                                   $"causale {Campo(riga, 29, 33)}, saldo iniziale {Mascherato.Importo(saldoIniziale)}");

                        if (!conti.ContainsKey(conto))
                        {
                            conti[conto] = LeggiConto(riga, conto);
                            _log.Trace($"Riga {numeroRiga}: conto {Mascherato.Conto(conto)} trovato nel file, IBAN " +
                                       (conti[conto].Iban == "" ? "non ricavabile" : Mascherato.Iban(conti[conto].Iban)));
                        }
                        break;

                    case "62": // movimento: se non è interpretabile, viene scartato solo lui
                        numeroUltimo = null;
                        try
                        {
                            var numero = Campo(riga, 11, 13);
                            var causale = Campo(riga, 42, 45);     // causale CBI (42-43) + causale interna della banca (44-45)

                            // Il riferimento della banca occupa 62-77 (16 caratteri). Fino al 05/11/2026 si
                            // leggeva 62-76: il riferimento fa parte della chiave primaria, e leggerlo più lungo
                            // farebbe rientrare come nuovi i movimenti già importati. Si continua con 15 caratteri
                            // e si segnala nel log se la posizione 77 è usata (docs/05-formato-cbi.md, sezione 8).
                            if (Campo(riga, 77, 77).Trim() != "") riferimentiLunghi++;

                            var movimento = new MovimentoCbi(
                                Conto: conto,
                                DataContabile: Data(Campo(riga, 20, 25)),
                                DataValuta: Data(Campo(riga, 14, 19)),
                                Importo: Importo(Campo(riga, 26, 26), Campo(riga, 27, 41)),
                                Causale: causale,
                                Riferimento: Campo(riga, 62, 76).Trim(),
                                DescrizioneBreve: Campo(riga, 87, 120).Trim(),
                                DescrizioneEstesa: "",
                                RigaFile: numeroRiga);

                            // per gli assegni (causale CBI 13) il numero dell'assegno sta in 46-61
                            var assegno = Campo(riga, 46, 61).Trim();
                            ultimaParteStrutturata = false;
                            if (CausaliCbi.Di(causale) == "13" && assegno != "")
                            {
                                movimento = movimento with { DescrizioneEstesa = $"Assegno n. {assegno}" };
                                ultimaParteStrutturata = true;
                            }

                            giorno.Add(movimento);
                            numeroUltimo = numero;
                            _log.Trace($"Riga {numeroRiga}: movimento {numero} del {movimento.DataContabile:dd/MM/yyyy}, " +
                                       $"importo {Mascherato.Importo(movimento.Importo)}, causale {movimento.Causale}, " +
                                       $"rif. {Mascherato.Testo(movimento.Riferimento)}, descrizione {Mascherato.Testo(movimento.DescrizioneBreve)}");
                        }
                        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
                        {
                            var scarto = new RecordScartato(MotivoScarto.RigaNonValida,
                                $"Movimento non interpretabile: {ex.Message}", numeroRiga, riga);
                            scartati.Add(scarto);
                            _log.Scarto(scarto);
                            _log.EccezioneGestita($"lettura del movimento a riga {numeroRiga}", ex);
                        }
                        break;

                    case "63": // informazioni del movimento che lo precede (fino a 5 record)
                        if (numeroUltimo is not null && Campo(riga, 11, 13) == numeroUltimo)
                        {
                            var (testo, strutturata, prosegue) = Informazioni(riga);
                            var precedente = giorno[^1].DescrizioneEstesa;
                            var separatore = precedente == "" || prosegue ? ""
                                : strutturata || ultimaParteStrutturata ? " · " : " ";
                            if (testo != "")
                                giorno[^1] = giorno[^1] with { DescrizioneEstesa = precedente + separatore + testo };
                            ultimaParteStrutturata = strutturata;
                            _log.Trace($"Riga {numeroRiga}: informazioni del movimento {numeroUltimo}" +
                                       (strutturata ? $" (struttura {Campo(riga, 14, 16)})" : ""));
                        }
                        else
                        {
                            var scarto = new RecordScartato(MotivoScarto.RigaNonValida,
                                $"Descrizione estesa senza movimento corrispondente (numero {Campo(riga, 11, 13)})",
                                numeroRiga, riga);
                            scartati.Add(scarto);
                            _log.Scarto(scarto);
                        }
                        break;

                    case "64": // saldo finale: chiude la giornata
                        numeroUltimo = null;
                        var saldoFinale = Importo(Campo(riga, 20, 20), Campo(riga, 21, 35));
                        var quadra = saldoIniziale + giorno.Sum(m => m.Importo) == saldoFinale;

                        var divisa64 = Campo(riga, 11, 13).Trim();
                        if (divisa64 != divisa)
                            Avvisa($"Conto {{conto}}, {dataGiorno:dd/MM/yyyy}: la divisa del saldo finale ({divisa64}) è diversa da quella del saldo iniziale ({divisa}).", conto);
                        if (TryData(Campo(riga, 14, 19)) is { } data64 && data64 != dataGiorno)
                            Avvisa($"Conto {{conto}}, {dataGiorno:dd/MM/yyyy}: la data del saldo finale è diversa da quella del saldo iniziale.", conto);

                        _log.Trace($"Riga {numeroRiga}: fine giornata {dataGiorno:dd/MM/yyyy}, {giorno.Count} movimenti, " +
                                   $"saldo finale {Mascherato.Importo(saldoFinale)}, quadratura {(quadra ? "OK" : "NON OK")}");
                        movimenti.AddRange(giorno);
                        giornate.Add(new Giornata(conto, dataGiorno, saldoIniziale, saldoFinale, giorno.Count, quadra));
                        giorno.Clear();
                        break;

                    case "EF":
                        trovatoEf = true;
                        ControllaCoda(riga, rendicontazioni, recordFlusso, avvisi);
                        _log.Trace($"Riga {numeroRiga}: record di coda EF");
                        break;

                    // il record 65 (liquidità future) non serve
                }
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                // errore su un record strutturale (61, 64): l'integrità del file è compromessa. Il messaggio di
                // .NET (che può contenere un pezzo della riga) va solo nel log, mascherato.
                _log.EccezioneGestita($"lettura del record {tipo} a riga {numeroRiga}", ex);
                throw new FileNonCbiException($"La riga {numeroRiga} (record {tipo}) non ha il formato previsto dal " +
                    "tracciato CBI: il file potrebbe essere rovinato o modificato. Scaricalo di nuovo dalla banca.");
            }
        }

        if (!giaLetto)
            throw new FileNonCbiException("Il file è vuoto: non contiene nessun record CBI. " + NonCbi);

        ChiudiGiornataIncompleta();
        ControllaSaldi(giornate, Avvisa);

        if (!trovatoEf)
            avvisi.Add("Il file non termina con il record di coda EF: potrebbe essere incompleto.");
        if (ritrasmissioni > 0)
            avvisi.Add("Il file è una ritrasmissione della banca (causale 93011): i movimenti già importati risultano già presenti.");

        if (riferimentiLunghi > 0)
            _log.Avviso($"{riferimentiLunghi} movimenti con la posizione 77 del riferimento usata: il riferimento è letto di 15 caratteri (62-76)");

        _log.Trace($"Lettura completata: {conti.Count} conti, {movimenti.Count} movimenti, {giornate.Count} giornate, " +
                   $"{scartati.Count} righe scartate, {avvisi.Count} avvisi");

        var saldi = giornate.Select(g => new SaldoGiornaliero(g.Conto, g.Data, g.Iniziale, g.Finale)).ToList();
        return new RisultatoImportCbi(conti.Values.ToList(), movimenti, saldi, scartati, avvisi);
    }

    // Controlli formali su ogni riga, prima di interpretarla (dal 05/11/2026: prima un file qualunque veniva
    // letto lo stesso, e da un testo con righe simili a record CBI si importavano movimenti, docs/05 sezione 11).
    // Alla prima riga sbagliata la lettura si ferma: niente arriva al database.
    private static void ControllaForma(string riga, string tipo, int numeroRiga, bool primoRecord, bool dopoLaCoda)
    {
        // un file binario (PDF, Excel, zip, immagini) ha caratteri di controllo: un file CBI è solo testo
        if (riga.Any(c => c < ' ' && c != '	'))
            throw new FileNonCbiException($"Il file non è di testo (riga {numeroRiga}). " + NonCbi);

        if (primoRecord && tipo != "RH")
        {
            // EC è l'estratto conto periodico (CBI-RND-001, capitolo 3), le altre teste di due lettere sono
            // altri flussi CBI (bonifici, Ri.Ba., ...)
            if (tipo == "EC")
                throw new FileNonCbiException("Il file è un estratto conto periodico CBI (record di testa EC): il programma " +
                    "legge solo la rendicontazione giornaliera \"saldi e movimenti\" (testa RH). In home banking scegli quella.");
            throw new FileNonCbiException(riga[0] == ' ' && tipo.All(char.IsAsciiLetterUpper)
                ? $"Il file è un flusso CBI di un altro tipo (record di testa {tipo}), non la rendicontazione dei movimenti del conto. " + NonCbi
                : "Il file non inizia con il record di testa RH. " + NonCbi);
        }

        if (riga[0] != ' ' || !RecordPrevisti.Contains(tipo))
            throw new FileNonCbiException($"La riga {numeroRiga} non è un record CBI \"saldi e movimenti\". " + NonCbi);

        if (riga.Length > 120)
            throw new FileNonCbiException($"La riga {numeroRiga} è lunga {riga.Length} caratteri: nel formato CBI le righe " +
                "sono di 120 caratteri. " + NonCbi);

        if (dopoLaCoda && tipo != "RH")
            throw new FileNonCbiException($"La riga {numeroRiga} viene dopo il record di coda EF, ma non apre un nuovo flusso: " +
                "il file è stato modificato o unito ad altro.");
    }

    // Controlli sui saldi, per conto e in ordine di data.
    // - Quadratura: saldo iniziale + movimenti = saldo finale. Per CBI-RND-001 è obbligatoria: il saldo
    //   iniziale è "di quadratura", ottenuto togliendo dal finale i movimenti della giornata.
    // - Continuità: il saldo iniziale coincide con il finale del giorno precedente. La specifica non la
    //   garantisce (proprio perché il saldo iniziale è di quadratura), ma se manca di solito mancano dei
    //   movimenti, quindi lo si dice.
    // - Operazioni non contabilizzate (export fatto in giornata): giornata senza movimenti, finale uguale
    //   a quello del giorno prima, solo l'iniziale diverso. È un solo fatto, con un solo avviso chiaro.
    internal static void ControllaSaldi(List<Giornata> giornate, Action<string, string, decimal?> avvisa)
    {
        foreach (var delConto in giornate.GroupBy(g => g.Conto))
        {
            Giornata? precedente = null;
            foreach (var g in delConto.OrderBy(g => g.Data))
            {
                if (precedente is not null && g.Movimenti == 0 && precedente.Finale == g.Finale && g.Iniziale != g.Finale)
                {
                    var differenza = g.Iniziale - g.Finale;
                    avvisa($"Conto {{conto}}, {g.Data:dd/MM/yyyy}: la banca ha " +
                           $"{(differenza < 0 ? "tolto dal" : "aggiunto al")} saldo iniziale " +
                           "{importo} € di operazioni non ancora contabilizzate, che non sono nel file. " +
                           "Verranno importate con un export successivo.", g.Conto, Math.Abs(differenza));
                }
                else
                {
                    if (precedente is not null && precedente.Finale != g.Iniziale)
                        avvisa($"Conto {{conto}}, {g.Data:dd/MM/yyyy}: il saldo iniziale non coincide con il saldo finale " +
                               $"della giornata precedente nel file ({precedente.Data:dd/MM/yyyy}): potrebbero mancare dei movimenti.",
                               g.Conto, null);
                    if (!g.Quadra)
                        avvisa($"Conto {{conto}}, {g.Data:dd/MM/yyyy}: i movimenti non quadrano con i saldi.", g.Conto, null);
                }
                precedente = g;
            }
        }
    }

    // Il record di coda dichiara quante rendicontazioni (46-52) e quanti record (83-89, testa e coda
    // comprese) contiene il flusso: se non tornano, il file è stato tagliato o modificato.
    private void ControllaCoda(string riga, int rendicontazioni, int record, List<string> avvisi)
    {
        if (int.TryParse(Campo(riga, 46, 52), out var dichiarate) && dichiarate > 0 && dichiarate != rendicontazioni)
            avvisi.Add($"Il record di coda dichiara {dichiarate} rendicontazioni (giornate di un conto), " +
                       $"il file ne contiene {rendicontazioni}: potrebbe essere incompleto.");
        if (int.TryParse(Campo(riga, 83, 89), out var dichiarati) && dichiarati > 0 && dichiarati != record)
            avvisi.Add($"Il record di coda dichiara {dichiarati} righe, il file ne contiene {record}: potrebbe essere incompleto.");
        _log.Trace($"Record di coda: {Campo(riga, 46, 52).Trim()} rendicontazioni dichiarate ({rendicontazioni} lette), " +
                   $"{Campo(riga, 83, 89).Trim()} record dichiarati ({record} letti)");
    }

    // Un record 63. Dopo certe causali i primi 63 hanno una struttura fissa, riconoscibile dal "flag
    // struttura" in 14-16 (CBI-RND-001, par. 2.6); gli altri sono testo libero dalla posizione 14.
    // Restituisce il testo da aggiungere alla descrizione estesa, se è strutturato e se prosegue il testo
    // del 63 precedente senza separatore (RI2 continua RI1).
    private static (string Testo, bool Strutturata, bool Prosegue) Informazioni(string riga)
    {
        string C(int da, int a) => Pulisci(Campo(riga, da, a));

        static string Parti(params string?[] parti) =>
            string.Join(", ", parti.Where(p => !string.IsNullOrWhiteSpace(p)));

        return Campo(riga, 14, 16) switch
        {
            // giroconto o cash pooling: il conto dell'altra parte
            "KKK" => (Parti(C(17, 39) is { Length: > 0 } c ? $"Conto controparte {c}" : null), true, false),

            // bonifico ricevuto (causale CBI 48): ordinante, località, data dell'ordine, codice fiscale
            "YYY" => (Parti(
                C(41, 80) is { Length: > 0 } ordinante ? $"Ordinante: {ordinante}" : null,
                C(81, 120),
                TryDataEstesa(C(17, 24)) is { } data ? $"ordine del {data:dd/MM/yyyy}" : null,
                C(25, 40) is { Length: > 0 } fiscale ? $"cod. fiscale {fiscale}" : null), true, false),
            "YY2" => (Parti(
                C(17, 66) is { Length: > 0 } via ? $"Indirizzo ordinante: {via}" : null,
                C(67, 100) is { Length: > 0 } iban ? $"IBAN ordinante {iban}" : null), true, false),

            // bonifici da e per l'estero
            "ZZ1" => (Parti(
                ImportoConDivisa("importo originario", C(17, 34), C(35, 37)),
                ImportoConDivisa("regolato", C(38, 55), C(56, 58)),
                ImportoConDivisa("negoziato", C(59, 76), C(77, 79)),
                long.TryParse(C(80, 91), out var cambio) && cambio > 0
                    ? $"cambio {(cambio / 100000m).ToString("0.#####", Italiano)}" : null,
                ImportoConDivisa("commissioni", C(92, 104), ""),
                ImportoConDivisa("spese", C(105, 117), ""),
                C(118, 120) is { Length: > 0 } paese ? $"paese {paese}" : null), true, false),
            "ZZ2" => (Parti(C(17, 120) is { Length: > 0 } o ? $"Ordinante: {o}" : null), true, false),
            "ZZ3" => (Parti(
                C(17, 66) is { Length: > 0 } b ? $"Beneficiario: {b}" : null,
                C(67, 120) is { Length: > 0 } m ? $"motivazione: {m}" : null), true, false),

            // riconciliazione dei pagamenti XML
            "ID1" => (Parti(
                C(17, 51) is { Length: > 0 } msg ? $"Id messaggio {msg}" : null,
                C(52, 86) is { Length: > 0 } e2e ? $"Id end-to-end {e2e}" : null), true, false),
            "RI1" => (C(17, 120) is { Length: > 0 } ri ? $"Riconciliazione: {ri}" : "", true, false),
            "RI2" => (C(17, 52), true, true),

            _ => (C(14, 120), false, false)
        };
    }

    // un importo dei 63 strutturati, scritto all'italiana se si legge, altrimenti com'è nel file
    private static string? ImportoConDivisa(string nome, string valore, string divisa)
    {
        if (valore == "" || valore.Trim('0', ',', '.') == "") return null;
        var testo = decimal.TryParse(valore, NumberStyles.AllowDecimalPoint, Italiano, out var importo)
            ? importo.ToString("N2", Italiano)
            : valore;
        return $"{nome} {testo}{(divisa != "" ? " " + divisa : "")}";
    }

    // Dati del conto dal record 61. L'IBAN si compone con paese e cifre di controllo (100-103),
    // CIN (52) e ABI + CAB + numero (53-74); se il controllo modulo 97 fallisce resta vuoto.
    private static ContoCorrente LeggiConto(string riga61, string codice)
    {
        var iban = (Campo(riga61, 100, 103) + Campo(riga61, 52, 52) + codice).Replace(" ", "");
        return new ContoCorrente(
            Codice: codice,
            Iban: IbanValido(iban) ? iban : "",
            Abi: Campo(riga61, 53, 57),
            Divisa: Campo(riga61, 75, 77).Trim(),
            Nome: "");
    }

    internal static bool IbanValido(string iban)
    {
        if (iban.Length < 15 || iban.Length > 34 || !iban.All(char.IsAsciiLetterOrDigit))
            return false;

        // si spostano i primi 4 caratteri in fondo, le lettere diventano numeri (A = 10 ... Z = 35)
        // e il resto della divisione per 97 deve essere 1
        int resto = 0;
        foreach (var c in iban[4..] + iban[..4])
        {
            var valore = char.IsAsciiDigit(c) ? c - '0' : char.ToUpperInvariant(c) - 'A' + 10;
            resto = (valore > 9 ? resto * 100 + valore : resto * 10 + valore) % 97;
        }
        return resto == 1;
    }

    // posizioni 1-based, estremi inclusi; tollera righe più corte
    private static string Campo(string riga, int da, int a)
    {
        if (riga.Length < da) return "";
        return riga.Substring(da - 1, Math.Min(a, riga.Length) - da + 1);
    }

    private static DateOnly Data(string ggmmaa) =>
        new(2000 + int.Parse(ggmmaa.Substring(4, 2)),
            int.Parse(ggmmaa.Substring(2, 2)),
            int.Parse(ggmmaa.Substring(0, 2)));

    private static DateOnly? TryData(string ggmmaa)
    {
        try { return ggmmaa.Length == 6 ? Data(ggmmaa) : null; }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return null; }
    }

    // le date dei 63 strutturati hanno l'anno a quattro cifre (GGMMAAAA)
    private static DateOnly? TryDataEstesa(string ggmmaaaa) =>
        DateOnly.TryParseExact(ggmmaaaa, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;

    // segno: D = addebito (negativo), C = accredito (positivo)
    private static decimal Importo(string segno, string valore)
    {
        var importo = decimal.Parse(valore, NumberStyles.AllowDecimalPoint, Italiano);
        return segno == "D" ? -importo : importo;
    }

    private static string Pulisci(string testo) => Regex.Replace(testo, @"\s+", " ").Trim();
}
