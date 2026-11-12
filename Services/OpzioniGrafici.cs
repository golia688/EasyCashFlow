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

    // Rosso sotto lo zero (dal 12/11): se una linea scende sotto lo zero, la parte negativa della linea e
    // dell'area è nel colore delle uscite. Lo fa una visualMap a sezioni di ECharts sul valore (dimensione 1),
    // che taglia il colore esattamente sullo zero anche con le linee morbide e con l'ingrandimento. La linea e
    // l'area della serie non devono avere un colore proprio, che vincerebbe sulla visualMap: per questo l'area
    // diventa di un colore pieno e leggero invece che sfumata, e lo si fa solo quando la linea va sotto lo zero.
    // Le due sezioni hanno limiti enormi ma finiti: con una sezione aperta ("lt: 0" da solo) ECharts 5.6 va in
    // errore nel calcolo della sfumatura della linea ("reading 'coord'"), provato il 12/11 in una pagina a parte.
    private static object RossoSottoZero(int serie, string sopra, ColoriGrafici c) => new
    {
        type = "piecewise", show = false, seriesIndex = serie, dimension = 1,
        pieces = new object[]
        {
            new { gt = -LimiteSezioni, lt = 0, color = c.Uscite },
            new { gte = 0, lt = LimiteSezioni, color = sopra }
        }
    };

    private const double LimiteSezioni = 1e15;

    // opacità dell'area piena quando una linea va sotto lo zero: lo stile classico tiene la sua
    private const double AreaSottoZero = 0.28;

    /// <summary>Entrate sopra lo zero, uscite sotto, flusso netto come linea: la posizione distingue
    /// entrate e uscite anche senza il colore (verde e rosso si confondono per i daltonici).
    /// Nello stile moderno (dall'11/11) la struttura è la stessa: le colonne sono sfumate, piene verso
    /// l'estremità e tenui verso lo zero, con gli angoli più tondi, e la linea del flusso netto è morbida
    /// e nel colore principale.</summary>
    public static object EntrateUscitePerMese(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c,
                                              StileGrafici stile = StileGrafici.Classico)
    {
        var moderno = stile == StileGrafici.Moderno;
        object coloreEntrate = moderno ? Sfumatura(c.Entrate, 1, 0.35) : c.Entrate;
        object coloreUscite = moderno ? Sfumatura(c.Uscite, 0.35, 1) : c.Uscite;
        var raggio = moderno ? 7 : 4;
        var coloreNetto = moderno ? c.Principale : c.Testo;
        return new
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
                    itemStyle = new { color = coloreEntrate, borderRadius = new[] { raggio, raggio, 0, 0 } },
                    data = mesi.Select(m => Arrotonda(m.Entrate))
                },
                new
                {
                    name = "Uscite", type = "bar", stack = "flusso", barMaxWidth = 28, cursor = "pointer",
                    itemStyle = new { color = coloreUscite, borderRadius = new[] { 0, 0, raggio, raggio } },
                    data = mesi.Select(m => Arrotonda(-m.Uscite))
                },
                new
                {
                    name = "Flusso netto", type = "line", symbol = "circle", symbolSize = 8, cursor = "pointer",
                    smooth = moderno,
                    lineStyle = new { width = moderno ? 2.5 : 2, color = coloreNetto },
                    itemStyle = new { color = coloreNetto, borderColor = c.Superficie, borderWidth = 2 },
                    data = mesi.Select(m => Arrotonda(m.Netto))
                }
            }
        };
    }

    // sfumatura verticale di un colore "#RRGGBB": l'opacità in alto e quella in basso
    private static object Sfumatura(string hex, double sopra, double sotto) => new
    {
        type = "linear", x = 0, y = 0, x2 = 0, y2 = 1,
        colorStops = new object[]
        {
            new { offset = 0, color = Trasparente(hex, sopra) },
            new { offset = 1, color = Trasparente(hex, sotto) }
        }
    };

    /// <summary>L'andamento del saldo, nello stile scelto in Impostazioni > Aspetto (dall'11/11): classico, una
    /// linea con un'area leggera; moderno, un'area sfumata con la barra per ingrandire un tratto.</summary>
    public static object Saldo(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c, StileGrafici stile) =>
        stile == StileGrafici.Moderno ? SaldoModerno(punti, c) : SaldoClassico(punti, c);

    /// <summary>Uscite (o entrate) per gruppo, nello stile scelto: classico, barre orizzontali di un solo colore;
    /// moderno, grafico a rosa con i colori in scala. Dati, tooltip e clic sono gli stessi.</summary>
    public static object PerGruppo(IReadOnlyList<TotaleGruppo> gruppi, bool uscite, ColoriGrafici c, StileGrafici stile)
    {
        var nome = uscite ? "Uscite" : "Entrate";
        if (stile == StileGrafici.Moderno)
            return PerGruppoModerno(gruppi, uscite ? c.UsciteScuro : c.EntrateScuro, uscite ? c.UsciteChiaro : c.EntrateChiaro, nome, c);
        return PerGruppoClassico(gruppi, uscite ? c.Uscite : c.Entrate, nome, c);
    }

    // stile classico: una linea con un'area leggera sotto
    private static object SaldoClassico(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c)
    {
        var negativo = punti.Any(p => p.Saldo < 0);
        return new
        {
            textStyle = Testo(c),
            tooltip = Tooltip(c, "axis"),
            grid = Griglia(16),
            xAxis = AsseCategorie(c, punti.Select(p => p.Data.ToString("dd/MM/yyyy", Italiano)),
                new { color = c.TestoSecondario, formatter = "@meseDaData" }),
            yAxis = AsseImporti(c, scala: true),
            visualMap = negativo ? new[] { RossoSottoZero(0, c.Principale, c) } : [],
            series = new object[]
            {
                new
                {
                    name = "Saldo", type = "line", showSymbol = false, symbolSize = 8,
                    lineStyle = negativo ? new { width = 2 } : (object)new { width = 2, color = c.Principale },
                    itemStyle = new { color = c.Principale },
                    areaStyle = negativo ? new { opacity = 0.12 } : (object)new { color = c.Principale, opacity = 0.12 },
                    data = punti.Select(p => Arrotonda(p.Saldo))
                }
            }
        };
    }

    /// <summary>Barre orizzontali dal gruppo più grande, con l'importo in fondo alla barra.
    /// Un solo colore: il gruppo si legge sull'asse, non serve distinguerlo con le tinte.
    /// Il tooltip dice quanti movimenti ci sono e, per "Altro", quali gruppi raccoglie.</summary>
    private static object PerGruppoClassico(IReadOnlyList<TotaleGruppo> gruppi, string colore, string nomeSerie, ColoriGrafici c) => new
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

    /// <summary>L'andamento del saldo come grafico ad area per tante giornate (dall'11/11, "large scale area"
    /// di ECharts): l'area sfuma dal colore principale verso il fondo, i punti si sfoltiscono senza perdere
    /// picchi e minimi (sampling "lttb"), e sotto c'è la barra per scegliere il tratto da guardare (anche con
    /// la rotellina del mouse sul grafico).</summary>
    private static object SaldoModerno(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c)
    {
        var negativo = punti.Any(p => p.Saldo < 0);
        return new
        {
            textStyle = Testo(c),
            visualMap = negativo ? new[] { RossoSottoZero(0, c.Principale, c) } : [],
            tooltip = Tooltip(c, "axis"),
            grid = new { left = 8, right = 16, top = 16, bottom = 48, containLabel = true },
            xAxis = new
            {
                type = "category",
                boundaryGap = false,
                data = punti.Select(p => p.Data.ToString("dd/MM/yyyy", Italiano)),
                axisLine = new { lineStyle = new { color = c.Griglia } },
                axisTick = new { show = false },
                axisLabel = new { color = c.TestoSecondario, formatter = "@meseDaData" }
            },
            yAxis = AsseImporti(c, scala: true),
            dataZoom = new object[]
            {
                new { type = "inside", start = 0, end = 100 },
                new
                {
                    type = "slider", start = 0, end = 100, height = 22, bottom = 8,
                    borderColor = c.Griglia,
                    fillerColor = Trasparente(c.Principale, 0.15),
                    handleStyle = new { color = c.Principale, borderColor = c.Principale },
                    moveHandleStyle = new { color = c.Principale },
                    dataBackground = new
                    {
                        lineStyle = new { color = c.TestoSecondario, opacity = 0.5 },
                        areaStyle = new { color = c.TestoSecondario, opacity = 0.15 }
                    },
                    selectedDataBackground = new
                    {
                        lineStyle = new { color = c.Principale },
                        areaStyle = new { color = c.Principale, opacity = 0.25 }
                    },
                    textStyle = new { color = c.TestoSecondario }
                }
            },
            series = new object[]
            {
                new
                {
                    name = "Saldo", type = "line", symbol = "none", sampling = "lttb", smooth = false,
                    lineStyle = negativo ? new { width = 1.5 } : (object)new { width = 1.5, color = c.Principale },
                    itemStyle = new { color = c.Principale },
                    // sfumatura verticale: piena sotto la linea, trasparente in fondo (piena e leggera se va sotto zero)
                    areaStyle = negativo ? new { opacity = AreaSottoZero } : (object)new { color = Sfumatura(c.Principale, 0.55, 0.03) },
                    data = punti.Select(p => Arrotonda(p.Saldo))
                }
            }
        };
    }

    // "#RRGGBB" -> "rgba(r,g,b,a)"
    private static string Trasparente(string hex, double opacita) =>
        $"rgba({Convert.ToInt32(hex[1..3], 16)},{Convert.ToInt32(hex[3..5], 16)},{Convert.ToInt32(hex[5..7], 16)},{opacita.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>Il saldo come sola linea, senza assi né griglia, per la Home: conta l'andamento,
    /// i valori si leggono nel tooltip.</summary>
    /// Nello stile moderno l'area è sfumata, piena sotto la linea e trasparente in fondo.
    public static object SaldoMini(IReadOnlyList<PuntoSaldo> punti, ColoriGrafici c, StileGrafici stile = StileGrafici.Classico)
    {
        var moderno = stile == StileGrafici.Moderno;
        var negativo = punti.Any(p => p.Saldo < 0);
        object area = negativo ? new { opacity = moderno ? AreaSottoZero : 0.12 }
            : moderno ? new { color = Sfumatura(c.Principale, 0.55, 0.03) } : new { color = c.Principale, opacity = 0.12 };
        return new
        {
            textStyle = Testo(c),
            tooltip = Tooltip(c, "axis"),
            grid = new { left = 2, right = 2, top = 6, bottom = 2 },
            xAxis = new { type = "category", show = false, boundaryGap = false, data = punti.Select(p => p.Data.ToString("dd/MM/yyyy", Italiano)) },
            yAxis = new { type = "value", show = false, scale = true },
            visualMap = negativo ? new[] { RossoSottoZero(0, c.Principale, c) } : [],
            series = new object[]
            {
                new
                {
                    name = "Saldo", type = "line", showSymbol = false, symbolSize = 8,
                    lineStyle = negativo ? new { width = moderno ? 2.5 : 2 } : (object)new { width = moderno ? 2.5 : 2, color = c.Principale },
                    itemStyle = new { color = c.Principale },
                    areaStyle = area,
                    data = punti.Select(p => Arrotonda(p.Saldo))
                }
            }
        };
    }

    /// <summary>Simulazione: saldo reale (linea piena con area) e, di seguito, saldo simulato a fine mese
    /// (tratteggiato, stesso colore), su un asse dei tempi vero: i punti reali sono giornalieri, quelli
    /// simulati mensili, e un asse a categorie li distanzierebbe in modo sbagliato. La linea simulata
    /// parte dall'ultimo saldo reale, così le due si toccano. Se il saldo simulato scende sotto zero,
    /// una linea rossa segna lo zero.</summary>
    ///
    /// Nello stile moderno (dall'11/11) il saldo reale ha l'area sfumata, il simulato un'area leggera nel
    /// suo colore e una linea morbida, e una linea verticale "oggi" separa il reale dal simulato.
    public static object Simulazione(IReadOnlyList<PuntoSaldo> storico, PuntoSaldo? inizio,
                                     IReadOnlyList<MeseSimulato> mesi, ColoriGrafici c,
                                     StileGrafici stile = StileGrafici.Classico)
    {
        var moderno = stile == StileGrafici.Moderno;
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

        // le linee di riferimento: lo zero, se il saldo simulato lo attraversa, e "oggi" nello stile moderno
        var riferimenti = new List<object>();
        if (sottoZero)
            riferimenti.Add(new
            {
                yAxis = 0,
                lineStyle = new { color = c.Uscite, width = 1, type = "solid" },
                label = new { formatter = "zero", color = c.Uscite }
            });
        if (moderno && inizio is not null)
            riferimenti.Add(new
            {
                xAxis = Data(inizio.Data),
                lineStyle = new { color = c.TestoSecondario, width = 1, type = "dotted" },
                label = new { formatter = "oggi", color = c.TestoSecondario }
            });

        // rosso sotto lo zero, per il saldo reale e per quello simulato (dal 12/11)
        var realeNegativo = storico.Any(p => p.Saldo < 0);
        var simulatoNegativo = sottoZero || inizio is { Saldo: < 0 };
        var sezioni = new List<object>();
        if (realeNegativo) sezioni.Add(RossoSottoZero(0, c.Principale, c));
        if (simulatoNegativo) sezioni.Add(RossoSottoZero(1, c.Ipotesi, c));

        object areaReale = realeNegativo ? new { opacity = moderno ? AreaSottoZero : 0.12 }
            : moderno ? new { color = Sfumatura(c.Principale, 0.55, 0.03) } : new { color = c.Principale, opacity = 0.12 };
        object? areaSimulata = !moderno ? null
            : simulatoNegativo ? new { opacity = AreaSottoZero * 0.6 } : new { color = Sfumatura(c.Ipotesi, 0.30, 0.02) };
        var serie = new List<object>
        {
            new
            {
                name = "Saldo reale", type = "line", showSymbol = false, symbolSize = 8,
                lineStyle = realeNegativo ? new { width = moderno ? 2.5 : 2 } : (object)new { width = moderno ? 2.5 : 2, color = c.Principale },
                itemStyle = new { color = c.Principale },
                areaStyle = areaReale,
                data = storico.Select(p => new object[] { Data(p.Data), Arrotonda(p.Saldo) })
            },
            new
            {
                name = "Saldo simulato", type = "line", symbol = "circle", symbolSize = 8,
                smooth = moderno,
                lineStyle = simulatoNegativo ? new { width = 2, type = "dashed" } : (object)new { width = 2, color = c.Ipotesi, type = "dashed" },
                itemStyle = new { color = c.Ipotesi, borderColor = c.Superficie, borderWidth = 2 },
                areaStyle = areaSimulata,
                data = simulati,
                markLine = riferimenti.Count > 0
                    ? new { silent = true, symbol = "none", data = riferimenti }
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
            visualMap = sezioni,
            series = serie
        };
    }

    /// <summary>Grafico a rosa (Nightingale, dall'11/11; prima barre orizzontali): un settore per gruppo, dal più
    /// grande, in senso orario. Tutti i settori hanno lo stesso angolo e la distanza dal centro dice l'importo
    /// (roseType "area"); le etichette fuori dicono nome, importo e percentuale. I colori vanno dallo scuro
    /// (il gruppo più grande) al chiaro (il più piccolo); "Altro" è grigio. Il tooltip dice quanti movimenti
    /// ci sono e, per "Altro", quali gruppi raccoglie; cliccando un settore si apre il dettaglio del gruppo.</summary>
    private static object PerGruppoModerno(IReadOnlyList<TotaleGruppo> gruppi, string scuro, string chiaro, string nomeSerie, ColoriGrafici c)
    {
        var scala = Scala(scuro, chiaro, gruppi.Count(g => g.Gruppo != "Altro"));
        var dati = new List<object>();
        var i = 0;
        foreach (var g in gruppi)
            dati.Add(new
            {
                name = g.Gruppo,
                value = Arrotonda(g.Totale),
                movimenti = g.Movimenti,
                contiene = g.Contiene,
                itemStyle = new { color = g.Gruppo == "Altro" ? c.Neutro : scala[i++] }
            });

        return new
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
            series = new object[]
            {
                new
                {
                    name = nomeSerie, type = "pie", roseType = "area",
                    radius = new[] { "16%", "68%" }, center = new[] { "50%", "50%" }, startAngle = 90,
                    cursor = "pointer",     // cliccando un settore si apre il dettaglio del gruppo
                    // lo stacco tra i settori ha il colore della superficie
                    itemStyle = new { borderRadius = 6, borderColor = c.Superficie, borderWidth = 2 },
                    label = new { show = true, color = c.Testo, formatter = "@etichettaGruppo", lineHeight = 16 },
                    labelLine = new { show = true, length = 10, length2 = 14, lineStyle = new { color = c.TestoSecondario } },
                    labelLayout = new { hideOverlap = true },
                    emphasis = new { scale = true, scaleSize = 6 },
                    data = dati
                }
            }
        };
    }

    // n colori dallo scuro al chiaro, in parti uguali (uno solo: lo scuro)
    private static IReadOnlyList<string> Scala(string scuro, string chiaro, int n)
    {
        static (int R, int G, int B) Rgb(string hex) =>
            (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
        var (r1, g1, b1) = Rgb(scuro);
        var (r2, g2, b2) = Rgb(chiaro);
        return Enumerable.Range(0, Math.Max(n, 0)).Select(i =>
        {
            var t = n <= 1 ? 0.0 : (double)i / (n - 1);
            int Mescola(int a, int b) => (int)Math.Round(a + (b - a) * t);
            return $"#{Mescola(r1, r2):X2}{Mescola(g1, g2):X2}{Mescola(b1, b2):X2}";
        }).ToList();
    }

    /// <summary>Uscite ricorrenti e variabili per mese: colonne impilate nello stile classico, aree impilate e
    /// sfumate nello stile moderno (dall'11/11), con i punti dei mesi cliccabili come le colonne.</summary>
    public static object RicorrentiVariabili(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c, StileGrafici stile = StileGrafici.Classico) =>
        stile == StileGrafici.Moderno ? RicorrentiVariabiliModerno(mesi, c) : RicorrentiVariabiliClassico(mesi, c);

    private static object RicorrentiVariabiliModerno(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c)
    {
        object Area(string nome, string colore, IEnumerable<double> dati) => new
        {
            name = nome, type = "line", stack = "uscite", smooth = true, cursor = "pointer",
            symbol = "circle", symbolSize = 7,
            lineStyle = new { width = 2, color = colore },
            itemStyle = new { color = colore, borderColor = c.Superficie, borderWidth = 2 },
            areaStyle = new { color = Sfumatura(colore, 0.65, 0.12) },
            data = dati
        };
        return new
        {
            textStyle = Testo(c),
            tooltip = Tooltip(c, "axis"),
            legend = Legenda(c),
            grid = Griglia(),
            xAxis = AsseCategorie(c, mesi.Select(m => Mese(m.Mese))),
            yAxis = AsseImporti(c),
            series = new object[]
            {
                Area("Ricorrenti", c.Categorie[0], mesi.Select(m => Arrotonda(m.UsciteRicorrenti))),
                Area("Variabili", c.Categorie[1], mesi.Select(m => Arrotonda(m.UsciteVariabili)))
            }
        };
    }

    private static object RicorrentiVariabiliClassico(IReadOnlyList<ValoriMese> mesi, ColoriGrafici c) => new
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

    /// <summary>L'anno scelto nel colore principale, il precedente in grigio tratteggiato. Nello stile moderno
    /// (dall'11/11) le linee sono morbide e l'anno scelto ha sotto un'area sfumata.</summary>
    public static object Cumulati(IReadOnlyList<CumulatoAnno> anni, ColoriGrafici c, StileGrafici stile = StileGrafici.Classico)
    {
        var moderno = stile == StileGrafici.Moderno;
        // rosso sotto lo zero per l'anno scelto (dal 12/11); l'anno prima resta grigio
        var ultimo = anni.Count - 1;
        var negativo = ultimo >= 0 && anni[ultimo].Mesi.Any(v => v < 0);
        return new
        {
            textStyle = Testo(c),
            tooltip = Tooltip(c, "axis"),
            legend = Legenda(c),
            grid = Griglia(),
            xAxis = AsseCategorie(c, NomiMesi),
            yAxis = AsseImporti(c),
            visualMap = negativo ? new[] { RossoSottoZero(ultimo, c.Principale, c) } : [],
            series = anni.Select((a, i) =>
            {
                var corrente = i == ultimo;
                var colore = corrente ? c.Principale : c.Neutro;
                var spezzata = corrente && negativo;
                return (object)new
                {
                    name = a.Anno.ToString(Italiano), type = "line", symbol = "circle", symbolSize = moderno ? 6 : 8,
                    smooth = moderno,
                    lineStyle = spezzata
                        ? new { width = moderno ? 2.5 : 2, type = "solid" }
                        : (object)new { width = moderno && corrente ? 2.5 : 2, color = colore, type = corrente ? "solid" : "dashed" },
                    itemStyle = new { color = colore, borderColor = c.Superficie, borderWidth = 2 },
                    areaStyle = !(moderno && corrente) ? null
                        : spezzata ? new { opacity = AreaSottoZero } : (object)new { color = Sfumatura(c.Principale, 0.45, 0.03) },
                    data = a.Mesi.Select(v => v is { } valore ? Arrotonda(valore) : (double?)null)
                };
            }).ToArray()
        };
    }
}
