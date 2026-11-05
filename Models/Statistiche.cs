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

/// <summary>I numeri del database mostrati nella Home.</summary>
public record StatisticheDatabase(
    int Movimenti,
    (DateOnly Prima, DateOnly Ultima)? Intervallo,
    int Conti,
    int Causali,
    int CausaliSenzaGruppo,
    int Gruppi,                 // gruppi usati dalle causali dei movimenti
    int GruppiInvestimento,
    int GiornateSaldo,
    int Scartati,
    int Importazioni,           // registrate (e recuperate dal log), anche ripetute sullo stesso file
    int FileImportati,          // nomi di file diversi
    ImportazioneRegistrata? UltimaImportazione);
