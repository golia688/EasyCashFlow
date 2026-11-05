# Architettura dei menu

Questo documento descrive come è costruita la navigazione di **EasyCashFlow**: la barra laterale a icone, i menu in alto che cambiano in base alla pagina, le sotto-pagine con la freccia per tornare indietro, e come aggiungere voci e pagine. Scritto il 22/10, riscritto il 04/11 sul codice di quel giorno.

Tutto il codice descritto si trova in `Main.razor` e nella cartella `Pages/`.

## Panoramica

L'interfaccia ha tre zone, gestite dal layout di MudBlazor:

```
┌────┬──────────────────────────────────────────────────────────────┐
│ ☰  │  DATI                                     LOG  🔔  AIUTO     │  ← MudAppBar (icone)
│────│──────────────────────────────────────────────────────────────│
│ 🏠 │                                                              │
│ ⇄  │                                                              │
│ 📊 │          Contenuto della pagina attiva                       │  ← MudMainContent
│ ⚙  │          (PaginaHome, PaginaMovimenti, ...)                  │
│ ⎋  │                                                              │
│    │                                                              │
│ 🔒 │                                                              │
└────┴──────────────────────────────────────────────────────────────┘
  ↑
  MudDrawer (modalità Mini, solo icone)
```

- **Barra laterale** (`MudDrawer`): Home, Movimenti, Analisi, Impostazioni ed **Esci**; **Blocca** (lucchetto, dal 03/11) è in fondo, da solo, dal 05/11 (suggerimento del tester). Il contenuto del drawer è una colonna flessibile (regola `.mud-drawer-content` in `Main.razor`), e uno spazio elastico spinge in basso il secondo `MudNavMenu` con Blocca.
- **Barra in alto** (`MudAppBar`): a sinistra i **menu della pagina attiva**; a destra, sempre presenti e dal 05/11 come **icone con il suggerimento** (prima erano le scritte LOG e AIUTO): il menu **Log** (foglio), la **campanella delle notifiche** (`CampanellaNotifiche.razor`, sezione "Le notifiche") e il menu **Aiuto** (punto interrogativo). Quando il livello del log non è Normale compare anche l'etichetta "LOG: TRACE" o "LOG: DEBUG". Il nome della pagina non c'è: dal 30/10 sta solo come titolo in cima alla pagina.
- **Area contenuti** (`MudMainContent`): la pagina attiva.

Una sola variabile, `_sezione`, decide cosa si vede. Quando cambia, Blazor aggiorna da solo barra in alto, evidenziazione e contenuto.

Prima di tutto questo, finché il database non è aperto (password da creare o da inserire), `Main.razor` mostra solo `PaginaAccesso`; da bloccato, la pagina di accesso copre tutto e il resto diventa inerte (vedi [07-sicurezza.md](07-sicurezza.md), sezione 11.5).

## Le sezioni

```csharp
private enum Sezione { Home, Movimenti, Scarti, Analisi, Impostazioni, Generali, Aspetto, Sicurezza,
                       GruppiCausali, DettaglioAnalisi, Manuale, Simulazione, GraficoPersonalizzato,
                       PercorsoDati, Backup }
```

Solo Home, Movimenti, Analisi e Impostazioni hanno un'icona. Le altre sono **sotto-pagine**: `SezionePadre` dice quale icona evidenziare quando sono aperte.

| Sotto-pagina | Icona evidenziata | Si apre da |
|---|---|---|
| Scarti | Movimenti | menu Dati, etichetta rossa accanto al titolo di Movimenti |
| Generali, Aspetto, Sicurezza | Impostazioni | menu Preferenze |
| Percorso dati, Backup | Impostazioni | menu Dati |
| Gruppi di causali | Impostazioni, oppure Movimenti se aperta da lì | menu Dati di Impostazioni o di Movimenti, Home |
| Dettaglio di un indicatore | quella della pagina da cui si è aperto | clic su schede, barre e colonne di Analisi, gruppi di causali |
| Simulazione, Grafico personalizzato | Analisi | menu di Analisi, pulsante "Simula il saldo futuro" |
| Manuale | nessuna (resta quella di prima) | Aiuto > Manuale, icona "?" accanto ai titoli |

## I menu in alto

Sono generati da un dizionario che associa a ogni sezione i suoi menu e le sue voci (`MenuPerSezione`); le sotto-pagine usano il menu della pagina madre. Stato al 04/11, senza più voci segnaposto:

| Pagina | Menu e voci |
|---|---|
| Home | **Dashboard**: Aggiorna, Scegli periodo |
| Movimenti | **Dati**: Importa file CBI, Movimenti scartati, Gruppi di causali |
| Scarti | **Dati**: Importa file CBI, Torna ai movimenti |
| Analisi (e dettaglio, simulazione, grafico personalizzato) | **Grafici**: Grafico personalizzato · **Simulazione**: Simula il saldo futuro · **Report**: Genera report Excel |
| Impostazioni (e sotto-pagine) | **Preferenze**: Generali, Aspetto, Sicurezza · **Dati**: Conti correnti, Gruppi di causali, Percorso dati, Backup |
| Manuale | nessuno |

Tolte il 04/11: in Movimenti Modifica (nuovo, modifica, elimina movimento: i movimenti arrivano solo dai file CBI), Esporta e Categorie; in Analisi "Cash flow mensile" ed "Entrate e uscite".

Il markup li disegna con due cicli annidati; il clic chiama `EseguiVoce(voce)`, uno `switch` sul testo della voce. `Segnaposto` resta come ramo `default` per una voce nuova non ancora collegata:

```razor
@foreach (var (titolo, voci) in MenuCorrente)
{
    <MudMenu Label="@titolo" Color="Color.Inherit" Variant="Variant.Text">
        @foreach (var voce in voci)
        {
            <MudMenuItem OnClick="@(() => EseguiVoce(voce))">@voce</MudMenuItem>
        }
    </MudMenu>
}
```

`MenuCorrente` è `MenuPerSezione[_sezione]`, tranne un caso: i gruppi di causali aperti da Movimenti mostrano il menu di Movimenti.

"Importa file CBI" (anche dai pulsanti di Home e Movimenti) apre la finestra di importazione (`DialogoImportazione.ApriAsync`): scelta del file, caricamento e riepilogo; se dal riepilogo si sceglie "Vedi i movimenti scartati", la pagina che l'ha aperta porta agli scarti.

Le voci di **Dashboard** agiscono sulla Home già mostrata: `Main.razor` la raggiunge con `@ref="_home"` e chiama `Aggiorna()` e `ScegliPeriodoAsync()`. "Genera report Excel" apre una finestra di dialogo (`DialogoReport`).

## La freccia per tornare indietro

Le sotto-pagine hanno una freccia accanto al titolo, con il suggerimento di dove riporta:

- **fissa**: Scarti → Movimenti, Simulazione e Grafico personalizzato → Analisi, Manuale → la pagina di prima (`_primaDelManuale`), Dettaglio → la pagina da cui si è aperto (`_ritornoDettaglio`);
- **secondo da dove si è arrivati** (dal 04/11): i gruppi di causali ricordano se ci si è arrivati dalla Home o da Movimenti (`_ritornoGruppi`), la pagina dei conti se dalla Home (`_ritornoConti`). Aperte dal menu Impostazioni non hanno freccia. `Vai()` aggiorna questi campi, tranne quando si torna dal dettaglio o con la freccia del manuale, così la freccia resta quella giusta.

## Le notifiche (05/11)

La campanella mostra il numero delle notifiche, con il colore della più grave (errore, avviso, informazione), e cliccandola l'elenco. Le raccoglie `NotificheService`:

- quasi tutte si **ricavano dallo stato del database** (`Statistiche`, conti, ultimo backup): movimenti scartati, causali senza gruppo, conti con il nome provvisorio, ultimo movimento di più di 7 giorni fa, nessun backup o più vecchio di 30 giorni, nessun movimento. Si ricalcolano dopo lo sblocco, dopo ogni importazione, a ogni cambio di pagina (`Vai`) e quando si apre l'elenco; spariscono da sole quando il fatto è risolto;
- l'**ultima importazione**, se non è riuscita o ha avvisi, resta finché il programma è aperto e il suo pulsante riapre il riepilogo (`DialogoImportazione.MostraRiepilogoAsync`);
- **Nascondi** toglie una notifica fino alla chiusura del programma (niente si salva); una nuova importazione fa ricomparire la sua.

Ogni notifica ha un pulsante (`AzioneNotifica`), eseguito da `Main.razor` in `EseguiNotifica`: importare, aprire scarti, gruppi di causali, conti o backup, rivedere il riepilogo. Per aggiungere una notifica: una condizione in `NotificheService.Raccogli` e, se porta in un posto nuovo, un valore di `AzioneNotifica` con il suo `case`.

## Lo stato che sopravvive al cambio di pagina

Cambiando sezione la pagina precedente viene distrutta e quella nuova creata da zero. Le scelte che devono restare stanno quindi nei **servizi** (vedi [04-dependency-injection.md](04-dependency-injection.md)):

| Cosa | Dove | Fino a quando |
|---|---|---|
| Periodo, conti, straordinari di Analisi | `AnalisiService` | programma aperto |
| Filtri e righe per pagina di Movimenti | `StatoMovimenti` | programma aperto |
| Filtri e selezione del grafico personalizzato | `StatoGraficoPersonalizzato` | programma aperto |
| Pagina iniziale, conto e periodo predefiniti, blocco, periodi e conti della Home, ultimo backup | `PreferenzeService` (database cifrato) | sempre |
| Aspetto, giorni dei log | `ImpostazioniService` (`preferenze.json`) | sempre |
| Importi nascosti (l'occhio accanto al "?", Ctrl+H) | `PreferenzeService` | sempre |

## Come aggiungere

### Una voce a un menu esistente
1. Aggiungi il testo all'array in `MenuPerSezione` (o in `MenuAnalisi`, `MenuImpostazioni`).
2. Aggiungi il `case` con lo stesso testo in `EseguiVoce`. Senza, la voce mostra l'avviso "funzione non ancora implementata".

### Una nuova sotto-pagina
1. Crea `Pages/PaginaNuova.razor`.
2. Aggiungi il valore all'enum `Sezione`.
3. Aggiungi la voce in `MenuPerSezione` (**obbligatorio**: senza, l'app va in errore aprendo la pagina; per nessun menu, `[]`).
4. Aggiungi la riga in `SezionePadre`.
5. Aggiungi il `case` nello `switch` del contenuto (con `CascadingValue Name="TemaScuro"` se la pagina ha grafici).
6. Collega il modo di aprirla: un `case` in `EseguiVoce`, un parametro `EventCallback` di un'altra pagina, oppure un collegamento `app:nome` del manuale (`ApriPaginaDalManuale`).
7. Se ha un capitolo nel manuale, un'icona `<AiutoManuale Capitolo="..." />` accanto al titolo.

### Una nuova icona nella barra laterale
Un `MudNavLink` in `Main.razor`, con l'icona scritta come `Impostazioni.Icona(nameof(Icons.Material.Filled.Nome))`: così segue lo stile di icone scelto in Aspetto (`Models/Icone.cs`).

## Comportamento della barra laterale

- `_drawerAperto` indica se è espansa (con i nomi) o compatta (solo icone); il pulsante ☰ la alterna.
- L'evidenziazione della voce attiva è in `StileAttivo`, con i colori del tema.
- **Blocca** chiama `AccessoService.Blocca("voce Blocca")`; **Esci** chiede conferma (vedi lo storico, sezione "Uscita").

## Dove si modifica cosa

| Voglio... | Dove |
|---|---|
| Cambiare le voci dei menu in alto | `MenuPerSezione`, `MenuAnalisi`, `MenuImpostazioni` e `EseguiVoce` in `Main.razor` |
| Cambiare icone o voci della barra laterale | `MudNavLink` in `Main.razor` |
| Cambiare quale icona si evidenzia per una sotto-pagina | `SezionePadre` in `Main.razor` |
| Cambiare dove riporta una freccia | `Vai`, `_ritornoGruppi`, `_ritornoConti`, `TestoRitorno` in `Main.razor` |
| Cambiare il contenuto di una pagina | `Pages/PaginaXxx.razor` |
| Cambiare il tema | `Tema` in `Main.razor` e `Models/ColoreTema.cs` |
