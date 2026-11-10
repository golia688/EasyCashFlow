namespace EasyCashFlow.Models;

public record MovimentoCbi(
    string Conto,
    DateOnly DataContabile,
    DateOnly DataValuta,
    decimal Importo,            // negativo = addebito, positivo = accredito
    string Causale,
    string Riferimento,
    string DescrizioneBreve,
    string DescrizioneEstesa,
    int RigaFile = 0,           // riga nel file di origine (0 se letto dal database)
    bool Straordinario = false, // segnato dall'utente: non si ripeterà
    TipoGruppo? TipoForzato = null)   // tipo scelto dall'utente al posto di quello del gruppo
{
    public ChiaveMovimento Chiave => new(Conto, DataContabile, Riferimento, Importo);

    // Due movimenti sono lo stesso movimento se hanno la stessa chiave primaria: così una riga selezionata
    // resta selezionata anche dopo averla segnata come straordinaria (riletta dal database è "diversa").
    public virtual bool Equals(MovimentoCbi? altro) => altro is not null && Chiave == altro.Chiave;
    public override int GetHashCode() => Chiave.GetHashCode();
}

/// <summary>La chiave primaria di un movimento (tabella Movimenti).</summary>
public record ChiaveMovimento(string Conto, DateOnly DataContabile, string Riferimento, decimal Importo)
{
    public override string ToString() => $"{Conto} {DataContabile:dd/MM/yyyy} {Riferimento} {Importo:N2}";
}

public record SaldoGiornaliero(string Conto, DateOnly Data, decimal SaldoIniziale, decimal SaldoFinale);

// Un conto corrente. Codice = ABI + CAB + numero di conto, lo stesso valore di MovimentoCbi.Conto.
// Iban è vuoto se non è stato possibile ricavarlo dal file; Nome è scelto dall'utente.
/// <summary>Con che tipo di file si importano i movimenti di un conto (dal 10/11): lo decide la prima
/// importazione, e poi resta quello. CBI vale per il file di testo e per l'XML, che danno gli stessi movimenti
/// con gli stessi riferimenti; Excel ha riferimenti costruiti dal programma, e mescolarli duplicherebbe i
/// movimenti. Misto: un conto che ha già movimenti di tutti e due (importati prima del 10/11).</summary>
public enum FormatoConto { Nessuno, Cbi, Excel, Misto }

public static class FormatiConto
{
    public static string Nome(FormatoConto formato) => formato switch
    {
        FormatoConto.Cbi => "CBI (testo o XML)",
        FormatoConto.Excel => "Excel",
        FormatoConto.Misto => "CBI ed Excel insieme",
        _ => "nessun movimento"
    };
}

public record ContoCorrente(string Codice, string Iban, string Abi, string Divisa, string Nome)
{
    // Nome assegnato alla registrazione, finché l'utente non ne sceglie uno.
    public static string NomeProvvisorio(string codice) =>
        codice.Length >= 9 ? $"ABI {codice[..5]} - c/c ...{codice[^4..]}" : codice;
}

public enum MotivoScarto
{
    GiaPresente,        // stessa chiave e stesso contenuto di un movimento già salvato
    ContenutoDiverso,   // stessa chiave di un movimento già salvato, ma contenuto diverso
    RigaNonValida,      // riga del file non interpretabile
    ErroreDatabase      // errore del database durante il salvataggio
}

public record RecordScartato(
    MotivoScarto Motivo,
    string Dettaglio,
    int? RigaFile = null,
    string? RigaOriginale = null,
    MovimentoCbi? Movimento = null);

// Una riga della tabella MovimentiScartati, con il movimento che ha la stessa chiave nel database
// (null se nel frattempo è stato eliminato).
public record MovimentoScartato(
    long Id,
    string Motivo,
    string NomeFile,
    int RigaFile,
    DateTime ScartatoIl,
    MovimentoCbi Scartato,
    MovimentoCbi? NelDatabase);

public record RisultatoImportCbi(
    IReadOnlyList<ContoCorrente> Conti,
    IReadOnlyList<MovimentoCbi> Movimenti,
    IReadOnlyList<SaldoGiornaliero> Saldi,
    IReadOnlyList<RecordScartato> Scartati,
    IReadOnlyList<string> Avvisi);

public record RisultatoInserimento(
    int Inseriti,
    IReadOnlyList<RecordScartato> Scartati,
    int DescrizioniAggiornate = 0);    // già presenti di cui si è aggiornata la descrizione

public record EsitoImportazione(
    string NomeFile,
    int Letti,
    int Inseriti,
    int GiaPresenti,    // scartati perché identici a movimenti già salvati
    int Scartati,       // tutti i record non caricati, per qualsiasi motivo (include i già presenti)
    IReadOnlyList<string> Avvisi,
    string? FileLog,
    string? Errore,
    // per il riepilogo della finestra di importazione
    int Giornate = 0,                   // rendicontazioni chiuse dal saldo finale (giornate di un conto)
    DateOnly? Dal = null,               // prima e ultima giornata del file
    DateOnly? Al = null,
    int Conti = 0,
    int DescrizioniAggiornate = 0,      // già presenti di cui si è aggiornata la descrizione
    int ContenutoDiverso = 0,           // stessa chiave, contenuto diverso: in MovimentiScartati
    int RigheNonValide = 0);            // righe del file che il lettore non ha potuto interpretare