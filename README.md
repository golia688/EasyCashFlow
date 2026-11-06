# EasyCashFlow

**Analisi del cash flow dei tuoi conti correnti, anche di banche diverse, a partire dagli export CBI.**

EasyCashFlow è un programma per Windows che legge l'elenco dei movimenti scaricato dall'home banking nel formato **CBI "saldi e movimenti"**, lo standard di rendicontazione delle banche italiane, e mostra entrate, uscite, saldo e grafici, mese per mese.

Siccome il formato CBI è uguale per tutte le banche, EasyCashFlow è anche un **aggregatore**: si importano i movimenti di più conti, anche in banche diverse, e li si analizza uno alla volta o tutti insieme, come se fossero un conto solo.

Funziona **senza internet e senza account**: è portable (si avvia da una cartella, senza installazione) e i dati restano sul computer, **cifrati** con una password.

> Versione **0.9.5**, in prova. Interfaccia e documentazione sono in italiano.

## Cosa fa

- **Importazione CBI** secondo la specifica CBI-RND-001: controlli formali sul file (un file che non è un export CBI si rifiuta senza salvare niente), quadratura e continuità dei saldi, deduplica dei movimenti già importati, riepilogo dell'importazione con gli avvisi raggruppati.
- **Più conti e più banche**: ogni conto si riconosce da solo dal file; Home, Movimenti e Analisi lavorano su un conto, su alcuni o su tutti. Le **causali CBI** (comuni a tutte le banche) danno un gruppo anche alle causali di una banca nuova.
- **Analisi**: entrate, uscite, flusso netto, tasso di risparmio, investito netto e saldo, con il dettaglio di ogni indicatore; grafici per mese e per gruppo di causali, spese ricorrenti e variabili, flusso cumulato, movimenti straordinari escludibili.
- **Simulazione del saldo** nei mesi futuri, **grafico personalizzato** dei movimenti scelti e **report Excel** con formule e grafici.
- **Sicurezza**: database cifrato (SQLCipher 4 con SQLite3 Multiple Ciphers), chiave protetta con Argon2id dalla password e da un codice di recupero, blocco per inattività, log senza dati in chiaro, importi nascondibili con un clic.
- **Notifiche**, **manuale** integrato, backup verificato, tema chiaro e scuro.

## Cosa serve

Per **usarlo**: Windows 10 o 11 a 64 bit, con Microsoft Edge WebView2 Runtime (già presente su Windows 11). La versione pubblicata include .NET: non serve installare altro. Le istruzioni per installarlo sono in [`Distribuzione/LEGGIMI.txt`](Distribuzione/LEGGIMI.txt), le novità di ogni versione in [`Distribuzione/NOVITA.txt`](Distribuzione/NOVITA.txt).

Per **compilarlo**: .NET 10 SDK e Visual Studio con i carichi di lavoro "Sviluppo di applicazioni desktop .NET" e "Sviluppo ASP.NET e Web" (per i file Razor).

```
dotnet build EasyCashFlow.csproj
```

La versione da distribuire si crea con il profilo `Properties/PublishProfiles/VersionePerProva.pubxml` (Visual Studio > Pubblica, oppure `dotnet publish -p:PublishProfile=VersionePerProva`): una cartella autonoma, con .NET incluso, in `bin\Pubblicazione\EasyCashFlow`.

## Com'è fatto

WPF + **Blazor Hybrid** (WebView2) + **MudBlazor** per l'interfaccia, **ECharts** per i grafici, SQLite cifrato per i dati.

| Cartella | Contenuto |
|---|---|
| `Pages/` | le pagine e i componenti Razor |
| `Services/` | importazione e lettore CBI, database, analisi, sicurezza, log, report |
| `Models/` | i tipi di dati (movimenti, conti, causali CBI, colori) |
| `Manuale/manuale.md` | il manuale per l'utente, incluso nell'eseguibile |
| `docs/` | la documentazione tecnica e la storia dello sviluppo |
| `wwwroot/` | la pagina host, ECharts e gli script |

La documentazione è in `docs/`: si comincia dallo [storico dello sviluppo](docs/00-storico-sviluppo.md), poi [stack](docs/01-architettura-stack.md), [menu](docs/02-architettura-menu.md), [avvio](docs/03-avvio-e-pagina-host.md), [servizi](docs/04-dependency-injection.md), [formato CBI](docs/05-formato-cbi.md), [analisi](docs/06-analisi.md), [sicurezza](docs/07-sicurezza.md) e [versionamento](docs/08-versionamento.md).

## Dati e privacy

I dati stanno nella cartella `dati` accanto al programma (database cifrato, file della chiave, preferenze) e non entrano mai nel repository, come gli export CBI e i log (`.gitignore`). Gli importi negli esempi della documentazione sono inventati.

I documenti tecnici del Consorzio CBI (CBI-RND-001, CBI-STD-001) non sono inclusi: sono riservati e si scaricano, previa registrazione, dal portale del CBI. La documentazione descrive solo le posizioni dei campi del tracciato.

## Licenza

Il codice di EasyCashFlow è distribuito con la licenza [MIT](LICENSE).

Componenti di terze parti:

| Componente | Licenza | Dove |
|---|---|---|
| [ECharts](https://echarts.apache.org) 5.6.0 | Apache 2.0 | incluso in `wwwroot/lib/echarts` |
| [MudBlazor](https://mudblazor.com) | MIT | pacchetto NuGet |
| Blazor Hybrid per WPF (`Microsoft.AspNetCore.Components.WebView.Wpf`) | MIT | pacchetto NuGet |
| `Microsoft.Data.Sqlite.Core` | MIT | pacchetto NuGet |
| [SQLite3 Multiple Ciphers](https://github.com/utelle/SQLite3MultipleCiphers) (`SQLite3MC.PCLRaw.bundle`) | MIT | pacchetto NuGet |
| `Konscious.Security.Cryptography.Argon2` | MIT | pacchetto NuGet |
| [Markdig](https://github.com/xoofx/markdig) | BSD 2-Clause | pacchetto NuGet |
| Open XML SDK (`DocumentFormat.OpenXml`) | MIT | pacchetto NuGet |
