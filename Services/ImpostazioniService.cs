using System.IO;
using System.Text.Json;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

public enum ModoTema
{
    Scuro,
    Chiaro,
    Sistema     // segue il tema chiaro o scuro scelto in Windows
}

/// <summary>Le varianti delle Material Icons incluse in MudBlazor (vedi <see cref="Icone"/>).
/// Rounded e Sharp sono escluse: alle dimensioni dell'interfaccia sono quasi identiche a Filled.</summary>
public enum StileIcone
{
    Piene,          // Icons.Material.Filled
    Contorno,       // Icons.Material.Outlined
    DueToni         // Icons.Material.TwoTone
}

public enum StilePulsanti
{
    Classico,       // Material 2: testo maiuscolo, angoli di 4 px, ombra sui pulsanti pieni
    Moderno,        // testo normale, angoli di 8 px, niente ombra
    Arrotondato     // come Moderno, a forma di pillola
}

/// <summary>Lo stile dei grafici (dall'11/11): gli stessi dati, disegnati in due modi (OpzioniGrafici).</summary>
public enum StileGrafici
{
    Classico,       // quelli di sempre: barre per gruppo, saldo come linea
    Moderno         // grafici a rosa per gruppo con i colori in scala, saldo ad area sfumata con la barra per ingrandire
}

/// <summary>Preferenze d'aspetto dell'utente e giorni di conservazione dei log, salvate in
/// dati\preferenze.json, in chiaro: servono prima della password (colori della schermata di avvio e della
/// barra del titolo, zoom, pulizia dei log all'avvio) e non sono dati personali (docs/07-sicurezza.md,
/// sezione 6). Le preferenze che servono solo a database aperto stanno in PreferenzeService. Per
/// aggiungerne una: una proprietà qui, con la sua chiave e il suo valore predefinito.</summary>
public class ImpostazioniService
{
    private const string ChiaveTema = "Aspetto.Tema";
    private const string ChiaveColore = "Aspetto.ColorePrincipale";
    private const string ChiaveIcone = "Aspetto.StileIcone";
    private const string ChiavePulsanti = "Aspetto.StilePulsanti";
    private const string ChiaveGrafici = "Aspetto.StileGrafici";
    private const string ChiaveZoom = "Aspetto.Zoom";
    private const string ChiaveGiorniLog = "Log.GiorniDiConservazione";

    /// <summary>I livelli di zoom selezionabili, in percentuale.</summary>
    public static readonly IReadOnlyList<int> LivelliZoom = [90, 100, 110, 125, 150];

    /// <summary>Per quanti giorni si possono conservare i log.</summary>
    public static readonly IReadOnlyList<int> GiorniLogPossibili = [7, 30, 60, 90, 180, 365];

    private readonly LogService _log;
    private readonly Dictionary<string, string> _salvate;

    /// <summary>Scatta quando un'impostazione cambia, per aggiornare l'interfaccia.</summary>
    public event Action? Cambiate;

    public ImpostazioniService(LogService log)
    {
        _log = log;
        _salvate = Carica();
        var salvate = _salvate;

        // un valore non riconosciuto (file modificato a mano, versione futura) lascia il predefinito
        Tema = LeggiEnum(salvate, ChiaveTema, ModoTema.Scuro);
        ColorePrincipale = ColoreTema.DaChiave(salvate.GetValueOrDefault(ChiaveColore));
        StileIcone = LeggiEnum(salvate, ChiaveIcone, StileIcone.Piene);
        StilePulsanti = LeggiEnum(salvate, ChiavePulsanti, StilePulsanti.Classico);
        StileGrafici = LeggiEnum(salvate, ChiaveGrafici, StileGrafici.Classico);
        if (salvate.TryGetValue(ChiaveZoom, out var zoom) && int.TryParse(zoom, out var percentuale)
            && LivelliZoom.Contains(percentuale))
            Zoom = percentuale;
        if (salvate.TryGetValue(ChiaveGiorniLog, out var giorni) && int.TryParse(giorni, out var g)
            && GiorniLogPossibili.Contains(g))
            GiorniLog = g;

        _log.Debug($"Impostazioni caricate: tema {Tema}, colore {ColorePrincipale.Chiave}, " +
                   $"icone {StileIcone}, pulsanti {StilePulsanti}, zoom {Zoom}%, log per {GiorniLog} giorni");
        _log.EliminaLogVecchi(GiorniLog);
    }

    // senza file (primo avvio) valgono i predefiniti; il file si crea alla prima preferenza cambiata
    private Dictionary<string, string> Carica()
    {
        try
        {
            if (File.Exists(PercorsiDati.Preferenze))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PercorsiDati.Preferenze)) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // file rovinato o illeggibile: si riparte dai predefiniti, l'app parte comunque
            _log.Errore("Preferenze non leggibili: si usano quelle predefinite", ex);
        }
        return [];
    }

    private void Salva(Dictionary<string, string> preferenze)
    {
        try
        {
            PercorsiDati.ScriviAtomico(PercorsiDati.Preferenze,
                JsonSerializer.Serialize(preferenze, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Errore("Preferenze non salvate", ex);
        }
    }

    private void SalvaImpostazione(string chiave, string valore)
    {
        _salvate[chiave] = valore;
        Salva(_salvate);
    }

    // Enum.TryParse accetta anche numeri qualsiasi ("7"): IsDefined scarta quelli senza un nome.
    private static T LeggiEnum<T>(Dictionary<string, string> salvate, string chiave, T predefinito)
        where T : struct, Enum =>
        salvate.TryGetValue(chiave, out var testo) && Enum.TryParse<T>(testo, out var valore)
            && Enum.IsDefined(valore)
            ? valore
            : predefinito;

    public ModoTema Tema { get; private set; } = ModoTema.Scuro;

    public ColoreTema ColorePrincipale { get; private set; } = ColoreTema.Predefinito;

    public StileIcone StileIcone { get; private set; } = StileIcone.Piene;

    public StilePulsanti StilePulsanti { get; private set; } = StilePulsanti.Classico;

    public StileGrafici StileGrafici { get; private set; } = StileGrafici.Classico;

    /// <summary>Zoom dell'intera interfaccia, in percentuale (uno dei <see cref="LivelliZoom"/>).</summary>
    public int Zoom { get; private set; } = 100;

    /// <summary>Per quanti giorni si conservano i log (uno dei <see cref="GiorniLogPossibili"/>).</summary>
    public int GiorniLog { get; private set; } = LogService.GiorniPredefiniti;

    /// <summary>L'icona di MudBlazor con questo nome, nella variante scelta.
    /// Si usa con nameof: <c>Icona(nameof(Icons.Material.Filled.Home))</c>.</summary>
    public string Icona(string nome) => Icone.Di(StileIcone, nome);

    public void ImpostaColorePrincipale(ColoreTema colore)
    {
        if (colore == ColorePrincipale) return;

        ColorePrincipale = colore;
        SalvaImpostazione(ChiaveColore, colore.Chiave);
        _log.Info($"Colore principale cambiato: {colore.Nome}");
        Cambiate?.Invoke();
    }

    public void ImpostaTema(ModoTema tema)
    {
        if (tema == Tema) return;

        Tema = tema;
        SalvaImpostazione(ChiaveTema, tema.ToString());
        _log.Info($"Tema cambiato: {tema}");
        Cambiate?.Invoke();
    }

    public void ImpostaStileIcone(StileIcone stile)
    {
        if (stile == StileIcone) return;

        StileIcone = stile;
        SalvaImpostazione(ChiaveIcone, stile.ToString());
        _log.Info($"Stile delle icone cambiato: {stile}");
        Cambiate?.Invoke();
    }

    public void ImpostaStileGrafici(StileGrafici stile)
    {
        if (stile == StileGrafici) return;

        StileGrafici = stile;
        SalvaImpostazione(ChiaveGrafici, stile.ToString());
        _log.Info($"Stile dei grafici cambiato: {stile}");
        Cambiate?.Invoke();
    }

    public void ImpostaStilePulsanti(StilePulsanti stile)
    {
        if (stile == StilePulsanti) return;

        StilePulsanti = stile;
        SalvaImpostazione(ChiavePulsanti, stile.ToString());
        _log.Info($"Stile dei pulsanti cambiato: {stile}");
        Cambiate?.Invoke();
    }

    public void ImpostaZoom(int percentuale)
    {
        if (percentuale == Zoom || !LivelliZoom.Contains(percentuale)) return;

        Zoom = percentuale;
        SalvaImpostazione(ChiaveZoom, percentuale.ToString());
        _log.Info($"Zoom cambiato: {percentuale}%");
        Cambiate?.Invoke();
    }

    /// <summary>Cambia i giorni di conservazione e cancella subito i log più vecchi.</summary>
    public void ImpostaGiorniLog(int giorni)
    {
        if (giorni == GiorniLog || !GiorniLogPossibili.Contains(giorni)) return;

        GiorniLog = giorni;
        SalvaImpostazione(ChiaveGiorniLog, giorni.ToString());
        _log.Info($"Conservazione dei log cambiata: {giorni} giorni");
        _log.EliminaLogVecchi(giorni);
        Cambiate?.Invoke();
    }
}
