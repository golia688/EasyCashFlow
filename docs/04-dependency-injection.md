# Il contenitore dei servizi (dependency injection)

Questo documento spiega cos'è il contenitore dei servizi di **EasyCashFlow**, come funziona la dependency injection e quali servizi ci sono. Scritto il 22/10, quando non c'era ancora nessun servizio nostro; aggiornato il 04/11 con l'elenco di quel giorno.

È il complemento di `03-avvio-e-pagina-host.md`, dove il contenitore compare nella sequenza di avvio.

## L'idea in breve

Il contenitore è un **magazzino centrale** dove si registrano gli strumenti (servizi) di cui l'app ha bisogno. Quando una pagina ha bisogno di uno strumento, non lo costruisce da sola: lo chiede al magazzino, che glielo consegna già pronto.

## Il problema che risolve

Senza contenitore, ogni pagina dovrebbe creare da sola ciò che le serve:

```csharp
// Senza dependency injection: ogni pagina crea il suo servizio
var analisi = new AnalisiService(...);   // un'istanza per pagina
```

Questo porta a tre difetti:

- **Dati non condivisi**: la pagina Analisi e la pagina della simulazione avrebbero due istanze diverse, e non vedrebbero lo stesso periodo scelto.
- **Codice rigido**: per cambiare il modo di salvare i dati bisognerebbe modificare ogni pagina.
- **Difficile da collaudare**: non si può sostituire il servizio reale con uno finto per le prove.

Con il contenitore, il servizio viene registrato **una volta sola** e chiunque lo chieda riceve quello giusto.

## Come si usa: due passi

**1. Registrazione** (in `MainWindow.xaml.cs`):

```csharp
var services = new ServiceCollection();
services.AddWpfBlazorWebView();
services.AddMudServices();
services.AddSingleton<DatabaseService>();
// ...
```

**2. Richiesta** (in una qualunque pagina `.razor`):

```razor
@inject AnalisiService Analisi
```

`@inject` significa "dammi il servizio di questo tipo": il contenitore lo fornisce.

## I servizi del programma (04/11)

Tutti registrati come **Singleton**: un'unica istanza per tutta la vita dell'app. Sono in `Services/`.

| Servizio | Cosa fa |
|---|---|
| `DatabaseService` | Il database SQLite cifrato: si apre con la chiave (`Inizializza`), legge e scrive movimenti, saldi, conti, gruppi, impostazioni |
| `ChiaviService` | La chiave del database in `chiave.json`: crea, sblocca con password o codice di recupero, cambia password, nuovo codice, la toglie dalla memoria |
| `AccessoService` | Dall'avvio all'apertura del database: crea password, sblocca, attese dopo gli errori, blocco |
| `BloccoService` | Il blocco per inattività (con `wwwroot/js/attivita.js`) |
| `ImportazioneService` | Importa un file CBI: lo legge con `CbiParser`, lo salva, segnala l'esito |
| `CbiParser` | Legge il formato CBI (documento 05) |
| `AnalisiService` | Calcoli di Analisi, dettaglio, simulazione, Home e grafico personalizzato; ricorda periodo, conti e straordinari scelti |
| `ReportService` | Il report Excel (Open XML) |
| `BackupService` | Il backup verificato in uno zip |
| `ImpostazioniService` | Preferenze d'aspetto e giorni dei log, in `preferenze.json` (servono prima della password) |
| `PreferenzeService` | Le altre preferenze, nel database cifrato: Generali, blocco, Home, ultimo backup |
| `StatoMovimenti`, `StatoGraficoPersonalizzato` | Filtri e selezioni delle due pagine, finché il programma è aperto |
| `LogService` | Il log a tre livelli, mascherato (con la classe statica `Mascherato`) |
| `SelettoreFile` | Le finestre di Windows per scegliere file e cartelle, aperte dopo l'evento di WebView2 in corso (`Dispatcher.InvokeAsync`) |
| `ManualeService` | Il manuale, diviso in capitoli e convertito in HTML |
| `ChiusuraService` | La conferma di uscita, tra la finestra WPF e Blazor |
| `AvvioService` | Avvisa la finestra quando Blazor ha disegnato la prima pagina |
| `NotificheService` | Le notifiche della campanella, ricavate dallo stato del database e dall'ultima importazione (dal 05/11) |
| `EliminazioneContoService` | Elimina un conto con i suoi dati e toglie il suo codice da preferenze, filtri e selezioni (dal 07/11) |

Oltre ai nostri, quelli delle librerie: `AddWpfBlazorWebView()` (Blazor dentro WPF), `AddMudServices()` (MudBlazor: `ISnackbar`, `IDialogService`…; con le notifiche accorciate a 150 ms di animazione e al massimo tre insieme), e in Debug `AddBlazorWebViewDeveloperTools()`.

Non sono servizi, ma classi statiche usate dappertutto: `PercorsiDati` (dove stanno i file), `Mascherato`, `ConsigliPassword`, `OpzioniGrafici` (le opzioni dei grafici ECharts).

## Servizi che usano altri servizi

Un servizio può chiederne altri nel costruttore, e il contenitore li fornisce automaticamente. Per esempio:

```csharp
public BackupService(DatabaseService database, PreferenzeService preferenze, LogService log)
```

Il contenitore costruisce prima `DatabaseService`, `PreferenzeService` e `LogService`, poi li consegna a `BackupService`. Basta che siano tutti registrati; l'ordine delle righe di registrazione non conta.

## Servizi condivisi e cambio di pagina

Come spiegato in `02-architettura-menu.md`, quando si cambia pagina quella precedente viene distrutta. Le scelte che devono restare stanno quindi nei servizi: `AnalisiService` ricorda periodo e conti di Analisi, `StatoMovimenti` i filtri di Movimenti, `StatoGraficoPersonalizzato` la selezione del grafico. Tornando alla pagina, li rilegge da lì.

## Quando i servizi parlano all'interfaccia

Alcuni servizi avvisano le pagine con un **evento**: `ImportazioneService.ImportCompletato`, `ImpostazioniService.Cambiate`, `AccessoService.Cambiato`, `BloccoService.BloccoRichiesto`. Le pagine si iscrivono in `OnInitialized` e si disiscrivono in `Dispose` (`@implements IDisposable`), e aggiornano l'interfaccia con `InvokeAsync(StateHasChanged)`. Attenzione: in Blazor dentro WPF `InvokeAsync` sul thread dell'interfaccia esegue subito (vedi lo storico, "La X non chiudeva da bloccato").

## La durata di vita: tre modi di registrare

| Metodo | Comportamento | Uso tipico |
|---|---|---|
| `AddSingleton<T>()` | Un'unica istanza per tutta la vita dell'app | Stato condiviso, accesso ai dati (tutti i nostri servizi) |
| `AddTransient<T>()` | Una nuova istanza ogni volta che viene richiesta | Piccole utility senza stato |
| `AddScoped<T>()` | Una istanza per "ambito" | In Blazor Hybrid si comporta in pratica come Singleton |

## Il ruolo di `Resources.Add("services", ...)`

Il contenitore è un oggetto C#, mentre `BlazorWebView` è dichiarato nel XAML. Per metterli in contatto:

```csharp
var provider = services.BuildServiceProvider();
// ... alcuni servizi si usano subito, prima della finestra (log, impostazioni, accesso)
Resources.Add("services", provider);
```

Nel XAML, `Services="{DynamicResource services}"` lo preleva e lo consegna a Blazor. `MainWindow` usa il contenitore anche per sé, prima che Blazor parta: lo sfondo dell'avvio dipende dal tema salvato, e l'accesso va preparato prima della finestra.

## Procedura per aggiungere un servizio

1. Crea la classe in `Services/`.
2. Registrala in `MainWindow.xaml.cs`, prima di `BuildServiceProvider()`: `services.AddSingleton<NomeServizio>();`
3. Nelle pagine che ne hanno bisogno: `@inject NomeServizio Nome`.

Se un servizio non è registrato e una pagina lo richiede, l'app va in errore quando la pagina viene mostrata, con un messaggio che indica il tipo mancante.
