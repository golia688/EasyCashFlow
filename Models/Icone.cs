using System.Collections.Concurrent;
using System.Reflection;
using EasyCashFlow.Services;
using MudBlazor;

namespace EasyCashFlow.Models;

/// <summary>Trova un'icona di MudBlazor nella variante scelta. In MudBlazor ogni variante è una
/// classe (Icons.Material.Filled, .Outlined, ...) con le stesse costanti: la stessa icona ha lo
/// stesso nome in tutte, quindi basta cercare il nome nella classe giusta.</summary>
public static class Icone
{
    // la riflessione serve una volta sola per ogni coppia variante-nome
    private static readonly ConcurrentDictionary<(StileIcone, string), string> _cache = new();

    public static string Di(StileIcone stile, string nome) =>
        _cache.GetOrAdd((stile, nome), chiave => Cerca(chiave.Item1, chiave.Item2));

    private static string Cerca(StileIcone stile, string nome)
    {
        var classe = stile switch
        {
            StileIcone.Contorno => typeof(Icons.Material.Outlined),
            StileIcone.DueToni => typeof(Icons.Material.TwoTone),
            _ => typeof(Icons.Material.Filled)
        };

        // se una variante non avesse quell'icona, si ripiega su quella piena
        return LeggiCostante(classe, nome)
               ?? LeggiCostante(typeof(Icons.Material.Filled), nome)
               ?? throw new ArgumentException($"Icona sconosciuta: {nome}", nameof(nome));
    }

    private static string? LeggiCostante(Type classe, string nome) =>
        classe.GetField(nome, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
}
