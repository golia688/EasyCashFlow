namespace EasyCashFlow.Models;

/// <summary>I colori dei grafici per il tema in uso.
/// Entrate, uscite e colore principale sono gli stessi dell'interfaccia (ColoreTema).
/// Le serie senza un significato proprio (per esempio ricorrenti e variabili) prendono i colori di
/// Categorie nell'ordine fisso, mai a rotazione. Quella palette è più satura dei pastelli
/// dell'interfaccia: con i pastelli puri le serie vicine non si distinguono, soprattutto per chi ha
/// un'alterazione della visione dei colori. È stata verificata con il validatore della skill dataviz
/// (banda di luminosità, saturazione minima, separazione CVD e normale tra serie adiacenti) sulle
/// superfici dei due temi; tre colori per tema stanno sotto 3:1 di contrasto sulla superficie,
/// per cui i grafici che li usano mostrano sempre legenda ed etichette.</summary>
public record ColoriGrafici(
    string Testo,
    string TestoSecondario,
    string Griglia,
    string Superficie,
    string SfondoTooltip,
    string Principale,
    string Entrate,
    string Uscite,
    string Neutro,
    string Ipotesi,     // valori simulati (giallo miele): si distinguono dai reali, nel colore principale
    IReadOnlyList<string> Categorie,
    // le scale dei grafici a rosa "Dove vanno i soldi" e "Da dove arrivano" (dall'11/11): il gruppo più grande
    // ha il colore scuro, il più piccolo quello chiaro, e quelli in mezzo le sfumature intermedie
    string UsciteScuro, string UsciteChiaro, string EntrateScuro, string EntrateChiaro)
{
    /// <summary>rilievo: con i riquadri a rilievo (StileRiquadri) la superficie è quella dei riquadri, blu-grigio
    /// nel tema scuro e avorio nel tema chiaro.</summary>
    public static ColoriGrafici Per(bool scuro, ColoreTema principale, bool rilievo = false) =>
        !rilievo ? PerTema(scuro, principale)
        : scuro ? PerTema(true, principale) with { Superficie = "#363C4A", SfondoTooltip = "#2F3542" }
        : PerTema(false, principale) with { Superficie = "#F4F2EC" };

    private static ColoriGrafici PerTema(bool scuro, ColoreTema principale) => scuro
        ? new ColoriGrafici(
            Testo: "rgba(255,255,255,0.80)",
            TestoSecondario: "rgba(255,255,255,0.55)",
            Griglia: "rgba(255,255,255,0.10)",
            Superficie: "#373740",
            SfondoTooltip: "#2C2C34",
            Principale: principale.Scuro,
            Entrate: ColoreTema.Entrate.Scuro,
            Uscite: ColoreTema.Uscite.Scuro,
            Neutro: "#8E8E99",
            Ipotesi: ColoreTema.Avviso.Scuro,
            Categorie: ["#695ca3", "#c98250", "#3a6aa7", "#b48f3c", "#8c5087", "#1faaa4"],
            UsciteScuro: "#8E1F1F", UsciteChiaro: "#F2A39C", EntrateScuro: "#1D5C38", EntrateChiaro: "#A3DAB5")
        : new ColoriGrafici(
            Testo: "rgba(0,0,0,0.80)",
            TestoSecondario: "rgba(0,0,0,0.55)",
            Griglia: "rgba(0,0,0,0.10)",
            Superficie: "#F7F6F2",
            SfondoTooltip: "#FFFFFF",
            Principale: principale.Chiaro,
            Entrate: ColoreTema.Entrate.Chiaro,
            Uscite: ColoreTema.Uscite.Chiaro,
            Neutro: "#8C8A85",
            Ipotesi: ColoreTema.Avviso.Chiaro,
            Categorie: ["#695ca6", "#dc905b", "#376aaa", "#c69f47", "#8d4e88", "#2dbbb4"],
            UsciteScuro: "#9A2A26", UsciteChiaro: "#EFB0AA", EntrateScuro: "#1F6A40", EntrateChiaro: "#AEDDBD");
}
