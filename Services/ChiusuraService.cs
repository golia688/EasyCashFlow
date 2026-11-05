using System.Windows;

namespace EasyCashFlow.Services;

/// <summary>Coordina la chiusura dell'applicazione tra la finestra WPF e l'interfaccia Blazor.
/// La X della finestra (e Alt+F4) passa da MainWindow, la voce Esci da Main.razor: in entrambi i casi
/// la conferma è la stessa finestra di MudBlazor, mostrata da Main.razor.</summary>
public class ChiusuraService
{
    /// <summary>True dopo la conferma (o se Windows si sta spegnendo): la finestra può chiudersi.</summary>
    public bool Consentita { get; private set; }

    /// <summary>La finestra chiede di chiudere: chi è iscritto mostra la richiesta di conferma.</summary>
    public event Func<Task>? ConfermaRichiesta;

    /// <summary>Avvia la richiesta di conferma. Restituisce false se nessuno può mostrarla (l'interfaccia
    /// non è ancora caricata): in quel caso la finestra si chiude senza chiedere.</summary>
    public bool ChiediConferma()
    {
        if (ConfermaRichiesta is null) return false;
        _ = ConfermaRichiesta.Invoke();
        return true;
    }

    /// <summary>Chiude davvero, senza chiedere di nuovo. La finestra appartiene al thread di WPF.
    /// BeginInvoke e non Invoke: da bloccato Main.razor chiama Chiudi mentre la finestra è ancora dentro
    /// il suo evento Closing (InvokeAsync di Blazor gira subito, sullo stesso thread), e lì Close() non fa
    /// niente; la chiusura deve arrivare dopo.</summary>
    public void Chiudi()
    {
        Consentita = true;
        Application.Current.Dispatcher.BeginInvoke(() => Application.Current.MainWindow?.Close());
    }

    /// <summary>Spegnimento o disconnessione di Windows: non si deve trattenere la chiusura.</summary>
    public void ConsentiSenzaConferma() => Consentita = true;
}
