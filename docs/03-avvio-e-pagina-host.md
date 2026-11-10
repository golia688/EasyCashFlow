# Avvio dell'applicazione e pagina host

Questo documento spiega cosa succede quando l'app parte, qual è il ruolo di `index.html`, di `MainWindow.xaml` e del file di progetto, e quali problemi di configurazione sono già stati incontrati e risolti. Scritto il 22/10, riscritto il 04/11: nel frattempo sono arrivati la schermata di avvio, la password e il database cifrato.

È il complemento di `01-architettura-stack.md` (i componenti tecnologici) e `02-architettura-menu.md` (la navigazione).

## La sequenza di avvio

```
1. Windows avvia EasyCashFlow.exe; WPF crea MainWindow
        │
2. MainWindow (costruttore):
        │   - SQLitePCL.Batteries_V2.Init(): il motore SQLite con la cifratura (SQLite3MC)
        │   - registra i servizi (vedi 04-dependency-injection.md)
        │   - registra i gestori degli errori (vanno nel log)
        │   - ImpostaSfondoAvvio: finestra e WebView del colore del tema salvato (niente lampo bianco)
        │   - PreparaAccesso: riconosce il database (assente, in chiaro, cifrato) e chiave.json;
        │     un database in chiaro delle versioni di prova o cifrato senza chiave.json → messaggio e uscita
        │   - mostra la schermata di avvio nativa (WPF), con la WebView ancora nascosta
        │
3. Il controllo BlazorWebView crea WebView2 e carica wwwroot/index.html
        │   (che carica blazor.webview.js, MudBlazor, ECharts e gli script del programma)
        │
4. Blazor monta Main.razor dentro <div id="app">; al primo disegno Main avvisa AvvioService,
        │   e MainWindow toglie la schermata nativa, mostra la WebView, porta davanti la finestra
        │   e le dà la tastiera (MostraInterfaccia)
        │
5. Main.razor mostra PaginaAccesso: crea la password (primo avvio) o chiede di inserirla
        │   (sblocco con Argon2, circa mezzo secondo; poi il database si apre con la chiave)
        │
6. Database aperto: Main mostra layout, barra laterale, menu e la pagina iniziale scelta in Generali
```

- **Schermata di avvio nativa** (31/10): la WebView impiega qualche secondo a caricare la pagina, e prima la finestra restava vuota. Ora `MainWindow.xaml` ha una schermata WPF (icona, nome, barra animata) visibile da subito, e la WebView parte nascosta (`Hidden`, non `Collapsed`, così si inizializza lo stesso). Dopo 10 secondi il testo dice che l'avvio sta richiedendo più del solito; dopo 30 la WebView viene mostrata comunque. Il log registra "Interfaccia pronta in N ms".
- **Tastiera** (03/11): il campo della password ha il cursore nella pagina, ma la tastiera di Windows va alla finestra WPF e non alla WebView: `MostraInterfaccia` chiama `Activate()` e `vistaBlazor.WebView.Focus()`, così si scrive la password senza cliccare.
- Dettagli di password, chiave e sblocco in [07-sicurezza.md](07-sicurezza.md), sezione 6.

## La pagina host: `wwwroot/index.html`

È l'**unico vero file HTML** dell'app. Tutto il resto dell'interfaccia (`Main.razor`, le pagine) viene scritto in Razor e inserito dentro questa pagina da Blazor.

Contiene:

| Elemento | Ruolo |
|---|---|
| `<base href="/" />` | Base per i percorsi relativi delle risorse |
| `MudBlazor.min.css` | Stile dei componenti MudBlazor |
| `<style>` della schermata di avvio | La seconda schermata di avvio (icona, nome, barra), quella HTML, visibile finché Blazor non disegna l'app. Non forza lo sfondo della pagina: lascerebbe il colore dell'avvio sotto i riquadri cambiando tema (problema del 01/11) |
| `<div id="app">` | Punto in cui Blazor inserisce l'interfaccia (vedi `Selector="#app"` in `MainWindow.xaml`); dentro, la schermata di avvio HTML |
| `blazor.webview.js` | Avvia Blazor in WebView2 |
| `MudBlazor.min.js` | Comportamenti dei componenti (menu a tendina, avvisi…) |
| `lib/echarts/echarts.min.js` | La libreria dei grafici, locale (niente internet) |
| `js/grafici.js` | Crea e aggiorna i grafici ECharts e ne gira i clic al C# |
| `js/manuale.js` | I collegamenti del manuale (`cap:` e `app:`), che non devono far navigare la WebView |
| `js/attivita.js` | Segnala l'attività dell'utente al blocco per inattività (`BloccoService`) |

I percorsi che iniziano con `_framework/` e `_content/` non sono file del progetto: sono risorse delle librerie (Blazor e MudBlazor) servite dal sistema. Le librerie esterne stanno in `wwwroot` e si caricano con percorsi relativi: l'app funziona offline ed è davvero portable.

## Il collegamento tra WPF e Blazor: `MainWindow.xaml`

```xml
<blazor:BlazorWebView x:Name="vistaBlazor" HostPage="wwwroot\index.html" Services="{DynamicResource services}">
    <blazor:BlazorWebView.RootComponents>
        <blazor:RootComponent Selector="#app" ComponentType="{x:Type local:Main}" />
    </blazor:BlazorWebView.RootComponents>
</blazor:BlazorWebView>
```

- `HostPage`: quale pagina HTML caricare all'avvio.
- `Services`: i servizi disponibili ai componenti Blazor (vedi `04-dependency-injection.md`).
- `RootComponent`: quale componente Razor (`Main`) mostrare e dentro quale elemento della pagina (`#app`).

Nello stesso file c'è la schermata di avvio nativa (`schermataAvvio`). `MainWindow.xaml.cs` fa anche altre cose a finestra creata: colora la barra del titolo come quella dell'app (`ColoraBarraDelTitolo`), applica lo zoom scelto in Aspetto (`ApplicaZoom`), intercetta la X per chiedere conferma (`IntercettaChiusura`) e alla chiusura toglie la chiave dalla memoria e chiude il database.

## Il file di progetto: `EasyCashFlow.csproj`

| Impostazione | Perché |
|---|---|
| `Sdk="Microsoft.NET.Sdk.Razor"` | Abilita la compilazione dei file `.razor` |
| `OutputType` `WinExe` | Applicazione Windows con interfaccia grafica, senza console |
| `TargetFramework` `net10.0-windows10.0.19041.0` | .NET 10 più la versione minima di Windows, necessaria a WebView2 |
| `UseWPF` | Abilita WPF |
| `Version` | La versione (1.2.1 dal 10/11 sera, con il trascinamento delle colonne dei file Excel corretto; 1.2.0 dal 10/11, con l'importazione dei file Excel e i gruppi per causale CBI; 1.1.0 dal 08/11, con l'importazione dei file XML CBI; 1.0.1 dal 07/11; la 1.0.0, dello stesso giorno, è la prima definitiva); la data della build si aggiunge da sola (`DataBuild`) |
| `ApplicationIcon` | L'icona dell'eseguibile |
| `SatelliteResourceLanguages` `it` | Solo le traduzioni italiane dei messaggi di sistema nella pubblicazione |
| `EmbeddedResource` `Manuale\manuale.md` | Il manuale dentro l'eseguibile |

Pacchetti NuGet: `Microsoft.AspNetCore.Components.WebView.Wpf` (Blazor in WPF), `MudBlazor` 9.11, `Markdig` (manuale), `Microsoft.Data.Sqlite.Core` con `SQLite3MC.PCLRaw.bundle` (SQLite con cifratura), `Konscious.Security.Cryptography.Argon2` (password), `DocumentFormat.OpenXml` (report Excel). Versioni e motivi nello storico.

## Altri file della radice

- **`App.xaml`**: punto di ingresso di WPF, indica quale finestra aprire (`MainWindow`).
- **`_Imports.razor`**: gli `@using` comuni a tutti i componenti Razor (deve essere compilato: vedi i problemi risolti nello storico, 30/10).
- **`Main.razor`**: componente radice dell'interfaccia (vedi `02-architettura-menu.md`).
- **`Pages/`**, **`Services/`**, **`Models/`**: pagine, servizi e tipi di dati.
- **`Manuale/manuale.md`**: il manuale per l'utente.
- **`Distribuzione/`**: `LEGGIMI.txt` e `NOVITA.txt`, copiati solo nella cartella pubblicata.
- **`Assets/`**: icona e script che la genera.
- **`docs/`**: questa documentazione, non inclusa nella pubblicazione.

## Dove stanno i dati

Accanto all'eseguibile, perché l'app è portable:

- `dati\easycashflow.db` (cifrato), `dati\chiave.json` (la chiave, chiusa con password e codice di recupero), `dati\preferenze.json` (aspetto e giorni dei log, in chiaro);
- `log\easycashflow-AAAA-MM-GG.log`, mascherati.

WebView2 tiene invece la sua cache in `%LOCALAPPDATA%\EasyCashFlow.WebView2` (il LEGGIMI dice di cancellarla per disinstallare): non contiene i dati dei movimenti.

## Problemi già incontrati e risolti

| Sintomo | Causa | Soluzione |
|---|---|---|
| Errore all'avvio: file `Microsoft.Windows.SDK.NET` non trovato | `TargetFramework` senza versione di Windows | Usare `net10.0-windows10.0.19041.0` |
| Finestra bianca all'avvio | `MainWindow.xaml` ancora con il contenuto del template | Inserire il `BlazorWebView` |
| Resta visibile "Caricamento..." | `autostart="false"` nel tag di `blazor.webview.js` | Rimuovere l'attributo |
| Componenti Mud senza stile | Mancava `@using MudBlazor` | Aggiungerlo |
| Nessun template "Razor Component" in Visual Studio | Mancava il workload "ASP.NET and web development" | Installarlo |
| Password da scrivere solo dopo un clic (03/11) | La tastiera restava alla finestra WPF | `Activate()` e focus alla WebView in `MostraInterfaccia` |

Altri problemi, più legati alle funzioni, sono nello storico, sezione "Problemi già risolti".

## Dove si modifica cosa

| Voglio... | Dove |
|---|---|
| Aggiungere una libreria JS/CSS | `wwwroot/index.html` (e il file in `wwwroot/`) |
| Cambiare la schermata di avvio | `MainWindow.xaml` (nativa) e lo `<style>` di `index.html` (HTML) |
| Cambiare cosa succede prima della password | `PreparaAccesso` in `MainWindow.xaml.cs`, `AccessoService` |
| Registrare un nuovo servizio | `MainWindow.xaml.cs` |
| Cambiare versione | `<Version>` nel `.csproj` |
