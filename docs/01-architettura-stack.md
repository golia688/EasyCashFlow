# Architettura dell'applicazione: lo stack

Questo documento spiega come sono combinati i componenti tecnologici di **EasyCashFlow**: WPF, WebView2, Blazor, MudBlazor ed ECharts.

## L'idea di fondo

L'applicazione è una **normale app Windows che contiene una pagina web**. L'app è scritta in C#, la pagina è l'interfaccia utente, e tutto gira in locale, senza server e senza connessione a internet.

```
┌─ App Windows (.exe, processo .NET) ──────────────────┐
│  WPF  →  finestra, barra del titolo, ridimensiona    │
│  ┌─ WebView2 (browser Edge incorporato) ───────────┐ │
│  │  Pagina HTML (index.html)                       │ │
│  │   └─ Blazor: UI scritta in C#/Razor             │ │
│  │       └─ MudBlazor: componenti grafici belli    │ │
│  │       └─ ECharts: grafici (JavaScript)          │ │
│  └─────────────────────────────────────────────────┘ │
│  Logica C#: calcoli, SQLite, import dei dati         │
└──────────────────────────────────────────────────────┘
```

## WPF: il contenitore

**Windows Presentation Foundation** è il framework Microsoft per creare applicazioni desktop in C#. Fornisce la finestra, l'eseguibile, il ciclo di vita dell'app e l'integrazione con Windows.

In questo progetto WPF è usato **solo come guscio**: `MainWindow.xaml` contiene un unico controllo, il `BlazorWebView`, che occupa tutta la finestra. Non si disegna nulla in XAML.

## WebView2: il browser incorporato

È un componente di Microsoft che permette a un'app di mostrare contenuti web usando il motore di **Edge (Chromium)**. È come avere una scheda di Chrome dentro la finestra dell'app, senza barra degli indirizzi.

Serve perché HTML, CSS e JavaScript sono oggi il modo più potente per ottenere interfacce e grafici di alta qualità. Il runtime è preinstallato su Windows 11.

## Blazor: l'interfaccia scritta in C#

Normalmente una pagina web si programma in JavaScript. **Blazor** permette di scriverla in **C#**, in file `.razor` che mescolano HTML e codice:

```razor
<MudText>Saldo: @saldo €</MudText>
<MudButton OnClick="Aggiungi">Aggiungi</MudButton>

@code {
    decimal saldo;
    void Aggiungi() => saldo += 100;
}
```

Nella variante **Blazor Hybrid** (quella usata qui) il codice C# **non gira nel browser**: gira nel normale processo .NET dell'app. Blazor calcola cosa mostrare e manda a WebView2 solo le istruzioni per aggiornare la pagina. Per questo può accedere direttamente ai file, a SQLite e a tutto ciò che fa un'app .NET.

## MudBlazor: i componenti già belli

È una **libreria di componenti** per Blazor, basata su Material Design: bottoni, tabelle, card, menu, dialoghi, tema scuro. Invece di scrivere HTML e CSS a mano, si usano tag come `<MudCard>` o `<MudDataGrid>` ottenendo un risultato curato.

I componenti `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider` e `MudSnackbarProvider` presenti in `Main.razor` sono i "servizi di base" che MudBlazor richiede per funzionare.

## ECharts: i grafici

Non fa parte della catena Blazor: è una libreria **JavaScript** per grafici. Il codice C# prepara i dati (ad esempio le entrate mensili) e li passa a ECharts, che li disegna dentro la pagina. Questo passaggio si chiama *JS interop*.

## Un esempio concreto del flusso

1. L'utente clicca su "Aggiungi movimento" (un componente MudBlazor, mostrato da WebView2).
2. Blazor esegue il metodo C# corrispondente, che salva il movimento in SQLite.
3. Il C# ricalcola i totali e passa i nuovi dati a ECharts.
4. ECharts ridisegna il grafico con un'animazione.

## Vantaggi e svantaggi

**Vantaggi**
- Grafica moderna e grafici di alto livello.
- Un solo linguaggio (C#) per logica e interfaccia.
- App portable, senza installer.

**Svantaggi**
- Ci sono due processi (l'app e quello di WebView2), quindi il consumo di memoria è maggiore rispetto a un'app WPF pura.
- Dipendenza dal runtime WebView2: presente su Windows 11, ma non garantito su sistemi molto vecchi.

## File principali del progetto

| File | Ruolo |
|---|---|
| `EasyCashFlow.csproj` | Configurazione del progetto (SDK Razor, `net10.0-windows10.0.19041.0`, pacchetti NuGet) |
| `MainWindow.xaml` / `.xaml.cs` | Finestra WPF che ospita il `BlazorWebView` e registra i servizi |
| `wwwroot/index.html` | Pagina host caricata da WebView2 (CSS e script di MudBlazor) |
| `Main.razor` | Componente radice di Blazor, con i provider di MudBlazor |
| `_Imports.razor` | `@using` comuni a tutti i componenti Razor |
