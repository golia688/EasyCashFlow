using EasyCashFlow.Services;

namespace EasyCashFlow.Models;

public enum TipoFiltro { Tutti, Entrate, Uscite }

/// <summary>Filtro sulle scelte fatte dall'utente sul singolo movimento (menu ⋮).</summary>
public enum FiltroEtichette { Tutti, Straordinari, NonStraordinari, TipoScelto }

public class FiltriMovimenti
{
    public IReadOnlyCollection<string> Conti { get; set; } = [];      // codici dei conti; nessuno = tutti
    public DateTime? DataDa { get; set; }
    public DateTime? DataA { get; set; }
    public TipoFiltro Tipo { get; set; } = TipoFiltro.Tutti;
    public FiltroEtichette Etichette { get; set; } = FiltroEtichette.Tutti;
    public decimal? ImportoDa { get; set; }
    public decimal? ImportoA { get; set; }
    public string? Testo { get; set; }
    public IReadOnlyCollection<string> Causali { get; set; } = [];    // nessuna = tutte

    // Per il log: conti, importi e testo cercato mascherati (Mascherato), date e causali in chiaro.
    public override string ToString()
    {
        var parti = new List<string>();
        if (Conti.Any()) parti.Add($"conti {string.Join(", ", Conti.Select(Mascherato.Conto))}");
        if (DataDa is DateTime da) parti.Add($"da {da:dd/MM/yyyy}");
        if (DataA is DateTime a) parti.Add($"a {a:dd/MM/yyyy}");
        if (Tipo != TipoFiltro.Tutti) parti.Add($"tipo {Tipo}");
        if (Etichette != FiltroEtichette.Tutti) parti.Add($"etichette {Etichette}");
        if (ImportoDa is decimal minimo) parti.Add($"importo >= {Mascherato.Importo(minimo)}");
        if (ImportoA is decimal massimo) parti.Add($"importo <= {Mascherato.Importo(massimo)}");
        if (!string.IsNullOrWhiteSpace(Testo)) parti.Add($"testo {Mascherato.Testo(Testo.Trim())}");
        if (Causali.Any()) parti.Add($"causali {string.Join(", ", Causali)}");
        return parti.Count == 0 ? "nessun filtro" : string.Join("; ", parti);
    }

    // Gli elenchi vengono copiati: i filtri applicati non devono cambiare se cambiano i campi.
    public FiltriMovimenti Copia()
    {
        var copia = (FiltriMovimenti)MemberwiseClone();
        copia.Conti = Conti.ToList();
        copia.Causali = Causali.ToList();
        return copia;
    }
}
