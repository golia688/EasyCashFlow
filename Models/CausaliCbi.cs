namespace EasyCashFlow.Models;

/// <summary>
/// Le causali CBI: le prime due posizioni della causale del record 62 (pos. 42-43), uguali per tutte le
/// banche; le altre due (44-45) sono la causale interna della banca (CBI-RND-001, par. 2.5). Le descrizioni
/// sono quelle della tabella ufficiale, l'Appendice B di CBI-STD-001 versione 6.10, senza le note
/// (docs/05-formato-cbi.md, sezione 7).
/// </summary>
public static class CausaliCbi
{
    /// <summary>La causale CBI di una causale a 4 caratteri (null se è più corta).</summary>
    public static string? Di(string causale) => causale.Length >= 2 ? causale[..2] : null;

    /// <summary>La causale fittizia dei movimenti di un file Excel di cui il programma non riconosce il tipo
    /// (dal 09/11): non è una causale CBI e non ha gruppo. L'utente sceglie per ogni movimento la causale
    /// vera (Movimenti, menu del movimento o barra di selezione).</summary>
    public const string Fittizia = "00XL";

    /// <summary>Le causali dei movimenti dei file Excel finiscono con "XL" (ExcelParser): sono dedotte dal
    /// programma, non date dalla banca, e l'utente le può cambiare.</summary>
    public static bool DaExcel(string causale) => causale.EndsWith("XL", StringComparison.Ordinal) && causale.Length == 4;

    /// <summary>Descrizione della causale CBI secondo lo standard, o null se il codice non è nella tabella.</summary>
    public static string? Descrizione(string causale) =>
        causale == Fittizia ? "Causale fittizia: tipo non riconosciuto nel file Excel" :
        Di(causale) is { } cbi && Descrizioni.TryGetValue(cbi, out var testo) ? testo : null;

    /// <summary>Le causali CBI che l'utente può scegliere per un movimento di un file Excel, in ordine di
    /// descrizione: senza i codici "per accordo tra le parti" e senza il saldo.</summary>
    // calcolato al primo uso: un inizializzatore qui girerebbe prima di quello di Descrizioni, più sotto
    public static IReadOnlyList<(string Codice, string Descrizione)> Scegliibili => _scegliibili ??=
        Descrizioni.Where(d => !d.Value.StartsWith("Valore ammesso") && d.Key != "62")
                   .Select(d => (d.Key, d.Value))
                   .OrderBy(d => d.Value, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("it-IT"), true))
                   .ToList();

    private static List<(string Codice, string Descrizione)>? _scegliibili;

    // CBI-STD-001 6.10, Appendice B "Tabella causali CBI": tutti i codici, nell'ordine della tabella
    private static readonly Dictionary<string, string> Descrizioni = new()
    {
        ["01"] = "Valore ammesso per accordo tra le parti",
        ["02"] = "Valore ammesso per accordo tra le parti",
        ["03"] = "Valore ammesso per accordo tra le parti",
        ["04"] = "Valore ammesso per accordo tra le parti",
        ["05"] = "Prelevamento a mezzo nostro sportello automatico",
        ["06"] = "Accredito per incassi con addebito in conto preautorizzato",
        ["07"] = "Accredito per incassi con addebito in conto non preautorizzato o per cassa",
        ["08"] = "Disposizioni di incasso preautorizzate impagate",
        ["09"] = "Incasso tramite P.O.S.",
        ["10"] = "Emissione assegni circolari o vaglia",
        ["11"] = "Pagamento utenze (servizi pubblici, luce, gas, telefono, ecc.)",
        ["13"] = "Vostro assegno bancario n.",
        ["14"] = "Cedole, dividendi e premi estratti",
        ["15"] = "Rimborso finanziamenti (mutui, prestiti personali, ecc.)",
        ["16"] = "Commissioni",
        ["17"] = "Locazioni (fitto, leasing ecc.) e premi assicurativi",
        ["18"] = "Interessi e competenze (conti correnti, conti di deposito ecc.)",
        ["19"] = "Imposte e tasse",
        ["20"] = "Canone cassette di sicurezza e custodia valori",
        ["21"] = "Contributi assistenziali e previdenziali",
        ["22"] = "Diritti di custodia e amministrazione titoli",
        ["24"] = "Pagamento o incasso di documenti sull'Italia",
        ["26"] = "Vostra disposizione a favore di",
        ["27"] = "Accredito per emolumenti (stipendi, pensioni, ecc.)",
        ["28"] = "Acquisto e vendita divise e/o banconote estere",
        ["29"] = "Accrediti RI.BA.",
        ["30"] = "Accrediti effetti s.b.f.",
        ["31"] = "Effetti ritirati (pagati)",
        ["32"] = "Effetti richiamati",
        ["34"] = "Disposizioni di giro conto (stessa banca)",
        ["35"] = "Storni RI.BA.",
        ["37"] = "Insoluti RI.BA.",
        ["39"] = "Vostra disposizione per emolumenti (stipendi, pensioni ecc.)",
        ["41"] = "Pagamento o disposizione a mezzo sportello automatico",
        ["42"] = "Effetti insoluti o protestati",
        ["43"] = "Pagamento tramite POS",
        ["44"] = "Addebito per utilizzo di credito documentario su estero",
        ["45"] = "Pagamento per utilizzo carte di credito",
        ["46"] = "Mandati di pagamento",
        ["47"] = "Accredito per utilizzo credito documentario sull'Italia",
        ["48"] = "Bonifico a Vostro favore (per ordine e conto)",
        ["49"] = "Valore ammesso per accordo tra le parti",
        ["50"] = "Pagamenti diversi",
        ["51"] = "Assegni turistici",
        ["52"] = "Prelevamento con moduli di sportello",
        ["53"] = "Addebito per utilizzo di credito documentario sull'Italia",
        ["55"] = "Assegni bancari insoluti o protestati",
        ["56"] = "Ricavo effetti o assegni in lire e/o valuta estera al dopo incasso",
        ["57"] = "Restituzione di assegni o vaglia irregolari",
        ["58"] = "Reversali d'incasso",
        ["60"] = "Rettifica valuta",
        ["62"] = "Saldo",
        ["63"] = "Accrediti sconto effetti diretti",
        ["64"] = "Accrediti effetti presentati allo sconto (sull'Italia)",
        ["65"] = "Competenze di sconto",
        ["66"] = "Spese",
        ["68"] = "Storno scritture",
        ["70"] = "Acquisto/vendita a pronti titoli e diritti di opzione",
        ["72"] = "Accredito per utilizzo di credito documentario da estero",
        ["74"] = "Valori bollati",
        ["78"] = "Versamento di contante o valori assimilati",
        ["79"] = "Disposizione di giro conto (da/a altra banca)",
        ["80"] = "Acquisto/Vendita a termine titoli",
        ["81"] = "Accensione riporto titoli",
        ["82"] = "Estinzione riporto titoli",
        ["83"] = "Sottoscrizione titoli e/o fondi comuni",
        ["84"] = "Titoli scaduti o estratti",
        ["85"] = "Aumento di capitale o operazioni societarie",
        ["86"] = "Finanziamenti import",
        ["87"] = "Anticipazioni export",
        ["88"] = "Valore ammesso per accordo tra le parti",
        ["90"] = "Pagamento o incasso di rimesse documentate da o per l'estero",
        ["91"] = "Prelevamento a mezzo sportello automatico di altra banca",
        ["92"] = "Costo libretto assegni",
        ["93"] = "Valore ammesso per accordo tra le parti",
        ["94"] = "Commissioni e spese su operazioni in titoli",
        ["95"] = "Commissioni e spese su operazioni con l'estero",
        ["96"] = "Rettifica importo",
        ["99"] = "Accredito per rimborsi imposte \"conto fiscale\"",
        ["Y1"] = "Anticipazioni su fatture Italia",
        ["Z1"] = "Disposizioni di giro di cash pooling",
        ["Z2"] = "Versamento di assegni bancari",
        ["Z3"] = "Versamento di assegni circolari emessi da altre banche",
        ["Z4"] = "Versamento di assegni postali non standardizzati",
        ["Z5"] = "Versamento indiretto",
        ["Z6"] = "Prelevamento a valere su credito semplice",
        ["Z7"] = "Accredito SDD",
        ["Z8"] = "Accredito MAV",
        ["Z9"] = "Insoluto/storno SDD",
        ["ZA"] = "Insoluto MAV",
        ["ZB"] = "Incasso certificati conformità",
        ["ZC"] = "Pagamento per fornitura elettrica",
        ["ZD"] = "Pagamento per servizio telefonico",
        ["ZE"] = "Pagamento per servizi acqua/gas",
        ["ZF"] = "Pagamento per operazioni su prodotti derivati",
        ["ZG"] = "Accredito per operazioni su prodotti derivati",
        ["ZH"] = "Rimborso titoli e/o fondi comuni",
        ["ZI"] = "Bonifico dall'estero",
        ["ZL"] = "Bonifico sull'estero",
        ["ZM"] = "Sconto effetti sull'estero",
        ["ZN"] = "Negoziazione assegni sull'estero",
        ["ZP"] = "Commissioni e spese su fideiussioni",
        ["ZQ"] = "Commissioni e spese su crediti documentari",
        ["ZR"] = "Penali",
        ["ZS"] = "Erogazione prestiti personali e finanziamenti diversi",
        ["ZT"] = "Pagamento/incasso bollettino bancario",
        ["ZU"] = "Bonifico per previdenza complementare",
        ["ZX"] = "Bonifico oggetto di oneri deducibili o detrazioni di imposta"
    };

    /// <summary>
    /// Il gruppo proposto per una causale che non ha un gruppo predefinito (per esempio di un'altra banca),
    /// in base alla sua causale CBI. I gruppi predefiniti a 4 cifre (DatabaseService) vengono prima: la
    /// causale 31 della BCC, per esempio, è un addebito SDD e non "effetti ritirati" come nell'elenco CBI.
    /// Le causali che possono essere sia entrate sia uscite di natura diversa (storni, rettifiche) restano
    /// senza gruppo: le sceglie l'utente.
    /// </summary>
    public static (string Gruppo, TipoGruppo Tipo, bool Ricorrente)? GruppoDiRipiego(string causale) =>
        Di(causale) is { } cbi && Ripiego.TryGetValue(cbi, out var gruppo) ? gruppo : null;

    private static readonly Dictionary<string, (string, TipoGruppo, bool)> Ripiego = new()
    {
        ["05"] = ("Prelievi contanti", TipoGruppo.Ordinario, false),
        ["52"] = ("Prelievi contanti", TipoGruppo.Ordinario, false),
        ["91"] = ("Prelievi contanti", TipoGruppo.Ordinario, false),
        ["11"] = ("Utenze e servizi", TipoGruppo.Ordinario, true),
        ["ZC"] = ("Utenze e servizi", TipoGruppo.Ordinario, true),
        ["ZD"] = ("Utenze e servizi", TipoGruppo.Ordinario, true),
        ["ZE"] = ("Utenze e servizi", TipoGruppo.Ordinario, true),
        ["13"] = ("Assegni", TipoGruppo.Ordinario, false),
        ["14"] = ("Rendite finanziarie", TipoGruppo.Ordinario, false),
        ["18"] = ("Rendite finanziarie", TipoGruppo.Ordinario, false),
        ["15"] = ("Mutuo", TipoGruppo.Ordinario, true),
        ["16"] = ("Spese bancarie", TipoGruppo.Ordinario, false),
        ["20"] = ("Spese bancarie", TipoGruppo.Ordinario, true),
        ["22"] = ("Spese bancarie", TipoGruppo.Ordinario, true),
        ["66"] = ("Spese bancarie", TipoGruppo.Ordinario, false),
        ["92"] = ("Spese bancarie", TipoGruppo.Ordinario, false),
        ["94"] = ("Spese bancarie", TipoGruppo.Ordinario, false),
        ["95"] = ("Spese bancarie", TipoGruppo.Ordinario, false),
        ["17"] = ("Affitti e assicurazioni", TipoGruppo.Ordinario, true),
        ["19"] = ("Imposte e tasse", TipoGruppo.Ordinario, false),
        ["74"] = ("Imposte e tasse", TipoGruppo.Ordinario, false),
        ["21"] = ("Contributi previdenziali", TipoGruppo.Ordinario, true),
        ["26"] = ("Bonifici effettuati", TipoGruppo.Ordinario, false),
        ["ZL"] = ("Bonifici effettuati", TipoGruppo.Ordinario, false),
        ["48"] = ("Bonifici ricevuti", TipoGruppo.Ordinario, false),
        ["ZI"] = ("Bonifici ricevuti", TipoGruppo.Ordinario, false),
        ["ZX"] = ("Lavori casa con detrazione", TipoGruppo.Ordinario, false),
        ["27"] = ("Stipendio", TipoGruppo.Ordinario, true),
        ["34"] = ("Giroconti", TipoGruppo.Giroconto, false),
        ["79"] = ("Giroconti", TipoGruppo.Giroconto, false),
        ["Z1"] = ("Giroconti", TipoGruppo.Giroconto, false),
        ["41"] = ("Carta di debito", TipoGruppo.Ordinario, false),
        ["43"] = ("Carta di debito", TipoGruppo.Ordinario, false),
        ["45"] = ("Carta di credito", TipoGruppo.Ordinario, false),
        ["50"] = ("Pagamenti diversi", TipoGruppo.Ordinario, false),
        ["78"] = ("Versamenti contanti", TipoGruppo.Ordinario, false),
        ["Z2"] = ("Versamenti di assegni", TipoGruppo.Ordinario, false),
        ["70"] = ("Investimenti", TipoGruppo.Investimento, false),
        ["83"] = ("Investimenti", TipoGruppo.Investimento, false),
        ["84"] = ("Investimenti", TipoGruppo.Investimento, false),
        ["ZH"] = ("Investimenti", TipoGruppo.Investimento, false),
        ["ZU"] = ("Previdenza complementare", TipoGruppo.Investimento, true)
    };
}
