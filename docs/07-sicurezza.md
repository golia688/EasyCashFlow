# 07 - Sicurezza dei dati: database cifrato e log mascherati

Questo documento è il **progetto** della protezione dei dati: cosa si vuole ottenere, le scelte fatte e perché, come cambiano avvio, database e log, in che ordine si lavora e come si verifica. Il codice non è ancora scritto: quando una parte sarà fatta, la sezione corrispondente verrà aggiornata con quello che è stato realizzato davvero.

Scelte confermate il 02/11:

| Tema | Scelta |
|---|---|
| Libreria | **SQLite3 Multiple Ciphers** (SQLite3MC), gratuita, con il database nel **formato SQLCipher 4** |
| Accesso | **master password** (una frase, con lunghezza minima), non un PIN |
| Derivazione della chiave | **Argon2id**, con salt casuale salvato in chiaro |
| Chiave del database | **casuale**, protetta dalla password (cifratura a busta) |
| Password dimenticata | **codice di recupero** mostrato una sola volta |
| Log | dati dei movimenti **mascherati**; in chiaro solo ciò che serve per il debug |
| Blocco per inattività | dopo 15 minuti (si sceglie: mai, 5, 10, 15, 30, 60), sezione 9 |

## 1. Da cosa ci si protegge

Oggi `dati\easycashflow.db` e i file in `log` contengono tutti i dettagli dei movimenti (importi, descrizioni, IBAN, controparti) senza password né cifratura. Chiunque abbia la cartella li legge.

**Dopo questo lavoro i dati sono protetti se qualcuno ottiene i file senza la password:**
- cartella copiata da un altro utente o da un collega;
- chiavetta persa, computer rubato o portato in assistenza;
- cartella sincronizzata su un cloud condiviso;
- copia di sicurezza finita nel posto sbagliato (anche le copie sono cifrate, perché si copia il file cifrato).

**Restano fuori, e il manuale lo dirà:**
- il programma **aperto e sbloccato**: chi usa il computer in quel momento vede tutto;
- un **virus** sul computer, che può leggere la memoria o registrare la tastiera;
- i **file CBI scaricati** dalla banca, che sono in chiaro e restano dove l'utente li ha salvati;
- una **password debole**: la cifratura vale quanto la password (sezione 3).

## 2. La libreria: SQLite3MC

### Perché non "SQLCipher" e basta

`Microsoft.Data.Sqlite` non cifra: usa un motore SQLite nativo fornito da **SQLitePCLRaw**. Fino alla versione 2 esisteva un pacchetto gratuito con SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`), ma con la versione 3 l'autore ha smesso di distribuire gratis le versioni con cifratura e quel pacchetto è deprecato. Le alternative indicate sono:

- **SQLCipher di Zetetic** (l'originale): per .NET è commerciale;
- **SQLite3 Multiple Ciphers** (SQLite3MC): open source (licenza MIT), mantenuto, con il pacchetto NuGet `SQLite3MC.PCLRaw.bundle`.

Si sceglie **SQLite3MC**. Supporta diversi cifrari; si usa il suo **schema compatibile con SQLCipher 4** (AES-256 con HMAC-SHA512 per pagina), così:
- il formato è documentato e collaudato;
- il file si può aprire anche con gli strumenti SQLCipher (per esempio DB Browser for SQLite), utile per un controllo o un recupero a mano;
- se un giorno si passasse a SQLCipher di Zetetic, il database resterebbe leggibile.

### Cosa cambia nel progetto

- Nel `.csproj`: `Microsoft.Data.Sqlite.Core` 10.0.13 (senza il motore SQLite predefinito) più `SQLite3MC.PCLRaw.bundle` 2.6.1, al posto di `Microsoft.Data.Sqlite`. Il bundle porta SQLitePCLRaw 3.0.2, che `Microsoft.Data.Sqlite.Core` 10 accetta.
- All'avvio, prima di aprire qualunque connessione: `SQLitePCL.Batteries_V2.Init()`.
- Ogni connessione, appena aperta e prima di qualunque altra istruzione, riceve:
  ```sql
  PRAGMA cipher = 'sqlcipher';
  PRAGMA legacy = 4;
  PRAGMA key = "x'<64 cifre esadecimali>'";
  ```
  Lo fa `DatabaseService.Apri()`, che già apre tutte le connessioni. **Non** si usa la parola chiave `Password` della stringa di connessione: passerebbe la chiave come frase e SQLCipher rifarebbe la sua derivazione (vedi sotto).
- Il codice SQL di `DatabaseService` non cambia: le pagine del file sono cifrate e decifrate dal motore, in modo trasparente.

### Esito del prototipo (fase 1, 02/11)

Programma di prova separato (`provasicurezza`, nella cartella temporanea di lavoro, non nel progetto) su **copie** di due database reali: quello di `bin\Debug` (322 movimenti, 8 tabelle) e quello di `bin\Release` (563 movimenti, 2 conti, 7 tabelle). **Tutte le verifiche superate**, anche con il programma pubblicato in modo autonomo per win-x64.

| Verifica | Risultato |
|---|---|
| Motore | SQLite 3.54.0, SQLite3 Multiple Ciphers 2.6.1, caricato da `Microsoft.Data.Sqlite.Core` 10.0.13 |
| Database nuovo cifrato | il testo inserito e l'intestazione `SQLite format 3` non si trovano nel file; si riapre con la chiave giusta; con una chiave sbagliata o senza chiave non si apre ("file is not a database") |
| Chiave grezza `x'…'` | apertura e prima query in **0,6 ms**; con una frase (PBKDF2 interno di SQLCipher 4) **137 ms** a ogni apertura. La chiave grezza salta davvero la derivazione |
| Cifratura di una copia del database | **copia del file + `PRAGMA rekey`** sulla copia: 15-22 ms; stessi conteggi in tutte le tabelle, `integrity_check` ok, nessuna delle descrizioni e degli IBAN campione (20-28 testi leggibili nel file in chiaro) si trova nel file cifrato |
| `VACUUM INTO` con `hexkey` nell'URI | crea un file cifrato, ma **non** si apre con la chiave grezza: il parametro viene trattato come frase in esadecimale. Scartato |
| Prestazioni | un'operazione con la connessione presa dal pool 0,16-0,19 ms; lettura di tutti i movimenti 0,4-0,6 ms cifrato contro 0,6-0,7 ms in chiaro. Nessuna differenza percepibile |
| Argon2id (`Konscious.Security.Cryptography.Argon2` 1.3.1) | risultato identico al **vettore di prova di RFC 9106**; con 1 thread: 64 MiB e 3 passaggi **490 ms**, 64 MiB e 2 passaggi 261 ms, 128 MiB 789 ms, 256 MiB 1.680 ms |
| AES-256-GCM (`AesGcm` di .NET) | con la password giusta si riottiene la chiave dei dati; con una password sbagliata o con i parametri di Argon2 modificati nel file la decifratura è rifiutata |
| Pubblicazione autonoma win-x64 | nella cartella ci sono `sqlite3mc.dll` (2,4 MB, nativa), `SQLite3MC.PCLRaw.provider.dll`, `SQLitePCLRaw.batteries_v2.dll`, `SQLitePCLRaw.core.dll`; il programma pubblicato supera le stesse verifiche |

Conseguenze per il progetto:
- **Libreria confermata**: SQLite3MC 2.6.1 con `Microsoft.Data.Sqlite.Core`.
- **Argon2 confermato**: `Konscious.Security.Cryptography.Argon2`, C# puro (nessuna DLL nativa in più), con i parametri iniziali 64 MiB e 3 passaggi (circa mezzo secondo).
- **Migrazione**: si usa copia del file + `PRAGMA rekey` (sezione 7), non `sqlcipher_export` né `VACUUM INTO`. Il database usa `journal_mode = delete`, condizione richiesta da `rekey`, che con WAL non funziona.
- **Pool delle connessioni**: una connessione nel pool tiene il file aperto (lo si è visto nel prototipo: il file restava bloccato finché non si chiamava `SqliteConnection.ClearAllPools()`). Va chiamato prima di sostituire, copiare o cancellare il file, e al blocco.

**Non ancora verificato**: che il file si apra con uno strumento SQLCipher esterno (per esempio DB Browser for SQLite nella versione con SQLCipher, scegliendo "SQLCipher 4 defaults" e chiave grezza). È una comodità per il recupero a mano, non un requisito: si può provare quando servirà.

## 3. La password

### Frase, non PIN

Chi ha copiato la cartella può provare le password **sul file, sul proprio computer, senza limiti di tentativi** e senza che il programma possa impedirlo. Un PIN di 6 cifre è un milione di combinazioni: anche se ogni tentativo costa mezzo secondo, con più processori si prova tutto in poche ore. Per questo:

- **lunghezza minima 10 caratteri**, nessun altro obbligo (niente "una maiuscola, un numero, un simbolo", che porta a password corte e prevedibili);
- il programma **suggerisce una frase** di 4-5 parole ("cavallo pentola autunno lampione"), facile da ricordare e molto più robusta;
- un **indicatore di robustezza** sotto il campo (debole / discreta / buona), calcolato in modo semplice: lunghezza, varietà di caratteri, presenza in un breve elenco di password comuni;
- la password si scrive **due volte** quando la si crea o la si cambia.

### Derivazione della chiave: Argon2id

La password non diventa direttamente la chiave: passa da **Argon2id**, una funzione volutamente lenta e che usa molta memoria. Così ogni tentativo di chi prova password a caso costa tempo e memoria, e le schede video (che fanno milioni di calcoli in parallelo, ma hanno poca memoria per ciascuno) aiutano poco. PBKDF2, che SQLCipher usa internamente, è lento ma non usa memoria: Argon2id è la scelta raccomandata oggi.

- **Parametri iniziali**: 64 MiB di memoria, 3 passaggi, 1 thread, salt di 16 byte, chiave di 32 byte: **490 ms** sul PC di sviluppo (prototipo del 02/11), in linea con l'obiettivo di circa mezzo secondo. I parametri sono salvati nel file della chiave (sezione 4), quindi si possono aumentare in futuro senza rompere i database esistenti: al primo sblocco con i parametri vecchi, la chiave si ricifra con quelli nuovi.
- **Libreria**: .NET 10 non ha Argon2 tra le sue funzioni. Si usa `Konscious.Security.Cryptography.Argon2` 1.3.1 (C# puro, MIT), che nel prototipo ha dato esattamente il risultato del vettore di prova ufficiale di RFC 9106.
- **Salt**: 16 byte casuali (`RandomNumberGenerator`), generati alla creazione della password e a ogni cambio. Il salt **non è un segreto**: serve solo a far sì che la stessa password dia chiavi diverse su installazioni diverse, così non si possono preparare in anticipo tabelle di chiavi. Può stare in chiaro.

## 4. La chiave del database (cifratura a busta)

### L'idea

Il database **non** si cifra con la chiave derivata dalla password, ma con una **chiave casuale** di 256 bit generata una volta sola (la "chiave dei dati"). La chiave derivata dalla password serve solo a **cifrare la chiave dei dati**, che viene salvata così, cifrata, in un piccolo file accanto al database.

```
password ──Argon2id(salt)──► chiave della password ──AES-GCM──► chiave dei dati cifrata ─┐
                                                                                         ├─► dati\chiave.json
codice di recupero ──Argon2id(salt2)──► chiave del recupero ──AES-GCM──► … cifrata ─────┘

chiave dei dati ──PRAGMA key──► dati\easycashflow.db (formato SQLCipher 4)
```

Perché è meglio che usare direttamente la password:

- **cambiare password è istantaneo**: si ricifra solo la chiave dei dati (pochi byte), non tutto il database;
- il **codice di recupero** è solo una seconda copia cifrata della stessa chiave dei dati;
- **una password sbagliata si riconosce subito**: AES-GCM rifiuta la decifratura (il "tag" non corrisponde), prima ancora di toccare il database. Con la password passata direttamente a SQLCipher, l'errore arriverebbe solo alla prima query, come "file is not a database", indistinguibile da un file rovinato;
- **ogni connessione si apre in fretta**: `DatabaseService` apre una connessione per ogni operazione (`Apri()`). Con una password, SQLCipher rifarebbe la derivazione (PBKDF2, 256.000 iterazioni) a ogni apertura non presa dal pool; con la chiave grezza (`PRAGMA key = "x'…'"`) non c'è nessuna derivazione.

### Il file `dati\chiave.json`

Sta **nella cartella `dati`, accanto al database**, non in un `config.json` separato: così una copia della cartella `dati` contiene sempre tutto il necessario per aprire il database. Senza questo file il database non si apre più, nemmeno con la password giusta: il manuale lo dirà nella parte sulle copie di sicurezza.

```json
{
  "versione": 1,
  "formatoDatabase": "sqlcipher4",
  "password": {
    "kdf": { "algoritmo": "argon2id", "memoriaKiB": 65536, "passaggi": 3, "parallelismo": 1 },
    "salt": "…base64…",
    "nonce": "…base64…",
    "chiave": "…base64 (chiave cifrata + tag)…"
  },
  "recupero": { "kdf": { … }, "salt": "…", "nonce": "…", "chiave": "…" },
  "creato": "2026-11-02"
}
```

I parametri di Argon2 stanno **in ciascuna delle due cifrature**, non una volta sola: quando si aumentano, la cifratura con la password si aggiorna al primo sblocco, mentre quella con il codice di recupero (che il programma non conosce) resta con i suoi finché non si genera un codice nuovo.

- Cifratura della chiave dei dati: **AES-256-GCM** (`System.Security.Cryptography.AesGcm`), nonce di 12 byte casuale a ogni cifratura.
- **Dati associati** (AAD) della cifratura: versione, formato, tipo di accesso (password o recupero) e parametri di Argon2. Se qualcuno modifica il file per abbassare i parametri (per rendere più facile indovinare la password), la decifratura fallisce.
- Il file non contiene niente di segreto in chiaro: salt, nonce e parametri si possono leggere senza danno.
- Si scrive in modo **atomico**: prima `chiave.json.nuovo`, poi sostituzione (`File.Replace`), così un'interruzione a metà non lascia un file rovinato.

### La chiave in memoria

- La chiave dei dati resta in memoria finché il programma è sbloccato, in un servizio dedicato (`ChiaviService`), come `byte[]`.
- Alla chiusura (e al blocco, se ci sarà) l'array viene azzerato (`CryptographicOperations.ZeroMemory`) e il pool delle connessioni svuotato (`SqliteConnection.ClearAllPools()`).
- La password e le chiavi derivate si azzerano subito dopo l'uso. La password arriva da un campo di testo di Blazor, quindi passa comunque per delle `string` che non si possono azzerare: è un limite accettato, coerente con la sezione 1 (un virus sul computer è fuori dal perimetro).
- La chiave **non compare mai** nei log, nei messaggi di errore o nella stringa di connessione registrata.

## 5. Il codice di recupero

- Si genera insieme alla password: **30 caratteri** casuali da un alfabeto senza caratteri ambigui (niente 0/O, 1/I/L), mostrati in 6 gruppi da 5 (`K7QXM-…`), circa 150 bit.
- Si mostra **una sola volta**, con l'invito ad **annotarlo** e conservarlo in un posto sicuro, lontano dal computer. Il programma **non offre di salvarlo** né ha un pulsante per copiarlo: un file sul PC lo lascerebbe a chi riesce ad aprire i file dell'utente, cioè proprio a chi la cifratura deve fermare. Il testo resta però selezionabile e si copia con Ctrl+C, per incollarlo in un gestore di password come KeePass (con il rischio, accettato, della cronologia degli appunti di Windows). Decisione del 02/11. Per proseguire l'utente deve **riscriverne un gruppo**, per essere sicuri che l'abbia davvero annotato.
- Con il codice di recupero si apre il database e si **sceglie subito una password nuova**; contestualmente si genera un **codice di recupero nuovo** (quello usato non vale più).
- Da **Impostazioni > Sicurezza** si può generare un nuovo codice in qualunque momento (chiede la password), per esempio se si teme che quello vecchio sia finito in mani altrui.
- Senza password e senza codice di recupero **i dati sono persi**: non esiste nessuna porta di servizio, ed è giusto così. Il manuale e la schermata di creazione lo dicono con parole semplici.

## 6. Avvio e schermate

### Il problema delle impostazioni d'aspetto (risolto nella fase 3)

Oggi `MainWindow` legge da subito tema, colore principale e zoom (`ImpostazioniService`, che li legge dal database) per colorare la schermata di avvio e la barra del titolo. Con il database cifrato, **prima dello sblocco il database non si può leggere**.

Soluzione: le preferenze d'aspetto (tema, colore, stile di icone e pulsanti, zoom, livello del log) **non sono dati personali** e passano in un file in chiaro, **`dati\preferenze.json`**. Le legge `ImpostazioniService` all'avvio, prima della password; senza il file valgono quelle predefinite. Nel database restano solo le impostazioni legate ai dati (per esempio la chiave dell'impronta dei log).

### La nuova sequenza

```
1. MainWindow: preferenze.json → colori della schermata di avvio, barra del titolo, zoom
2. Blazor pronto → Main.razor mostra, al posto delle pagine:
     - nessun database e nessuna chiave     → "Crea la password" (primo avvio)
     - chiave.json presente                 → "Sblocca" (password, oppure "Ho dimenticato la password")
3. Sblocco riuscito → ChiaviService ha la chiave → DatabaseService apre e crea/aggiorna lo schema
4. Solo ora: Home, menu (un database in chiaro delle versioni di prova si ferma prima, sezione 7)
```

- **`DatabaseService` non apre più il database nel costruttore** (fatto nella fase 3): lo fa `Inizializza(chiave)`, chiamato dopo lo sblocco. Tutti i servizi che leggono dati all'avvio (la Home, `AnalisiService`) vanno spostati dopo lo sblocco. Va controllato ogni punto in cui un servizio tocca il database nel costruttore.
- La schermata di sblocco ha: campo password con "mostra", pulsante Sblocca, link "Ho dimenticato la password" (che chiede il codice di recupero), pulsante Esci. Niente menu e niente barra laterale finché non si sblocca.
- **Tentativi sbagliati**: dopo 5 errori di fila, un'attesa crescente (5, 10, 30 secondi) prima del tentativo successivo. Non ferma chi copia il file (che non usa il programma), ma scoraggia chi prova a mano sul computer dell'utente. Gli errori si registrano nel log (solo il numero, mai la password).
- **Impostazioni > Sicurezza** (pagina nuova): cambia password, nuovo codice di recupero, blocco per inattività.

## 7. Database delle versioni precedenti: niente migrazione

**Decisione del 02/11.** Le versioni senza cifratura (fino alla 0.9.2) le hanno solo due persone, lo sviluppatore e un amico che prova il programma. Una migrazione automatica (schermata "Proteggi i tuoi dati", copia cifrata con `PRAGMA rekey`, verifica, cancellazione del file in chiaro, pulizia dei log vecchi) servirebbe due volte e resterebbe nel codice per sempre. Quindi **non si fa**:

- all'avvio un database **in chiaro** viene riconosciuto dall'intestazione (`DatabaseService.Stato()`): `MainWindow` mostra un messaggio e chiude il programma, senza toccare il file. Il messaggio dice di cancellare le cartelle `dati` e `log` accanto al programma, riavviarlo, creare la password e reimportare i file CBI;
- reimportando tornano movimenti e saldi; si perdono quello che si era fatto a mano (movimenti straordinari, tipo scelto per un movimento, gruppi delle causali modificati, nomi dei conti) e le preferenze d'aspetto;
- con la compatibilità è stato tolto anche il codice che serviva solo ai database vecchi: le colonne aggiunte dopo (`AggiungiColonneMovimenti`, ora nel `CREATE TABLE`), i conti registrati dai movimenti già presenti (`RegistraContiGiaImportati`), il recupero delle importazioni dai log (`RecuperaImportazioniDaiLog`), il passaggio delle preferenze d'aspetto dal database a `preferenze.json`.

Da qui in poi lo schema del database parte dalla 0.9.3: un cambiamento futuro allo schema di un database già cifrato andrà gestito, ma solo quando servirà.

## 8. I log

**Stato (02/11): fatto.** I log vecchi non si puliscono dal programma (8.4). Le regole sono nella classe `Services/Mascherato.cs`; l'esito della prova è in 8.6.

### 8.1 Il principio

Nel log resta **in chiaro solo quello che serve per capire un errore** e non identifica persone né somme: date, codici, tipi di record, numeri di riga, lunghezze, conteggi, tempi, nomi delle pagine. Tutto il resto si **maschera**. Il mascheramento si fa **in un solo punto**, una classe `Mascherato` con poche funzioni usate da tutto il codice, così le regole sono uguali ovunque e si controllano facilmente.

| Dato | Nel log | Esempio |
|---|---|---|
| Date (contabile, valuta, giornata) | in chiaro | `27/10/2026` |
| Causale (codice) e tipo di record CBI | in chiaro | `causale 4500`, `record 62` |
| Numero di riga, lunghezza, conteggi, durate | in chiaro | `riga 128, 120 caratteri` |
| Nome del file importato | solo il nome, senza cartella (il percorso contiene il nome utente di Windows) | `movimenti_ottobre.txt` |
| Importi e saldi | solo segno e forma delle cifre | `-#.###,##` |
| Descrizioni, riferimento, testo cercato | lunghezza e impronta | `[34 car. #a3f9]` |
| IBAN | paese e ultime 4 cifre | `IT…1234` |
| Codice del conto | ultime 4 cifre | `…6789` |
| Nome del conto e dei gruppi (scritti dall'utente) | impronta | `[conto #51c0]` |
| Riga originale del file CBI | solo la forma: cifre → `9`, lettere → `X`, spazi conservati | `62 99999 XXXX 999999…` |
| Parametri SQL | come i dati che contengono (importi e testi mascherati) | |
| Totali e somme (analisi, selezione) | non si scrivono | `selezionati 12 movimenti` |

**L'impronta** è un HMAC-SHA256 dei dati, accorciato a 4 caratteri esadecimali, calcolato con una chiave casuale salvata **nel database** (tabella `Impostazioni`, chiave `Log.ChiaveImpronta`, creata al primo avvio da `DatabaseService`; dalla fase 3 il database sarà cifrato). Serve a capire che due righe del log parlano dello stesso movimento o dello stesso testo, senza poter risalire al contenuto. Chi riceve un log non ha la chiave, quindi non può nemmeno provare a indovinare un testo calcolandone l'impronta. Prima dello sblocco nel log non finiscono dati dei movimenti, quindi l'impronta non serve.

### 8.2 Cosa resta utile per il debug

Con queste regole si capisce ancora:
- dove si è fermata la lettura di un file (numero di riga, tipo di record, lunghezza);
- se un campo ha il formato sbagliato (la forma `99999XX9` mostra una lettera dove ci vuole una cifra);
- se un importo è letto con il segno o le cifre sbagliate (la forma `+###.###,##` rispetto a `-#,##`);
- se un movimento è doppio (stessa impronta della descrizione, stessa data, stessa forma dell'importo);
- quali pagine, filtri e operazioni ha usato l'utente e con quali conteggi.

Per un problema che richiede proprio i dati veri, si chiede all'utente il **file CBI** (che sceglie lui di mandare) invece di metterli nel log.

### 8.3 Punti del codice da cambiare

Rilevati il 02/11 con una ricerca delle chiamate al log:

| Dove | Oggi scrive | Diventa |
|---|---|---|
| `LogService.Scarto` | conto, importo, riferimento, descrizione e **la riga originale** del CBI | ultime cifre del conto, date, causale, forma dell'importo, impronte, forma della riga |
| `CbiParser` (Trace) | conto, saldi iniziali e finali, IBAN, descrizioni estese, importi dei movimenti | date, numeri di riga e tipi di record; il resto mascherato |
| `DatabaseService` (`Sintesi`, `ElencaParametri`, inserimenti) | movimenti inseriti e parametri SQL in chiaro | mascherati con le stesse funzioni |
| `DatabaseService` (conti) | IBAN e nome dei conti nuovi o rinominati | IBAN e codice mascherati, nome con impronta |
| `DatabaseService.SalvaGruppoCausale` | nome del gruppo scritto dall'utente | causale in chiaro, gruppo con impronta |
| `DatabaseService` (straordinari, tipo) | la chiave del movimento (conto, data, riferimento, importo) | data e impronta |
| `AnalisiService` | totali di entrate, uscite, investito, base della simulazione | solo periodo e conteggi |
| `ImportazioneService` | percorso completo del file | solo il nome del file |
| `PaginaMovimenti`, `PaginaScarti` (`FiltriMovimenti.ToString`, selezione) | testo cercato, importi dei filtri, somma della selezione | quali filtri sono attivi e quante righe |

Le righe di **riepilogo dell'importazione** ("Importazione avviata: …", "Importazione terminata: … N movimenti letti …") restano come sono, con il solo nome del file: non contengono dati dei movimenti.

### 8.4 I log già scritti

I file in `log` scritti dalle versioni precedenti contengono tutto in chiaro. Il messaggio della sezione 7 dice di cancellare anche la cartella `log`, ma chi cancella solo `dati` li lascerebbe lì (successo il 02/11). Quindi **quando nasce un database nuovo** (creazione della password, `AccessoService.CreaAsync`) il programma cancella **tutti** i file di log, compreso quello di oggi, che può contenere righe in chiaro di una versione vecchia avviata lo stesso giorno (`LogService.EliminaTutti`), e scrive nel log nuovo "log precedenti cancellati (N file)". Non è compatibilità con le versioni vecchie: anche in futuro, con un database nuovo, i log rimasti riguardano dati che non esistono più. Un file aperto in un altro programma non si può cancellare: resta un avviso nel log.

### 8.5 Livelli del log

Le regole valgono a **ogni livello**, compresi Trace e Trace-debug: anche lì il log non contiene dati in chiaro. Il messaggio che avvisava che Trace-debug "contiene dati personali" ora dice che importi, descrizioni e IBAN restano mascherati.

### 8.6 Come è stato realizzato e verificato (02/11)

- **`Mascherato`** (classe statica): `Importo`, `Testo`, `Nome`, `Conto`, `Iban`, `Forma`, `Messaggio`, `Percorso`, `Movimento`, `Chiave`, `Parametro`, `Impronta`. Le usano `LogService.Scarto`, `CbiParser`, `DatabaseService` (conti, gruppi, straordinari, tipo, parametri SQL, sintesi dei movimenti inseriti), `ImportazioneService` (percorso del file), `ToString` di `FiltriMovimenti` e `FiltriScarti` (che servono solo al log), `DialogoInformazioni`.
- **Messaggi delle eccezioni**: .NET scrive tra apici il valore che non ha saputo leggere ("The input string '12A' was not in a correct format"), che può essere un pezzo di importo o di descrizione. `LogService.Errore`, `Fatale` ed `EccezioneGestita` passano messaggio e stack trace da `Mascherato.Messaggio`, che sostituisce il testo tra apici con la sua forma.
- **Avvisi dell'importazione**: l'utente li vede completi nel riepilogo (e sono salvati così nella tabella `Importazioni`); nel log il conto e l'importo delle operazioni non contabilizzate sono mascherati.
- **Analisi e simulazione**: nel log non si scrivono più i totali, solo periodo e conteggi. Nella selezione delle griglie, solo il numero di righe, non la somma.
- **Percorsi**: il nome dell'utente di Windows diventa `<utente>` (cartella dell'app, database, file importato).
- **Prova** (programma `provalog` nella cartella di lavoro temporanea, con i sorgenti veri dell'app): importa con il log in Trace-debug un export CBI reale (98 righe, 20 movimenti, 19 giornate), più una riga 62 rovinata apposta per far scrivere uno scarto con la forma della riga; lo importa una seconda volta (20 scarti "già presente"); poi segna uno straordinario, cambia un tipo, rinomina il conto, cambia un gruppo, cerca con filtri di testo e di importo, registra un errore con un valore tra apici. Poi cerca nel log **152 dati sensibili** presi dal file e dalle operazioni: descrizioni brevi ed estese, riferimenti, IBAN, codice del conto, importi e saldi in tre formati ("1.234,56", "1234,56", "1234.56"), nomi scritti dall'utente. **Nessuno compare nel log**.
- Un esempio di riga del log dopo il mascheramento:
  ```
  [TRACE  ] Riga 3: INSERITO conto …6789, data contabile 01/10/2025, data valuta 30/09/2025, importo -#,##, causale 1905, rif. [15 car. #ad7f], descrizione [16 car. #7c03], dettaglio [30 car. #8b5b]
  ```

## 9. Blocco per inattività (fatto il 02/11, sezione 11.5)

Il progetto iniziale (com'è stato fatto: sezione 11.5):
- dopo N minuti senza uso (mouse e tastiera nella finestra), il programma torna alla schermata di sblocco, azzera la chiave e svuota il pool delle connessioni;
- N si sceglie in Impostazioni > Sicurezza: mai, 5, 15 (predefinito), 30, 60 minuti;
- un'importazione o un calcolo in corso finiscono prima del blocco.

## 10. Documentazione da aggiornare

Stato al 04/11: tutto fatto (manuale, `docs/03`, `LEGGIMI.txt`, `NOVITA.txt`).

- **Manuale** (fatto il 02-03/11; "I tuoi dati" riscritto il 03/11):
  - "Primi passi": il nuovo primo passo è creare la password e salvare il codice di recupero;
  - "I tuoi dati": va riscritto. Il database è cifrato, cosa protegge e cosa no, perché una frase è meglio di un PIN, cosa succede se si dimentica la password, il file `chiave.json` da copiare sempre insieme al database, i log mascherati, i file CBI ancora in chiaro;
  - un capitolo o una sezione su Impostazioni > Sicurezza.
- **LEGGIMI.txt**: il punto 4 ("I tuoi dati non sono protetti") cambia; il punto 5 (copia di sicurezza) aggiunge `chiave.json`.
- **NOVITA.txt**: la versione con la cifratura, con le istruzioni di aggiornamento: cancellare le cartelle `dati` e `log` della versione vecchia, creare la password, reimportare i file CBI (sezione 7).
- `docs/03-avvio-e-pagina-host.md`: la nuova sequenza di avvio (fatto il 04/11, con 02 e 04).
- `docs/00-storico-sviluppo.md`: quello che è stato fatto, con le verifiche.

## 11. Fasi di lavoro

| Fase | Contenuto | Si può provare da sola? |
|---|---|---|
| 1 | **Prototipo** della libreria (sezione 2) e scelta della libreria Argon2, nel programma di prova su una copia del database. **Fatto il 02/11**, tutte le verifiche superate | sì |
| 2 | **Log mascherati** (sezione 8, tranne 8.4): classe `Mascherato` e tutti i punti della tabella 8.3. Fino alla fase 3 la chiave dell'impronta sta nel database in chiaro. **Fatto il 02/11** (8.6) | sì, ed è utile subito |
| 3 | `ChiaviService`, `chiave.json`, Argon2id, AES-GCM; `DatabaseService` che apre il database dopo lo sblocco; `preferenze.json`. **Fatto il 02/11** (sezione 11.1) | con un database nuovo |
| 4 | Schermate: crea password, sblocca, codice di recupero. **Fatto il 02/11** (sezione 11.2) | sì |
| 5 | ~~Migrazione dei database in chiaro e pulizia dei log vecchi~~. **Tolta il 02/11**: niente compatibilità con le versioni di prova (sezioni 7 e 11.3) | |
| 6 | Impostazioni > Sicurezza: cambio password, nuovo codice di recupero. **Fatto il 02/11** (sezione 11.4) | sì |
| 7 | (opzionale) Blocco per inattività. **Fatto il 02/11** (sezione 11.5) | sì |
| 8 | Manuale, LEGGIMI, NOVITA, documenti tecnici, nuova versione. **Fatto il 04/11** (manuale, documenti tecnici, `LEGGIMI.txt`, `NOVITA.txt` della 0.9.3); resta la pubblicazione | |

La fase 2 non dipende dalle altre e riduce subito il rischio maggiore dei log (`LogService.Scarto` e il Trace del parser): si può fare per prima.

### 11.1 Fase 3: cosa è stato fatto (02/11)

- **Pacchetti**: `Microsoft.Data.Sqlite` sostituito da `Microsoft.Data.Sqlite.Core` 10.0.13 + `SQLite3MC.PCLRaw.bundle` 2.6.1; aggiunto `Konscious.Security.Cryptography.Argon2` 1.3.1. `MainWindow` chiama `SQLitePCL.Batteries_V2.Init()` come prima istruzione.
- **`PercorsiDati`** (statica): cartella `dati`, `easycashflow.db`, `chiave.json`, `preferenze.json`, e `ScriviAtomico` (file `.nuovo` accanto, poi `File.Replace`).
- **`ChiaviService`** (singleton): `Crea(password)` (restituisce il codice di recupero e lascia il programma sbloccato), `Sblocca(password)`, `SbloccaConCodice(codice)` (accetta minuscole, spazi e trattini), `CambiaPassword`, `NuovoCodiceRecupero`, `Blocca` (azzera la chiave), `PasswordAccettabile` (almeno 10 caratteri). Le operazioni con Argon2 durano: `Crea` circa 1 s (due derivazioni), `Sblocca` circa 0,4 s; la fase 4 le chiamerà con `Task.Run`.
- **`DatabaseService`**: il costruttore non apre più il database. `Stato()` riconosce il file dall'intestazione (assente, in chiaro, cifrato); `Inizializza(chiave)` crea o aggiorna lo schema, con la chiave cifrato e senza in chiaro; `Apri()` dà a ogni connessione `PRAGMA cipher`, `legacy` e `key` con la chiave grezza; `Chiudi()` svuota il pool e dimentica la chiave; `LeggiImpostazioniPrimaDiAprire()` legge un database in chiaro in sola lettura, senza pool.
- **`ImpostazioniService`**: le preferenze d'aspetto stanno in `preferenze.json`. La prima volta si copiano dalla tabella `Impostazioni` del database in chiaro (le chiavi `Aspetto.*`); nel database restano, inutilizzate, fino alla migrazione.
- **Avvio, provvisorio fino alla fase 4** (`MainWindow.ApriDatabase`): senza `chiave.json` il database si apre **in chiaro, come prima**, così l'app resta usabile. Con `chiave.json`, o con un database cifrato, l'app mostra un messaggio e si chiude: la schermata di sblocco arriva con la fase 4. Alla chiusura della finestra: `ChiaviService.Blocca()` e `DatabaseService.Chiudi()`.
- **Prova** (programma `provachiavi` nella cartella di lavoro temporanea, con i sorgenti veri dell'app): tutte le verifiche superate.
  - Password corta rifiutata; `chiave.json` non si sovrascrive; il database nuovo è cifrato e nel file non si leggono né i dati né l'intestazione.
  - Password sbagliata rifiutata; quella giusta apre; la chiave dell'impronta del log sta nel database cifrato; con una chiave dei dati sbagliata, o senza chiave, il database non si apre.
  - Il codice di recupero apre anche scritto in minuscolo e con gli spazi; dopo il cambio di password e il nuovo codice, password e codice vecchi non valgono più e il database, mai ricifrato, si apre con quelli nuovi.
  - Abbassando i parametri di Argon2 nel file, lo sblocco è rifiutato; senza `chiave.json` lo sblocco segnala l'errore.
  - Una copia del database in chiaro di `bin\Debug` risulta in chiaro, le sue preferenze d'aspetto passano in `preferenze.json` (tema, colore, icone, pulsanti, zoom), una preferenza cambiata si ritrova al riavvio dal file, e il database si apre come prima.

### 11.2 Fase 4: cosa è stato fatto (02/11)

- **`AccessoService`** (singleton): `Prepara()` all'avvio decide la fase (`CreaPassword` senza database né chiave; `Sblocca` se c'è `chiave.json`; con un database in chiaro delle versioni precedenti lo apre subito, fino alla migrazione della fase 5). `CreaAsync`, `SbloccaAsync`, `SbloccaConCodiceAsync` e `NuovaPasswordDopoRecuperoAsync` fanno il lavoro di Argon2 con `Task.Run` e aprono il database; `Completa()` mostra le pagine (evento `Cambiato`) e fa le operazioni che prima faceva `MainWindow` subito dopo l'apertura (il recupero delle importazioni dai log). Gli esiti dello sblocco: riuscito, sbagliato, in attesa, database non apribile con quella chiave (per esempio un `chiave.json` di un'altra cartella: la chiave viene subito dimenticata), file della chiave rovinato. Dopo 5 errori di fila si aspetta 5, poi 10, poi 30 secondi.
- **`ConsigliPassword`**: `SuggerisciFrase()` (5 parole a caso da un elenco di 256, 40 bit) e `Valuta()` (troppo corta, debole se è una password comune anche con cifre in fondo o fatta di pochi caratteri ripetuti, discreta, buona, ottima secondo lunghezza, numero di parole e tipi di caratteri).
- **`PaginaAccesso.razor`**: un riquadro al centro, senza menu, con cinque viste. *Crea* (password con "mostra", indicatore di robustezza, "Suggerisci una frase", ripetizione, avviso sulla perdita dei dati); *Codice* (il codice di recupero in grande, con il terzo gruppo evidenziato, invito ad annotarlo e conservarlo lontano dal computer, selezionabile con Ctrl+C per un gestore di password, senza salvataggio, e per continuare va riscritto il gruppo evidenziato); *Sblocca* (Invio per confermare, errori, conto alla rovescia dell'attesa, "Ho dimenticato la password", "Esci"); *Recupero* (il codice, scritto anche in minuscolo o senza trattini); *Nuova password* dopo il recupero, che porta di nuovo a *Codice* con il codice nuovo.
- **`Main.razor`**: finché `AccessoService.Aperto` è falso mostra solo `PaginaAccesso` (i provider di MudBlazor restano, quindi la conferma di uscita con la X funziona anche lì). **`MainWindow`**: registra `AccessoService`, chiama `Prepara()`; un database cifrato senza `chiave.json` mostra un messaggio e chiude il programma, perché dall'interfaccia non si potrebbe fare niente.
- **Prova della logica** (programma `provaaccesso` nella cartella di lavoro temporanea, con i sorgenti veri dell'app): tutte le verifiche superate. Primo avvio (fase, database aperto ma pagine in attesa del codice, database cifrato); sblocco giusto e sbagliato; 5 errori di fila con l'attesa di 5 secondi, poi di 10, e la password giusta che apre e azzera; recupero con il codice, password e codice nuovi, quelli vecchi che non valgono più; `chiave.json` di un'altra cartella e `chiave.json` rovinato segnalati; database in chiaro aperto come prima; valutazione di otto password di esempio e della frase suggerita.
- **Non ancora provato nell'app**: l'aspetto e il funzionamento delle schermate. Con il database in chiaro di sviluppo le schermate non compaiono (si apre come prima): per vederle serve una cartella `dati` vuota, per esempio avviando il programma pubblicato in una cartella nuova.

### 11.3 Tolta la compatibilità con le versioni di prova (02/11)

Al posto della fase 5 (sezione 7). Quello che le sezioni 11.1 e 11.2 dicono del database in chiaro "aperto come prima" e delle preferenze copiate dal database non vale più.

- **`MainWindow.PreparaAccesso`**: con un database in chiaro mostra il messaggio e chiude, come già con un database cifrato senza `chiave.json`.
- **`AccessoService`**: `Prepara()` sceglie solo tra `CreaPassword` e `Sblocca`; `Completa()` mostra le pagine e basta. Non dipende più da `ImportazioneService`.
- **`DatabaseService`**: `Inizializza(chiave)` vuole sempre la chiave; `Straordinario` e `TipoForzato` sono nel `CREATE TABLE` di `Movimenti`; tolti `AggiungiColonneMovimenti`, `RegistraContiGiaImportati`, `LeggiImpostazioniPrimaDiAprire`, `PrimaImportazioneRegistrata` e l'ora passata a `RegistraImportazione`.
- **`ImportazioneService`**: tolto `RecuperaImportazioniDaiLog`.
- **`ImpostazioniService`**: legge solo `preferenze.json` e non dipende più da `DatabaseService`.

### 11.4 Impostazioni > Sicurezza: nuovo codice di recupero e cambio password (02/11)

Il 02/11, al primo avvio con la cifratura, il programma si è chiuso di colpo mentre si selezionava con il mouse il codice di recupero appena mostrato: il codice era già salvato in `chiave.json`, ma l'utente non aveva fatto in tempo ad annotarlo. Al riavvio la password funzionava, il codice era perso.

- **La causa**: nel registro eventi di Windows l'errore è in `EmbeddedBrowserWebView.dll` (WebView2 156.0.4314.58), codice `0x80000003`: un controllo interno di Chromium, non un'eccezione .NET, per questo il log dell'app non dice niente. Il codice era mostrato con `user-select: all`; ora la selezione è quella normale del testo (si copia sempre con Ctrl+C). Non è dimostrato che fosse quella la causa: se capita di nuovo, il rimedio sotto vale comunque.
- **Il rimedio**: Impostazioni > Preferenze > **Sicurezza** (`PaginaSicurezza.razor`). Con la password (`ChiaviService.PasswordGiusta`, che decifra la chiave con la password e la confronta con quella in memoria senza cambiare niente) si crea un codice nuovo (`NuovoCodiceRecupero`), che si mostra una volta sola come il primo; quello vecchio smette subito di valere. Anche la schermata del codice alla creazione della password ora dice che, se lo si perde, se ne può creare uno nuovo da qui.
- **Attese**: mentre si lavora con la password (creazione, sblocco, codice di recupero, nuovo codice) al posto del modulo compaiono una barra di caricamento e una scritta su cosa sta facendo, come nella schermata di avvio (componente `AttesaCalcolo.razor`); prima c'era solo un cerchio dentro il pulsante, poco visibile per un'attesa di circa un secondo.
- **Cambio password**, nella stessa pagina: password attuale, poi quella nuova due volte, con indicatore di robustezza e "Suggerisci una frase". Si controlla l'attuale (`PasswordGiusta`) e si ricifra la chiave dei dati con la nuova (`CambiaPassword`, salt nuovo): il database non si tocca e il codice di recupero resta valido. Durante l'attesa il modulo si nasconde ma resta montato, così con una password attuale sbagliata quella nuova non va riscritta.
- Il modulo per scegliere una password nuova è un componente comune, `NuovaPassword.razor`, usato alla creazione, dopo il recupero e nel cambio password.
- Il codice si mostra con un componente comune, `CodiceRecupero.razor` (gruppi, terzo gruppo evidenziato, gruppo da riscrivere, pulsante di conferma), usato da `PaginaAccesso` e da `PaginaSicurezza`.

### 11.5 Blocco per inattività e preferenze generali (02/11)

- **Quando**: dopo i minuti scelti in Impostazioni > Preferenze > Sicurezza (mai, 5, 10, **15** predefinito, 30, 60) senza attività nella finestra. L'attività la segnala la pagina (`wwwroot/js/attivita.js`: mouse, tasti, rotellina, tocchi, al massimo una chiamata ogni 15 secondi) a `BloccoService`, che ogni 15 secondi controlla. Il tempo si misura con `Environment.TickCount64`, che corre anche con il computer sospeso: al risveglio il programma si blocca subito. Durante un'importazione, anche con la finestra di scelta del file aperta, non si blocca (`ImportazioneService.InCorso`). C'è anche **Blocca adesso**, e dal 03/11 la voce **Blocca** (lucchetto) nella barra a sinistra, sopra Esci, che fa lo stesso da qualunque pagina.
- **Cosa fa**: `AccessoService.Blocca` toglie la chiave dalla memoria (`ChiaviService.Blocca`) e chiude il database (`DatabaseService.Chiudi`), come alla chiusura; la fase torna `Sblocca`. Il blocco lo esegue `Main.razor` nel thread dell'interfaccia, così il database non si chiude a metà di un suo aggiornamento.
- **Cosa si vede**: la pagina di accesso ("EasyCashFlow è bloccato") copre tutta la finestra; quello che c'è sotto, dialoghi e menu aperti compresi, resta com'era ma `inert` (niente clic, tasti o Tab), e sbloccando si ritrova la pagina com'era. Funzionano anche "Ho dimenticato la password" ed "Esci"; da bloccato la X della finestra chiude senza chiedere conferma, perché la conferma resterebbe sotto. Sotto restano nel DOM i dati già mostrati (la chiave no): chi è davanti al computer non li vede e non può aprire gli strumenti di sviluppo, che nella versione pubblicata non ci sono.
- **Dove sta la scelta**: nella tabella `Impostazioni` del database cifrato (`PreferenzeService`), non in `preferenze.json`: in chiaro chiunque potrebbe spegnere il blocco modificando il file. Lì stanno anche le preferenze di Impostazioni > Preferenze > **Generali** (pagina iniziale, conto e periodo proposti), che servono solo a database aperto; il conto è un dato personale. I giorni di conservazione dei log invece stanno in `preferenze.json`, perché la pulizia dei log si fa all'avvio, prima della password.
- **Prova della logica** (programma `provaaccesso`): preferenze predefinite; da bloccato la chiave non c'è, il database non si legge e una password sbagliata non apre; la password giusta riapre e le pagine tornano; le preferenze si rileggono dal database cifrato e il conto non finisce in `preferenze.json`; con 7 giorni un log di 10 giorni fa si cancella subito e la scelta si ricorda.
- **Prova nell'app** (02/11, copia della build in una cartella nuova): pagina Generali con le scelte ricordate al riavvio e la pagina iniziale Analisi; "Blocca adesso"; da bloccato le pagine e i dialoghi sono `inert` e il Tab resta nella schermata di blocco; password sbagliata e poi giusta riportano alla pagina com'era; con 5 minuti e il dialogo Informazioni aperto il programma si è bloccato dopo 5 minuti esatti e sbloccando il dialogo era ancora lì. Due correzioni trovate così: la X della finestra da bloccato scriveva nel log la chiusura ma la finestra restava aperta (`ChiusuraService.Chiudi` chiamava `Close()` dentro l'evento `Closing`: ora usa `BeginInvoke`; verificata anche l'uscita normale con conferma); i pulsanti dei giorni dei log andavano a capo con la spunta (ora 1 settimana, 1 mese, 2, 3, 6 mesi, 1 anno, senza spunta). Il dialogo Informazioni diceva ancora "senza password né cifratura": testo aggiornato.

## 12. Verifiche

Con il programma di prova (`provaparser`) e su copie del database:

- **Cifratura**: nel file `easycashflow.db` non si trovano né una descrizione nota, né un IBAN, né la stringa `SQLite format 3` dell'intestazione.
- **Password**: sbagliata → rifiutata senza toccare il database; giusta → si apre; cambiata → la vecchia non vale più e i dati restano gli stessi.
- **Codice di recupero**: apre il database, obbliga a scegliere una password nuova e smette di valere dopo l'uso.
- **`chiave.json`**: assente, rovinato o con i parametri modificati → messaggio chiaro, nessuna perdita di dati e nessun file sovrascritto.
- **Database in chiaro** delle versioni di prova: messaggio chiaro e uscita, il file non viene toccato.
- **Copia di sicurezza**: copiando `dati` su un altro computer, il programma chiede la password e apre tutto.
- **Log**: si importa un file CBI con il log in Trace-debug, poi un test cerca nel log ogni descrizione, ogni riferimento, ogni IBAN e ogni importo del file (scritto in tutti i formati usati, "1.234,56", "1234,56", "1234.56"): **non ne deve trovare nessuno**. Il test resta nel programma di prova e si ripete a ogni modifica dei log.
- **Prestazioni**: avvio fino alla schermata di sblocco non più lento di oggi; sblocco intorno al mezzo secondo; importazione e analisi con tempi confrontabili con quelli di oggi.
- **Pubblicazione**: la cartella pubblicata contiene la DLL nativa di SQLite3MC e il programma si avvia su un PC senza strumenti di sviluppo.

## 13. Riferimenti

- [SQLite encryption options for use with SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw/wiki/SQLite-encryption-options-for-use-with-SQLitePCLRaw): la fine dei pacchetti gratuiti con cifratura e le alternative.
- [SQLCipher 4.11.0 Release (Zetetic)](https://www.zetetic.net/blog/2025/10/08/sqlcipher-4.11.0-release/): SQLCipher commerciale per .NET con SQLitePCLRaw 3.
- [SQLite3MC.PCLRaw.provider su NuGet](https://packages.nuget.org/packages/SQLite3MC.PCLRaw.provider) e il bundle `SQLite3MC.PCLRaw.bundle`: versione da controllare su nuget.org prima di fissarla.
- RFC 9106: Argon2, con i vettori di prova per verificare la libreria scelta.
