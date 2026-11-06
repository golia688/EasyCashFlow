# 06 - Analisi e grafici

La sezione **Analisi** raccoglie gli indicatori e i grafici standard del cash flow di un conto corrente. Questo documento descrive le regole di calcolo, i dati su cui si basano e come sono costruiti i grafici.

## 1. Cosa mostra la pagina

In cima c'è la scelta del **periodo**: ultimo mese completo, ultimi 3, 6 o 12 mesi completi (il predefinito, o quello scelto in Generali), un anno per ogni anno con dati, oppure tutto. Dal 04/11 i periodi "completi" finiscono con l'ultimo mese finito (fino al 03/11 comprendevano il mese dell'ultimo movimento anche se appena cominciato: vedi la sezione 2). Accanto, le date **Data contabile da** e **a**: le riempie il periodo, e cambiandole a mano il periodo diventa "Personalizzato" (dal 03/11). Se i conti registrati sono più di uno, compare anche la scelta dei **conti**. I periodi si contano dall'**ultimo movimento importato**, non da oggi, perché gli export arrivano sempre con qualche giorno di ritardo. L'elenco dei periodi (`AnalisiService.Periodi()`) è lo stesso nella Simulazione e nel filtro Periodo della griglia Movimenti, dove riempie le due date.

Accanto ci sono l'interruttore **Escludi straordinari** con il pulsante "Vedi gli straordinari (N)" (sezione 6) e il pulsante **Simula il saldo futuro** (sezione 5).

| # | Elemento | Cosa mostra |
|---|---|---|
| 1 | Indicatori | Entrate, uscite, flusso netto e investito netto (tutti con la media mensile), tasso di risparmio, saldo a fine periodo con la variazione. Sopra, l'avviso dei mesi incompleti se il periodo ne ha. Investito netto solo se ci sono investimenti (sotto) |
| 2 | Entrate e uscite per mese | Entrate sopra lo zero, uscite sotto, flusso netto del mese come linea; clic su colonne e punti |
| 3 | Andamento del saldo | Saldo contabile a fine giornata |
| 4 | Dove vanno i soldi | Uscite per gruppo di causali, dalla più grande; clic sulle barre |
| 5 | Da dove arrivano | Entrate per gruppo di causali; clic sulle barre |
| 6 | Spese ricorrenti e variabili | Per mese, uscite fisse e variabili impilate; clic sulle colonne |
| 7 | Flusso netto cumulato | Mese per mese dall'inizio dell'anno, con l'anno precedente per confronto |
| 8 | Movimenti più grandi | I dieci movimenti di importo maggiore nel periodo |

**Investito netto solo con investimenti** (dal 06/11): se tra i movimenti dei conti scelti, in qualunque periodo, non ce n'è nessuno di tipo Investimento (quello del gruppo o quello scelto per il movimento, `DatabaseService.CiSonoInvestimenti`), la scheda non si mostra: sarebbe sempre zero. Nelle spiegazioni delle altre schede spariscono i riferimenti agli investimenti ("non comprende i versamenti negli investimenti (0,00 €)"), e la verifica del saldo diventa "flusso netto (+ giroconti)". Il controllo è su tutti i periodi e non su quello scelto, così con investimenti la scheda non compare e scompare cambiando periodo, e lo zero di un mese senza versamenti resta un'informazione. Simulazione e report Excel restano come sono.

### Pagine di dettaglio degli indicatori

Cliccando una scheda degli indicatori si apre la sua pagina di dettaglio (`PaginaDettaglioIndicatore.razor`), con lo stesso periodo e gli stessi conti scelti in Analisi. Periodo, conti ed esclusione degli straordinari li ricorda `AnalisiService` (`PeriodoScelto`, `ContiScelti`, `EscludiStraordinari`) finché l'applicazione resta aperta: tornando indietro la pagina Analisi è come l'avevi lasciata.

Da dove si apre il dettaglio, e con quali parametri (in `Main.razor`, `ApriDettaglio`):

| Da | Indicatore | Periodo | Conti e straordinari | Filtro |
|---|---|---|---|---|
| Scheda di Analisi | quello della scheda | scelto in Analisi | di Analisi | nessuno |
| Barra di "Dove vanno i soldi" / "Da dove arrivano" | Uscite / Entrate | scelto in Analisi | di Analisi | gruppo della barra (nessuno per un "Altro" che ne raccoglie più) |
| Colonna di "Entrate e uscite per mese" | Entrate, Uscite o Flusso netto (punto della linea) | solo quel mese (parametro `Mese`) | di Analisi | nessuno |
| Colonna di "Spese ricorrenti e variabili" | Uscite | solo quel mese | di Analisi | `Ricorrenti` = true o false |
| Freccia "dove finisce" in Gruppi di causali | secondo il tipo del gruppo | Tutto (parametro `Periodo`) | tutti i conti, straordinari inclusi | gruppo della causale |

La pagina contiene:
1. **Come si calcola**: la logica e la formula con i numeri del periodo.
2. Solo per il saldo, la **riconciliazione**: saldo iniziale + flusso netto − investito netto + giroconti (+ straordinari esclusi, se l'esclusione è attiva) = saldo atteso, confrontato con il saldo finale della banca. Una differenza indica movimenti mancanti.
3. **Per gruppo**: gruppo, tipo, causali, numero di movimenti, totale (e la quota, per entrate e uscite). Cliccando un gruppo, le liste sotto mostrano solo i suoi movimenti. La riga del gruppo scelto è in risalto (sfondo e bordo del colore principale, grassetto) e le altre sono attenuate, così si vede subito quale gruppo mostrano le liste, anche arrivando da "dove finisce" nei gruppi di causali.
4. **Movimenti inclusi**: quelli che compongono l'indicatore (`TabellaMovimenti.razor`, ordinabile e paginata). La somma coincide con il valore della scheda. Sopra la griglia ci sono un campo di ricerca (descrizioni, causale, gruppo e importo scritto all'italiana, per esempio "325,40") e un intervallo di date contabili, con il numero e il totale dei movimenti trovati. Gli stessi filtri ci sono anche nella griglia dei movimenti esclusi.
5. **Movimenti esclusi**, ripiegati: quelli del periodo che potrebbero sembrare mancanti, con il motivo. Per le entrate, per esempio, i movimenti positivi di tipo Investimento ("conta in Investito netto"). Non si elencano tutte le uscite come escluse dalle entrate: sarebbe solo rumore.

| Indicatore | Inclusi | Esclusi |
|---|---|---|
| Entrate | Ordinari positivi | positivi di tipo Investimento o Giroconto |
| Uscite | Ordinari negativi | negativi di tipo Investimento o Giroconto |
| Flusso netto, Tasso di risparmio | tutti gli Ordinari | tutti gli altri |
| Investito netto | tipo Investimento (totale sul conto con il segno opposto all'indicatore) | Giroconti |
| Saldo | tutti i movimenti | nessuno |

Da **Impostazioni > Dati > Gruppi di causali**, la freccia a fine riga ("dove finisce") apre il dettaglio dell'indicatore in cui finisce quella causale, già filtrato sul suo gruppo, su **tutto il periodo** e tutti i conti (come il totale della riga). L'indicatore dipende dal gruppo salvato: Investimento → Investito netto, Giroconto → Saldo, Ordinario → Entrate o Uscite secondo il segno del totale.

Verifica fatta il 31/10 su una copia del database, per Ultimi 12 mesi, Anno 2026 e Tutto: per ogni indicatore la somma dei movimenti inclusi coincide con il valore della scheda, e la riconciliazione del saldo arriva esattamente al saldo della banca.

## 2. Regole di calcolo

- Le date sono quelle **contabili**: un movimento appartiene al mese in cui la banca lo registra.
- Ogni causale appartiene a un **gruppo** con un **tipo** (tabella `GruppiCausali`, sezione 3):
  - **Ordinario**: entrata o uscita secondo il segno. Solo questi movimenti entrano in entrate, uscite, flusso netto e grafici per gruppo.
  - **Investimento**: soldi spostati verso gli investimenti o rientrati da essi (sottoscrizione di fondi, PAC, vendita di titoli). Non sono né spese né entrate: se lo fossero, il tasso di risparmio di un mese con un PAC sembrerebbe più basso di quello reale. Si sommano a parte nell'indicatore **Investito netto** (acquisti meno vendite e rimborsi).
  - **Giroconto**: spostamenti tra conti propri, esclusi da tutto. Oggi nessuna causale è di questo tipo, perché c'è un solo conto; un singolo movimento può però diventarlo con il tipo scelto a mano (sezione 6).
- Il tipo che conta è quello **effettivo**: il tipo scelto per il singolo movimento, se c'è, altrimenti quello del gruppo. Con l'esclusione attiva, i movimenti straordinari non entrano in nessun indicatore né grafico (sezione 6).
- **Tasso di risparmio** = flusso netto / entrate. Può essere negativo: vuol dire che nel periodo si è speso più di quanto è entrato, attingendo al saldo.
- **Uscita media mensile** = uscite / numero di mesi del periodo (anche i mesi senza movimenti).
- **Date a mano** (dal 03/11): accanto al periodo, "Data contabile da" e "Data contabile a", come in Movimenti. Scegliere un periodo riempie le date; cambiando una data, `AnalisiService.ScegliDate` cerca un periodo dell'elenco con le stesse date, altrimenti crea il periodo **Personalizzato** (`PeriodoPersonalizzato`, ricordato finché il programma resta aperto) e lo rende quello scelto. `TrovaPeriodo("Personalizzato")` lo restituisce, quindi valgono le stesse date nel dettaglio degli indicatori e nella simulazione, che lo mette in fondo al suo elenco come "Dal … al …" (`PeriodiConPersonalizzato`). Se "da" supera "a", l'altra data si sposta sulla stessa; una data cancellata non cambia niente. I mesi della media si contano come spiegato sotto (mesi incompleti in proporzione ai giorni).
- **Periodi a mesi completi** (dal 04/11, `AnalisiService.Periodi`): "Ultimo mese completo", "Ultimi 3, 6, 12 mesi completi" finiscono con l'ultimo mese finito (con movimenti fino al 03/11, l'ultimo mese completo è ottobre; il mese dell'ultimo movimento è finito solo se il movimento è dell'ultimo giorno). Prima comprendevano il mese dell'ultimo movimento, e il 03/11 "Ultimo mese" erano 3 giorni. Gli anni restano da gennaio a dicembre (l'anno in corso prende quello che c'è), "Tutto" dal primo all'ultimo movimento. Senza nessun mese finito i periodi completi mancano. La Home confronta il mese dell'ultimo movimento (`MeseUltimoMovimento`) con la media dei 12 mesi completi, e il suo riquadro apre Analisi con le date di quel mese (periodo personalizzato). Un periodo predefinito salvato con i nomi vecchi in Generali vale come quello completo corrispondente (`PreferenzeService.NomiVecchi`).
- **Mesi della media** (dal 03/11, `AnalisiService.Durata`): si contano sulla parte del periodo coperta dai movimenti (dalla prima all'ultima data importata: "Anno 2026" con movimenti fino al 20/10 finisce il 20/10), e i mesi incompleti valgono in proporzione ai giorni (20 giorni di ottobre = 20/31 = 0,65). Prima ogni mese toccato contava come intero: "Ultimi 3 mesi" fino al 20/10 dava 3 mesi invece di 2,65, e l'anno in corso 12 mesi invece di 9,65. `IndicatoriPeriodo` e `BaseSimulazione` hanno `Mesi` decimale e `MesiIncompleti`; con mesi incompleti Analisi e Simulazione mostrano `AvvisoMesiIncompleti.razor`, perché la proporzione suppone i soldi distribuiti in modo uniforme nel mese e uno stipendio nei giorni mancanti la smentisce. Con meno di un mese le schede non mostrano la media ("periodo di 0,65 mesi: niente media"), e la simulazione non mostra le medie né il risultato ma un avviso che chiede un periodo più lungo (`PaginaSimulazione.PeriodoTroppoCorto`): il 03/11 "Ultimo mese" era dal 01/11 al 03/11, 0,1 mesi, e (per esempio) 90 € di spese e 300 € versati in un fondo diventavano 900 € di uscite e 3.000 € di investito al mese, con il saldo sotto zero in pochi mesi. I grafici per mese restano per mesi di calendario. Prova (programma `provamesi`): ultimi 3 mesi fino al 20/10 = 2,65; dal 15/05 al 14/08 = 3,00 con due mesi incompleti; anno in corso = 9,65; mesi completi senza avviso; periodo senza dati = 0.
- **Medie mensili di entrate, flusso netto e investito netto** = totale / numero di mesi del periodo (dal 03/11, `PaginaAnalisi.AlMese`): flusso netto e investito netto medi sono gli stessi da cui parte la simulazione del saldo. La scheda prima mostrava solo il totale del periodo, che non si confrontava con la media della simulazione. Si dividono per i mesi calcolati come sopra.
- Nei grafici per gruppo ci sono al massimo **8 barre**: oltre non si leggono più bene. Se i gruppi sono 8 o meno compaiono tutti con il loro nome. Se sono di più, restano i primi 7 e gli altri confluiscono in **"Altro"**, sempre in fondo, insieme alle causali senza gruppo. Il tooltip di "Altro" elenca i gruppi che raccoglie ("Comprende: Carta di debito, Imposte e tasse, Spese bancarie"). Raccogliere in "Altro" un gruppo solo nasconderebbe il nome senza risparmiare spazio: con la regola di prima, nell'ultimo mese i bolli, unico gruppo rimasto fuori dai primi 7, comparivano come "Altro".
- Il tooltip di ogni barra mostra anche il numero di movimenti del gruppo.
- **Saldo**: per ogni giorno è la somma dei saldi finali dei conti scelti. Un conto senza saldo in quel giorno (festivo, o assente dal file) conserva l'ultimo saldo noto. La **variazione** del saldo si mostra solo se i saldi salvati coprono l'inizio del periodo; altrimenti sarebbe la variazione di un pezzo del periodo, e l'indicatore dice da quando ci sono saldi.
- **Cumulato**: i mesi prima del primo movimento importato e dopo l'ultimo restano vuoti. Uno zero farebbe credere a un mese senza entrate né uscite.
- **Tooltip degli indicatori**: passando con il mouse su una scheda compare il calcolo fatto con i numeri del periodo. Quello del saldo fa anche una **verifica**: la variazione del saldo deve essere uguale a flusso netto − investito netto + giroconti (più gli straordinari esclusi, se l'esclusione è attiva, perché il saldo della banca li comprende). Se non coincide, la differenza indica movimenti non importati. Sui dati veri del 2026 coincide al centesimo. Per esempio, con flusso netto −12.400,00 €, investito netto 2.600,00 € e nessun giroconto, la variazione del saldo deve essere −12.400,00 − 2.600,00 = −15.000,00 €.

## 3. Dati nel database

### Saldi giornalieri

Tabella `SaldiGiornalieri` (conto, data, saldo iniziale e finale in centesimi). Si scrive a ogni importazione con i saldi dei record `61` e `64`. Una giornata già salvata viene **sovrascritta**, perché un export più recente può averla completata (per esempio la giornata in corso al momento dell'export).

I file importati prima di questa tabella non hanno lasciato saldi. Per avere lo storico basta **importare di nuovo i file CBI** già caricati: i movimenti risultano "già presenti", ma i saldi vengono aggiunti. Finché i saldi mancano, la pagina lo spiega al posto del grafico.

### Gruppi di causali

Tabella `GruppiCausali` (causale, gruppo, tipo, ricorrente). Al primo avvio viene riempita con una mappatura predefinita delle causali osservate negli export (`InserisciGruppiPredefiniti` in `DatabaseService`). Le voci già presenti non vengono mai sovrascritte, così le modifiche future dell'utente restano. Una causale senza gruppo finisce in **"Altro"**, di tipo Ordinario e variabile.

**Gruppi di ripiego** (dal 05/11): la mappatura predefinita ha 28 causali a 4 cifre, quelle della BCC. Le prime due cifre di ogni causale sono la **causale CBI**, uguale per tutte le banche; le altre due la causale interna della banca ([05-formato-cbi.md](05-formato-cbi.md), sezione 7). Una causale dei movimenti che non ha ancora un gruppo riceve quello proposto per la sua causale CBI (`CausaliCbi.GruppoDiRipiego`, 42 causali CBI su 108: 48 → Bonifici ricevuti, 34 e 79 → Giroconti di tipo Giroconto, 83 → Investimenti…), all'avvio e dopo ogni importazione (`DatabaseService.AssegnaGruppiDiRipiego`). Così anche le causali di un'altra banca partono con un gruppo. Le causali CBI che possono essere cose diverse (storni, rettifiche, la 31 che la BCC usa per gli SDD) non hanno un ripiego e restano senza gruppo. Il gruppo resta per causale a 4 cifre perché la causale interna distingue cose che la causale CBI mette insieme: 3122 (SDD di utenze) e 3126 (PAC, un investimento) sono entrambe CBI 31.

**Esportazione e importazione** (dal 06/11): i pulsanti **Esporta** e **Importa** della pagina dei gruppi salvano in un file e riprendono tutta la tabella `GruppiCausali`, così su un'installazione nuova i gruppi non si rifanno a mano. Il file (`FileGruppiCausali`) è JSON, non cifrato perché non ha né movimenti né importi:

```json
{
  "formato": "EasyCashFlow.GruppiCausali",
  "versione": 1,
  "esportatoIl": "2026-11-06T18:30:00",
  "gruppi": [
    { "causale": "2611", "gruppo": "Bonifici effettuati", "tipo": "Ordinario", "ricorrente": false,
      "descrizioneBanca": "Bonifico tramite Internet Banking" }
  ]
}
```

- Si esportano **tutte** le righe della tabella, anche le predefinite e quelle di causali senza movimenti, con la descrizione della banca se ci sono movimenti (solo per chi legge il file: all'importazione si ignora).
- All'importazione (`DatabaseService.ImportaGruppiCausali`, in una transazione) una causale del file prende gruppo, tipo e ricorrenza del file; quelle che il file non ha restano come sono. Prima di scrivere, la finestra di conferma (`DialogoImportaGruppi.razor`) mostra **esattamente** cosa cambia: la tabella delle causali che cambiano (causale, descrizione della banca, gruppo, tipo, spesa ricorrente o variabile; del valore diverso quello attuale barrato e quello del file evidenziato), quella delle causali che si aggiungono, il numero delle uguali e il motivo di ogni riga non valida. Il pulsante dice quante causali si importano. Fino al pomeriggio del 06/11 la conferma mostrava solo i conteggi.
- Controlli: il formato e la versione del file (un file di un'altra versione più recente si rifiuta); per ogni riga la causale di 4 caratteri, il gruppo non vuoto (al massimo 60 caratteri) e il tipo tra Ordinario, Investimento e Giroconto. Una riga sbagliata si scarta da sola, e la conferma le elenca una per una con il motivo.
- Su un database nuovo i gruppi importati prima dei movimenti valgono appena arrivano i movimenti: né i gruppi predefiniti (`ON CONFLICT DO NOTHING`) né quelli di ripiego (solo per le causali senza riga) li sovrascrivono.
- Prova (programma `provagruppi`, con i sorgenti veri e database cifrati nuovi): esportazione di 29 causali, importazione su un database nuovo (1 nuova, 1 cambiata, 27 uguali), reimportazione senza cambiamenti, ripiego che non tocca i gruppi importati, righe sbagliate scartate una a una, file di un altro formato, non JSON o di una versione futura rifiutati.

I gruppi si modificano da **Impostazioni > Dati > Gruppi di causali** (`PaginaGruppiCausali.razor`). C'è una riga per ogni causale presente nei movimenti, con:
- la descrizione della banca e, sotto, la causale CBI con la sua descrizione secondo lo standard (dal 05/11), il numero di movimenti e il totale;
- il gruppo, con i suggerimenti dei gruppi esistenti oppure un nome nuovo. Un nome che differisce da uno esistente solo per le maiuscole prende quello esistente, così non nascono doppioni;
- il tipo e l'indicazione "ricorrente".

Sopra l'elenco ci sono i **filtri**: testo (cerca nel codice e nella descrizione della banca), gruppo (anche "Senza gruppo"), tipo e spesa ricorrente o variabile, con il pulsante Azzera e il conteggio "N causali su M". Si applicano subito e guardano i valori **salvati**: se guardassero quelli scritti e non ancora salvati, una riga sparirebbe mentre la si modifica. Salvando una riga, le modifiche non ancora salvate delle altre righe restano.

Le causali senza gruppo hanno l'etichetta "senza gruppo"; dal 04/11 l'elenco è in ordine alfabetico della descrizione della banca (prima le causali senza gruppo erano in cima), e per trovarle tutte c'è la voce "Senza gruppo" del filtro Gruppo. Il filtro di testo cerca anche nella descrizione della causale CBI.

I nomi della tabella sotto sono quelli di partenza, scelti da me in base al testo delle causali; vanno presi come proposta. Per esempio "Lavori casa con detrazione" è dedotto dai riferimenti di legge dei bonifici per le detrazioni fiscali (art. 16 bis per le ristrutturazioni, L. 296/06 per l'ecobonus, L. 90/13 per il bonus mobili), ma tra i bonifici per il bonus mobili ci sono acquisti che non sono lavori: un nome più esatto è "Bonifici con detrazione fiscale".

| Gruppo | Causali | Tipo | Ricorrente |
|---|---|---|---|
| Stipendio | 2700 | Ordinario | sì |
| Bonifici ricevuti | 4800 | Ordinario | no |
| Rendite finanziarie | 1410 cedole, 1450 dividendi, 1800 interessi | Ordinario | no |
| Versamenti contanti | 7850 | Ordinario | no |
| Investimenti | 7030 vendita titoli, 8320 fondi, 3126 SDD finanziario (PAC) | Investimento | solo 3126 |
| Mutuo | 1510 | Ordinario | sì |
| Utenze e servizi | 1129, 3122 | Ordinario | sì |
| SDD commerciali | 5046 (ricariche Satispay e simili) | Ordinario | no |
| Carta di credito | 4500 | Ordinario | no |
| Carta di debito | 4302, 4310 | Ordinario | no |
| Prelievi contanti | 0500, 5240 | Ordinario | no |
| Bonifici effettuati | 2610, 2611 | Ordinario | no |
| Lavori casa con detrazione | 2609, 2615, 2617 | Ordinario | no |
| Imposte e tasse | 1905, 1906, 1985 | Ordinario | 1905 e 1906 |
| Spese bancarie | 1640, 2210 | Ordinario | solo 2210 |

**Limite**: la causale è grossolana. "Bonifico tramite Internet Banking" o "Bonifici ricevuti" mettono insieme cose molto diverse, compresi eventuali trasferimenti da altri conti propri. Una divisione per categorie vere (spesa, casa, auto…) richiede regole sulla descrizione: è il passo successivo (menu "Categorie").

## 4. Grafici (ECharts)

### Report Excel (04/11)

Analisi > Report > **Genera report Excel** (`DialogoReport.razor`, `ReportService`): un file .xlsx con le scelte della pagina Analisi in quel momento (periodo, conti, straordinari, da `AnalisiService`). Scritto con **Open XML SDK** (`DocumentFormat.OpenXml` 3.1.1, licenza MIT; EPPlus avrebbe richiesto una licenza). Tre fogli:

- **Analisi**: intestazione (periodo, conti, straordinari, data), indicatori, tabella per mese, uscite ed entrate per gruppo, e quattro grafici nativi di Excel (colonne entrate/uscite con la linea del flusso netto, saldo, barre orizzontali per gruppo), con i colori del tema chiaro. Indicatori e tabelle sono formule `SUMIFS` sul foglio Movimenti, con il valore già calcolato dal programma nella cella (per chi apre il file senza ricalcolare) e il ricalcolo all'apertura (`FullCalculationOnLoad`). Il saldo a fine periodo e i mesi della media sono valori: vengono dai saldi della banca e da `Durata`. Le tabelle per gruppo partono dai movimenti, senza il raggruppamento in "Altro" della pagina;
- **Movimenti**: tutti i movimenti del periodo e dei conti, compresi investimenti, giroconti e straordinari, come tabella Excel con i filtri; le colonne **Tipo** e **Nelle analisi** (No per gli straordinari esclusi) sono quelle che usano le formule. La colonna Mese ha la stessa etichetta testuale ("ott 2026") della tabella per mese, che la confronta;
- **Saldo**: data e saldo a fine giornata.

Il file non è cifrato: la finestra lo dice in giallo prima di scegliere dove salvarlo. Google Fogli apre il file .xlsx: per questo non c'è un'integrazione diretta, che vorrebbe account, internet e i dati sui server di Google.

Prova (programma `provareport`, con un database cifrato nuovo e 12 mesi di movimenti finti): il file si scrive, il **validatore Open XML** non trova errori, ci sono i tre fogli e i quattro grafici. L'aspetto in Excel e in Google Fogli è da vedere aprendo il file.

### Grafico personalizzato (04/11)

Analisi > Grafici > **Grafico personalizzato** (`PaginaGraficoPersonalizzato.razor`) al posto delle voci segnaposto "Cash flow mensile" ed "Entrate e uscite". In alto i filtri di Movimenti, ora nel componente comune `PannelloFiltriMovimenti.razor` (usato anche da `PaginaMovimenti`), e una griglia con le caselle; sotto, entrate e uscite per mese dei movimenti scelti (`AnalisiService.PerMese`, dal mese del primo a quello dell'ultimo, mesi vuoti a zero, senza distinzione di tipo: li sceglie l'utente) con totali e medie al mese; cliccando una colonna o un punto si apre `DialogoMovimentiMese.razor` con i movimenti scelti di quel mese (solo entrate, solo uscite o tutti, secondo la serie). Lo stesso grafico di "Entrate e uscite per mese" (`OpzioniGrafici.EntrateUscitePerMese`).

- **Aggiungi al grafico** applica i filtri e aggiunge alla selezione tutti i risultati (`DatabaseService.TuttiMovimenti`), **Togli dal grafico** li toglie, **Cerca soltanto** mostra i risultati senza toccare la selezione, **Svuota il grafico**. Le caselle aggiungono o tolgono il singolo movimento; l'interruttore "Mostra solo i movimenti nel grafico" fa mostrare alla griglia la selezione invece dei risultati.
- Filtri e selezione restano finché il programma è aperto (`StatoGraficoPersonalizzato`), non si salvano: una selezione di singoli movimenti non comprenderebbe quelli importati dopo. Salvarla per nome, memorizzando filtri ed eccezioni, è un possibile sviluppo.
- Prova del calcolo per mese (programma `provamesi`): nessun movimento, mesi vuoti in mezzo, entrate e uscite dello stesso mese, cambio d'anno.

- **Libreria**: ECharts 5.6.0, file `wwwroot/lib/echarts/echarts.min.js` (circa 1 MB), scaricato da cdnjs e verificato con l'impronta SHA-512 pubblicata. È locale, quindi l'app resta portable e funziona offline.
- **Ponte JavaScript**: `wwwroot/js/grafici.js` crea il grafico, lo ridimensiona con l'elemento (`ResizeObserver`) e lo distrugge quando la pagina si chiude. Usa il renderer SVG, che resta nitido con lo zoom dell'interfaccia.
- **Componente**: `Pages/Grafico.razor` riceve le opzioni ECharts come oggetto qualsiasi, serializzato in JSON, e si ridisegna quando riceve un oggetto diverso.
- **Formattatori**: il JSON non può contenere funzioni. Le opzioni usano stringhe segnaposto che `grafici.js` sostituisce con la funzione corrispondente. Attenzione: `valueFormatter` dei tooltip riceve il valore, mentre `label.formatter` delle etichette riceve un oggetto con il valore in `value`, quindi servono due formattatori diversi:
  - `@euro`: importo completo, "1.234,56 €", per i tooltip;
  - `@euroEtichetta`: lo stesso, per le etichette delle barre;
  - `@euroAsse`: importo compatto per gli assi, "3,5k €";
  - `@meseDaData`: "ott 25" da una data "gg/mm/aaaa";
  - `@meseDaTempo`: "nov 26" su un asse dei tempi (valori in millisecondi), usato dalla simulazione;
  - `@tooltipTempo`: tooltip dei grafici con asse dei tempi, la data e una riga per serie;
  - `@tooltipGruppo`: tooltip delle barre per gruppo, con importo, numero di movimenti, i gruppi raccolti in "Altro" e l'invito a cliccare. I nomi dei gruppi li scrive l'utente, quindi vengono resi innocui prima di finire nell'HTML del tooltip.
- **Opzioni**: le costruisce `Services/OpzioniGrafici.cs`, un metodo per grafico. La pagina le ricalcola quando cambiano i dati o il tema.

### Scelte di disegno

- **Un solo asse delle ordinate per grafico.** Due grandezze con scale diverse vanno in due grafici.
- **Entrate e uscite**: le uscite sono sotto lo zero. La posizione distingue entrate e uscite anche senza il colore: verde e rosso si confondono per chi ha un'alterazione della visione dei colori.
- **Gruppi**: barre orizzontali di un solo colore, con l'importo in fondo. Il nome del gruppo si legge sull'asse, quindi non serve una tinta per ogni gruppo. Le barre si confrontano meglio delle fette di una torta.
- **Cursore**: ECharts mostra la manina su barre, linee e punti, come se fossero cliccabili. `grafici.js` imposta `cursor: "default"` su ogni serie che non dice altro, così la manina resta sulla legenda, dove il clic nasconde o mostra una serie, e sulle sole serie che dichiarano `cursor: "pointer"` perché il clic fa qualcosa (punti seguenti).
- **Barre per gruppo cliccabili**: in "Dove vanno i soldi" e "Da dove arrivano" la serie ha `cursor: "pointer"` e il clic su una barra apre il dettaglio di Uscite o Entrate filtrato su quel gruppo, con lo stesso periodo e gli stessi conti. Se la barra è un "Altro" che raccoglie più gruppi, il dettaglio si apre senza filtro e il riepilogo per gruppo li mostra tutti. Il tooltip lo ricorda ("Clicca per vedere i movimenti"). Il meccanismo è generale: `Grafico.razor` ha il parametro `ClicElemento`, e `grafici.js` gli passa il nome della categoria cliccata.
- **Colonne per mese cliccabili**: in "Entrate e uscite per mese" (barre Entrate e Uscite e punti del Flusso netto) e in "Spese ricorrenti e variabili" le serie hanno `cursor: "pointer"`. `Grafico.razor` ha anche il parametro `ClicSerie`, che riceve il nome della serie e la posizione dell'elemento nei dati (`dataIndex`), cioè il mese. `PaginaAnalisi.ApriMese` apre il dettaglio dell'indicatore della serie (Entrate, Uscite, Flusso netto; Ricorrenti e Variabili aprono Uscite) su un periodo di un solo mese, tagliato sulle date del periodo scelto come nei grafici. Il dettaglio riceve il mese nel parametro `Mese` e usa conti e straordinari della pagina Analisi; con `Ricorrenti` (true o false) tiene solo i movimenti delle causali ricorrenti o variabili, e titolo, valore e spiegazione parlano di "Uscite ricorrenti" o "Uscite variabili".
- **Segni sottili**: barre larghe al massimo 28 px con estremità arrotondate di 4 px, linee da 2 px, punti da 8 px con un bordo del colore della superficie. Griglia leggera, tooltip al passaggio del mouse, legenda quando le serie sono più di una.

### Colori (`Models/ColoriGrafici.cs`)

- **Entrate, uscite e saldo** usano i colori dell'interfaccia: `ColoreTema.Entrate`, `ColoreTema.Uscite` e il colore principale.
- **Anno precedente** nel cumulato: grigio tratteggiato.
- **Saldo simulato** (dal 03/11): tratteggiato giallo miele (`ColoriGrafici.Ipotesi`, il colore Avviso), non più nel colore principale come il saldo reale, con cui si confondeva.
- **Serie senza un significato proprio** (oggi ricorrenti e variabili, domani le categorie): una palette categorica a 6 colori, sempre nello stesso ordine:

| Ordine | Tema chiaro | Tema scuro |
|---|---|---|
| 1 lavanda | `#695ca6` | `#695ca3` |
| 2 pesca | `#dc905b` | `#c98250` |
| 3 blu | `#376aaa` | `#3a6aa7` |
| 4 ocra | `#c69f47` | `#b48f3c` |
| 5 malva | `#8d4e88` | `#8c5087` |
| 6 verde acqua | `#2dbbb4` | `#1faaa4` |

I pastelli dell'interfaccia non vanno bene per le serie dei grafici. Verificati con il validatore della skill dataviz, falliscono la saturazione minima e la separazione tra colori vicini, sia per la visione normale sia per le alterazioni della visione dei colori: lavanda e azzurro, per esempio, risultano quasi identici.

La palette sopra è più satura, ma resta morbida. È stata cercata con uno script tra le combinazioni di tinte, alternando una tonalità più chiara e una più scura. Supera tutti i controlli sulle superfici dei due temi (`#F7F6F2` e `#373740`):

| Controllo | Tema chiaro | Tema scuro |
|---|---|---|
| Separazione minima tra colori vicini, alterazione della visione | ΔE 19,8 | ΔE 14,6 |
| Separazione minima tra colori vicini, visione normale | ΔE 28,0 | ΔE 23,7 |

Le soglie sono 8 e 15. Tre colori per tema stanno sotto 3:1 di contrasto con la superficie: per questo i grafici che li usano mostrano sempre la legenda e il tooltip.

## 5. Simulazione del saldo

Pagina `PaginaSimulazione.razor`, sotto-pagina di Analisi: si apre dal pulsante "Simula il saldo futuro" della pagina Analisi o dal menu **Simulazione** (al posto del vecchio segnaposto "Previsioni > Nuova previsione").

- **Base** (`AnalisiService.BaseSimulazione`): sul periodo scelto (lo stesso elenco di Analisi, partendo da quello scelto lì) si calcolano entrate e uscite medie al mese dei movimenti Ordinari e l'investito medio dei movimenti Investimento, **senza i gruppi esclusi** (spese o entrate che non si ripeteranno). I mesi sono quelli del periodo, anche senza movimenti. I giroconti non contano. Il saldo di partenza è il saldo reale più recente (fino a oggi); il grafico mostra anche il saldo reale degli ultimi 12 mesi.
- **Ipotesi**: un flusso netto unico al mese (partenza: la media arrotondata) e un investito netto al mese; in più, un flusso netto diverso per singoli mesi. Il **tasso di risparmio** si ricalcola come flusso netto / entrate medie della base: le entrate restano fisse, quindi cambiare il flusso netto vuol dire spendere di più o di meno.
- **Calcolo** (`AnalisiService.Simula`): dal mese dopo l'ultimo saldo, ogni mese saldo = saldo precedente + flusso netto − investito netto. È la stessa relazione verificata sui dati reali (variazione del saldo = flusso netto − investito netto).
- **Risultato**: saldo a fine dell'ultimo mese, variazione rispetto a oggi, tasso di risparmio medio dei mesi simulati, primo mese con saldo negativo; grafico con il saldo reale (linea piena) e quello simulato (tratteggiato) su un asse dei tempi vero, con una linea rossa sullo zero se il saldo simulato ci scende; tabella mese per mese con il flusso netto modificabile (i mesi modificati sono colorati e si annullano con una freccia).
- **Tasso positivo e saldo in calo** (01/11, dopo una domanda dell'utente): con (per esempio) flusso netto +150 € e investito 400 € al mese il conto scende di 250 € al mese, mentre il tasso di risparmio resta positivo, perché non comprende gli investimenti. Per renderlo evidente: sotto le ipotesi la riga "Ogni mese il saldo del conto cambia di +150 € − 400 € = −250 €" (con una frase di spiegazione quando si risparmia ma il conto scende); la scheda "Variazione del conto" divisa in risparmiato e versato; un tooltip sul tasso di risparmio (base e risultato); nel grafico la linea a puntini **"Conto + versato negli investimenti"**, che dal saldo di oggi cresce del solo flusso netto e compare solo se l'investito non è zero.
- Le ipotesi non vengono salvate.
- Dal 03/11: con le date scelte a mano in Analisi il periodo personalizzato è già selezionato ("Dal … al …"); se il periodo ha meno di un mese di movimenti non ci sono medie né simulazione ma un avviso che chiede un periodo più lungo (`PeriodoTroppoCorto`); il saldo simulato è giallo miele.
- Verifica del 01/11 sui dati reali, ultimi 12 mesi: le medie della base coincidono con quelle calcolate a mano dai movimenti; escludendo il gruppo dei lavori in casa, che nel periodo aveva spese molto grandi e non ripetibili, le uscite medie scendono di quella quota e il flusso netto passa da negativo a positivo, con il tasso di risparmio ricalcolato di conseguenza (i numeri veri non si riportano: sono dati personali).

## 6. Movimenti straordinari e tipo del singolo movimento (01/11)

Il gruppo di una causale decide tipo e ricorrenza di tutti i suoi movimenti, ma un movimento può fare eccezione: tra i bonifici normali, quello per un impianto di aria condizionata è una spesa che non si ripeterà; un bonifico verso un proprio altro conto è un giroconto.

- **Dati**: due colonne nella tabella `Movimenti`: `Straordinario` (0/1) e `TipoForzato` (NULL = tipo del gruppo, altrimenti Ordinario, Investimento o Giroconto). Una reimportazione non le tocca, perché i movimenti già presenti non vengono riscritti.
- **Tipo effettivo**: `MovimentiPerAnalisi` legge `COALESCE(TipoForzato, tipo del gruppo, 'Ordinario')`; tutte le analisi usano il tipo effettivo. `MovimentoAnalisi` porta anche `Straordinario`, `TipoForzato` (vero se il tipo è scelto per il movimento) e `TipoDelGruppo`.
- **"Straordinario" non è un tipo**: il movimento resta Ordinario (o quello che è) e l'etichetta dice solo che non si ripeterà.
- **Dove si sceglie**: il menu ⋮ a fine riga (`MenuMovimento.razor`) nella griglia Movimenti e nelle tabelle di dettaglio, con le etichette "straordinario" e del tipo scelto; nella griglia Movimenti anche per tutte le righe selezionate, dalla barra di selezione. Dopo una modifica la griglia si ricarica sulla stessa pagina e il dettaglio si ricalcola. `MovimentoCbi` ora si confronta per chiave primaria, così una riga resta selezionata anche dopo essere stata modificata.
- **Analisi**: interruttore "Escludi straordinari", spento all'inizio e ricordato come il periodo (`AnalisiService.EscludiStraordinari`). Acceso, gli straordinari non contano in indicatori, grafici e cumulato; il loro totale resta in `StraordinariEsclusi`, e la verifica e la riconciliazione del saldo lo aggiungono, perché il saldo della banca li comprende. Nel dettaglio vanno tra gli esclusi con il motivo. Il dettaglio aperto dai gruppi di causali non li esclude.
- **Simulazione**: interruttore "Escludi i movimenti straordinari", acceso all'inizio, con il numero e il totale degli straordinari del periodo.
- **Ritrovarli**: accanto agli interruttori, in Analisi e Simulazione, il pulsante "Vedi gli straordinari" apre `DialogoStraordinari.razor`, l'elenco degli straordinari del periodo (con la stessa tabella dei dettagli e il suo menu ⋮). Chiudendolo, Analisi ricalcola; la Simulazione ricalcola la base solo se qualcosa è cambiato, per non perdere le ipotesi. Nella griglia Movimenti il filtro "Straordinari e tipo scelto" (`FiltroEtichette`: tutti, solo straordinari, senza straordinari, solo con il tipo scelto a mano) li trova in qualunque periodo.
- Verifica sul 2026 (copia del database): segnando straordinario un bonifico grande e trasformando un bonifico piccolo in giroconto, con l'esclusione attiva le uscite scendono esattamente della somma dei due importi, e la riconciliazione del saldo arriva sempre esattamente al saldo della banca.

## 7. Aperto

- Categorie vere, con regole sulla descrizione.
- Giroconti, quando ci saranno più conti.
- Dal dettaglio di un indicatore, aprire la griglia Movimenti già filtrata. Il clic sui grafici oggi apre il dettaglio dell'indicatore (sezione 1), che ha già ricerca, date e menu ⋮.
- Salvare per nome le selezioni del grafico personalizzato (con filtri ed eccezioni, così i movimenti nuovi entrano da soli).
- Password sul file del report Excel.
