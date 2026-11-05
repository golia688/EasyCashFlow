using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>
/// I filtri della pagina Movimenti, ricordati finché il programma resta aperto. La pagina viene ricreata
/// a ogni navigazione: senza questo, andando ai movimenti scartati o ai gruppi di causali e tornando,
/// i filtri impostati andavano persi. Come le scelte di Analisi in AnalisiService, non si salvano alla
/// chiusura.
/// </summary>
public class StatoMovimenti
{
    /// <summary>Null finché la pagina non si apre la prima volta: allora riceve i filtri iniziali.</summary>
    public FiltriMovimenti? Filtri { get; set; }          // valori nei campi dei filtri

    public FiltriMovimenti? FiltriAttivi { get; set; }    // filtri realmente applicati alla griglia

    public int RighePerPagina { get; set; } = 25;

    /// <summary>La colonna su cui è ordinata la griglia (null = dal movimento più recente).</summary>
    public OrdineGriglia? Ordine { get; set; }
}
