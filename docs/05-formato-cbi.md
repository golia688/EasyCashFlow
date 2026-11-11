# Formato CBI "Saldi e movimenti" (export del conto corrente)

Questo documento descrive il file che l'home banking esporta con il pulsante **"esporta CBI"** e che EasyCashFlow importerà per leggere i movimenti del conto corrente.

> **Nota sulle fonti**: fino al 04/11 il tracciato era **ricostruito osservando un export reale** (19 giornate contabili, 20 movimenti). Dal 05/11 è **confrontato con la specifica ufficiale**: CBI-RND-001 versione 6.05, capitolo 2 ("Rendicontazione saldi e movimenti di conti correnti"), e per le causali l'Appendice B di CBI-STD-001 versione 6.10, scaricati dall'utente dal portale del Consorzio CBI. La sezione 10 distingue ciò che dice la specifica, ciò che è verificato sui dati e ciò che è dedotto.

> **I documenti CBI sono riservati**: ogni pagina dice che contengono informazioni confidenziali di CBI S.c.p.a. e ne vieta divulgazione e riproduzione senza autorizzazione scritta. Qui ci sono le posizioni dei campi, con il rimando al paragrafo, e non il testo della specifica; i PDF restano fuori dal progetto. Le descrizioni delle causali CBI nel programma sono invece quelle della tabella ufficiale: scelta dell'utente del 05/11 (sezione 7).

> **Riservatezza**: i file reali contengono dati personali (numero di conto, nomi di controparti, descrizioni, importi). Gli esempi di questo documento sono **inventati**. Non inserire export reali nella cartella `docs` né in un eventuale repository (aggiungili all'elenco dei file ignorati).

## 1. Come riconoscere il file

- File di testo con **record a lunghezza fissa**: ogni riga è lunga **120 caratteri**.
- La posizione 1 è uno spazio; il **tipo di record** sta nelle posizioni 2-3.
- La prima riga è il record di testa `RH`, l'ultima il record di coda `EF`.
- In mezzo compaiono i record `61`, `62`, `63`, `64` e `65`.
- Codifica consigliata per la lettura: Latin-1 (`Encoding.Latin1`).

## 2. Struttura: una rendicontazione per conto e giornata

La specifica chiama **rendicontazione** il blocco di un conto per una giornata; un flusso (`RH` … `EF`) ne contiene quante ne servono, anche di conti diversi della stessa impresa. Il numero progressivo (posizioni 4-10) è lo stesso in tutti i record della rendicontazione e cresce di uno a ogni rendicontazione. La banca deve mandare la rendicontazione anche per un giorno senza movimenti (solo 61, 64 e, facoltativo, 65).

**L'ordine delle giornate non è fissato** dalla specifica. L'export completo della BCC del 05/11 (184 giornate, dal 01/10/2025 al 04/11/2026) e quello di un tester vanno dalla più recente alla più vecchia. Il lettore fa quindi i controlli sui saldi alla fine, conto per conto e in ordine di data (sezione 6).

```
RH                      testa del file
61  saldo iniziale      ┐
62  movimento 001       │ coppie 62 + 63, una per ogni
63  descrizione 001     │ movimento della giornata
62  movimento 002       │ (possono essere nessuna, una o molte)
63  descrizione 002     ┘
64  saldo finale
65  disponibilità future (ignorato dall'app)
... (giornata successiva: di nuovo 61, ...)
EF                      coda del file
```

Osservazioni (verificate sul campione):
- Le giornate **senza movimenti** hanno solo 61, 64 e 65.
- I **giorni non lavorativi non compaiono**: si passa, per esempio, dal venerdì al lunedì.
- Il record 63 è collegato al suo 62 dal **numero movimento** (posizioni 11-13), che riparte da 001 in ogni giornata. Per la specifica i 63 sono **da 0 a 5** dopo ogni 62, e il numero movimento dopo 999 riparte da 001: il lettore attacca ogni 63 al 62 che lo precede, controllando che il numero sia lo stesso.

## 3. Tracciato dei record

Le posizioni sono contate da 1 e gli estremi sono inclusi. Fonte: CBI-RND-001, paragrafi 2.2-2.8; la colonna "Uso" dice cosa ne fa il programma.

### RH: testa del flusso (par. 2.2)
| Posizioni | Contenuto | Uso |
|---|---|---|
| 2-3 | `RH` | inizio del flusso |
| 4-8 | ABI della banca mittente | nel log |
| 9-13 | codice SIA dell'impresa destinataria | no |
| 14-19 | data di creazione `GGMMAA` | nel log |
| 20-39 | nome del supporto, scelto dalla banca, unico per data e mittente | no |

### EF: coda del flusso (par. 2.3)
| Posizioni | Contenuto | Uso |
|---|---|---|
| 2-3 | `EF` | fine del flusso |
| 4-39 | gli stessi dati della testa | no |
| 46-52 | numero di rendicontazioni del flusso | controllo di completezza |
| 83-89 | numero di record del flusso, testa e coda comprese | controllo di completezza |

### 61: saldo iniziale (par. 2.4)
| Posizioni | Contenuto | Uso |
|---|---|---|
| 4-10 | progressivo della rendicontazione | no |
| 24-28 | ABI della banca assorbita, solo dopo una fusione | no |
| 29-33 | causale: `93001` invio normale, `93011` ritrasmissione | avviso se `93011` |
| 34-49 | descrizione del rapporto, libera | no |
| 50-51 | tipo di conto, con la codifica della banca | no |
| 52 | CIN (carattere di controllo delle coordinate) | IBAN |
| 53-57 | ABI (deve coincidere con il mittente della testa) | conto |
| 58-62 | CAB | conto |
| 63-74 | numero di conto | conto |
| 75-77 | divisa | tabella `Conti`, controllo con il 64 |
| 78-83 | data contabile `GGMMAA` (la stessa del 64) | giornata |
| 84 | segno del saldo: `C` credito, `D` debito | saldo |
| 85-99 | saldo iniziale **di quadratura** | saldo |
| 100-101 | paese dell'IBAN (`IT` o `SM`) | IBAN |
| 102-103 | cifre di controllo dell'IBAN | IBAN |

Le posizioni 53-74 identificano il conto (ABI + CAB + numero). L'ABI identifica la banca, quindi il codice resta univoco anche tra conti di banche diverse.

**Saldo iniziale di quadratura.** Per la specifica è il saldo finale (record 64) meno i movimenti della rendicontazione, e **non coincide per forza** con il saldo finale del giorno prima. Per questo la quadratura è un controllo che la banca deve rispettare, la continuità no (sezione 6).

**IBAN.** Le posizioni 100-103 (paese e cifre di controllo), 52 (CIN) e 53-74 (ABI + CAB + numero), lette in questo ordine, compongono l'IBAN completo: `IT` + 2 cifre + CIN + ABI + CAB + numero, 27 caratteri. La specifica lo conferma ("campi a disposizione per il completamento delle coordinate IBAN"), e sul campione il controllo modulo 97 è valido. Il lettore controlla ogni IBAN ricavato con il modulo 97: se il controllo fallisce lascia l'IBAN vuoto e identifica il conto solo con il codice.

### 62: movimento (par. 2.5)
| Posizioni | Contenuto | Uso |
|---|---|---|
| 4-10 | progressivo della rendicontazione | no |
| 11-13 | numero movimento, da 001 (dopo 999 riparte) | collega i 63 |
| 14-19 | data valuta `GGMMAA` | sì |
| 20-25 | data contabile `GGMMAA` | sì, chiave |
| 26 | segno: `D` addebito, `C` accredito | importo |
| 27-41 | importo | sì, chiave |
| 42-43 | **causale CBI**, uguale per tutte le banche (sezione 7) | causale, gruppi di ripiego |
| 44-45 | **causale interna** della banca | causale |
| 46-61 | numero dell'assegno, solo con la causale CBI 13 | nella descrizione estesa |
| 62-77 | riferimento della banca (per esempio il CRO), 16 caratteri | 62-76, chiave (sezione 8) |
| 78-86 | tipo di riferimento del cliente (`NROSUPCBI`, `PAYORDREF`, `NRPRATICA`, `NDISTINTA`, `RIFESICBI`) | no |
| 87-120 | riferimento del cliente o descrizione del movimento (34 caratteri) | descrizione breve |

Il programma salva la causale come un solo codice di 4 caratteri (42-45), come prima: i gruppi di causali e i movimenti già importati restano validi. La causale CBI è sempre ricavabile dalle prime due cifre (`CausaliCbi.Di`).

### 63: informazioni del movimento (par. 2.6)
Da 0 a 5 dopo ogni 62. Posizioni 4-10 e 11-13 come nel 62. Dopo alcune causali i primi 63 hanno una **struttura fissa**, riconoscibile dal codice nelle posizioni 14-16; gli altri sono **testo libero** dalla posizione 14 alla 120. La specifica dice che la struttura non è controllata e che un 63 non strutturato non va scartato. Nell'export della BCC del 05/11 ogni bonifico ricevuto (causale 4800, 22 movimenti) ha un `YYY` (data dell'ordine e ordinante; codice fiscale e località vuoti), un `YY2` (indirizzo dell'ordinante) e poi il testo libero che inizia con `*`.

| Codice | Quando | Contenuto (posizioni) |
|---|---|---|
| `KKK` | giroconto (causale CBI 34), cash pooling (Z1) | conto dell'altra parte (17-39) |
| `YYY` | bonifico ricevuto (48) | data dell'ordine `GGMMAAAA` (17-24), codice fiscale (25-40), ordinante (41-80), località (81-120) |
| `YY2` | bonifico ricevuto (48), facoltativo | indirizzo dell'ordinante (17-66), IBAN dell'ordinante (67-100) |
| `ZZ1` | bonifici da e per l'estero (ZI, ZL) | importi originario, regolato e negoziato con le divise, cambio (5 decimali), commissioni, spese, paese |
| `ZZ2` | come sopra | ordinante (17-120) |
| `ZZ3` | come sopra | beneficiario (17-66), motivazione (67-120) |
| `ID1` | pagamenti XML | identificativo del messaggio (17-51), identificativo end-to-end (52-86) |
| `RI1`, `RI2` | pagamenti XML | informazioni di riconciliazione (17-120, poi 17-52) |

Il lettore unisce tutti i 63 di un movimento nella **descrizione estesa**: il testo libero così com'è (spazi multipli ridotti a uno), quello strutturato scritto per esteso ("Ordinante: …, ordine del 31/10/2026, cod. fiscale …"), separati da " · ". `RI2` continua `RI1` senza separatore. Fino al 04/11 teneva solo l'ultimo 63, e i precedenti andavano persi: nell'export del 05/11 (328 movimenti) 27 movimenti hanno da 2 a 4 record 63, e per i bonifici ricevuti restava il `YY2` grezzo, codice compreso, senza il nome dell'ordinante. Reimportando un file vecchio la descrizione si aggiorna (sezione 11).

### 64: saldo finale (par. 2.7)
| Posizioni | Contenuto | Uso |
|---|---|---|
| 11-13 | divisa, uguale a quella del 61 | controllo |
| 14-19 | data contabile `GGMMAA`, uguale a quella del 61 | controllo |
| 20 | segno del saldo contabile | saldo |
| 21-35 | saldo contabile di chiusura: saldo iniziale + movimenti | saldo, quadratura |
| 36 | segno del saldo liquido | no |
| 37-51 | saldo liquido della giornata | no |

Nel campione il saldo contabile e il saldo liquido sono sempre uguali.

### 65: liquidità future (par. 2.8)
Facoltativo. Dalla posizione 11, **cinque gruppi da 22 caratteri**: data (6) + segno (1) + importo (15), i saldi liquidi dei giorni successivi. Nel campione gli importi sono sempre zero. Il programma li ignora.

## 4. Formato dei campi

| Tipo | Formato | Esempio | Valore |
|---|---|---|---|
| Importo | 15 caratteri, zeri iniziali, **virgola** decimale, nessun separatore delle migliaia | `000000000025,50` | 25,50 |
| Data | `GGMMAA`, anno a due cifre | `151025` | 15/10/2025 |
| Segno | un carattere | `D` / `C` | addebito (negativo) / accredito (positivo) |

Per ottenere un importo con segno: `D` diventa negativo, `C` positivo. Lo stesso vale per i saldi.

## 5. Esempio (dati inventati)

Due giornate: la prima con un addebito e un accredito, la seconda senza movimenti. Le righe sono lunghe 120 caratteri; gli spazi finali sono significativi per la lunghezza ma non per i contenuti.

```text
 RH99999     151025EXPORT20251016090000                                                                                 
 610000001                  93001                  A1234567890000000012345EUR151025C000000001000,00IT00                 
 620000001001151025151025D000000000025,502611                250001111110001          Bonifico tramite Internet Banking 
 630000001001*Mario Rossi          esempio di causale libera                                                            
 620000001002151025151025C000000000300,002700                250002222220002          Emolumenti                        
 630000001002*2025-10-15*AZIENDA ESEMPIO S.R.L. EMOLUMENTI 10-2025,                                                     
 640000001EUR151025C000000001274,50C000000001274,50                                                                     
 650000001161025C000000000000,00171025C000000000000,00181025C000000000000,00191025C000000000000,00201025C000000000000,00
 610000002                  93001                  A1234567890000000012345EUR161025C000000001274,50IT00                 
 640000002EUR161025C000000001274,50C000000001274,50                                                                     
 650000002171025C000000000000,00181025C000000000000,00191025C000000000000,00201025C000000000000,00211025C000000000000,00
```

Lettura della prima giornata: saldo iniziale 1.000,00; addebito di 25,50 (causale 2611); accredito di 300,00 (causale 2700); saldo finale 1.274,50. Si verifica che 1.000,00 − 25,50 + 300,00 = 1.274,50. L'esempio non include la riga `EF`, il cui contenuto non è documentato qui.

## 6. Controlli di coerenza

Il lettore esegue questi controlli e riporta un **avviso** quando falliscono, invece di importare in silenzio dati sbagliati. I controlli sui saldi (1-3) si fanno alla fine della lettura, **conto per conto e in ordine di data**, qualunque sia l'ordine delle giornate nel file.

1. **Quadratura**: saldo iniziale + somma dei movimenti = saldo finale. La specifica la impone (il saldo iniziale è "di quadratura"): se manca, il file è sbagliato.
2. **Continuità**: il saldo iniziale coincide con il saldo finale della giornata precedente dello stesso conto nel file. La specifica **non** la garantisce, ma quando manca di solito mancano dei movimenti, e l'avviso lo dice.
3. **Operazioni non contabilizzate**: il caso descritto sotto, con un solo avviso al posto dei due precedenti.
4. **Completezza**: il file termina con il record `EF`, e il numero di rendicontazioni e di record dichiarati nell'`EF` coincide con quelli letti.
5. **Rendicontazione**: la divisa e la data del 64 coincidono con quelle del 61; una rendicontazione senza 64 tiene i movimenti letti e lo segnala.
6. **Ritrasmissione**: con la causale `93011` nel 61 la banca sta rimandando un flusso; lo si dice, e i movimenti risultano già presenti.

Sul campione reale i controlli 1 e 2 sono soddisfatti per tutte le 19 giornate; il file del campione era un estratto e non conteneva la riga `EF`.

**Ordine delle giornate** (05/11): fino al 04/11 la continuità si controllava confrontando ogni giornata con quella letta prima. Il file di un tester ha le giornate dalla più recente alla più vecchia, e l'importazione ha dato 183 avvisi di continuità su 184 giornate, tutti falsi (nessun avviso di quadratura). Ora si ordina per data prima di confrontare.

### Operazioni non contabilizzate (verificato su un export reale del 30/10/2026)

Nell'export fatto il giorno stesso, l'ultima giornata (30/10) aveva questa forma (gli importi qui sono di esempio, non quelli veri):

| | Saldo |
|---|---|
| Finale del 29/10 | 12.480,00 |
| Iniziale del 30/10 | 12.296,40 |
| Movimenti (`62`) | nessuno |
| Finale del 30/10 | 12.480,00 |

I 183,60 € di differenza erano esattamente la somma di tre pagamenti con carta che l'home banking mostrava come "non contabilizzato". La banca li ha sottratti dal saldo iniziale, ma non li ha scritti nel file e il saldo finale li ignora. Così falliscono sia la continuità sia la quadratura (che la specifica imporrebbe), anche se nel file non manca niente da importare: i movimenti arriveranno con un export successivo, quando saranno contabilizzati.

Il lettore riconosce il caso da tre condizioni insieme: la giornata **non ha movimenti**, il saldo finale è **uguale** a quello della giornata precedente, e solo il saldo iniziale è diverso. Allora dà un solo avviso: la banca ha tolto (o aggiunto) dal saldo iniziale quell'importo di operazioni non ancora contabilizzate, che verranno importate con un export successivo. In tutti gli altri casi restano gli avvisi di continuità e di quadratura.

## 7. Causali

La causale del record 62 sono **due codici** (CBI-RND-001, par. 2.5):

- posizioni 42-43, la **causale CBI**: un elenco comune a tutte le banche (Appendice B di CBI-STD-001), con codici di due caratteri anche alfanumerici (per esempio `Z1`, `ZI`, `ZL`);
- posizioni 44-45, la **causale interna** della banca, con una codifica sua.

Fino al 04/11 le 4 cifre si leggevano come un codice solo; il programma continua a salvarle così (gruppi di causali, filtri), ma ora ne conosce il significato. Nell'export del 05/11 le causali sono 28, e tutte le causali CBI sono nella tabella. Le causali della BCC tornano tutte con l'elenco CBI: 2611 = 26 bonifico disposto, 2700 = 27 stipendio, 4500 = 45 carta di credito, 1510 = 15 rata di finanziamento, 1905 = 19 imposte e tasse, 8320 = 83 sottoscrizione di fondi, 7850 = 78 versamento di contanti. Un'eccezione: la BCC usa la 31 (3122, 3126) per gli addebiti SDD, mentre nell'elenco CBI la 31 sono gli effetti ritirati.

Nel programma (`Models/CausaliCbi.cs`):
- le **descrizioni** di tutte le 108 causali CBI della tabella ufficiale (senza le note), mostrate nella pagina dei gruppi di causali sotto la descrizione della banca ("causale CBI 26: Vostra disposizione a favore di") e cercabili nel suo filtro di testo. Fino al 05/11 mattina erano descrizioni brevi scritte da noi, per la riservatezza dei documenti CBI; l'utente ha chiesto quelle dello standard;
- **gruppi di ripiego**: una causale senza gruppo predefinito, per esempio di un'altra banca, riceve il gruppo della sua causale CBI (48 → Bonifici ricevuti, 34 e 79 → Giroconti di tipo Giroconto, 83 → Investimenti…), all'avvio e dopo ogni importazione (`DatabaseService.AssegnaGruppiDiRipiego`). I gruppi predefiniti a 4 cifre vengono prima, e un gruppo già assegnato, anche dall'utente, non si tocca. Le causali che possono essere cose diverse (storni, rettifiche, la 31) restano senza gruppo. Il motivo: nel file del tester (05/11, banca dello stesso gruppo) c'erano causali come 1616, 4829, 4515 e 6800, che con i soli predefiniti della BCC partivano tutte senza gruppo. Dal 10/11 il gruppo si sceglie direttamente per causale CBI (tabella `GruppiCausaliCbi`, [06-analisi.md](06-analisi.md), sezione 3).

Causali osservate nel campione (la causale a 4 cifre corrisponde sempre alla stessa descrizione breve):

| Causale | Segno | Descrizione breve (testo della banca) |
|---|---|---|
| 1129 | D | SDD Utenze-Servizi |
| 1410 | C | Cedole su titoli |
| 1510 | D | Addebito rata mutuo |
| 1905 | D | Imposta di bollo |
| 1906 | D | Addebito bolli per dossier titoli |
| 2611 | D | Bonifico tramite Internet Banking |
| 2700 | C | Emolumenti |
| 3122 | D | SDD Core - Richiesta Incasso SEPA |
| 3126 | D | SDD Finanziario |
| 4500 | D | Utilizzo carta di credito |
| 5046 | D | SDD Commerciale |
| 7850 | C | Versamento contante tramite CSA |

## 8. Riferimento dell'operazione e deduplica

- Per la specifica il riferimento della banca occupa le posizioni **62-77** (16 caratteri); il programma legge **62-76** (15), come dall'inizio. Il riferimento fa parte della chiave primaria: leggendolo più lungo, un file già importato rientrerebbe con chiavi diverse e i suoi movimenti comparirebbero due volte. Finché la posizione 77 è vuota le due letture coincidono; il lettore scrive un avviso **nel log** se trova la posizione 77 usata. Se capitasse, il passaggio a 16 caratteri andrà fatto riconoscendo i movimenti già salvati con il riferimento troncato.
- Il riferimento è **diverso per ogni movimento** del campione (20 su 20).
- **Osservazione**: nel campione i riferimenti degli addebiti terminano con `0001` e quelli degli accrediti con `0002` (17 e 3 casi). È un'osservazione, non una regola verificata: l'app non vi si appoggia.
- Poiché lo stesso movimento può comparire in più export che si sovrappongono, la **chiave di deduplica** è la combinazione di: conto, data contabile, riferimento, importo con segno.

## 9. La descrizione estesa (record 63)

Nei file della BCC i 63 sono testo libero, la cui forma cambia con il tipo di operazione. Esempi osservati, in forma generica:

- **Bonifici**: inizia con `*`, seguito dal nome della controparte e dalla causale scritta dall'ordinante.
- **Addebiti SDD**: descrizione dell'operazione, nome del creditore e identificativi del mandato, separati da spazi.
- **Rata del mutuo**: testo con scadenza, quota capitale, interessi e spese.
- **Cedole e bolli**: spesso inizia con `@`.
- **Carta di credito**: numero di carta mascherato e nome dell'emittente.

Le strutture fisse previste dalla specifica (`YYY`, `KKK`, `ZZ1`…) sono nella sezione 3. Nell'export del 05/11 compaiono `YYY` e `YY2`, nei bonifici ricevuti; le altre no.

## 10. Specifica, verificato e dedotto

| Informazione | Stato |
|---|---|
| Righe da 120 caratteri, tipo record nelle posizioni 2-3 | Specifica e verificato |
| Una rendicontazione per conto e giornata: 61, (62 + 0-5 × 63)*, 64, 65 facoltativo | Specifica e verificato |
| Posizioni dei campi dei record 61, 62, 64 | Specifica e verificato (la quadratura dei saldi lo conferma) |
| Segno `C` = accredito, `D` = addebito | Specifica e verificato |
| Seconda data del 62 = data contabile, prima = data valuta | Specifica (prima era dedotto) |
| Posizioni 100-103 + 52 + 53-74 del record 61 = IBAN completo | Specifica e verificato (modulo 97) |
| Posizioni 29-33 del 61 = causale 93001/93011 | Specifica (prima "non noto") |
| Causale = causale CBI (42-43) + causale interna (44-45) | Specifica, verificato sulle causali della BCC |
| Riferimento della banca 62-77, 16 caratteri | Specifica; il programma ne legge 15 (sezione 8). La posizione 77 è vuota in tutti i 328 movimenti dell'export del 05/11 |
| Quadratura dei saldi | Imposta dalla specifica, verificata sui file |
| Continuità dei saldi tra giornate | Non garantita dalla specifica; verificata sul campione |
| Ordine delle giornate | Non fissato; decrescente negli export del 05/11, della BCC e del tester |
| Record 63 strutturati | Specifica; `YYY` e `YY2` verificati nei bonifici ricevuti, gli altri non osservati |
| Contenuto del record EF | Specifica e verificato: 184 rendicontazioni e 1.266 record dichiarati, gli stessi letti |
| Corrispondenza causale ↔ descrizione breve | Osservata su 12 causali, non garantita |

## 11. Il lettore (`CbiParser`)

File coinvolti:
- `Models/MovimentoCbi.cs`: i record `MovimentoCbi`, `SaldoGiornaliero` e `RisultatoImportCbi`.
- `Models/CausaliCbi.cs`: causale CBI, descrizioni brevi e gruppi di ripiego (sezione 7).
- `Services/CbiParser.cs`: la classe `CbiParser`, con il metodo `Leggi(IEnumerable<string> righe)`.

Funzionamento:
- **Controlli formali** (dal 05/11), riga per riga e prima di interpretarla (`ControllaForma`): niente caratteri di controllo (un file binario: PDF, Excel, zip, immagini); il primo record deve essere la testa `RH` (con `EC`, l'estratto conto periodico, e con le teste di altri flussi CBI il messaggio lo dice); ogni riga deve essere un record previsto (`RH`, `61`-`65`, `EF`) che inizia con lo spazio; nessuna riga oltre i 120 caratteri; dopo l'`EF` può venire solo un nuovo `RH`. Un record 61 o 64 illeggibile, un file vuoto o una di queste condizioni fermano la lettura con `FileNonCbiException`, il cui messaggio è per l'utente (l'errore di .NET resta nel log, mascherato): `ImportazioneService` lo mette nel riepilogo e **non salva niente**, perché il database si scrive solo dopo aver letto tutto il file. Un file senza `EF` (tagliato) si importa con l'avviso. Prima un file qualunque veniva letto lo stesso: da un PDF o uno zip usciva un errore tecnico con caratteri senza senso, e da un testo che conteneva righe simili a record CBI (questo documento, con l'esempio della sezione 5) si sarebbero importati movimenti inventati. Provato (programma `provaestranei`) con un PDF, un'immagine, un file Excel, due file Markdown, uno zip, un file vuoto, una testa `EC`, una testa di un altro flusso, una riga aggiunta dopo la coda: tutti rifiutati con il loro motivo; l'export vero e uno tagliato a metà si leggono come prima.
- Legge le righe in ordine e le distingue per il tipo di record (posizioni 2-3).
- `RH` apre un flusso e azzera i conteggi; il `61` apre una rendicontazione; i `62` creano i movimenti; i `63` si aggiungono alla descrizione estesa del 62 che li precede (sezione 3); il `64` chiude la rendicontazione; l'`EF` chiude il flusso e ne controlla i conteggi. Il `65` è ignorato.
- Alla fine fa i controlli sui saldi per conto e in ordine di data (sezione 6).
- Restituisce conti, movimenti, saldi giornalieri, righe scartate e avvisi.

**Reimportazione** (05/11): un movimento con la stessa chiave e lo stesso contenuto tranne la descrizione estesa è lo stesso movimento, letto da un lettore più completo. `DatabaseService.InserisciMovimenti` ne aggiorna la descrizione invece di metterlo tra gli scarti, e il riepilogo dell'importazione dice quanti. Se cambiano anche data valuta, causale o descrizione breve resta uno scarto "contenuto diverso", come prima. Dal 08/11 la regola è più generale: lo stesso movimento tiene la descrizione più completa, da qualunque formato arrivi (sezione 14).

Prove del 05/11 (programmi `provacbi` e `provadb` nella cartella di lavoro temporanea, con i sorgenti veri): giornate in ordine inverso senza avvisi; più 63 uniti, `YYY` con ordinante, data e codice fiscale, `ID1` + `RI1` + `RI2`, numero dell'assegno; avvisi per continuità, divisa, conteggi dell'EF e ritrasmissione; le operazioni non contabilizzate con le giornate in ordine inverso; sul database cifrato, la descrizione aggiornata reimportando, lo scarto per un contenuto davvero diverso, i gruppi di ripiego che non toccano quelli dell'utente. **File vero** (05/11, export della BCC di 1.266 righe, 184 giornate, 328 movimenti, letto dal programma di prova `provafile` senza importarlo): tutte le righe di 120 caratteri, nessun avviso e nessuna riga scartata (con il lettore di prima le giornate in ordine decrescente avrebbero dato un avviso di continuità quasi ogni giorno), conteggi dell'EF giusti, 22 bonifici ricevuti con `YYY` + `YY2` + testo libero scritti per esteso, 27 movimenti con più di un 63. Non ancora reimportato nel programma.

Uso:

```csharp
var righe = File.ReadLines(percorso, Encoding.Latin1);
var risultato = new CbiParser(log).Leggi(righe);
```

## 12. Aperto

- **Riferimento di 16 caratteri** (sezione 8): da rivedere solo se il log segnala la posizione 77 usata.
- **Estratto conto periodico** (testa `EC`, capitolo 3 di CBI-RND-001): un'altra funzione dello stesso documento, non letta dal programma. Gli export visti sono tutti "saldi e movimenti" (`RH`).
- **Rendicontazione XML** (camt.053 del CBI): letta dal 08/11, sezione 14. Restano da vedere il camt.053 "puro" delle banche estere, con quasi la stessa struttura, e i movimenti raggruppati (`Btch`), non presenti negli export visti.
- **Saldi negativi** (segno `D` sui saldi) e **divise diverse da EUR**: previsti dal lettore ma non presenti nei campioni.
- **Un file con più conti**: non osservato; il lettore è predisposto (conto da ogni 61, controlli per conto). Il 04/11 è stato importato il file di un secondo conto, separato: i due conti convivono nel database e nelle pagine.
- **Nome provvisorio del conto**: si potrebbe usare la descrizione del rapporto (61, posizioni 34-49), se la banca la scrive.
- **Salvataggio dei dati**: fatto dal 26/10 (`DatabaseService.InserisciMovimenti`, con la chiave di deduplica della sezione 8); dal 02/11 il database è cifrato ([07-sicurezza.md](07-sicurezza.md)).

## 13. La documentazione CBI disponibile

Il 05/11 l'utente ha scaricato dal portale del Consorzio CBI una raccolta di standard (cartella "STANDARD CBI" fuori dal progetto: i documenti sono riservati e non vanno nel progetto). Esaminati tutti, guardando i manuali principali e l'elenco dei file degli archivi; per il programma contano questi.

**Usati.**
- **CBI-RND-001 6.05**, capitolo 2 "Rendicontazione saldi e movimenti di conti correnti": il tracciato di questo documento (sezioni 3-8). Lo stesso documento descrive altre funzioni con lo stesso tipo di record: l'**estratto conto periodico** (capitolo 3, testa `EC` invece di `RH`, record 61-64 quasi uguali), conti anticipi, dossier titoli e portafoglio (capitoli 4-6), che a un privato non servono.
- **CBI-STD-001 6.10**, le regole comuni: l'Appendice B con le causali CBI (sezione 7); le date facoltative possono essere spazi o zeri (par. 3.2.3, il lettore accetta entrambi nei 63 strutturati); i caratteri consigliati sono quelli ASCII (Appendice A), quindi la lettura in Latin-1 basta. Gli importi sono "con virgola virtuale" (in centesimi, senza virgola) se il documento della funzione non dice altro: CBI-RND-001 dice altro, tutti i suoi importi finiscono con la virgola e due decimali (Premessa), come legge il lettore. L'Appendice D (divise) servirebbe con conti in valuta.

**Rendicontazioni XML** (STRN-MO-001 v.00.01.02, con gli schemi XSD; letta dal programma dal 08/11, sezione 14). È lo stesso servizio "saldi e movimenti" in XML, costruito sullo standard internazionale **ISO 20022 camt.053** (estratto conto; il CBI lo chiama `CBIBkToCstmrStmt`), in tre versioni: giornaliera (`CBIDlyStmtReqLogMsg`), periodica (`CBIPrdcStmtReqLogMsg`) e intraday (`CBIIdyStmtReqLogMsg`, sullo standard camt.052).
- Servirebbe se una banca, anche estera, desse come export il camt.053 invece del file a 120 caratteri.
- Il movimento (`Ntry`) contiene la **causale CBI** nei primi due caratteri del codice proprietario dell'operazione (`BkTxCd/Prtry/Cd`, con emittente `CBI`): causali, descrizioni e gruppi di ripiego (sezione 7) varrebbero anche lì. Accanto c'è il codice ISO (`Domn`/`Fmly`/`SubFmlyCd`).
- I controlli che la banca deve rispettare corrispondono a quelli del lettore: saldo iniziale e finale (`OPBD`, `CLBD`) della stessa data, numero e somma dei movimenti (`TxsSummry`, come i conteggi dell'EF), riferimento del movimento (`NtryRef`) unico nell'estratto.
- L'**intraday** riporta movimenti "pending", che la banca può ancora annullare, e il documento dice di non usarlo per la riconciliazione: come per le operazioni non contabilizzate (sezione 6), andrebbe tenuto separato dai movimenti.

**Non servono al programma**, perché riguardano disposizioni che un'azienda manda alla banca o l'infrastruttura del circuito CBI: bonifici (CBI-BON-001, Area IP "Pagamenti", STPE bonifico estero), incassi (CBI-RIB-001 Ri.Ba., CBI-MAV-001, CBI-BOB-001 bollettino Freccia, CBI-AVV-001 avvisatura effetti, Incassi SDD, STIN SEDA), deleghe F24 (CBI-F24-001, convenzione I24), Request to Pay, anticipo fatture, esiti PSD, InfTrf, Area GD, flussi a struttura libera (CBI-STL-001), parte generale, framework dei servizi, porting e firma digitale. Le "prossime release" del 15/11/2026 (bonifico estero XML, disposizioni di pagamento XML, incassi SDD) sono anch'esse servizi di pagamento. I movimenti che nascono da questi servizi (un F24 pagato, un SDD addebitato) arrivano comunque nel file di rendicontazione, con la loro causale CBI.

## 14. La rendicontazione XML (`CbiXmlParser`, dal 08/11)

Dal 08/11 il programma importa anche la **rendicontazione XML** del CBI: lo stesso servizio "saldi e movimenti", nel formato XML descritto da STRN-MO-001 v.00.01.02 (sezione 13) e costruito sullo standard ISO 20022 camt.053. Relax Banking (BCC del gruppo Iccrea) la esporta accanto al file a 120 caratteri.

**Confronto su due export veri degli stessi tre mesi** (08/11, stessa banca e stesso conto; letti solo dal programma di prova, i dati non sono nel repository):

| | File a 120 caratteri | XML | Esito |
|---|---|---|---|
| Movimenti | 74 | 74 | stessi |
| Chiave (data contabile, riferimento, importo) | posizioni 20-25, 62-76, 26-41 | `BookgDt`, `AcctSvcrRef` (15 cifre), `Amt` + `CdtDbtInd` | **identica per tutti** |
| Data valuta | 14-19 | `ValDt` | uguale per tutti |
| Causale | 42-45, per esempio `4800` | `BkTxCd/Prtry/Cd` = `48//00`, `Issr` = `CBI` | uguale togliendo `//` |
| Conto | 53-74 (ABI + CAB + numero) | `Acct/Id/IBAN` | l'IBAN italiano, dopo i primi 5 caratteri, è il codice del conto |
| Saldi | 61 e 64 | `Bal` con `OPBD` e `CLBD` (più `CLAV`, il disponibile, non usato) | 42 giornate, uguali |
| Descrizione | breve (87-120) + record 63 | `NtryDtls/TxDtls/AddtlTxInf`, un testo unico | il testo XML comincia con la breve e contiene tutto il testo dei 63, più le "Info aggiuntive" (ordinante, IBAN del beneficiario, causale del bonifico, esito) |

L'XML ha un blocco `Stmt` per ogni giornata con movimenti (le giornate senza movimenti non ci sono), le date con il fuso (`2026-11-06+01:00`), `NtryRef` che riparte da 1 in ogni giornata (non è una chiave) e lo stato `Sts` = `BOOK` (contabilizzato). La banca mette la controparte in `RltdPties/Cdtr/Nm` anche per gli accrediti: il programma non lo usa, perché il nome è già nel testo.

**Il lettore** (`Services/CbiXmlParser.cs`) restituisce lo stesso `RisultatoImportCbi` di `CbiParser`, quindi salvataggio, deduplica, finestra di importazione, gruppi e analisi sono gli stessi.
- Legge il file senza DTD e senza risorse esterne (un XML costruito apposta non può far leggere altri file né scaricare niente) e cerca gli elementi per nome locale, così prefissi e versioni degli spazi dei nomi non contano.
- Legge solo la versione **giornaliera** (`CBIDlyStmtReqLogMsg`). Rifiuta, con il motivo, l'**intraday** (`CBIIdyStmtReqLogMsg`: movimenti che la banca può ancora annullare), il **periodico** (`CBIPrdcStmtReqLogMsg`: estratto conto, come la testa `EC` del file a 120 caratteri), un camt.053 non CBI e qualunque altro XML.
- Controlli come per il file a 120 caratteri: saldi `OPBD` e `CLBD` obbligatori e della stessa data, quadratura e continuità (la stessa `CbiParser.ControllaSaldi`), il riepilogo `TxsSummry` (numero dei movimenti, somma dei valori assoluti, somma con il segno) al posto dei conteggi dell'EF. Un movimento non interpretabile si scarta da solo, con la riga dell'XML; un saldo o un IBAN mancanti fermano tutto.
- I movimenti con stato diverso da `BOOK` non si importano (avviso).
- Riferimento: `AcctSvcrRef`, letto di 15 caratteri come nel file a 120. Se manca, se ne fa uno dalla data e da `NtryRef`, con un avviso: quel movimento non si riconoscerebbe nel file a 120 caratteri.

**La descrizione breve.** Nell'XML non è un campo a sé: è l'inizio del testo. Il lettore la ricava così, nell'ordine:
1. una descrizione breve già nel database per la stessa causale, se il testo comincia così (`DatabaseService.DescrizioniBreviDellaCausale`): con lo storico importato dal file a 120 caratteri, sul file vero dà la breve giusta per 74 movimenti su 74;
2. il testo prima di ` *` o ` @`, che nei testi della banca aprono la parte dei 63;
3. la parte iniziale comune ai testi diversi con la stessa causale nel file, al massimo 34 caratteri;
4. i primi 34 caratteri, senza spezzare una parola.

Senza storico, i passi 2-4 indovinano 60 brevi su 74 nel file vero: per i pagamenti POS, per esempio, la breve prende anche la parola successiva ("… Eurozona Del"). Il testo non si perde mai (breve + estesa = testo della banca), e importando poi il file a 120 caratteri degli stessi giorni la breve diventa quella della banca.

**Lo stesso movimento da due fonti** (`DatabaseService.AggiornaDescrizioni`). Stessa chiave, data valuta e causale vuol dire stesso movimento, anche con descrizioni diverse; se ne tiene la più completa, così l'ordine delle importazioni non conta:
- la descrizione estesa **più lunga** (a parte gli spazi): l'XML arricchisce i movimenti venuti dal file a 120 caratteri, e reimportare quel file non li impoverisce;
- la descrizione breve del file a 120 caratteri, che è un campo della banca; quella ricavata dall'XML non sostituisce quella salvata.

Uno scarto "contenuto diverso" resta solo se cambiano data valuta o causale. Prima del 08/11 bastava una descrizione breve diversa.

**Il formato si riconosce dal contenuto** (`Formati.Riconosci`): un XML comincia con `<`, dopo l'eventuale BOM. L'export "Excel" di Relax Banking (estensione `.xls`, ma è testo con le colonne separate da tabulazioni: data contabile, data valuta, importo, descrizione, note) ha gli stessi 74 movimenti e lo stesso testo dell'XML, ma non ha causale, riferimento, saldi e conto. Fino al 09/11 si rifiutava con il motivo; dal 09/11 si importa come gli altri file Excel, con l'abbinamento delle colonne (sezione 15).

**Prove del 08/11** (programma `provaxml` nella cartella di lavoro temporanea, con i sorgenti veri; stampa solo conteggi): sui due export veri, riconoscimento dei tre formati, stessi conti, chiavi, date valuta, causali e saldi; testo poi XML (0 nuovi, nessuno scarto, 11 descrizioni arricchite), poi di nuovo testo (niente cambia); XML su un database vuoto poi testo (brevi corrette, estesa più lunga tenuta); causale o data valuta diverse ancora tra gli scarti; XML rovinato, di altro tipo, intraday, periodico e con DTD rifiutati. Sull'esempio inventato: stessi 373 movimenti, conti e saldi del file a 120 caratteri, nessun avviso.

**Esempio con dati inventati**: `docs/esempi/esempio-cbi-due-conti.xml`, gli stessi movimenti di `esempio-cbi-due-conti.txt`, scritto dallo stesso `genera-esempio-cbi.py` con un secondo nome di file.

## 15. L'elenco dei movimenti in Excel (`LettoreTabelle`, `ExcelParser`, dal 09/11)

Molte banche fanno esportare i movimenti solo in "Excel". Non è uno standard: ogni banca ha colonne, intestazioni e formati suoi, e dietro lo stesso nome ci sono file diversi. Per questo non si indovina il formato: l'utente abbina le colonne in una finestra (`DialogoAbbinamentoExcel`), trascinandole nelle caselle del modello.

**Lettura del file** (`LettoreTabelle`). Il tipo si riconosce dal contenuto:

- archivio zip: un vero `.xlsx`, letto con DocumentFormat.OpenXml (primo foglio; le celle numeriche con un formato di data diventano gg/mm/aaaa, gli altri numeri hanno il punto e almeno due decimali, così "1500.00" non si confonde con 1.500);
- `D0 CF 11 E0`: il vecchio `.xls` binario di Excel 97-2003, rifiutato con l'invito a salvarlo in `.xlsx`;
- una pagina HTML con una tabella (molte banche la salvano come `.xls`): righe `<tr>` e celle `<td>`/`<th>`, con `colspan`;
- altrimenti testo con le colonne separate da tabulazioni, punti e virgola, virgole o barre (il separatore più frequente nelle prime righe), con i campi tra virgolette; UTF-8 se il file lo è, altrimenti Latin1. È il caso dell'export "Excel" di Relax Banking.

Il file si apre con la condivisione in scrittura (`LettoreTabelle.Apri`): così si legge anche mentre è aperto in Excel, che lo tiene bloccato.

`Formati.Riconosci` lo considera un Excel se è uno zip, un `.xls` binario, una pagina HTML, ha estensione `.xlsx`/`.xls`/`.csv`, oppure la prima riga ha almeno due tabulazioni o due punti e virgola (la prima riga di un file CBI non ne ha).

**L'abbinamento** (`AbbinamentoExcel`): la riga delle intestazioni e, per ogni casella (`CampoExcel`), le colonne. Obbligatorie la data contabile, la descrizione (anche più colonne, unite con uno spazio) e l'importo: una colonna con il segno, oppure uscite ed entrate (si prende il valore assoluto di ciascuna). Facoltative la data valuta (se manca è la contabile), la causale e il saldo.

- La riga delle intestazioni la sceglie l'utente al passo 1 della finestra, cliccandola nell'anteprima del file (dal 09/11 sera, dopo un export BPER con logo, data, conto e saldi sopra la tabella). `TrovaIntestazioni` propone quella di partenza: la prima riga con almeno tre celle di testo, seguita entro due righe piene da una riga che contiene una data. Al passo 2 si mostrano solo le colonne con l'intestazione o dei valori sotto (BPER lascia vuota la colonna A).
- `Proponi`: dai nomi delle colonne ("valuta", "data contabile", "data operazione", "saldo", "uscite"/"dare", "entrate"/"avere", "importo", "causale", "descrizione"...). Le colonne "note" si aggiungono alla descrizione; se nessuna colonna si chiama "descrizione", la colonna "causale" (dove molte banche mettono il testo) va nella descrizione.
- L'abbinamento usato si salva nella tabella `Impostazioni`, con chiave `ExcelAbbinamento:` più un'impronta delle intestazioni, insieme al conto: un file con le stesse intestazioni si riapre con le caselle già piene (`ImportazioneService.AbbinamentoSalvato`).

**Le righe.** Date nei formati gg/mm/aaaa, gg/mm/aa, gg-mm-aaaa, gg.mm.aaaa, aaaa-mm-gg (anche con l'ora), e con il mese in lettere, intero o abbreviato ("07 novembre 2026", "7 nov 2026", come BPER). Una data valuta che non si legge (BPER mette "-") è uguale alla data contabile. La colonna "categoria" va nella causale; "BANCOMAT" senza "prelievo" è un pagamento con la carta, e le commissioni sono spese bancarie anche quando nominano un'utenza. Importi all'italiana o all'inglese, con il simbolo dell'euro, il segno davanti o dietro, o tra parentesi. Una riga senza data, o con un testo al posto della data ("Totale", "Saldo finale"), si ignora; una riga con una data ma senza importo leggibile è una riga non valida.

**Cosa manca rispetto al CBI, e come si ricava:**

- **Conto**: lo sceglie l'utente; l'IBAN è obbligatorio (dall'11/11; prima era facoltativo), e un conto nuovo ha come codice l'IBAN senza i primi cinque caratteri, come nei file CBI. I conti creati prima senza IBAN (codice `EXCEL001`, `EXCEL002`...) lo chiedono alla prima importazione, e lo ricevono (`ImpostaIban`).
- **Causale**: se la colonna causale contiene un codice CBI (due o quattro caratteri della tabella), si usa; altrimenti si deduce dal testo della causale e della descrizione con poche regole in ordine (stipendio, giroconto, POS e PagoBancomat, prelievo, carta di credito, bonifico estero, bonifico, utenze, mutuo, imposte, assicurazioni, interessi, spese, versamento, assegni, SDD). Il segno distingue bonifico ricevuto (48) e fatto (26). La causale è il codice CBI seguito da `XL` (per esempio `43XL`): il gruppo arriva da `CausaliCbi.GruppoDiRipiego`, e le causali dedotte non si confondono con quelle delle banche. Quello che non si riconosce ha la causale fittizia `00XL` (`CausaliCbi.Fittizia`), senza gruppo: in Gruppi di causali non le si può dare un gruppo (varrebbe per tutti i non riconosciuti), e in Movimenti l'utente sceglie la causale di ogni movimento Excel (menu del movimento o barra di selezione, `DialogoCausaleExcel`, `DatabaseService.ImpostaCausaleExcel`, solo per i riferimenti `XL`). Reimportando lo stesso file, per i movimenti `XL` una causale diversa non è un contenuto diverso (`HaContenutoDiverso`): resta quella scelta.

**Causali imparate** (dal 10/11). La scelta della causale si ricorda per descrizione nella tabella `CausaliImparate` (chiave: `ExcelParser.ChiaveDescrizione`, la descrizione in maiuscolo con solo le lettere; resta anche se si elimina il conto). `ImpostaCausaleExcel` la registra e dà la stessa causale agli altri movimenti `XL` con la causale fittizia e la stessa descrizione. All'importazione e nell'anteprima (`CausaliImparate.Cerca`) un movimento prende, in quest'ordine: il codice CBI della colonna causale, la causale imparata (descrizione uguale, poi stesse prime quattro parole, la causale più frequente), le regole sul testo. Un movimento già presente con la causale fittizia prende quella riconosciuta dal file reimportato (`CorreggiCausaleFittizia`); una causale già scelta non si tocca. Al primo avvio con questa versione `RicordaCausaliGiaScelte` riempie la tabella con le scelte fatte prima (movimenti `XL` con una causale diversa dalla fittizia e da quella delle regole).
- **Riferimento**: `XL` più 13 cifre esadecimali dello SHA-256 di conto, date, importo, descrizione normalizzata e di un contatore per i movimenti identici dello stesso giorno. Reimportare lo stesso periodo dallo stesso tipo di file non duplica; lo stesso movimento da CBI ed Excel invece sì, perché i riferimenti sono diversi. Dal 10/11 un conto ha un **formato** (`FormatoConto`, ricavato dai riferimenti dei suoi movimenti con `DatabaseService.FormatiDeiConti`): CBI se ha movimenti della banca, Excel se ha movimenti `XL`, Misto se ha tutti e due (importati prima). La finestra dell'Excel non lascia scegliere un conto CBI né crearne uno con lo stesso IBAN, e `ImportazioneService` rifiuta un file CBI o XML con i movimenti di un conto Excel, e viceversa, senza salvare niente. Il formato si vede in Impostazioni, accanto al conto.
- **Saldi**: dalla colonna del saldo (il saldo dopo ogni movimento: il finale della giornata è quello dell'ultimo movimento del giorno, l'iniziale si ottiene togliendo i movimenti del giorno; l'ordine dentro la giornata si sceglie tra i due possibili, quello in cui ogni saldo torna con il precedente più l'importo), oppure dal saldo dopo l'ultimo movimento indicato dall'utente, all'indietro giorno per giorno. Senza nessuno dei due, nessun saldo e un avviso.
- **Descrizione breve**: i primi 34 caratteri, senza tagliare una parola se si può; l'estesa è il testo intero.

I movimenti si salvano con `breveDellaBanca: false`, come per l'XML. Se il file elenca prima i movimenti più recenti, si rovescia.

**Prove del 09/11** (programma `provaexcel` nella cartella di lavoro temporanea, con i sorgenti veri): importi, date e causali; quattro file inventati con gli stessi sei movimenti (`.xlsx` con titolo sopra, uscite/entrate, saldo e ordine decrescente; `.csv` con punto e virgola e virgolette; HTML salvato come `.xls` con una riga di totale; testo a tabulazioni in Latin1 come l'export di Relax), tutti con l'abbinamento proposto giusto, stesse date, valute e importi, riferimenti stabili, saldi corretti, reimportazione senza doppioni; `.xls` binario rifiutato; il CBI e l'XML di esempio riconosciuti come prima. Sull'export "Excel" vero di Relax (stampando solo conteggi): 74 movimenti, gli stessi del CBI dello stesso periodo con le stesse date e gli stessi importi, nessuna riga non valida; la causale CBI dedotta coincide con quella vera in 55 casi su 74, e gli altri 19 sono addebiti SDD che la BCC registra con la sua causale 31, dedotti come 50 (pagamenti diversi).
