using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace EasyCashFlow.Services;

/// <summary>
/// La chiave del database (docs/07-sicurezza.md, sezioni 3-5). Il database si cifra con una chiave
/// casuale di 256 bit, la "chiave dei dati". Questa si salva in dati\chiave.json cifrata due volte con
/// AES-256-GCM: con una chiave ricavata dalla password e con una ricavata dal codice di recupero, entrambe
/// con Argon2id. Così cambiare password vuol dire ricifrare solo la chiave dei dati, e una password
/// sbagliata si riconosce subito, perché la decifratura viene rifiutata.
///
/// Le operazioni con Argon2 durano circa mezzo secondo: dall'interfaccia vanno chiamate fuori dal thread
/// dell'interfaccia (Task.Run).
/// </summary>
public class ChiaviService
{
    /// <summary>Lunghezza minima della password: una frase, non un PIN (sezione 3).</summary>
    public const int LunghezzaMinimaPassword = 10;

    // Parametri di Argon2id per le cifrature nuove: circa mezzo secondo sul PC di sviluppo (prototipo
    // del 02/11). Ogni cifratura salva i suoi, quindi si possono aumentare senza rompere i file esistenti.
    private static readonly ParametriKdf ParametriAttuali = new("argon2id", MemoriaKiB: 65536, Passaggi: 3, Parallelismo: 1);

    private const int Versione = 1;
    private const string Formato = "sqlcipher4";

    // codice di recupero: 30 caratteri senza quelli che si confondono (0/O, 1/I/L), in gruppi da 5
    private const string AlfabetoRecupero = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int LunghezzaRecupero = 30;

    private readonly LogService _log;
    private byte[]? _chiaveDati;

    public ChiaviService(LogService log)
    {
        _log = log;
    }

    /// <summary>C'è il file della chiave: il database è (o sarà) cifrato e va sbloccato.</summary>
    public bool FileEsiste => File.Exists(PercorsiDati.Chiave);

    public bool Sbloccato => _chiaveDati is not null;

    /// <summary>La chiave dei dati, finché il programma è sbloccato. Non va mai scritta da nessuna parte.</summary>
    public byte[] ChiaveDati => _chiaveDati ?? throw new InvalidOperationException("La chiave del database non è disponibile: il programma è bloccato.");

    /// <summary>Primo avvio o migrazione: crea la chiave dei dati e il file che la protegge.
    /// Restituisce il codice di recupero, da mostrare una volta sola; il programma resta sbloccato.</summary>
    public string Crea(string password)
    {
        ControllaPassword(password);
        if (FileEsiste) throw new InvalidOperationException("Il file della chiave esiste già: non si sovrascrive.");

        var chiave = RandomNumberGenerator.GetBytes(32);
        var codice = GeneraCodiceRecupero();
        var file = new FileChiave(Versione, Formato,
            Password: Cifra(chiave, password, "password"),
            Recupero: Cifra(chiave, Normalizza(codice), "recupero"),
            Creato: DateTime.Now.ToString("yyyy-MM-dd"));
        Salva(file);

        _chiaveDati = chiave;
        _log.Info("Chiave del database creata, con password e codice di recupero");
        return codice;
    }

    /// <summary>Sblocca con la password. Falso se è sbagliata.</summary>
    public bool Sblocca(string password)
    {
        var file = Leggi();
        var chiave = Decifra(file.Password, password, "password");
        if (chiave is null)
        {
            _log.Avviso("Sblocco non riuscito: password sbagliata");
            return false;
        }

        _chiaveDati = chiave;
        _log.Info("Database sbloccato con la password");

        // parametri di Argon2 più vecchi di quelli attuali: si ricifra con quelli nuovi, ora che si può
        if (file.Password.Kdf != ParametriAttuali)
        {
            Salva(file with { Password = Cifra(chiave, password, "password") });
            _log.Info($"Protezione della password aggiornata ai parametri attuali di Argon2 ({ParametriAttuali.MemoriaKiB / 1024} MiB, {ParametriAttuali.Passaggi} passaggi)");
        }
        return true;
    }

    /// <summary>Sblocca con il codice di recupero (password dimenticata). Falso se è sbagliato.
    /// Dopo, l'interfaccia deve far scegliere subito una password nuova (<see cref="CambiaPassword"/>)
    /// e mostrare il codice nuovo (<see cref="NuovoCodiceRecupero"/>): quello usato non vale più.</summary>
    public bool SbloccaConCodice(string codice)
    {
        var chiave = Decifra(Leggi().Recupero, Normalizza(codice), "recupero");
        if (chiave is null)
        {
            _log.Avviso("Sblocco non riuscito: codice di recupero sbagliato");
            return false;
        }

        _chiaveDati = chiave;
        _log.Info("Database sbloccato con il codice di recupero");
        return true;
    }

    /// <summary>Nuova password, con un salt nuovo. Il database non si tocca: cambia solo il file della chiave.</summary>
    public void CambiaPassword(string nuova)
    {
        ControllaPassword(nuova);
        var file = Leggi();
        Salva(file with { Password = Cifra(ChiaveDati, nuova, "password") });
        _log.Info("Password cambiata");
    }

    /// <summary>Vero se la password è quella attuale. Serve a confermare un'operazione delicata a programma
    /// già sbloccato (per esempio un nuovo codice di recupero): non cambia niente.</summary>
    public bool PasswordGiusta(string password)
    {
        var chiave = Decifra(Leggi().Password, password, "password");
        if (chiave is null) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(chiave, ChiaveDati);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chiave);
        }
    }

    /// <summary>Un codice di recupero nuovo: quello vecchio smette di valere. Va mostrato una volta sola.</summary>
    public string NuovoCodiceRecupero()
    {
        var file = Leggi();
        var codice = GeneraCodiceRecupero();
        Salva(file with { Recupero = Cifra(ChiaveDati, Normalizza(codice), "recupero") });
        _log.Info("Nuovo codice di recupero generato: il precedente non vale più");
        return codice;
    }

    /// <summary>Dimentica la chiave (chiusura, blocco per inattività).</summary>
    public void Blocca()
    {
        if (_chiaveDati is null) return;
        CryptographicOperations.ZeroMemory(_chiaveDati);
        _chiaveDati = null;
        _log.Info("Chiave del database tolta dalla memoria");
    }

    public static bool PasswordAccettabile(string? password) =>
        password is not null && password.Trim().Length >= LunghezzaMinimaPassword;

    private static void ControllaPassword(string password)
    {
        if (!PasswordAccettabile(password))
            throw new ArgumentException($"La password deve avere almeno {LunghezzaMinimaPassword} caratteri.");
    }

    // ---- codice di recupero ----

    private static string GeneraCodiceRecupero()
    {
        var caratteri = RandomNumberGenerator.GetItems<char>(AlfabetoRecupero, LunghezzaRecupero);
        return string.Join("-", Enumerable.Range(0, LunghezzaRecupero / 5).Select(i => new string(caratteri, i * 5, 5)));
    }

    // si accetta il codice scritto con minuscole, spazi o senza trattini
    private static string Normalizza(string codice) =>
        new(codice.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    // ---- cifratura della chiave dei dati ----

    // I dati associati legano la cifratura a versione, formato, tipo di accesso e parametri di Argon2:
    // se qualcuno abbassa i parametri nel file (per indovinare la password più in fretta), la decifratura fallisce.
    private static byte[] DatiAssociati(string accesso, ParametriKdf k) =>
        Encoding.UTF8.GetBytes($"v{Versione}|{Formato}|{accesso}|{k.Algoritmo}|{k.MemoriaKiB}|{k.Passaggi}|{k.Parallelismo}");

    private static ChiaveCifrata Cifra(byte[] chiaveDati, string segreto, string accesso)
    {
        var k = ParametriAttuali;
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var protezione = Deriva(segreto, salt, k);
        try
        {
            var cifrato = new byte[chiaveDati.Length + 16];     // in fondo il tag di GCM
            using var aes = new AesGcm(protezione, 16);
            aes.Encrypt(nonce, chiaveDati, cifrato.AsSpan(0, chiaveDati.Length), cifrato.AsSpan(chiaveDati.Length),
                        DatiAssociati(accesso, k));
            return new ChiaveCifrata(k, Convert.ToBase64String(salt), Convert.ToBase64String(nonce), Convert.ToBase64String(cifrato));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protezione);
        }
    }

    private static byte[]? Decifra(ChiaveCifrata c, string segreto, string accesso)
    {
        if (c.Kdf.Algoritmo != "argon2id") throw new InvalidDataException($"Algoritmo non riconosciuto nel file della chiave: {c.Kdf.Algoritmo}");

        var cifrato = Convert.FromBase64String(c.Chiave);
        var protezione = Deriva(segreto, Convert.FromBase64String(c.Salt), c.Kdf);
        try
        {
            var chiave = new byte[cifrato.Length - 16];
            using var aes = new AesGcm(protezione, 16);
            aes.Decrypt(Convert.FromBase64String(c.Nonce), cifrato.AsSpan(0, chiave.Length), cifrato.AsSpan(chiave.Length),
                        chiave, DatiAssociati(accesso, c.Kdf));
            return chiave;
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;    // segreto sbagliato, oppure file modificato
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protezione);
        }
    }

    private static byte[] Deriva(string segreto, byte[] salt, ParametriKdf k)
    {
        var byteSegreto = Encoding.UTF8.GetBytes(segreto);
        try
        {
            using var argon = new Argon2id(byteSegreto)
            {
                Salt = salt, MemorySize = k.MemoriaKiB, Iterations = k.Passaggi, DegreeOfParallelism = k.Parallelismo
            };
            return argon.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(byteSegreto);
        }
    }

    // ---- file ----

    private static readonly JsonSerializerOptions OpzioniJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static FileChiave Leggi()
    {
        if (!File.Exists(PercorsiDati.Chiave))
            throw new FileNotFoundException("Manca il file della chiave del database (dati\\chiave.json).", PercorsiDati.Chiave);
        var file = JsonSerializer.Deserialize<FileChiave>(File.ReadAllText(PercorsiDati.Chiave), OpzioniJson)
                   ?? throw new InvalidDataException("Il file della chiave del database è vuoto.");
        if (file.Versione != Versione || file.FormatoDatabase != Formato)
            throw new InvalidDataException($"File della chiave non riconosciuto (versione {file.Versione}, formato {file.FormatoDatabase}).");
        return file;
    }

    private static void Salva(FileChiave file) =>
        PercorsiDati.ScriviAtomico(PercorsiDati.Chiave, JsonSerializer.Serialize(file, OpzioniJson));

    // il contenuto di dati\chiave.json; niente di segreto in chiaro (salt, nonce e parametri si possono leggere)
    private record ParametriKdf(string Algoritmo, int MemoriaKiB, int Passaggi, int Parallelismo);
    private record ChiaveCifrata(ParametriKdf Kdf, string Salt, string Nonce, string Chiave);
    private record FileChiave(int Versione, string FormatoDatabase, ChiaveCifrata Password, ChiaveCifrata Recupero, string Creato);
}
