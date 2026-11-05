using System.IO;

namespace EasyCashFlow.Services;

/// <summary>
/// I file della cartella "dati", accanto all'eseguibile (l'app è portable). Vanno copiati sempre
/// insieme: senza chiave.json il database cifrato non si apre più (docs/07-sicurezza.md, sezione 4).
/// </summary>
public static class PercorsiDati
{
    public static string Cartella { get; } = Path.Combine(AppContext.BaseDirectory, "dati");

    /// <summary>Il database, cifrato (uno in chiaro delle versioni di prova non si apre).</summary>
    public static string Database => Path.Combine(Cartella, "easycashflow.db");

    /// <summary>La chiave del database, cifrata con la password e con il codice di recupero.</summary>
    public static string Chiave => Path.Combine(Cartella, "chiave.json");

    /// <summary>Preferenze d'aspetto, in chiaro: servono prima della password e non sono dati personali.</summary>
    public static string Preferenze => Path.Combine(Cartella, "preferenze.json");

    /// <summary>Scrive un file in modo che un'interruzione a metà non lo lasci rovinato: prima un file
    /// nuovo accanto, poi la sostituzione in un colpo solo.</summary>
    public static void ScriviAtomico(string percorso, string testo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        var nuovo = percorso + ".nuovo";
        File.WriteAllText(nuovo, testo);
        if (File.Exists(percorso))
            File.Replace(nuovo, percorso, null);
        else
            File.Move(nuovo, percorso);
    }
}
