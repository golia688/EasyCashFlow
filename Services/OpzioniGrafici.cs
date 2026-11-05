using System.Globalization;
using EasyCashFlow.Models;

namespace EasyCashFlow.Services;

/// <summary>Costruisce le opzioni ECharts dei grafici della pagina Analisi.
/// Sono oggetti anonimi serializzati in JSON: le stringhe "@euro", "@euroAsse" e "@meseDaData" sono
/// formattatori che wwwroot/js/grafici.js sostituisce con le funzioni corrispondenti.
/// Scelte comuni: un solo asse delle ordinate per grafico, griglia leggera, segni sottili con
/// estremità arrotondate di 4 px, tooltip al passaggio del mouse, legenda quando le serie sono più di una.</summary>
public static class OpzioniGrafici
{
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");
    private static readonly string[] NomiMesi = ["Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic"];

    private static string Mese(DateOnly mese) =>
        $"{NomiMesi[mese.Month - 1]} {mese.Year % 100:00}";

    private static double Arrotonda(decimal valore) => (double)Math.Round(valore, 2);

    private static object Testo(ColoriGrafici c) => new { fontFamily = "Roboto, Helvetica, Arial, sans-serif", color = c.Testo };

    private static object Tooltip(ColoriGrafici c, string attivazione) => new
    {
        trigger = attivazione,
        backgroundColor = c.SfondoTooltip,
        borderColor = c.Griglia,
        textStyle = new { color = c.Testo },
        valueFormatter = "@euro"
    };

    private static object AsseCategorie(ColoriGrafici c, IEnumerable<string> valori, object? etichette = null) => new
    {
        type = "category",
        data = valori,
        axisLine = new { lineStyle = new { color = c.Griglia } },
        axisTick = new { show = false },
        axisLabel = etichette ?? new { color = c.TestoSecondario }
    };

    private static object AsseImporti(ColoriGrafici c, bool scala = false) => new
    {
        type = "value",
        scale = scala,      // per il saldo: l'asse non parte da zero, così si vede la variazione
        axisLabel = new { color = c.TestoSecondario, formatter = "@euroAsse" },
        splitLine = new { lineStyle = new { color = c.Griglia } }
    };

    private static object Legenda(ColoriGrafici c) => new
    {
        top = 0,
        left = 0,
        icon = "roundRect",
        itemWidth = 12,
        itemHeight = 12,
        textStyle = new { color = c.TestoSecondario }
    };

    private static object Griglia(int alto = 36) => new { left = 8, right = 16, top = alto, bottom = 8, containLabel = true };

    /// <summary>Entrate sopra lo zero, uscite sotto, flusso netto come linea: la posizione distingue
    /// entrate e uscite anche senza il colore (verde e rosso si confondono per i daltonici).</summary>
    public static object EntrateUscitePerMese(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = Tooltip(c, "axis"),
        legend = Legenda(c),
        grid = Griglia(),
        xAxis = AsseCategorie(c, mesi.Select(m => Mese(m.Mese))),
        yAxis = AsseImporti(c),
        series = new object[]
        {
            new
            {
                name = "Entrate", type = "bar", stack = "flusso", barMaxWidth = 28, cursor = "pointer",
                itemStyle = new { color = c.Entrate, borderRadius = new[] { 4, 4, 0, 0 } },
                data = mesi.Select(m => Arrotonda(m.Entrate))
            },
            new
            {
                name = "Uscite", type = "bar", stack = "flusso", barMaxWidth = 28, cursor = "pointer",
                itemStyle = new { color = c.Uscite, borderRadius = new[] { 0, 0, 4, 4 } },
                data = mesi.Select(m => Arrotonda(-m.Uscite))
            },
            new
            {
                name = "Flusso netto", type = "line", symbol = "circle", symbolSize = 8, cursor = "pointer",
                lineStyle = new { width = 2, color = c.Testo },
                itemStyle = new { color = c.Testo, borderColor = c.Superficie, borderWidth = 2 },
                data = mesi.Select(m => Arrotonda(m.Netto))
            }
        }
    };

    public static object Saldo(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = Tooltip(c, "axis"),
        grid = Griglia(16),
        xAxis = AsseCategorie(c, punti.Select(p => p.Data.ToString("dd/MM/yyyy", Italiano)),
            new { color = c.TestoSecondario, formatter = "@meseDaData" }),
        yAxis = AsseImporti(c, scala: true),
        series = new object[]
        {
            new
            {
                name = "Saldo", type = "line", showSymbol = false, symbolSize = 8,
                lineStyle = new { width = 2, color = c.Principale },
                itemStyle = new { color = c.Principale },
                areaStyle = new { color = c.Principale, opacity = 0.12 },
                data = punti.Select(p => Arrotonda(p.Saldo))
            }
        }
    };

    /// <summary>Il saldo come sola linea, senza assi né griglia, per la Home: conta l'andamento,
    /// i valori si leggono nel tooltip.</summary>
    public static object SaldoMini(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = Tooltip(c, "axis"),
        grid = new { left = 2, right = 2, top = 6, bottom = 2 },
        xAxis = new { type = "category", show = false, boundaryGap = false, data = punti.Select(p => p.Data.ToString("dd/MM/yyyy", Italiano)) },
        yAxis = new { type = "value", show = false, scale = true },
        series = new object[]
        {
            new
            {
                name = "Saldo", type = "line", showSymbol = false, symbolSize = 8,
                lineStyle = new { width = 2, color = c.Principale },
                itemStyle = new { color = c.Principale },
                areaStyle = new { color = c.Principale, opacity = 0.12 },
                data = punti.Select(p => Arrotonda(p.Saldo))
            }
        }
    };

    /// <summary>Simulazione: saldo reale (linea piena con area) e, di seguito, saldo simulato a fine mese
    /// (tratteggiato, stesso colore), su un asse dei tempi vero: i punti reali sono giornalieri, quelli
    /// simulati mensili, e un asse a categorie li distanzierebbe in modo sbagliato. La linea simulata
    /// parte dall'ultimo saldo reale, così le due si toccano. Se il saldo simulato scende sotto zero,
    /// una linea rossa segna lo zero.</summary>
    public static object Simulazione(IReadOnlyList<PuntoSaldo> storico, PuntoSaldo? inizio,
                                     IReadOnlyList<MeseSimulato> mesi, ColoriGrafici c)
    {
        static string Data(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        static DateOnly FineMese(DateOnly m) => m.AddMonths(1).AddDays(-1);

        var simulati = new List<object[]>();
        if (inizio is not null)
            simulati.Add([Data(inizio.Data), Arrotonda(inizio.Saldo)]);
        simulati.AddRange(mesi.Select(m => new object[] { Data(FineMese(m.Mese)), Arrotonda(m.Saldo) }));

        var sottoZero = mesi.Any(m => m.Saldo < 0);

        // Conto + versato negli investimenti: dal saldo di oggi cresce del solo flusso netto, cioè di
        // quello che si risparmia. Con dei versamenti si separa dalla linea del conto e mostra che i
        // soldi complessivi crescono anche quando il conto scende. Senza versamenti coinciderebbe.
        var conInvestimenti = new List<object[]>();
        if (inizio is not null && mesi.Any(m => m.Investito != 0))
        {
            var totale = inizio.Saldo;
            conInvestimenti.Add([Data(inizio.Data), Arrotonda(totale)]);
            foreach (var m in mesi)
            {
                totale += m.Netto;
                conInvestimenti.Add([Data(FineMese(m.Mese)), Arrotonda(totale)]);
            }
        }

        var serie = new List<object>
        {
            new
            {
                name = "Saldo reale", type = "line", showSymbol = false, symbolSize = 8,
                lineStyle = new { width = 2, color = c.Principale },
                itemStyle = new { color = c.Principale },
                areaStyle = new { color = c.Principale, opacity = 0.12 },
                data = storico.Select(p => new object[] { Data(p.Data), Arrotonda(p.Saldo) })
            },
            new
            {
                name = "Saldo simulato", type = "line", symbol = "circle", symbolSize = 8,
                lineStyle = new { width = 2, color = c.Ipotesi, type = "dashed" },
                itemStyle = new { color = c.Ipotesi, borderColor = c.Superficie, borderWidth = 2 },
                data = simulati,
                markLine = sottoZero
                    ? new
                    {
                        silent = true, symbol = "none",
                        lineStyle = new { color = c.Uscite, width = 1 },
                        label = new { formatter = "zero", color = c.Uscite },
                        data = new object[] { new { yAxis = 0 } }
                    }
                    : null
            }
        };
        if (conInvestimenti.Count > 0)
            serie.Add(new
            {
                name = "Conto + versato negli investimenti", type = "line", symbol = "circle", symbolSize = 6,
                lineStyle = new { width = 2, color = c.Neutro, type = "dotted" },
                itemStyle = new { color = c.Neutro, borderColor = c.Superficie, borderWidth = 2 },
                data = conInvestimenti
            });

        return new
        {
            textStyle = Testo(c),
            tooltip = new
            {
                trigger = "axis",
                backgroundColor = c.SfondoTooltip,
                borderColor = c.Griglia,
                textStyle = new { color = c.Testo },
                formatter = "@tooltipTempo"
            },
            legend = Legenda(c),
            grid = Griglia(),
            xAxis = new
            {
                type = "time",
                axisLine = new { lineStyle = new { color = c.Griglia } },
                axisLabel = new { color = c.TestoSecondario, formatter = "@meseDaTempo" },
                splitLine = new { show = false }
            },
            yAxis = AsseImporti(c, scala: !sottoZero),
            series = serie
        };
    }

    /// <summary>Barre orizzontali dal gruppo più grande, con l'importo in fondo alla barra.
    /// Un solo colore: il gruppo si legge sull'asse, non serve distinguerlo con le tinte.
    /// Il tooltip dice quanti movimenti ci sono e, per "Altro", quali gruppi raccoglie.</summary>
    public static object PerGruppo(IReadOnlyList<TotaleGruppo> gruppi, string colore, string nomeSerie, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = new
        {
            trigger = "item",
            backgroundColor = c.SfondoTooltip,
            borderColor = c.Griglia,
            textStyle = new { color = c.Testo },
            formatter = "@tooltipGruppo"
        },
        grid = new { left = 8, right = 96, top = 8, bottom = 8, containLabel = true },
        xAxis = new { type = "value", show = false },
        yAxis = new
        {
            type = "category",
            inverse = true,     // il gruppo più grande in alto
            data = gruppi.Select(g => g.Gruppo),
            axisLine = new { show = false },
            axisTick = new { show = false },
            axisLabel = new { color = c.Testo }
        },
        series = new object[]
        {
            new
            {
                name = nomeSerie, type = "bar", barMaxWidth = 18,
                cursor = "pointer",     // cliccando una barra si apre il dettaglio del gruppo
                itemStyle = new { color = colore, borderRadius = new[] { 0, 4, 4, 0 } },
                label = new { show = true, position = "right", color = c.TestoSecondario, formatter = "@euroEtichetta" },
                // oggetti invece di numeri: il valore più i dati che servono al tooltip
                data = gruppi.Select(g => new { value = Arrotonda(g.Totale), movimenti = g.Movimenti, contiene = g.Contiene })
            }
        }
    };

    public static object RicorrentiVariabili(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = Tooltip(c, "axis"),
        legend = Legenda(c),
        grid = Griglia(),
        xAxis = AsseCategorie(c, mesi.Select(m => Mese(m.Mese))),
        yAxis = AsseImporti(c),
        series = new object[]
        {
            new
            {
                name = "Ricorrenti", type = "bar", stack = "uscite", barMaxWidth = 28, cursor = "pointer",
                // bordo del colore della superficie: lo stacco di 2 px tra i due segmenti
                itemStyle = new { color = c.Categorie[0], borderColor = c.Superficie, borderWidth = 1 },
                data = mesi.Select(m => Arrotonda(m.UsciteRicorrenti))
            },
            new
            {
                name = "Variabili", type = "bar", stack = "uscite", barMaxWidth = 28, cursor = "pointer",
                itemStyle = new { color = c.Categorie[1], borderColor = c.Superficie, borderWidth = 1, borderRadius = new[] { 4, 4, 0, 0 } },
                data = mesi.Select(m => Arrotonda(m.UsciteVariabili))
            }
        }
    };

    /// <summary>L'anno scelto nel colore principale, il precedente in grigio tratteggiato.</summary>
    public static object Cumulati(IReadOnlyList<CumulatoAnno> anni, ColoriGrafici c) => new
    {
        textStyle = Testo(c),
        tooltip = Tooltip(c, "axis"),
        legend = Legenda(c),
        grid = Griglia(),
        xAxis = AsseCategorie(c, NomiMesi),
        yAxis = AsseImporti(c),
        series = anni.Select((a, i) =>
        {
            var corrente = i == anni.Count - 1;
            var colore = corrente ? c.Principale : c.Neutro;
            return (object)new
            {
                name = a.Anno.ToString(Italiano), type = "line", symbol = "circle", symbolSize = 8,
                lineStyle = new { width = 2, color = colore, type = corrente ? "solid" : "dashed" },
                itemStyle = new { color = colore, borderColor = c.Superficie, borderWidth = 2 },
                data = a.Mesi.Select(v => v is { } valore ? Arrotonda(valore) : (double?)null)
            };
        }).ToArray()
    };
}
