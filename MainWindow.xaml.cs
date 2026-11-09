using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using EasyCashFlow.Models;
using EasyCashFlow.Services;

namespace EasyCashFlow;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var cronometro = Stopwatch.StartNew();
        // il motore SQLite con la cifratura (SQLite3MC), prima di qualunque connessione
        SQLitePCL.Batteries_V2.Init();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        // Notifiche (snackbar) più rapide delle predefinite di MudBlazor, che entrano ed escono in mezzo
        // secondo con una dissolvenza lenta anche chiudendole con la X (segnalazione del 05/11); al massimo
        // tre insieme, e la stessa notifica non si ripete
        services.AddMudServices(configurazione =>
        {
            configurazione.SnackbarConfiguration.ShowTransitionDuration = 150;
            configurazione.SnackbarConfiguration.HideTransitionDuration = 150;
            configurazione.SnackbarConfiguration.MaxDisplayedSnackbars = 3;
            configurazione.SnackbarConfiguration.PreventDuplicates = true;
        });
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<ChiaviService>();
        services.AddSingleton<AccessoService>();
        services.AddSingleton<CbiParser>();
        services.AddSingleton<CbiXmlParser>();
        services.AddSingleton<SelettoreFile>();
        services.AddSingleton<ImportazioneService>();
        services.AddSingleton<LogService>();
        services.AddSingleton<ImpostazioniService>();
        services.AddSingleton<PreferenzeService>();
        services.AddSingleton<StatoMovimenti>();
        services.AddSingleton<StatoGraficoPersonalizzato>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<BloccoService>();
        services.AddSingleton<AnalisiService>();
        services.AddSingleton<ChiusuraService>();
        services.AddSingleton<ManualeService>();
        services.AddSingleton<AvvioService>();
        services.AddSingleton<NotificheService>();
        services.AddSingleton<EliminazioneContoService>();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        var provider = services.BuildServiceProvider();

        // prima i gestori degli errori, così anche un problema del database all'avvio finisce nel log
        var log = provider.GetRequiredService<LogService>();
        RegistraGestoriErrori(log);

        // i servizi prima della finestra: lo sfondo dell'avvio dipende dal tema salvato
        ImpostaSfondoAvvio(provider.GetRequiredService<ImpostazioniService>());
        PreparaAccesso(provider, log);
        InitializeComponent();
        Resources.Add("services", provider);

        GestisciAvvio(provider.GetRequiredService<AvvioService>(), log, cronometro);
        ColoraBarraDelTitolo(provider.GetRequiredService<ImpostazioniService>());
        ApplicaZoom(provider.GetRequiredService<ImpostazioniService>());
        IntercettaChiusura(provider.GetRequiredService<ChiusuraService>(), log);
        log.Info("Applicazione avviata");
    }

    // Accesso ai dati (docs/07-sicurezza.md, sezione 6): AccessoService decide se chiedere di creare la
    // password o di sbloccare; Main.razor mostra la pagina di accesso finché il database non è aperto.
    // Due casi non si risolvono dall'interfaccia, quindi lo si dice e si esce: un database cifrato senza
    // dati\chiave.json, e uno in chiaro delle versioni di prova fino alla 0.9.2 (non si converte: con due
    // soli utenti si reimportano i file CBI). Alla chiusura la chiave si toglie dalla memoria e le
    // connessioni si chiudono.
    private void PreparaAccesso(IServiceProvider provider, LogService log)
    {
        var database = provider.GetRequiredService<DatabaseService>();
        var chiavi = provider.GetRequiredService<ChiaviService>();

        var testo = database.Stato() switch
        {
            StatoDatabase.InChiaro =>
                "Il database è di una versione di prova precedente (fino alla 0.9.2), che non lo cifrava: " +
                "questa versione non lo apre.\n\n" +
                "Cancella le cartelle dati e log che stanno accanto al programma e avvialo di nuovo: " +
                "ti chiederà di creare una password. Poi importa di nuovo i file scaricati dalla banca.",
            StatoDatabase.Cifrato when !chiavi.FileEsiste =>
                "Il database è cifrato ma manca il file della chiave (dati\\chiave.json): senza non si può aprire.\n\n" +
                "Rimetti il file chiave.json nella cartella dati, prendendolo dalla stessa copia di sicurezza del database.",
            _ => null
        };
        if (testo is not null)
        {
            log.Errore($"Database non aperto: {testo}");
            System.Windows.MessageBox.Show(testo, "EasyCashFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }

        provider.GetRequiredService<AccessoService>().Prepara();
        Closed += (_, _) =>
        {
            chiavi.Blocca();
            database.Chiudi();
        };
    }

    // All'avvio, prima che Blazor disegni l'app, si vedono la finestra e la WebView vuote: di base sono
    // bianche, e con il tema scuro c'è un lampo bianco. Si colorano entrambe con lo sfondo del tema scelto
    // (gli stessi colori di Main.razor). La WebView legge il colore da una variabile d'ambiente, che va
    // impostata prima che venga creata; il formato è AARRGGBB.
    private void ImpostaSfondoAvvio(ImpostazioniService impostazioni)
    {
        var colore = TemaScuro(impostazioni) ? "32333D" : "EFEEE9";

        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "FF" + colore);
        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#" + colore)!;
    }

    // lo stesso calcolo di Main.razor: Chiaro, Scuro o il tema delle app di Windows
    private static bool TemaScuro(ImpostazioniService impostazioni) => impostazioni.Tema switch
    {
        ModoTema.Chiaro => false,
        ModoTema.Sistema => WindowsInTemaScuro(),
        _ => true
    };

    // Schermata di avvio nativa (MainWindow.xaml): resta finché Blazor non ha disegnato la prima pagina,
    // poi lascia il posto alla WebView. Se l'avvio va per le lunghe lo dice; dopo 30 secondi mostra
    // comunque la WebView, per non restare bloccati sulla schermata se qualcosa non segnala la fine.
    private void GestisciAvvio(AvvioService avvio, LogService log, Stopwatch cronometro)
    {
        var attesa = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        attesa.Tick += (_, _) =>
        {
            if (cronometro.Elapsed < TimeSpan.FromSeconds(30))
            {
                testoAvvio.Text = "L'avvio sta richiedendo più del solito…\nAttendi ancora qualche secondo.";
                return;
            }
            attesa.Stop();
            log.Avviso("L'interfaccia non ha segnalato la fine dell'avvio entro 30 secondi: viene mostrata comunque");
            MostraInterfaccia();
        };
        attesa.Start();

        avvio.InterfacciaPronta += () => Dispatcher.BeginInvoke(() =>
        {
            attesa.Stop();
            MostraInterfaccia();
            log.Info($"Interfaccia pronta in {cronometro.ElapsedMilliseconds} ms dall'avvio della finestra");
        });
    }

    // Il campo della password ha già il cursore nella pagina (AutoFocus), ma la tastiera di Windows resta
    // sulla finestra WPF e non arriva alla WebView finché non ci si clicca: le si dà il focus qui, e si
    // porta davanti la finestra (lanciata da Visual Studio, resterebbe dietro).
    private void MostraInterfaccia()
    {
        vistaBlazor.Visibility = Visibility.Visible;
        schermataAvvio.Visibility = Visibility.Collapsed;
        Activate();
        vistaBlazor.WebView?.Focus();
    }

    // La barra del titolo è disegnata da Windows: di base è chiara (o del colore di accento, se attivo
    // in Impostazioni > Personalizzazione > Colori) e non segue il tema dell'app. Su Windows 11 si può
    // colorarla: qui prende lo stesso colore e lo stesso testo della barra in alto dell'app, così finestra
    // e app sembrano un pezzo solo. Su Windows 10 il colore non è supportato e resta quello di sistema,
    // ma almeno con il tema scuro la barra diventa scura (DWMWA_USE_IMMERSIVE_DARK_MODE).
    private void ColoraBarraDelTitolo(ImpostazioniService impostazioni)
    {
        void Applica()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;

            var scuro = TemaScuro(impostazioni);
            var sfondo = scuro ? "#27272F" : impostazioni.ColorePrincipale.Chiaro;    // AppbarBackground di Main.razor
            var testo = scuro ? "#FFFFFFB2" : ColoreTema.TestoSu(impostazioni.ColorePrincipale.Chiaro);

            int modoScuro = scuro ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmaModoScuro, ref modoScuro, sizeof(int));
            int coloreSfondo = ColoreRef(sfondo), coloreTesto = ColoreRef(testo);
            DwmSetWindowAttribute(handle, DwmaColoreBarra, ref coloreSfondo, sizeof(int));
            DwmSetWindowAttribute(handle, DwmaColoreTesto, ref coloreTesto, sizeof(int));
        }

        SourceInitialized += (_, _) => Applica();
        impostazioni.Cambiate += () => Dispatcher.BeginInvoke(Applica);
        // con "Come Windows" la barra segue anche un cambio di tema fatto ad app aperta
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (impostazioni.Tema == ModoTema.Sistema) Dispatcher.BeginInvoke(Applica);
        };
    }

    // "#RRGGBB" (o "#RRGGBBAA", la trasparenza si ignora) -> COLORREF di Windows, 0x00BBGGRR
    private static int ColoreRef(string esadecimale)
    {
        var r = Convert.ToInt32(esadecimale.Substring(1, 2), 16);
        var g = Convert.ToInt32(esadecimale.Substring(3, 2), 16);
        var b = Convert.ToInt32(esadecimale.Substring(5, 2), 16);
        return r | (g << 8) | (b << 16);
    }

    private const int DwmaModoScuro = 20;       // DWMWA_USE_IMMERSIVE_DARK_MODE
    private const int DwmaColoreBarra = 35;     // DWMWA_CAPTION_COLOR (Windows 11)
    private const int DwmaColoreTesto = 36;     // DWMWA_TEXT_COLOR (Windows 11)

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr finestra, int attributo, ref int valore, int dimensione);

    // tema delle app di Windows: 0 = scuro (Impostazioni > Personalizzazione > Colori)
    private static bool WindowsInTemaScuro()
    {
        using var chiave = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return chiave?.GetValue("AppsUseLightTheme") is int valore && valore == 0;
    }

    // La X della finestra e Alt+F4 non chiudono subito: la chiusura viene annullata e l'interfaccia
    // chiede conferma (la stessa finestra della voce Esci). Confermando, ChiusuraService chiude di
    // nuovo la finestra e questa volta la chiusura passa. Se Windows si spegne o l'utente si disconnette
    // non si chiede niente, per non bloccare lo spegnimento.
    private void IntercettaChiusura(ChiusuraService chiusura, LogService log)
    {
        Closing += (_, e) =>
        {
            if (chiusura.Consentita) return;
            if (chiusura.ChiediConferma())
            {
                e.Cancel = true;
                log.Debug("Chiusura della finestra: richiesta conferma");
            }
        };

        System.Windows.Application.Current.SessionEnding += (_, _) =>
        {
            chiusura.ConsentiSenzaConferma();
            log.Info("Applicazione chiusa per lo spegnimento o la disconnessione di Windows");
        };
    }

    // Lo zoom dell'interfaccia è quello nativo di WebView2, come in un browser: ingrandisce tutto,
    // testo, campi, icone e griglie insieme. Si sceglie solo dalla pagina Aspetto: Ctrl+rotellina
    // e Ctrl+/- sono disattivati, altrimenti lo zoom cambierebbe senza aggiornare l'impostazione.
    private void ApplicaZoom(ImpostazioniService impostazioni)
    {
        vistaBlazor.BlazorWebViewInitialized += (_, e) =>
        {
            e.WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            e.WebView.ZoomFactor = impostazioni.Zoom / 100.0;

            impostazioni.Cambiate += () => Dispatcher.Invoke(() =>
                e.WebView.ZoomFactor = impostazioni.Zoom / 100.0);
        };
    }

    // Le eccezioni non gestite vengono scritte nel log (sempre con lo stack trace completo)
    // prima che l'applicazione si chiuda.
    private static void RegistraGestoriErrori(LogService log)
    {
        System.Windows.Application.Current.DispatcherUnhandledException += (_, e) =>
            log.Fatale("Eccezione non gestita (interfaccia)", e.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Fatale("Eccezione non gestita (applicazione)",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        TaskScheduler.UnobservedTaskException += (_, e) =>
            log.Errore("Eccezione non osservata in un'attività asincrona", e.Exception);
    }
}