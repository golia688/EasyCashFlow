namespace EasyCashFlow.Services;

/// <summary>Avvisa la finestra WPF quando Blazor ha disegnato la prima pagina: fino ad allora la
/// finestra mostra la sua schermata di avvio e tiene nascosta la WebView (vedi MainWindow).</summary>
public class AvvioService
{
    public bool Pronta { get; private set; }

    public event Action? InterfacciaPronta;

    /// <summary>Chiamato da Main.razor dopo il primo disegno. Le chiamate successive non fanno niente.</summary>
    public void SegnalaPronta()
    {
        if (Pronta) return;
        Pronta = true;
        InterfacciaPronta?.Invoke();
    }
}
