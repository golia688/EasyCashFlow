namespace EasyCashFlow.Models;

/// <summary>Una riga della tabella Importazioni.</summary>
public record ImportazioneRegistrata(
    string NomeFile,
    DateTime ImportatoIl,
    int Letti,
    int Inseriti,
    int GiaPresenti,
    int Scartati,           // tutti i non caricati, compresi i già presenti
    DateOnly? Dal,          // prima e ultima giornata presenti nel file
    DateOnly? Al,
    int Avvisi);

/// <summary>I dati di un conto, mostrati prima di eliminarlo.</summary>
public record DatiConto(
    int Movimenti,
    (DateOnly Prima, DateOnly Ultima)? Intervallo,
    int Giornate,           // giornate di saldo salvate
    int Scartati,           // righe in MovimentiScartati
    int Straordinari);      // movimenti segnati come straordinari o con il tipo scelto a mano

/// <summary>I numeri del database mostrati nella Home.</summary>
public record StatisticheDatabase(
    int Movimenti,
    (DateOnly Prima, DateOnly Ultima)? Intervallo,
    int Conti,
    int Causali,                // causali CBI dei movimenti (le prime due cifre), senza la fittizia dei file Excel
    int CausaliSenzaGruppo,     // causali CBI dei movimenti senza gruppo, senza la fittizia
    int Gruppi,                 // gruppi usati dalle causali dei movimenti
    int GruppiInvestimento,
    int GiornateSaldo,
    int Scartati,
    int Importazioni,           // registrate (e recuperate dal log), anche ripetute sullo stesso file
    int FileImportati,          // nomi di file diversi
    ImportazioneRegistrata? UltimaImportazione,
    int MovimentiCausaleFittizia = 0);   // movimenti Excel di cui il programma non ha riconosciuto il tipo (00XL)
