using System.Windows;
using System.Windows.Threading;

namespace EasyCashFlow.Services;

/// <summary>
/// Le finestre di Windows per scegliere file e cartelle.
///
/// Si aprono con Dispatcher.InvokeAsync, non con Invoke: un clic in Blazor arriva da un evento di WebView2,
/// e in Blazor dentro WPF il codice gira sullo stesso thread. Con Invoke la finestra modale si apriva
/// dentro quell'evento, e WebView2 non lo sopporta: il 05/11 il programma è rimasto bloccato sulla scelta
/// del file e si è chiuso solo dopo la X. Con InvokeAsync l'evento finisce, e la finestra si apre subito dopo.
/// </summary>
public class SelettoreFile
{
    /// <summary>Restituisce il percorso scelto, oppure null se l'utente annulla.</summary>
    public Task<string?> ScegliFileCbiAsync() => Apri(() =>
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importa file CBI, XML o Excel",
            Filter = "File CBI, XML o Excel (*.txt;*.cbi;*.xml;*.xlsx;*.xls;*.csv)|*.txt;*.cbi;*.xml;*.xlsx;*.xls;*.csv|" +
                     "File CBI di testo o XML (*.txt;*.cbi;*.xml)|*.txt;*.cbi;*.xml|" +
                     "File Excel (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Tutti i file (*.*)|*.*"
        };
        return dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null;
    });

    /// <summary>Dove salvare il report Excel; null se l'utente annulla. Windows chiede conferma se il file
    /// esiste già.</summary>
    public Task<string?> ScegliDoveSalvareExcelAsync(string nomeProposto) => Apri(() =>
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salva il report Excel",
            FileName = nomeProposto,
            DefaultExt = ".xlsx",
            Filter = "Cartella di lavoro di Excel (*.xlsx)|*.xlsx",
            OverwritePrompt = true
        };
        return dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null;
    });

    /// <summary>Dove salvare l'esportazione dei gruppi di causali; null se l'utente annulla.</summary>
    public Task<string?> ScegliDoveSalvareGruppiAsync(string nomeProposto) => Apri(() =>
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Esporta i gruppi di causali",
            FileName = nomeProposto,
            DefaultExt = ".json",
            Filter = "Gruppi di causali (*.json)|*.json",
            OverwritePrompt = true
        };
        return dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null;
    });

    /// <summary>Un file di gruppi di causali esportato prima; null se l'utente annulla.</summary>
    public Task<string?> ScegliFileGruppiAsync() => Apri(() =>
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importa i gruppi di causali",
            Filter = "Gruppi di causali (*.json)|*.json|Tutti i file (*.*)|*.*"
        };
        return dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null;
    });

    /// <summary>Una cartella (per il backup); null se l'utente annulla.</summary>
    public Task<string?> ScegliCartellaAsync(string titolo, string? iniziale) => Apri(() =>
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog { Title = titolo };
        if (iniziale is not null && System.IO.Directory.Exists(iniziale))
            dialogo.InitialDirectory = iniziale;
        return dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FolderName : null;
    });

    // la finestra va aperta sul thread dell'interfaccia di WPF, dopo l'evento in corso
    private static Task<string?> Apri(Func<string?> mostra) =>
        Application.Current.Dispatcher.InvokeAsync(mostra, DispatcherPriority.Normal).Task;
}
