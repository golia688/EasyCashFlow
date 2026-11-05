using MudBlazor;

namespace EasyCashFlow.Models;

/// <summary>
/// L'ordine scelto cliccando l'intestazione di una colonna in una griglia che legge i dati dal database una
/// pagina alla volta (Movimenti, scarti, grafico personalizzato): la griglia da sola ordinerebbe solo la
/// pagina visibile, quindi l'ordine va chiesto alla query. Campo è il nome della proprietà della colonna
/// ("Importo", "DataContabile"…); DatabaseService lo traduce in una colonna SQL da un elenco fisso, così
/// nella query non entra mai testo libero.
/// </summary>
public record OrdineGriglia(string Campo, bool Decrescente)
{
    /// <summary>L'ordine attuale della griglia (null = quello predefinito). Di una proprietà annidata
    /// ("Scartato.Importo") si tiene l'ultimo nome.</summary>
    public static OrdineGriglia? Da<T>(IEnumerable<SortDefinition<T>> ordinamenti) =>
        ordinamenti.OrderBy(o => o.Index).FirstOrDefault() is { SortBy: { Length: > 0 } nome } o
            ? new OrdineGriglia(nome[(nome.LastIndexOf('.') + 1)..], o.Descending)
            : null;

    /// <summary>Per InitialDirection delle colonne: la griglia ricreata (dopo i filtri, tornando alla
    /// pagina) mostra ancora la freccia sulla colonna ordinata.</summary>
    public static SortDirection Direzione(OrdineGriglia? ordine, string campo) =>
        ordine?.Campo == campo ? (ordine.Decrescente ? SortDirection.Descending : SortDirection.Ascending) : SortDirection.None;
}
