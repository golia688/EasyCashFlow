using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace EasyCashFlow.Services;

/// <summary>Esito di un backup: lo zip creato, quanto pesa e quanti movimenti contiene la copia verificata;
/// oppure l'errore.</summary>
public record EsitoBackup(string? Percorso, long Byte, int Movimenti, string? Errore);

/// <summary>
/// Impostazioni > Dati > Backup: uno zip con il database cifrato, chiave.json e preferenze.json, nella
/// cartella scelta dall'utente. La copia si apre con la password (e il codice di recupero) di adesso: il
/// database non si ricifra. Dopo averlo scritto, lo zip si rilegge: il database contenuto deve aprirsi con
/// la chiave attuale e avere gli stessi movimenti, e chiave.json deve essere uguale a quello in uso.
/// Il ripristino resta manuale (manuale, "I tuoi dati").
/// </summary>
public class BackupService
{
    private readonly DatabaseService _database;
    private readonly PreferenzeService _preferenze;
    private readonly LogService _log;

    public BackupService(DatabaseService database, PreferenzeService preferenze, LogService log)
    {
        _database = database;
        _preferenze = preferenze;
        _log = log;
    }

    /// <summary>Il nome dello zip per quel momento: EasyCashFlow-backup-2026-11-04-1530.zip.</summary>
    public static string NomeFile(DateTime quando) => $"EasyCashFlow-backup-{quando:yyyy-MM-dd-HHmm}.zip";

    public async Task<EsitoBackup> CreaAsync(string cartella)
    {
        var quando = DateTime.Now;
        var percorso = Path.Combine(cartella, NomeFile(quando));
        for (var n = 2; File.Exists(percorso); n++)       // due backup nello stesso minuto
            percorso = Path.Combine(cartella, Path.GetFileNameWithoutExtension(NomeFile(quando)) + $"-{n}.zip");
        var parziale = percorso + ".parziale";
        _log.Info($"Backup avviato: {Mascherato.Percorso(percorso)}");

        try
        {
            var movimenti = _database.Statistiche().Movimenti;

            // a connessioni chiuse il file del database è completo e si può copiare
            _database.RilasciaFile();
            await Task.Run(() =>
            {
                using (var zip = ZipFile.Open(parziale, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(PercorsiDati.Database, Path.GetFileName(PercorsiDati.Database));
                    zip.CreateEntryFromFile(PercorsiDati.Chiave, Path.GetFileName(PercorsiDati.Chiave));
                    if (File.Exists(PercorsiDati.Preferenze))
                        zip.CreateEntryFromFile(PercorsiDati.Preferenze, Path.GetFileName(PercorsiDati.Preferenze));
                }
                Verifica(parziale, movimenti);
                File.Move(parziale, percorso);
            });

            var byte_ = new FileInfo(percorso).Length;
            _preferenze.RegistraBackup(cartella, quando);
            _log.Info($"Backup riuscito e verificato: {movimenti} movimenti, {byte_ / 1024} KB");
            return new EsitoBackup(percorso, byte_, movimenti, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or SqliteException or InvalidOperationException)
        {
            _log.Errore("Backup non riuscito", ex);
            try { File.Delete(parziale); } catch (IOException) { }
            return new EsitoBackup(null, 0, 0, ex.Message);
        }
    }

    // Lo zip appena scritto si rilegge da capo: il database deve aprirsi con la chiave attuale e avere gli
    // stessi movimenti, e chiave.json deve essere quello in uso (senza, il database non si riaprirebbe).
    private void Verifica(string zipPercorso, int movimentiAttesi)
    {
        var temporaneo = Path.Combine(Path.GetTempPath(), $"easycashflow-verifica-{Guid.NewGuid():N}.db");
        try
        {
            using (var zip = ZipFile.OpenRead(zipPercorso))
            {
                var db = zip.GetEntry(Path.GetFileName(PercorsiDati.Database))
                         ?? throw new InvalidDataException("nel backup manca il database");
                db.ExtractToFile(temporaneo);

                var chiave = zip.GetEntry(Path.GetFileName(PercorsiDati.Chiave))
                             ?? throw new InvalidDataException("nel backup manca chiave.json");
                using var lettore = new StreamReader(chiave.Open());
                if (lettore.ReadToEnd() != File.ReadAllText(PercorsiDati.Chiave))
                    throw new InvalidDataException("chiave.json nel backup è diverso da quello in uso");
            }

            var movimenti = _database.ContaMovimentiNellaCopia(temporaneo);
            if (movimenti != movimentiAttesi)
                throw new InvalidDataException($"la copia ha {movimenti} movimenti invece di {movimentiAttesi}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(temporaneo); } catch (IOException) { }
        }
    }
}
