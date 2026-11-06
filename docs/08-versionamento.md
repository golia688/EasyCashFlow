# 08 - Versionamento del codice con git

Dal 05/11 il progetto è sotto **git**, per ora solo in locale (nessun collegamento a GitHub). Sarà pubblicato su GitHub come repository **pubblico**, con l'utente `golia688`: per questo nei file non ci sono dati veri dell'utente e i commit non portano né il suo nome né la sua email (sezioni 2 e 4). Questo documento spiega cosa è git, come è stato configurato il repository e come si lavora da Visual Studio.

## 1. Cos'è, in breve

Git tiene la **storia** del progetto. Ogni volta che si fa un *commit* si salva una fotografia di tutti i file versionati, con un messaggio che dice cosa è cambiato. Si può poi:

- vedere cosa è cambiato tra una fotografia e l'altra, riga per riga;
- tornare a come era un file (o tutto il progetto) in un commit precedente;
- provare una modifica su un **ramo** (*branch*) separato, senza toccare quello principale.

La storia sta nella cartella nascosta **`.git`** dentro la cartella del progetto. **Non va cancellata né modificata a mano**: è lì che c'è tutto. Una copia della cartella del progetto, `.git` compresa, è una copia completa della storia.

"In locale" vuol dire che la storia esiste solo su questo PC. GitHub serve a tenerne una copia online (backup, lavoro da più computer): si può aggiungere quando si vuole, senza rifare niente (sezione 6).

## 2. Come è stato configurato (05/11)

| Cosa | Valore | Perché |
|---|---|---|
| Ramo principale | `main` | il nome usato oggi da GitHub; quello predefinito di git per Windows, installato sul PC, era ancora `master` |
| Autore dei commit | `golia688`, email `352818144+golia688@users.noreply.github.com` | l'indirizzo "noreply" di GitHub (il numero è quello dell'account, pubblico): GitHub collega i commit al profilo senza mostrare l'email vera. Impostati **solo per questo repository** (`git config user.name` / `user.email`, senza `--global`); nella configurazione di git del PC non c'era nessun nome |
| `.gitattributes` | `* -text` | i file si salvano **così come sono**, senza convertire gli a capo (sezione 3) |
| `.gitignore` | sezione 4 | cosa non entra mai nel repository |

Il primo commit contiene i 100 file del progetto: codice, pagine, servizi, modelli, documentazione, manuale, `LEGGIMI.txt` e `NOVITA.txt`, icone, la parte web (`wwwroot`, compreso ECharts) e i profili di pubblicazione.

## 3. Gli a capo

Windows termina le righe con due caratteri (CRLF), Linux e macOS con uno (LF). Git per Windows è installato con `core.autocrlf = true`: senza indicazioni convertirebbe gli a capo a ogni commit e a ogni checkout. Nel progetto convivono i due tipi (Visual Studio scrive CRLF, alcuni documenti hanno LF), e il 01/11 gli a capo moltiplicati da uno script avevano già dato problemi (storico, "Problemi già risolti"). Con `* -text` in `.gitattributes` git non tocca mai gli a capo: il file nel repository è identico, byte per byte, a quello su disco. Se un giorno il progetto si aprisse anche su Linux o macOS, si potrà passare a `* text=auto` e normalizzare.

## 4. Cosa resta fuori (`.gitignore`)

| Esclusi | Perché |
|---|---|
| `bin/`, `obj/` | il risultato della compilazione: si ricrea con Visual Studio o `dotnet build`. Dentro `bin` ci sono anche le cartelle `dati` e `log` delle prove e la cartella di pubblicazione con gli zip |
| `.vs/`, `*.user`, `*.suo` | scelte personali di Visual Studio (finestre aperte, profilo di pubblicazione scelto) |
| `dati/`, `log/`, `*.db`, `chiave.json`, `preferenze.json`, `*.log` | i dati del programma: stanno già in `bin`, queste righe sono una sicurezza in più se finissero altrove |
| `*.cbi`, `*exportCBI*`, `*.zip` | export veri della banca e archivi: contengono i movimenti |

Per controllare se un file è escluso e da quale regola: `git check-ignore -v <file>`.

**Niente dati veri nei file versionati** (05/11, prima della pubblicazione). La documentazione e alcuni testi del programma riportavano numeri veri dei movimenti dell'utente: i saldi e l'importo del caso delle operazioni non contabilizzate, i totali delle verifiche di analisi e simulazione, la rata del mutuo usata come esempio di ricerca, il nome provvisorio del conto con l'ABI della banca e le ultime cifre del conto, l'acquisto che stava dietro una causale. Sono stati sostituiti con esempi inventati ma coerenti (le somme tornano) o con la sola descrizione della verifica ("coincide al centesimo"), dicendo dove gli importi sono di esempio. Restano i numeri che non dicono niente dei movimenti: quanti movimenti, giornate e righe aveva un file, i tempi, le date. Il primo commit, che aveva i dati veri e il nome vero dell'autore, è stato riscritto prima di qualunque pubblicazione (sezione 7).

Regola da qui in poi: **negli esempi solo importi inventati**, anche nei messaggi dei commit; per una verifica sui dati veri si scrive cosa è stato controllato e l'esito, non gli importi. Prima di ogni push un controllo veloce: `git grep -n -E "[0-9]{1,3}(\.[0-9]{3})*,[0-9]{2}"` elenca gli importi con i centesimi presenti nei file.

## 5. Lavorare da Visual Studio

Visual Studio riconosce da solo il repository aprendo la soluzione.

- **Finestra Modifiche Git** (menu *Git > Commit or Stash…*, oppure `Ctrl+0, Ctrl+G`): elenca i file cambiati dall'ultimo commit. Si scrive il messaggio in alto e si preme **Commit All**. Con il tasto destro su un file: *Compare with Unmodified* mostra le differenze, *Undo Changes* lo riporta all'ultimo commit (le modifiche si perdono).
- **Finestra Repository Git** (menu *Git > View Branch History*): la storia dei commit. Cliccando un commit si vedono i file cambiati e le differenze.
- **Storia di un solo file**: tasto destro sul file in Esplora soluzioni > *Git > View History*.
- **Barra di stato in basso a destra**: il ramo attuale (`main`) e il numero di modifiche non ancora nel commit. Da lì si crea o si cambia ramo.

Come si lavora sul progetto:

1. si fa una modifica (in Visual Studio o con Claude Code) e si prova;
2. quando funziona, un commit con un messaggio che dice **cosa** e **perché**, per esempio "Controlli formali sul file CBI: un file che non è CBI si rifiuta senza salvare niente";
3. per una modifica lunga o rischiosa, prima un ramo (*New Branch*), poi, quando va bene, lo si unisce a `main` (*Merge*).

Un commit per ogni cosa fatta e provata: la storia resta leggibile e si può tornare indietro a un punto preciso.

Da riga di comando (Terminale di Visual Studio o PowerShell, nella cartella del progetto) i comandi equivalenti sono `git status`, `git diff`, `git add -A`, `git commit -m "messaggio"`, `git log --oneline`.

## 6. Più avanti: GitHub

Quando si pubblicherà:

1. su GitHub, in **Settings > Emails**: spuntare *Keep my email addresses private* e *Block command line pushes that expose my email*. Così un commit fatto per sbaglio con l'email vera viene rifiutato invece di finire online;
2. *New repository*, nome `EasyCashFlow`, **Public**, senza README né `.gitignore` (ci sono già);
3. in Visual Studio: *Git > Push*, oppure *Git > Create Git Repository > GitHub* sul repository esistente; Visual Studio chiede di accedere all'account `golia688`;
4. da quel momento *Push* manda i commit su GitHub e *Pull* scarica quelli fatti altrove.

Una volta pubblicato, quello che è in un commit resta leggibile da chiunque nella storia, anche se lo si cancella in un commit successivo: il controllo della sezione 4 va fatto **prima** del push.

## 7. Il primo commit riscritto (05/11)

Il primo commit era stato fatto con il nome e l'email veri dell'utente e con i numeri veri nella documentazione. Siccome non era stato pubblicato, è stato **riscritto**: autore `golia688` con l'indirizzo noreply, file senza dati veri (`git commit --amend --reset-author`). La versione vecchia è stata poi cancellata anche dalla memoria interna di git (`git reflog expire --expire=now --all` e `git gc --prune=now`), così non resta da nessuna parte nella cartella `.git`.

Il 06/11, nel controllo finale prima della pubblicazione (tutte le versioni di tutti i file, i messaggi, gli autori, gli oggetti interni di git), è rimasta una sola cosa: la frase di questo documento che elencava cosa era stato tolto citava l'importo, arrotondato, del caso delle operazioni non contabilizzate. La storia (tre commit) è stata riscritta di nuovo con la frase corretta (`git filter-branch --tree-filter`), con gli stessi messaggi, date e autore, e le versioni vecchie cancellate come sopra. Nello stesso giorno sono stati aggiunti `README.md` (la pagina del repository su GitHub) e `LICENSE`: licenza **MIT**, titolare `golia688`.
