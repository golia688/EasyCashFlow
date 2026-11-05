using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// Filtri e movimenti scelti nel grafico personalizzato (Analisi > Grafici > Grafico personalizzato),
/// ricordati finché il programma resta aperto, come i filtri di Movimenti in StatoMovimenti. La selezione
/// non si salva alla chiusura: è fatta di singoli movimenti, e i file importati dopo non ci entrerebbero.
/// </summary>
public class StatoGraficoPersonalizzato
{
    /// <summary>Null finché la pagina non si apre la prima volta: allora riceve i filtri iniziali.</summary>
    public FiltriMovimenti? Filtri { get; set; }          // valori nei campi dei filtri

    public FiltriMovimenti? FiltriAttivi { get; set; }    // filtri dei risultati mostrati nella griglia

    /// <summary>I movimenti nel grafico. MovimentoCbi è un record: la stessa riga riletta dal database
    /// risulta uguale, quindi resta spuntata cambiando pagina o filtri.</summary>
    public HashSet<MovimentoCbi> Selezione { get; set; } = [];

    /// <summary>La griglia mostra solo i movimenti nel grafico invece dei risultati del filtro.</summary>
    public bool SoloSelezione { get; set; }

    public int RighePerPagina { get; set; } = 10;

    /// <summary>La colonna su cui è ordinata la griglia (null = dal movimento più recente).</summary>
    public OrdineGriglia? Ordine { get; set; }
}
