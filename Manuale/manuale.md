## Cos'è EasyCashFlow {#introduzione}

EasyCashFlow ti aiuta a capire **dove vanno i tuoi soldi**, su un conto o su tutti i tuoi conti insieme.

Funziona così:

1. scarichi dall'home banking l'elenco dei movimenti di ogni tuo conto corrente, nel formato **CBI**: un **file di testo** oppure un **file XML**, vanno bene tutti e due;
2. lo importi in EasyCashFlow;
3. il programma ti mostra entrate, uscite, saldo e grafici, mese per mese.

**Tutti i tuoi conti, anche in banche diverse**

EasyCashFlow è un **aggregatore**: mette insieme in un posto solo i movimenti di tutti i tuoi conti correnti, anche se sono in banche diverse. Funziona perché i file CBI seguono degli **standard**, uguali per tutte le banche italiane: il file di una banca e quello di un'altra si leggono allo stesso modo.

- **Importi l'export di ogni conto**, uno dopo l'altro. Il programma riconosce da solo a quale conto appartiene ogni movimento: un conto nuovo si aggiunge all'elenco, e non si confondono mai i movimenti di due conti.
- **Scegli cosa guardare**: un conto alla volta, alcuni conti, oppure tutti insieme. In questo caso vedi il **cash flow aggregato**: entrate e uscite di tutti i conti, e il saldo come somma dei saldi. La scelta dei conti c'è nella Home, nei movimenti e nelle analisi; la simulazione del saldo e il report Excel usano i conti scelti nelle analisi.
- **Le causali parlano la stessa lingua**: le prime due cifre di ogni causale sono la *causale CBI*, uguale per tutte le banche, così bonifici, carte o utenze di banche diverse finiscono negli stessi gruppi.
- **I giroconti tra i tuoi conti** (un bonifico da un tuo conto a un altro tuo conto) non sono né spese né entrate: guardando i conti insieme si conterebbero due volte, come uscita da un conto ed entrata nell'altro. Il programma li riconosce quando la banca usa le causali del giroconto; per un singolo bonifico puoi sceglierlo tu (tipo **Giroconto**, vedi [La pagina dei movimenti](cap:movimenti)).

Non serve internet e non serve un account: tutto resta sul tuo computer, nella cartella del programma.

> **Importante:** i tuoi movimenti vengono salvati **cifrati** e si aprono solo con la tua password. Prima di cominciare, leggi [I tuoi dati](cap:dati): spiega come funziona la protezione, cosa non copre e dove mettere il programma.

Se è la prima volta che lo usi, comincia dai [Primi passi](cap:primi-passi).

## Primi passi {#primi-passi}

**Prima di tutto: la password**

Al primo avvio il programma ti chiede di scegliere una **password**: i tuoi movimenti vengono salvati **cifrati** e senza la password nessuno può leggerli, nemmeno copiando la cartella del programma. Te la chiederà a ogni avvio.

- Scegli una **password robusta**: lunga (almeno 10 caratteri, meglio di più), che non usi per nient'altro e che non si possa indovinare conoscendoti (niente nomi, date di nascita, parole come "password123"). Sotto il campo vedi quanto è robusta. Se non ti viene in mente, il pulsante **Suggerisci una frase** ne propone una fatta di cinque parole a caso, robusta e facile da ricordare. Il perché della password robusta è spiegato in [I tuoi dati](cap:dati).
- Subito dopo vedrai il **codice di recupero**: 30 caratteri, in sei gruppi. È **l'unico modo** per aprire i dati se dimentichi la password, e lo vedi una volta sola. **Annotalo** e conservalo in un posto sicuro, lontano dal computer, oppure in un gestore di password come KeePass: puoi selezionarlo e copiarlo con Ctrl+C. Il programma non lo salva da nessuna parte, apposta: un codice in un file sul computer lo troverebbe anche chi riesce ad aprire i tuoi file. Per lo stesso motivo non lasciarlo in un documento o in un appunto. Per continuare ti verrà chiesto di riscriverne un gruppo.

> **Importante:** senza password e senza codice di recupero i tuoi dati non si possono più aprire. Nessuno può farlo, nemmeno chi ha scritto il programma.

Poi bastano tre passi. Li trovi anche nella [Home](app:home), dove si segnano come fatti da soli.

**1. Importa il primo file**

Scarica dall'home banking i movimenti del conto in un file CBI, di testo o XML, e importalo. Il capitolo [Scaricare l'export dalla banca](cap:export-banca) spiega cosa cercare.

**2. Dai un nome al conto**

Il conto si registra da solo, con un nome provvisorio tipo "ABI 12345 - c/c ...6789". Cambialo con un nome che riconosci, per esempio "Conto di casa". Lo trovi in [Impostazioni](app:impostazioni).

Hai altri conti, anche in altre banche? Importa il file CBI (di testo o XML) di ciascuno: ogni conto si aggiunge da solo, e dai un nome anche a quelli. Da quel momento puoi guardarli uno alla volta o tutti insieme.

**3. Controlla i gruppi di causali**

Ogni movimento ha una *causale*, cioè un codice della banca che dice che tipo di operazione è (bonifico, carta, stipendio…). Il programma raggruppa le causali per fare i grafici. Le più comuni sono già sistemate, anche quelle di altre banche; se ne resta qualcuna senza gruppo, assegnagliene uno in [Gruppi di causali](app:gruppi).

Fatto questo, apri le [Analisi](app:analisi).

In cima a ogni pagina, accanto al titolo, ci sono due icone: il **punto di domanda** apre la spiegazione di quella pagina in questo manuale; l'**occhio** nasconde tutti gli importi, se qualcuno si avvicina allo schermo (anche con **Ctrl+H**; vedi [I tuoi dati](cap:dati)).

## La Home {#home}

La [Home](app:home) è la prima pagina che vedi: riassume in un colpo d'occhio come sta il conto.

**Più conti**

Se hai importato i movimenti di più conti, in alto a destra c'è il campo **Conti**: scegli su quali conti ragiona la Home. Vale per il saldo, per il mese e per gli ultimi movimenti; il riepilogo del database e l'ultima importazione riguardano sempre tutto. La scelta resta anche alle prossime aperture; la prima volta vale il conto predefinito scelto in [Generali](app:generali), oppure tutti.

**Il conto oggi**

Il saldo più recente, una piccola linea con il suo andamento e quanto è cambiato nello stesso periodo: all'inizio gli ultimi 90 giorni, ma puoi sceglierne un altro (vedi sotto). Con più conti scelti il riquadro si chiama **I conti oggi**: mostra il saldo di ciascun conto e sotto il totale, che è quello del grafico. Passa con il mouse sulla linea per vedere il saldo di un giorno preciso.

**Il mese**

Entrate, uscite e flusso netto del mese dell'ultimo movimento importato, ciascuno accanto alla **media mensile** di un periodo di confronto, all'inizio gli ultimi 12 mesi completi: così capisci subito se è stato un mese normale o no. Clicca il riquadro per aprire l'analisi di quel mese.

Con più conti, ognuno è aggiornato fino al giorno del suo ultimo export: per esempio uno fino al 31 ottobre e l'altro fino al 5 novembre. Allora il mese arriva fino al giorno a cui sono aggiornati **tutti** i conti scelti (nell'esempio, ottobre), così il totale comprende davvero i movimenti di ciascuno; il riquadro lo scrive sotto il titolo. Scegliendo un conto solo vedi il mese fino al suo ultimo giorno.

**I dati nel database**

Quanti movimenti, importazioni, conti, causali, gruppi e saldi ci sono. Alcuni riquadri si possono cliccare e portano alla pagina relativa: per esempio "Causali" apre i gruppi di causali.

**Ultima importazione e ultimi movimenti**

Il riepilogo dell'ultimo file importato, con il pulsante per importarne un altro, e gli ultimi movimenti arrivati.

Se l'ultimo movimento ha più di una settimana, in alto compare un promemoria: è il momento di scaricare un export nuovo dalla banca.

Se dalla Home vai ai conti o ai gruppi di causali (con i pulsanti dei Primi passi o cliccando i riquadri del database), la freccia in alto a sinistra di quelle pagine ti riporta alla Home.

**Le notifiche**

In alto a destra, tra l'icona del log e quella dell'aiuto (il punto interrogativo), c'è la **campanella**. Il numero sopra dice quante cose ci sono da fare o da guardare; il colore dice la più importante (rosso un errore, giallo un avviso, azzurro un'informazione). Clicca la campanella per vedere l'elenco: ogni notifica ha un pulsante che ti porta dove serve.

- **Ultima importazione con avvisi** o **non riuscita**: riapre il riepilogo dell'importazione.
- **Movimenti scartati da controllare**: porta ai movimenti scartati.
- **Causali senza gruppo**: porta ai gruppi di causali.
- **Dai un nome ai conti**: porta ai conti correnti.
- **Importa un export recente**, se l'ultimo movimento ha più di una settimana.
- **Fai un backup**, se non l'hai mai fatto o l'ultimo ha più di 30 giorni.

Una notifica sparisce da sola quando la cosa è sistemata. **Nascondi** la toglie fino alla prossima apertura del programma.

**Il menu Dashboard**

- **Aggiorna** rilegge tutti i numeri. Di solito non serve, perché la Home si aggiorna da sola dopo ogni importazione; è utile se il programma è rimasto aperto da ieri, per ricalcolare i giorni dall'ultimo movimento e il saldo fino a oggi.
- **Scegli periodo** apre una finestra con due scelte: quanto tempo mostra **Il conto oggi** (30 o 90 giorni, 6 o 12 mesi) e con la media di quale periodo confrontare **il mese** (ultimi 3, 6 o 12 mesi completi). Le scelte si applicano subito e restano anche alle prossime aperture del programma.

## Scaricare l'export dalla banca {#export-banca}

EasyCashFlow legge i **file CBI**: i file con cui le banche italiane danno saldi e movimenti dei conti, secondo gli standard del Consorzio CBI. L'home banking li fa scaricare in **due tipi di file**, che seguono tutti e due gli standard CBI, e il programma li legge entrambi:

- **File di testo CBI**: il file ha estensione `.txt` (a volte `.cbi`) e, aperto con il Blocco note, è fatto di righe di testo tutte della stessa lunghezza. Contiene movimenti, causali e saldi di ogni giornata. È il formato classico, quello che ogni banca italiana sa dare. Lo standard è il **CBI-RND-001** del Consorzio CBI, "Rendicontazione saldi e movimenti di conti correnti".
- **File XML CBI**: il file ha estensione `.xml` e, aperto con il Blocco note, comincia con `<?xml`. Contiene gli stessi movimenti, causali e saldi, e in più qualche informazione: per esempio nome e località di chi ti ha fatto un bonifico, o l'IBAN di chi ne ha ricevuto uno da te. Lo standard è lo **STRN-MO-001** del Consorzio CBI, "Rendicontazioni XML", costruito sullo standard internazionale ISO 20022 (camt.053); il programma legge la rendicontazione giornaliera.

Il programma riconosce da solo quale dei due hai scelto. Se la tua banca li offre entrambi, puoi usare quello che preferisci, anche alternandoli: lo stesso movimento importato una volta dal file di testo e una volta dall'XML resta **uno solo**, e tiene la descrizione più completa. L'XML è un po' più ricco; il file di testo è il più diffuso.

Nell'home banking cerca una funzione con un nome simile a:

- "Esporta movimenti" o "Scarica movimenti";
- "Saldi e movimenti";
- "Formato CBI", "Rendicontazione CBI" oppure "XML".

**Relax Banking** (Home Banking del gruppo Iccrea). EasyCashFlow è stato provato con **Relax Banking**, l'home banking del gruppo Iccrea, ed è **compatibile al 100%**: i file veri, sia il file di testo CBI sia il file XML, si importano con tutti i movimenti, le causali e i saldi, senza avvisi, e i due formati danno esattamente gli stessi movimenti. Per esportare:

1. entra in Relax Banking e apri la lista dei **movimenti del conto**, con il periodo che ti serve;
2. premi il pulsante **Esporta**, in alto sopra l'elenco;
3. scegli **Esporta CBI** (il file di testo CBI) oppure **Esporta XML** (il file XML CBI). **Esporta PDF** ed **Esporta EXCEL** non vanno bene per il programma.

**Le altre banche.** I file CBI sono uguali per tutte le banche italiane, ma ogni home banking ha menu diversi: se non trovi la funzione, chiedi alla tua banca come scaricare **"i movimenti del conto in un file CBI"**, di testo o XML.

Alcuni consigli:

- scegli un periodo ampio la prima volta, per esempio l'ultimo anno: più dati hai, più le analisi sono utili;
- poi basta scaricare ogni tanto gli ultimi movimenti;
- i periodi possono sovrapporsi: i movimenti già importati **non vengono duplicati**.

Non serve aprire il file che scarichi: il Blocco note serve solo, se vuoi, a capire quale dei due formati hai.

**Se la tua banca non dà il file CBI.** Molte banche fanno esportare i movimenti solo in **Excel**. Anche quello si può importare, con un passaggio in più: gli dici tu cosa c'è in ogni colonna. Lo spiega il capitolo [Importare un file Excel](cap:excel). Quando la banca offre il file CBI, però, sceglilo: è più completo, perché ha la causale, il riferimento della banca e i saldi di ogni giorno. Il PDF non va mai bene.

## Importare i movimenti {#importare}

Per importare un file:

1. vai in [Movimenti](app:movimenti);
2. premi **Importa movimenti** (oppure usa il menu **Dati > Importa movimenti**); passando sopra il pulsante, un suggerimento ricorda i formati che vanno bene: CBI (testo o XML) o Excel;
3. scegli il file scaricato dalla banca, il file di testo CBI o il file XML: il programma riconosce da solo il formato. Se è un file Excel, prima si apre la finestra per abbinare le colonne, descritta nel capitolo [Importare un file Excel](cap:excel).

Si apre la finestra **Importa movimenti**: mentre il programma legge il file vedi una barra che scorre, e dopo pochi secondi un riepilogo in tabella. Ecco cosa vogliono dire le righe:

- **Giornate nel file**: quanti giorni copre il file, dal primo all'ultimo.
- **Movimenti nel file**: quanti movimenti contiene.
- **Nuovi, importati**: movimenti aggiunti adesso.
- **Già presenti**: movimenti che avevi già importato con un altro file. È normale quando i periodi si sovrappongono: non vengono duplicati.
- **Stessa chiave, contenuto diverso**: compare solo se ce ne sono. Sono movimenti non importati, che trovi in [I movimenti scartati](cap:scarti): il pulsante **Vedi i movimenti scartati** ti ci porta.
- **Righe non valide**: compare solo se ce ne sono; sono righe del file che il programma non ha saputo leggere.
- **Avvisi**: quanti, oppure "nessuno" se il file è completo e i saldi tornano.

Gli **avvisi** sono sotto la tabella, raggruppati per tipo: per esempio "3 × il saldo iniziale non coincide…". Clicca su un avviso per vedere le date in cui capita. Un avviso non vuol dire che qualcosa si è rotto: il programma ti segnala una cosa strana nel file, e i movimenti letti sono stati importati comunque. L'avviso più comune riguarda le [operazioni non ancora contabilizzate](cap:domande). Altri dicono, per esempio, che i saldi di due giorni non si collegano (potrebbero mancare dei movimenti) o che il file sembra tagliato.

**Se scegli il file sbagliato** (un PDF, un'immagine, un altro tipo di file CBI, un file XML che non è quello dei movimenti secondo lo standard CBI) il programma se ne accorge prima di leggere i movimenti: la finestra lo dice in rosso, con il motivo, e **non salva niente**. Lo stesso se il file è rovinato. Un file tagliato a metà, invece, si importa con l'avviso che manca la fine: i movimenti che contiene sono veri, e quelli mancanti arriveranno con l'export successivo.

Il pulsante **Apri il log** apre il file in cui il programma scrive cosa ha fatto: serve se devi segnalare un problema. **Chiudi** chiude la finestra.

Alcuni movimenti già presenti possono ricevere una **descrizione più completa**: succede se importi l'XML degli stessi giorni di un file di testo già importato, o se importi di nuovo un file che avevi caricato con una versione precedente del programma. Il riepilogo te lo dice. I movimenti non vengono duplicati, e una descrizione non diventa mai meno completa.

L'ultima importazione è riassunta anche nella [Home](app:home).

## Importare un file Excel {#excel}

Se la tua banca fa esportare i movimenti solo in Excel, puoi importare quel file. Si importa come gli altri (**Importa movimenti**, poi scegli il file): il programma si accorge che è un Excel e apre la finestra **Importa file Excel**, in cui gli spieghi dov'è la tabella e cosa c'è in ogni colonna. Ogni banca fa l'Excel a modo suo, e questo è l'unico modo per leggerli tutti. Si fa in due passi.

**Passo 1: dov'è la tabella.** Molte banche mettono sopra la tabella dei movimenti il logo, la data, il conto, i saldi. La finestra mostra il file come in Excel, con le righe numerate: **clicca la riga con le intestazioni delle colonne**, quella con "Data", "Descrizione", "Importo" e simili. La riga si colora, quelle sopra si spengono e quelle sotto, i movimenti, si evidenziano. Il programma ne ha già scelta una: se è giusta, premi **Avanti**. Se sotto la riga scelta non ci sono date, la finestra ti avvisa che probabilmente non è quella giusta.

**Passo 2: le colonne.**

- **A sinistra** ci sono le colonne del file: per ognuna vedi l'intestazione e i primi valori, così capisci cosa contiene.
- **A destra** c'è il modello di EasyCashFlow: le caselle dei dati che servono al programma.
- **Trascina** ogni colonna di sinistra nella sua casella di destra. Se preferisci, clicca la colonna e poi la casella. La **x** su una casella toglie la colonna.

Le caselle con l'asterisco sono obbligatorie:

- **Data contabile** \*;
- **Descrizione** \*: puoi metterci più colonne, per esempio la descrizione e le note, e si uniscono;
- **Importo con segno**, oppure **Uscite** ed **Entrate** se la banca usa due colonne (a volte si chiamano Dare e Avere) \*;
- **Data valuta**: se la lasci vuota, è uguale alla data contabile;
- **Causale / tipo operazione**: se la lasci vuota, il programma la ricava dalla descrizione (vedi sotto);
- **Saldo**: se il file ha una colonna con il saldo dopo ogni movimento.

Quando apri la finestra il programma ha già fatto una **proposta**, dai nomi delle colonne: di solito basta controllarla. Sopra le caselle scegli il **conto** in cui importare, oppure **Nuovo conto…**, a cui dai un nome (l'IBAN è facoltativo). Se ti accorgi che la riga delle intestazioni non era quella giusta, **Indietro** ti riporta al passo 1.

Sotto c'è l'**anteprima**: le prime righe come le importerà il programma, con le uscite in rosso e le entrate in verde, e il numero di movimenti trovati. Se una data o un importo non si leggono, la riga lo dice. **Importa** si accende quando è tutto pronto; poi vedi il riepilogo come per gli altri file.

**La seconda volta è più veloce.** Il programma ricorda come hai abbinato le colonne. Quando importi un altro file della stessa banca, con le stesse intestazioni, la finestra si apre direttamente al passo 2, con le caselle già piene e il conto già scelto: basta premere Importa.

**Cosa manca a un file Excel, e come si rimedia**

- **La causale.** Se il file non ha una colonna con la causale, il programma la ricava dalla descrizione: "POS" o "PagoBancomat" diventano pagamenti con carta, "bonifico" un bonifico (fatto o ricevuto, secondo il segno), "prelievo" un prelievo, "stipendio" lo stipendio, e così via. Così i movimenti partono già con un gruppo. Le causali ricavate finiscono tutte con **XL**, così le distingui da quelle della banca. I movimenti di cui il programma non riconosce il tipo hanno la **causale fittizia 00XL**, senza gruppo, e in [Movimenti](cap:movimenti) l'etichetta rossa "causale fittizia". Per ognuno scegli la causale giusta: dal menu **⋮** del movimento, **Scegli la causale…**, oppure selezionandone più di uno e usando **Scegli la causale** nella barra in fondo. Il gruppo arriva con la causale. Allo stesso modo puoi correggere una causale ricavata male. Alla causale fittizia non si dà un gruppo in [I gruppi di causali](cap:gruppi): varrebbe per tutti i movimenti non riconosciuti, qualunque cosa siano.
- **I saldi.** Se il file non ha la colonna del saldo, scrivi nel campo **Saldo del conto dopo l'ultimo movimento** quanto avevi sul conto quel giorno (lo trovi nell'home banking): il programma ricava i saldi di tutti i giorni. Se lo lasci vuoto, i movimenti si importano lo stesso, ma per quel periodo l'andamento del saldo resta vuoto.
- **Il riferimento della banca**, che serve a riconoscere i movimenti già importati. Il programma ne costruisce uno da data, importo e descrizione: se importi di nuovo un periodo già importato dallo stesso tipo di file, i movimenti **non si duplicano**.

**Un conto, un tipo di file.** Un movimento importato dal file CBI e lo stesso movimento importato dall'Excel hanno riferimenti diversi, e verrebbero contati **due volte**. Per ogni conto usa sempre lo stesso tipo di file. Se scegli un conto che ha già movimenti da file CBI o XML, la finestra te lo ricorda.

**Altri casi.**

- Il file può essere un vero `.xlsx`, un `.csv`, o un file `.xls` che in realtà è testo o una pagina web (molte banche fanno così): il programma li legge tutti.
- Il vecchio formato di Excel 97-2003 non si legge: aprilo con Excel e salvalo come **Cartella di lavoro di Excel (.xlsx)**.
- Puoi importare il file anche mentre è aperto in Excel.

## La pagina dei movimenti {#movimenti}

In [Movimenti](app:movimenti) trovi l'elenco di tutti i movimenti importati, dal più recente.

**Gli importi**

- in **verde** le entrate (soldi arrivati sul conto);
- in **rosso** le uscite (soldi usciti dal conto).

**Cercare un movimento**

In alto ci sono i filtri: conto, date, tipo (entrate o uscite), importo, testo nella descrizione, causale e **straordinari e tipo scelto** (vedi più sotto). Compila quelli che ti servono e premi **Applica filtri**. **Azzera** li toglie tutti. Per **ordinare** l'elenco clicca l'intestazione di una colonna (data, importo, descrizione…); cliccala di nuovo per invertire l'ordine. L'ordine vale per tutti i movimenti trovati, non solo per la pagina che vedi. I filtri e l'ordine restano finché il programma è aperto: se vai a un'altra pagina (per esempio i movimenti scartati o i gruppi di causali) e torni, li ritrovi come li avevi lasciati.

Il campo **Periodo** offre gli stessi periodi della pagina Analisi (ultimo mese completo, ultimi 3, 6 o 12 mesi completi, i singoli anni, tutto): sceglierne uno riempie le due date, che puoi poi ritoccare a mano. Se le date non corrispondono a nessun periodo, il campo mostra "Personalizzato".

**Sommare dei movimenti**

Spunta la casella a sinistra delle righe che ti interessano: in fondo alla pagina compare una barra con il numero di righe, il totale delle entrate, il totale delle uscite e la somma.

Per selezionare tutti i movimenti trovati dai filtri, anche quelli nelle altre pagine, premi **Seleziona tutti i risultati**.

Esempio: vuoi sapere quanto hai pagato quest'anno per la luce e il gas? Le bollette addebitate sul conto riportano il nome del fornitore nella descrizione. Scrivi il nome del fornitore in "Testo nella descrizione", scegli l'anno nel campo Periodo, premi **Applica filtri** e poi **Seleziona tutti i risultati**: la somma è in fondo.

**Cosa non trovi nell'elenco**

Il programma vede solo quello che passa dal conto corrente:

- i pagamenti con la **carta di credito** compaiono come un unico addebito al mese, per il totale: i singoli acquisti sono nell'estratto conto della carta;
- i pagamenti in **contanti** non compaiono: vedi solo il prelievo.

**Movimenti straordinari e tipo di un singolo movimento**

Ogni movimento prende il tipo dal suo gruppo di causali (vedi [I gruppi di causali](cap:gruppi)). A volte però un movimento è diverso dagli altri dello stesso gruppo: per esempio, tra tanti bonifici normali, quello per un impianto di aria condizionata è una spesa che non si ripeterà.

Il pulsante **⋮** in fondo a ogni riga apre un piccolo menu:

- **Segna come straordinario**: il movimento resta quello che è, ma diventa *straordinario*. Nelle [Analisi](app:analisi) puoi escludere gli straordinari con un interruttore; nella simulazione del saldo sono esclusi già all'inizio. Accanto al movimento compare l'etichetta "straordinario".
- **Tipo di questo movimento**: scegli un tipo diverso da quello del gruppo. Per esempio un bonifico verso un tuo altro conto è un **Giroconto**, uno verso un fondo è un **Investimento**. "Come il gruppo" torna al tipo del gruppo.
- **Scegli la causale…**: c'è solo per i movimenti importati da un file Excel, la cui causale l'ha ricavata il programma (vedi [Importare un file Excel](cap:excel)). Cerchi la causale per nome o per gruppo, per esempio "bonifico" o "utenze", e il movimento prende quella causale e il suo gruppo. Serve soprattutto per quelli con l'etichetta "causale fittizia". I movimenti dei file CBI e XML hanno la causale della banca, che non si cambia.

Per segnarne tanti insieme, selezionali con le caselle a sinistra e usa **Segna come straordinari** nella barra in fondo alla pagina. Se tra quelli selezionati ci sono movimenti dei file Excel, la barra offre anche **Scegli la causale**, che vale solo per quelli.

Per **ritrovarli**, per esempio per togliere l'etichetta, usa il filtro **Straordinari e tipo scelto**: "Solo gli straordinari" mostra tutti quelli che hai segnato, "Solo con il tipo scelto a mano" quelli a cui hai cambiato il tipo. Poi dal menu **⋮** togli l'etichetta, oppure selezionali e usa **Non straordinari** nella barra in fondo.

Lo stesso menu c'è anche negli elenchi di movimenti che si aprono dalle analisi (senza la scelta della causale). Le scelte, causale compresa, restano anche se importi di nuovo lo stesso file.

## I movimenti scartati {#scarti}

Quando importi un file, il programma riconosce i movimenti già presenti e non li duplica.

Raramente capita che un movimento del file sembri lo stesso di uno già salvato (stessa data, stesso importo, stesso riferimento) ma abbia **qualche dettaglio diverso**, per esempio la descrizione. In questo caso il programma non sa quale sia quello giusto: **tiene quello che aveva già** e mette l'altro tra gli scartati, così puoi controllarlo.

Se ce ne sono, in [Movimenti](app:movimenti) compare un'etichetta rossa, "N movimenti scartati": cliccala per aprire l'elenco. Aprendo una riga vedi i due movimenti uno accanto all'altro, con le differenze in rosso.

Nella maggior parte dei casi non devi fare niente: è solo un controllo.

## Le analisi {#analisi}

In [Analisi](app:analisi) trovi i numeri e i grafici del tuo conto. In alto scegli il **periodo**: ultimo mese completo, ultimi 3, 6 o 12 mesi completi, un anno, oppure tutto.

- I periodi **completi** sono fatti di mesi interi e finiscono con l'ultimo mese finito: se hai importato movimenti fino al 3 novembre, "Ultimo mese completo" è ottobre e "Ultimi 3 mesi completi" va da agosto a ottobre. Così le medie al mese sono sempre su mesi interi. I giorni del mese in corso li vedi con l'anno in corso, con "Tutto" o con le date a mano.
- L'**anno in corso** comprende quello che c'è, fino all'ultimo movimento importato; gli anni passati sono completi.

Il periodo riempie le due date **Data contabile da** e **Data contabile a**, che puoi cambiare a mano, come nella pagina dei movimenti: per esempio dal 1° maggio al 31 agosto. Il periodo diventa allora **Personalizzato**, e vale anche quando apri il dettaglio di un numero e nella simulazione del saldo.

Le **medie al mese** (sotto entrate, uscite, flusso netto e investito netto) contano i mesi che il periodo prende per intero, più quelli **incompleti in proporzione ai giorni**. Con i periodi completi questo capita solo all'inizio dei tuoi dati, se il primo file importato comincia a metà mese, o con l'anno in corso, che si ferma all'ultimo movimento; capita invece spesso con le date scelte a mano. Per esempio, dal 1° agosto al 20 ottobre fa agosto + settembre + 20 giorni su 31 di ottobre = 2,65 mesi. Contano solo i giorni in cui ci sono movimenti importati: l'anno in corso finisce all'ultimo movimento, non al 31 dicembre. Quando c'è un mese incompleto compare un **avviso**: se un'entrata o una spesa importante cade nei giorni che mancano (per esempio lo stipendio a fine mese) la media non è affidabile, e conviene scegliere mesi completi. Con meno di un mese la media non si mostra.

**I sei numeri in alto**

- **Entrate**: i soldi arrivati sul conto (stipendio, bonifici ricevuti…).
- **Uscite**: i soldi spesi.
- **Flusso netto**: entrate meno uscite. Se è positivo hai messo da parte dei soldi, se è negativo hai speso più di quanto è entrato. Sotto vedi anche la media al mese: è il numero da cui parte la simulazione del saldo, che ragiona mese per mese. La media al mese c'è anche per entrate, uscite e investito netto.
- **Tasso di risparmio**: quanta parte delle entrate non hai speso, in percentuale.
- **Investito netto**: i soldi spostati verso investimenti (fondi, PAC, titoli), meno quelli rientrati dalle vendite. Se nei conti scelti non c'è nessun movimento di investimento, in nessun periodo, questa scheda non compare: sarebbe sempre zero. Compare da sola appena un movimento (o una causale, nei [Gruppi di causali](app:gruppi)) è di tipo Investimento.
- **Saldo a fine periodo**: quanto c'era sul conto alla fine del periodo.

Passa con il mouse su un numero per vedere come è calcolato. **Cliccalo** per vedere tutti i movimenti che lo compongono: è il modo migliore per capire da dove viene.

L'interruttore **Escludi straordinari**, accanto al periodo, toglie da numeri e grafici i movimenti che hai segnato come straordinari (vedi [La pagina dei movimenti](cap:movimenti)). È spento all'inizio: quelle spese sono avvenute davvero. Acceso, aiuta a vedere com'è un periodo "normale". Il saldo invece resta quello vero della banca. Accanto, **Vedi gli straordinari** apre l'elenco di quelli del periodo, con il loro numero: dal menu ⋮ di ogni riga puoi togliere l'etichetta, e chiudendo l'elenco i numeri si aggiornano.

Nella pagina che si apre:

- la sezione **Per gruppo** divide i movimenti per gruppo: clicca un gruppo per vedere solo i suoi movimenti (la riga scelta si colora, le altre si attenuano); cliccalo di nuovo per tornare a vederli tutti;
- sopra l'elenco dei movimenti puoi **cercare** una parola, una causale o un importo (per esempio "325,40") e scegliere un intervallo di **date**: accanto compaiono quanti movimenti hai trovato e il loro totale;
- più in basso, chiusi, ci sono i **movimenti esclusi**: quelli del periodo che non entrano in quel numero, ciascuno con il motivo (per esempio un versamento in un fondo, che non è una spesa);
- per il **saldo** c'è anche la **riconciliazione**: dal saldo di inizio periodo, sommando i movimenti, si deve arrivare al saldo della banca. Se c'è una differenza, probabilmente manca un file da importare.

La freccia in alto a sinistra riporta alle analisi, con il periodo e i conti che avevi scelto.

**Perché gli investimenti non sono uscite?**

Quando metti 500 € in un fondo, quei soldi non sono spariti: hanno solo cambiato posto. Per questo il programma non li conta come spese, ma a parte, in "Investito netto". Allo stesso modo, quando vendi un titolo, i soldi che rientrano non sono un guadagno.

**I grafici**

- **Entrate e uscite per mese**: le barre sopra lo zero sono le entrate, quelle sotto le uscite; la linea è il flusso netto del mese.
- **Andamento del saldo**: come è cambiato il saldo giorno per giorno.
- **Dove vanno i soldi** e **Da dove arrivano**: le uscite e le entrate divise per gruppo.
- **Spese ricorrenti e variabili**: le spese fisse (mutuo, bollette, tasse) e tutte le altre.
- **Flusso netto cumulato**: quanto hai messo da parte dall'inizio dell'anno, a confronto con l'anno prima. Se la linea sale, in quel mese è entrato più di quanto è uscito; se scende, hai speso più di quanto è entrato.
- **Movimenti più grandi**: in fondo alla pagina, i dieci movimenti più grandi del periodo, entrate e uscite.

Passando con il mouse su un grafico vedi i valori precisi.

Nei grafici **Dove vanno i soldi** e **Da dove arrivano** puoi anche **cliccare una barra**: si apre l'elenco dei movimenti di quel gruppo. Per esempio, clicca "Mutuo" per vedere tutte le rate del periodo.

Allo stesso modo, in **Entrate e uscite per mese** e in **Spese ricorrenti e variabili** puoi **cliccare una colonna**: si apre l'elenco dei movimenti di quel mese. Cliccando la parte verde (entrate) vedi le entrate del mese, la parte rossa le uscite, un punto della linea il flusso netto; nel grafico delle spese, la parte ricorrente o quella variabile. I conti e gli straordinari restano quelli scelti nella pagina Analisi.

Nella legenda sopra alcuni grafici (per esempio "Entrate · Uscite · Flusso netto") puoi cliccare una voce per nasconderla o mostrarla.

**Il report Excel**

Con il menu **Report > Genera report Excel** della pagina Analisi crei un file Excel con le analisi che stai guardando: stesso periodo, stessi conti, straordinari esclusi o no. La finestra ti mostra queste scelte; se vuoi cambiarle, chiudila e cambiale nella pagina. Poi premi **Scegli dove salvarlo**.

Il file ha tre fogli:

- **Analisi**: gli indicatori (entrate, uscite, flusso netto, tasso di risparmio, investito netto, medie al mese, saldo), le tabelle per mese e per gruppo e quattro grafici: entrate e uscite per mese, andamento del saldo, uscite per gruppo, entrate per gruppo. Indicatori e tabelle sono **formule** che leggono il foglio Movimenti: puoi vedere come sono calcolati;
- **Movimenti**: tutti i movimenti del periodo e dei conti, come tabella con i filtri nelle intestazioni. Ci sono anche investimenti, giroconti e straordinari: le colonne **Tipo** e **Nelle analisi** dicono quali contano negli indicatori;
- **Saldo**: il saldo di ogni giorno.

Da lì in poi il file è tuo: puoi filtrare, aggiungere colonne, fare grafici e tabelle pivot. Si apre anche con Google Fogli (caricandolo su Drive o con **File > Importa**).

> **Attenzione:** il file Excel **non è cifrato**. Chiunque lo apra vede tutti i movimenti del periodo. Salvalo in una cartella solo tua, non lasciarlo in cartelle condivise e cancellalo quando non ti serve più.

## La simulazione del saldo {#simulazione}

La simulazione risponde a una domanda: **come andrà il saldo nei prossimi mesi**, se entrate e uscite restano come nel passato? Si apre dal pulsante **Simula il saldo futuro** nelle [Analisi](app:analisi), oppure dal menu in alto **Simulazione > Simula il saldo futuro**.

**1. Scegli la base**

- **Periodo**: il passato su cui fare la media, come nelle analisi (per esempio gli ultimi 12 mesi completi). Se in Analisi hai scelto le date a mano, qui trovi già quel periodo ("Dal … al …"). Per i mesi incompleti vale quello che c'è scritto in [Analisi](cap:analisi): contano in proporzione ai giorni, e un avviso te lo ricorda. Se il periodo ha **meno di un mese** di movimenti (per esempio con date scelte a mano di pochi giorni) la simulazione non si fa: pochi giorni moltiplicati darebbero medie senza senso, e la pagina ti chiede di scegliere un periodo più lungo.
- **Mesi da simulare**: 6, 12, 24 o 36.
- **Gruppi da escludere**: le spese (o le entrate) che non si ripeteranno, per esempio dei lavori in casa già finiti. Senza di loro la media è più realistica.
- **Escludi i movimenti straordinari**: acceso all'inizio, lascia fuori dalla media i singoli movimenti che hai segnato come straordinari, anche se il loro gruppo resta. **Vedi gli straordinari del periodo** li elenca, e lì puoi togliere l'etichetta.

Sotto vedi le medie al mese: entrate, uscite, flusso netto, tasso di risparmio, investito, e il saldo da cui si parte.

**2. Prova le tue ipotesi**

- **Flusso netto al mese**: parte dalla media; cambialo per provare a spendere di più o di meno. Il **tasso di risparmio** si aggiorna subito: le entrate restano quelle medie.
- **Investito netto al mese**: quanto metti ogni mese in PAC o fondi.
- **Ripristina i valori della base** riporta tutto alle medie.

**3. Leggi il risultato**

Ogni mese il saldo del conto cambia di **flusso netto meno investito**: la pagina lo scrive sotto le ipotesi, per esempio "+150 € − 400 € = −250 €". Vedi:

- il saldo alla fine dell'ultimo mese simulato e quanto è cambiato rispetto a oggi, diviso in quanto hai risparmiato e quanto hai versato negli investimenti;
- il grafico: la linea piena è il saldo reale, quella tratteggiata gialla il saldo simulato; se versi negli investimenti c'è anche una linea a puntini, **conto + versato negli investimenti**, che mostra quanti soldi hai in tutto;
- se il saldo scende sotto zero, il primo mese in cui succede.

**Perché il saldo può scendere anche se risparmio?**

Il **tasso di risparmio** dice quanta parte delle entrate non spendi. I soldi che versi in PAC o fondi non li spendi: li sposti dal conto agli investimenti, e restano tuoi. Se versi più di quanto risparmi, il conto scende, ma il totale dei tuoi soldi cresce lo stesso.

Esempio: risparmi 150 € al mese e ne versi 400 nei PAC. Il conto scende di 250 € al mese, ma conto più investimenti salgono di 150 € al mese.

**4. Cambia un mese preciso**

Nella tabella **Mese per mese** puoi scrivere un flusso netto diverso per un singolo mese: una tredicesima a dicembre, l'assicurazione dell'auto a marzo, le vacanze ad agosto. Il mese si colora; la freccia accanto lo riporta al valore di tutti gli altri mesi.

La simulazione è un'ipotesi, non una previsione: dice cosa succede **se** le cose vanno come hai scritto. Le scelte non vengono salvate: chiudendo la pagina si riparte dalle medie.

## Il grafico personalizzato {#grafico}

Le analisi ragionano per gruppi di causali, ma a volte vuoi seguire una spesa precisa che sta dentro un gruppo più grande: le bollette della luce, il carburante, le spese per un figlio. Il [grafico personalizzato](app:grafico) (menu **Grafici > Grafico personalizzato** nella pagina Analisi) mostra mese per mese entrate e uscite dei soli movimenti che scegli tu.

**Come si usa**

1. In alto ci sono gli stessi filtri della pagina dei movimenti. Compila quelli che ti servono: per esempio scrivi il nome del fornitore in **Testo nella descrizione**.
2. Premi **Aggiungi al grafico**: tutti i movimenti che corrispondono ai filtri entrano nel grafico, anche quelli nelle altre pagine della griglia, e nella griglia compaiono spuntati.
3. Controlla la griglia e **togli la spunta** ai movimenti che non c'entrano (per esempio un rimborso che contiene lo stesso nome). Puoi anche spuntarne altri a mano.
4. Per aggiungere altri movimenti cambia i filtri e premi di nuovo **Aggiungi al grafico**: quelli di prima restano.

Gli altri pulsanti:

- **Cerca soltanto** mostra i risultati dei filtri senza cambiare il grafico, per guardare prima di aggiungere;
- **Togli dal grafico** toglie tutti i movimenti che corrispondono ai filtri;
- **Svuota il grafico** ricomincia da zero;
- l'interruttore **Mostra solo i movimenti nel grafico** fa vedere nella griglia soltanto quelli scelti, per ricontrollarli.

**Il grafico**

In fondo alla pagina, il grafico si aggiorna a ogni spunta: le entrate sopra lo zero, le uscite sotto, la linea è il flusso netto del mese. Sopra ci sono il numero dei movimenti scelti e i totali di entrate, uscite e flusso netto, con la media al mese. **Clicca una colonna** (entrate o uscite) o un punto della linea per aprire l'elenco dei movimenti scelti di quel mese, con il totale: con la colonna delle uscite vedi solo le uscite, con il punto del flusso netto tutti i movimenti del mese. La media si fa dal mese del primo movimento scelto a quello dell'ultimo, compresi i mesi in cui non ce n'è nessuno: se una spesa arriva ogni due mesi, la media al mese è la metà della singola spesa.

Qui contano tutti i movimenti che scegli, qualunque sia il loro gruppo o tipo: anche i versamenti negli investimenti e i giroconti, se li spunti.

La scelta resta finché il programma è aperto, anche se cambi pagina; alla chiusura si perde. I movimenti importati dopo non entrano da soli: per aggiungerli, premi di nuovo **Aggiungi al grafico** con gli stessi filtri.

## I gruppi di causali {#gruppi}

Ogni movimento ha una **causale**: un codice di quattro caratteri con cui la banca dice che tipo di operazione è. Per esempio 2700 è "Emolumenti" (lo stipendio), 4500 è "Utilizzo carta di credito".

- Le **prime due cifre** sono la **causale CBI**: un codice dello standard CBI, uguale per tutte le banche italiane. Per esempio 48 è "Bonifico a Vostro favore", 43 "Pagamento tramite POS", 27 gli stipendi.
- Le **ultime due** le sceglie la banca, per distinguere i suoi sottotipi; per il programma non contano.

Per fare i grafici, il programma mette i movimenti in **gruppi**: "Stipendio", "Mutuo", "Carta di credito" e così via. Il gruppo si sceglie per **causale CBI**, in [Gruppi di causali](app:gruppi) (menu **Impostazioni > Dati > Gruppi di causali**, oppure **Dati > Gruppi di causali** nella pagina dei movimenti: da lì la freccia in alto riporta ai movimenti). Il gruppo di una causale CBI vale per **tutti i movimenti con quel codice**: per esempio il gruppo del 48 vale per 4800, 4801, per i 48 di qualunque altra banca e per i bonifici ricevuti dei file Excel (48XL), anche per quelli che importerai in futuro.

La pagina ha una riga per ogni causale CBI, con la descrizione dello standard, quanti movimenti hai con quel codice e il loro totale. Per ogni causale CBI scegli:

- **Gruppo**: il nome che vedrai nei grafici. La tendina propone tutti i gruppi già usati; puoi anche scriverne uno nuovo, oppure svuotarlo per lasciare la causale senza gruppo.
- **Tipo**:
  - **Ordinario**: un'entrata o un'uscita normale. Quasi tutte le causali sono di questo tipo.
  - **Investimento**: soldi spostati verso investimenti o rientrati da essi. Non contano né come spese né come entrate.
  - **Giroconto**: soldi spostati tra due tuoi conti. Non contano nelle analisi.
- **Ricorrente**: spuntalo per le spese fisse, come mutuo, bollette e tasse.

Poi premi **Salva** sulla riga.

Il programma parte con un gruppo già proposto per le causali CBI più comuni. Restano senza gruppo quelle che possono voler dire cose diverse, come gli storni, e le causali che alcune banche usano a modo loro: la BCC, per esempio, usa il **31** per gli addebiti diretti (SDD), che per lo standard sarebbe un'altra cosa. Dagli tu il gruppo che preferisci. Le causali senza gruppo nei grafici finiscono in "Altro".

**Trovare una causale CBI.** All'inizio vedi solo le causali CBI dei tuoi movimenti; togli **Solo le causali dei miei movimenti** per vederle tutte, per esempio per preparare i gruppi prima di importare. Poi:

- **Cerca**: scrivi il codice (per esempio "48") o una parola della descrizione o del gruppo (per esempio "bonifico");
- **Gruppo**: mostra solo un gruppo, oppure solo le causali **senza gruppo**.

I filtri guardano le scelte già salvate: una riga che stai modificando non sparisce finché non premi **Salva**.

**Dove finisce una causale?** La freccia in fondo alla riga apre l'analisi in cui finiscono i suoi movimenti (entrate, uscite o investimenti), già filtrata sul suo gruppo.

**Un movimento è diverso dagli altri?** Il gruppo vale per tutti i movimenti della causale CBI. Se uno solo è diverso, per esempio un bonifico verso un tuo altro conto o un addebito che in realtà è un investimento, cambialo dal menu **⋮** del movimento (vedi [La pagina dei movimenti](cap:movimenti)): puoi sceglierne il tipo o segnarlo come straordinario.

**Salvare i gruppi in un file e riprenderli.** In alto a destra nella pagina ci sono due pulsanti:

- **Esporta** salva in un file i gruppi delle causali CBI: il gruppo, il tipo e se è ricorrente. Il file (`EasyCashFlow-gruppi-causali-` e la data, `.json`) non contiene movimenti né importi.
- **Importa** riprende i gruppi da un file esportato prima. Prima di cambiare qualcosa ti mostra, causale per causale, cosa succede: quelle che **cambiano**, con il gruppo, il tipo o la spesa di adesso barrati e quelli del file evidenziati accanto; quelle che **si aggiungono**, con il loro gruppo; quante sono già uguali; le righe del file non valide, con il motivo. Poi aspetta la tua conferma. Le causali che il file non ha restano come sono. Un file esportato con una versione precedente del programma, che aveva i gruppi delle causali complete, si importa ancora: ogni causale CBI prende il gruppo più usato dalle sue causali.

Serve quando riparti da zero, per esempio con il programma in una cartella nuova o su un altro computer: importi il file dei gruppi, anche prima dei movimenti, e non devi rifare a mano le tue scelte.

## Conti, preferenze e sicurezza {#impostazioni}

**I conti**

In [Impostazioni](app:impostazioni) trovi i conti importati, di qualunque banca: per ognuno IBAN, codice della banca (ABI) e divisa, letti dal file. Il nome che scegli compare in tutto il programma; premi **Salva** dopo averlo cambiato. Con un nome chiaro per ogni conto ("Conto di casa", "Conto online") la scelta dei conti nelle pagine si legge al volo.

Un conto che non ti interessa più si elimina con il **cestino** in fondo alla sua riga, insieme a tutti i suoi dati: movimenti, saldi giornalieri, movimenti scartati e le scelte fatte sui singoli movimenti (straordinari e tipo). Prima di eliminare, la finestra di conferma mostra quanti movimenti ha il conto e di quale periodo, e per confermare devi scrivere il nome del conto. **Non si può annullare**: se pensi che i dati ti possano servire, fai prima un [backup](app:backup). Restano i gruppi di causali, che valgono per tutti i conti. Se poi importi di nuovo un file di quel conto, il conto ricompare con il nome provvisorio, come la prima volta.

Quando i conti sono più di uno, nella Home, nei movimenti e nelle analisi compare la scelta dei **conti** (la simulazione e il report Excel usano quelli delle analisi): nessuno scelto vuol dire tutti, cioè il quadro complessivo. Se ne guardi soprattutto uno, puoi farlo proporre già scelto (in Generali, qui sotto).

**I dati**

Nel menu **Impostazioni > Dati** ci sono anche **Percorso dati**, che mostra dove sono i file del programma e apre le cartelle, e **Backup**, che crea una copia di sicurezza verificata: sono spiegati in [I tuoi dati](cap:dati).

**Le preferenze generali**

In [Generali](app:generali) (menu **Impostazioni > Preferenze > Generali**) puoi scegliere:

- la **pagina iniziale**, quella che vedi dopo aver inserito la password: Home, Movimenti o Analisi;
- il **conto** già scelto nei filtri di Movimenti e di Analisi, utile se hai più conti e ne guardi soprattutto uno;
- il **periodo** proposto in Analisi e nella simulazione (per esempio gli ultimi 12 mesi completi, oppure l'anno più recente);
- per quanto tempo tenere i **log**, da una settimana a un anno: i file più vecchi si cancellano da soli.

Conto e periodo sono solo il punto di partenza: nelle pagine puoi sempre cambiarli.

**L'aspetto**

In [Aspetto](app:aspetto) (menu **Impostazioni > Preferenze > Aspetto**) puoi scegliere:

- il **tema**: chiaro, scuro, oppure come Windows;
- il **colore principale** dei pulsanti e dei menu;
- lo **stile delle icone** e dei **pulsanti**;
- la **dimensione** di tutto il programma, se i testi ti sembrano troppo piccoli o troppo grandi.

Ogni scelta si applica subito e viene ricordata.

**La sicurezza**

In [Sicurezza](app:sicurezza) (menu **Impostazioni > Preferenze > Sicurezza**) puoi:

- **cambiare la password**: scrivi quella attuale e due volte quella nuova (anche qui c'è **Suggerisci una frase**). Cambia solo il file `chiave.json`: i tuoi dati restano come sono e il codice di recupero continua a valere; dal prossimo avvio userai la password nuova. Attenzione alle copie di sicurezza fatte prima: contengono il `chiave.json` vecchio e si aprono con la password vecchia (vedi [I tuoi dati](cap:dati));
- creare un **nuovo codice di recupero**, scrivendo la tua password. Serve se hai perso il codice, se non hai fatto in tempo ad annotarlo o se pensi che qualcuno l'abbia visto. Il codice nuovo si vede una volta sola, come il primo, e quello vecchio smette subito di valere;
- scegliere dopo quanto tempo senza usarlo il programma si **blocca**: 5, 10, 15 (la scelta iniziale), 30 o 60 minuti, oppure mai. Bloccato, mostra solo la richiesta della password; quando la scrivi ritrovi tutto come l'avevi lasciato. Con il pulsante **Blocca adesso**, sotto la scelta dei minuti, o con la voce **Blocca** (il lucchetto) in fondo alla barra a sinistra, lo blocchi subito, per esempio prima di allontanarti dal computer.

## Domande frequenti {#domande}

**Mentre lo uso, il programma mi chiede di nuovo la password.**

È il blocco per inattività: se per un po' non muovi il mouse e non scrivi nella finestra, il programma si blocca, così chi passa davanti al computer non vede i tuoi dati. Scrivi la password e ritrovi tutto com'era. Puoi allungare il tempo o spegnere il blocco in [Sicurezza](app:sicurezza).

**Ho perso il codice di recupero, ma ricordo la password.**

Apri il programma con la password e vai in [Sicurezza](app:sicurezza): con la password puoi creare un codice di recupero nuovo. Quello perso smette di valere.

**Ho dimenticato la password. Cosa faccio?**

Nella schermata della password clicca **Ho dimenticato la password** e scrivi il **codice di recupero** che hai ricevuto quando l'hai creata (maiuscole, spazi e trattini non contano). Poi scegli una password nuova: riceverai anche un codice di recupero nuovo, perché quello usato non vale più. Conserva il nuovo come il primo.

Se non hai più nemmeno il codice, i dati non si possono aprire: ricomincia da una cartella nuova e importa di nuovo i file CBI (di testo o XML) scaricati dalla banca.

**Ho sbagliato la password più volte e devo aspettare.**

Dopo cinque tentativi sbagliati di fila il programma ti fa aspettare qualche secondo prima di riprovare, e l'attesa cresce se continui a sbagliare. Serve a scoraggiare chi prova a indovinarla sul tuo computer.

**All'avvio un messaggio dice che il database è di una versione di prova precedente.**

Le versioni di prova fino alla 0.9.2 non cifravano i dati, e questa versione non apre quei database. Cancella le cartelle **dati** e **log** che stanno accanto al programma (se dimentichi i log, li cancella il programma quando crei la password), avvialo di nuovo e crea la password; poi importa di nuovo i file CBI o XML scaricati dalla banca. Movimenti e saldi tornano tutti; vanno rifatti a mano i movimenti straordinari, i gruppi di causali che avevi cambiato, i nomi dei conti e le scelte di [Aspetto](app:aspetto).

**Ho importato due volte lo stesso file. È un problema?**

No. I movimenti già presenti vengono riconosciuti e non si duplicano. Nel riepilogo li vedi come "già presenti".

**Cosa sono le operazioni non ancora contabilizzate?**

Sono pagamenti, di solito con la carta, che la banca ha già registrato ma non ancora completato. Se scarichi l'export proprio in quei giorni, la banca ne tiene conto nel saldo ma non li mette nel file. Il programma te lo segnala con un avviso e non perde niente: quei movimenti arriveranno con l'export successivo, quando la banca li avrà completati.

**Perché il saldo nelle analisi è vuoto o parte da una certa data?**

Il saldo si salva ogni volta che importi un file, per le giornate di quel file. Se vuoi lo storico completo, importa di nuovo anche i file più vecchi: i movimenti risulteranno "già presenti", ma i saldi verranno aggiunti.

**Un movimento finisce nel gruppo sbagliato. Come lo correggo?**

Il gruppo dipende dalle prime due cifre della causale, la causale CBI. Vai in [Gruppi di causali](app:gruppi), cerca quella causale CBI e cambiale gruppo. Attenzione: la modifica vale per tutti i movimenti con quella causale CBI.

Se invece è un solo movimento a fare eccezione, non serve cambiare il gruppo: dal menu **⋮** del movimento puoi sceglierne il tipo (per esempio Giroconto) o segnarlo come straordinario. Vedi [La pagina dei movimenti](cap:movimenti).

**Ho importato per sbaglio il file di un conto che non volevo. Come lo tolgo?**

Vai in [Impostazioni](app:impostazioni) e premi il cestino nella riga di quel conto: il conto sparisce con tutti i suoi movimenti, e gli altri conti restano come sono. Vedi [Conti, preferenze e sicurezza](cap:impostazioni).

**Il programma dice che l'ultimo movimento è vecchio.**

È un promemoria: è passata più di una settimana dall'ultimo movimento importato. Scarica un export più recente dalla banca e importalo.

## I tuoi dati {#dati}

**Dove sono**

Tutto è nella cartella del programma:

- **dati**: contiene il file `easycashflow.db`, con movimenti, saldi, conti e gruppi; il file `chiave.json`, con la chiave del database protetta dalla password; il file `preferenze.json`, con le scelte di [Aspetto](app:aspetto) e i giorni di conservazione dei log (le altre preferenze stanno nel database cifrato);
- **log**: il diario di quello che fa il programma, un file per giorno, che si cancella da solo dopo 60 giorni (puoi cambiarlo in [Generali](app:generali)).

**Come sono protetti**

Il file `easycashflow.db` contiene **tutti i dettagli dei tuoi movimenti**: importi, date, IBAN, nomi delle persone e delle aziende a cui hai pagato o da cui hai ricevuto soldi. Dalla versione 0.9.3 è **cifrato**: aperto con un altro programma, è solo una sequenza di byte senza senso.

Il meccanismo è questo:

1. quando crei la password, il programma genera una **chiave** a caso, lunga 256 bit: è quella che cifra il database. Non la scegli tu e non la vedi;
2. la chiave viene salvata nel file **`chiave.json`**, accanto al database, ma **mai in chiaro**: il file la contiene due volte, una chiusa con la tua **password** e una chiusa con il **codice di recupero**. Né la password né il codice sono salvati da qualche parte;
3. a ogni avvio scrivi la password: il programma la usa per riaprire la chiave in `chiave.json`, e con la chiave apre il database. Il passaggio dalla password alla chiave è volutamente lento (circa mezzo secondo) e usa molta memoria: per te è un attimo, ma chi volesse provare milioni di password per indovinare la tua impiegherebbe tantissimo;
4. con il codice di recupero si riapre la seconda copia della chiave: per questo il codice apre i dati anche se dimentichi la password.

Cambiare la password o il codice di recupero vuol dire richiudere la stessa chiave con il segreto nuovo: cambia solo `chiave.json`, il database resta com'è.

**Perché la chiave sta in un file accanto ai dati**

EasyCashFlow è *portable*: non si installa e funziona dalla cartella in cui lo metti. Si poteva affidare la chiave a Windows, che la terrebbe legata al tuo utente; ma allora i dati si aprirebbero solo su quel computer e con quell'utente, e una copia della cartella su un altro PC sarebbe inutilizzabile. Tenendo la chiave in `chiave.json`, chiusa con la password, la cartella si porta dove vuoi e si apre ovunque con la tua password.

Il prezzo di questa scelta è che chi copia la cartella `dati` ha in mano sia il database sia `chiave.json`: a separarlo dai tuoi movimenti c'è **solo la password**. Per questo deve essere **robusta**: lunga, usata solo qui, impossibile da indovinare conoscendoti. Una password corta o comune si indovina con tanti tentativi fatti con calma su una copia, anche se ognuno costa tempo.

**Mentre il programma è aperto**

Quando scrivi la password la chiave viene riaperta e resta **nella memoria del computer** finché il programma è aperto e sbloccato: serve per leggere e scrivere i dati. Vuol dire che:

- se qualcuno si avvicina allo schermo, clicca l'**occhio** accanto al punto di domanda in cima a ogni pagina, oppure premi **Ctrl+H**: tutti gli importi diventano **••••••**, in ogni pagina, nelle finestre e nei grafici (dove restano colonne e linee, ma non i numeri); i campi in cui si scrive un importo e le spiegazioni dei calcoli si sfocano. Cliccando di nuovo l'occhio, o con Ctrl+H, tornano visibili. La scelta resta anche alla prossima apertura del programma;
- chi si siede al computer mentre il programma è aperto vede tutto, come lo vedi tu. Per questo il programma si **blocca da solo** dopo un po' che non lo usi e puoi bloccarlo subito con la voce **Blocca** (il lucchetto) in fondo alla barra a sinistra, oppure con il pulsante **Blocca adesso** in [Sicurezza](app:sicurezza): bloccato o chiuso, la chiave viene cancellata dalla memoria e serve di nuovo la password;
- un programma malevolo (un virus) che gira sul tuo computer mentre EasyCashFlow è aperto potrebbe leggere la chiave o quello che vedi sullo schermo. Contro questo nessuna cifratura basta: tieni il computer aggiornato e con l'antivirus attivo.

**Cosa non è cifrato**

- i **file CBI** (di testo o XML) che scarichi dalla banca: restano in chiaro dove li hai salvati (vedi sotto);
- il file `preferenze.json`: contiene solo le scelte di [Aspetto](app:aspetto) e i giorni dei log, che servono prima della password e non dicono niente dei tuoi soldi;
- i **log**: non sono cifrati, ma non contengono i dati dei tuoi movimenti (vedi più avanti).

**Dove mettere il programma**

EasyCashFlow è *portable*: non si installa, funziona dalla cartella in cui lo metti. Per questo la scelta della cartella è importante:

- mettilo in una cartella **del tuo utente di Windows**, per esempio dentro `Documenti`, a cui gli altri utenti del computer non possono accedere;
- usa un utente di Windows **protetto da password**;
- **non** metterlo in cartelle condivise in rete, su un computer usato da altri con il tuo stesso utente o su una chiavetta che presti;
- **non** metterlo in una cartella sincronizzata con un servizio cloud **condiviso** con altre persone;
- se il computer è portatile, è meglio che il disco sia cifrato (in Windows si chiama **BitLocker** o **Crittografia del dispositivo**): così, se il computer viene rubato, i dati restano illeggibili.

**Fare una copia di sicurezza**

Il modo più semplice è il menu **Impostazioni > Dati > [Backup](app:backup)**: premi **Crea un backup**, scegli la cartella e il programma crea un file come `EasyCashFlow-backup-2026-11-04-1530.zip`, con il database, il file `chiave.json` e le preferenze. Subito dopo lo rilegge e controlla che si apra e contenga tutti i movimenti. La pagina ricorda anche quando hai fatto l'ultimo backup e propone la stessa cartella.

Puoi anche farla a mano: chiudi il programma e copia **l'intera cartella `dati`**. In tutti e due i casi il database da solo non basta, perché senza il file `chiave.json` che gli sta accanto non si apre più, nemmeno con la password giusta.

Vale la stessa attenzione di sopra: la copia è cifrata, ma contiene tutti i tuoi movimenti e si apre con la tua password, quindi mettila **fuori da questo computer**, su una chiavetta o in uno spazio cloud **personale**. Per tornare indietro: chiudi il programma, metti i file della copia (estratti dallo zip) nella cartella `dati` accanto al programma, sostituendo quelli che ci sono, e riavvialo. **Attenzione:** i dati attuali vengono sovrascritti con quelli della copia, quindi quello che hai fatto dopo il backup (movimenti importati, nomi, gruppi) va rifatto.

Ricorda che una copia si apre con la password che valeva **quando l'hai fatta**: se poi cambi la password, la copia vecchia continua ad aprirsi con quella vecchia. Se hai cambiato la password perché temevi che qualcuno la conoscesse, cancella le copie fatte prima e fanne una nuova.

Quando non ti serve più un file CBI o XML scaricato dalla banca, cancellalo o mettilo nello stesso posto sicuro: contiene gli stessi dati.

**Dove trovarli**

Il menu **Impostazioni > Dati > [Percorso dati](app:percorso)** mostra dove sono la cartella del programma, i file della cartella `dati` con le dimensioni e i log, con un pulsante per aprire ciascuna cartella. Anche il menu **Aiuto > Informazioni su EasyCashFlow** mostra dove sono la cartella del programma, il database e i log, con un pulsante per aprire ciascuna cartella. Mostra anche la **versione** del programma e la data in cui è stato preparato: indicale se chiedi aiuto a qualcuno.

**Il log**

Se qualcosa non va come ti aspetti, il log aiuta a capire cosa è successo. Dall'icona del **Log** in alto a destra (il foglio, con il suggerimento "Log") puoi aprire il file di oggi o la cartella. Lascia il livello su **Normale**, a meno che non ti venga chiesto di cambiarlo per cercare un problema.

Dalla versione 0.9.3 il log **non contiene più i dati dei tuoi movimenti**: restano le date, i codici delle causali e i numeri che servono a capire un problema, ma gli importi diventano per esempio `-#.###,##`, le descrizioni `[34 car. #a3f9]` (quanti caratteri e un codice che non permette di risalire al testo) e del conto e dell'IBAN si vedono solo le ultime quattro cifre. Così puoi mandare un file di log a chi ti aiuta senza consegnargli i tuoi movimenti.

**Uscire**

Per chiudere il programma usa **Esci** nella barra a sinistra, oppure la X della finestra: ti verrà chiesta una conferma. Non c'è niente da salvare: ogni modifica è salvata nel momento in cui la fai.
