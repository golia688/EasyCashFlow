using EasyCashFlow.Services;

namespace EasyCashFlow.Models;

public class FiltriScarti
{
    public IReadOnlyCollection<string> Conti { get; set; } = [];      // codici dei conti; nessuno = tutti
    public DateTime? DataDa { get; set; }       // data contabile del movimento scartato
    public DateTime? DataA { get; set; }
    public string? NomeFile { get; set; }
    public decimal? ImportoDa { get; set; }
    public decimal? ImportoA { get; set; }
    public string? Testo { get; set; }          // cercato in descrizioni e riferimento

    // Per il log: conti, importi e testo cercato mascherati (Mascherato), date e nome del file in chiaro.
    public override string ToString()
    {
        var parti = new List<string>();
        if (Conti.Any()) parti.Add($"conti {string.Join(", ", Conti.Select(Mascherato.Conto))}");
        if (DataDa is DateTime da) parti.Add($"da {da:dd/MM/yyyy}");
        if (DataA is DateTime a) parti.Add($"a {a:dd/MM/yyyy}");
        if (!string.IsNullOrEmpty(NomeFile)) parti.Add($"file {NomeFile}");
        if (ImportoDa is decimal minimo) parti.Add($"importo >= {Mascherato.Importo(minimo)}");
        if (ImportoA is decimal massimo) parti.Add($"importo <= {Mascherato.Importo(massimo)}");
        if (!string.IsNullOrWhiteSpace(Testo)) parti.Add($"testo {Mascherato.Testo(Testo.Trim())}");
        return parti.Count == 0 ? "nessun filtro" : string.Join("; ", parti);
    }

    // Gli elenchi vengono copiati: i filtri applicati non devono cambiare se cambiano i campi.
    public FiltriScarti Copia()
    {
        var copia = (FiltriScarti)MemberwiseClone();
        copia.Conti = Conti.ToList();
        return copia;
    }
}
