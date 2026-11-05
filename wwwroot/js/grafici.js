// Ponte tra Blazor ed ECharts. Blazor passa le opzioni del grafico come JSON, che non può contenere
// funzioni: le stringhe che iniziano con "@" e sono tra i formattatori qui sotto vengono sostituite
// dalla funzione corrispondente prima di disegnare.
window.grafici = (() => {
    const istanze = new Map();      // elemento -> { grafico, osservatore }

    // useGrouping "always": in italiano Intl non mette il separatore delle migliaia sotto 10.000
    // ("2788,00"), mentre il resto dell'applicazione scrive "2.788,00"
    const euro = new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR", useGrouping: "always" });
    const numeroBreve = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 1, useGrouping: "always" });

    // i nomi dei gruppi li scrive l'utente: nel tooltip, che è HTML, vanno resi innocui
    const html = testo => String(testo).replace(/[&<>"']/g, c => `&#${c.charCodeAt(0)};`);
    const mesi = ["gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic"];

    // importi nascosti (l'occhio accanto al "?"): i formattatori degli importi scrivono •••• e restano solo
    // colonne e linee. Lo imposta Main.razor con impostaNascosti, che ridisegna i grafici aperti.
    let nascosti = false;
    const coperto = "••••";
    const importo = v => nascosti ? coperto : euro.format(v);

    const formattatori = {
        // importo completo, per i tooltip (valueFormatter riceve il valore)
        "@euro": v => v == null ? "–" : importo(v),
        // importo completo, per le etichette delle serie (label.formatter riceve un oggetto, non il valore)
        "@euroEtichetta": p => p.value == null ? "–" : importo(p.value),
        // tooltip delle barre per gruppo: nome, importo, numero di movimenti e, per "Altro", i gruppi raccolti
        "@tooltipGruppo": p => {
            const d = p.data ?? {};
            let testo = `${html(p.name)}<br><b>${importo(p.value)}</b> · ${d.movimenti} ${d.movimenti === 1 ? "movimento" : "movimenti"}`;
            if (d.contiene?.length) testo += `<br>Comprende: ${d.contiene.map(html).join(", ")}`;
            return testo + `<br><i>Clicca per vedere i movimenti</i>`;
        },
        // asse dei tempi (valori in millisecondi): mese e anno, "nov 26"
        "@meseDaTempo": v => {
            const d = new Date(v);
            return `${mesi[d.getMonth()]} ${String(d.getFullYear()).slice(2)}`;
        },
        // tooltip dei grafici con asse dei tempi e dati [ "aaaa-mm-gg", valore ]: data e una riga per serie
        "@tooltipTempo": punti => {
            const [aaaa, mm, gg] = String(punti[0].value[0]).split("-");
            const righe = punti.map(p => `${p.marker}${html(p.seriesName)}: <b>${importo(p.value[1])}</b>`);
            return [`${gg}/${mm}/${aaaa}`, ...righe].join("<br>");
        },
        // importo compatto per gli assi: "1,2M €", "3,5k €", "800 €"
        "@euroAsse": v => {
            if (nascosti) return coperto;
            const a = Math.abs(v);
            if (a >= 1e6) return `${numeroBreve.format(v / 1e6)}M €`;
            if (a >= 1e3) return `${numeroBreve.format(v / 1e3)}k €`;
            return `${numeroBreve.format(v)} €`;
        },
        // asse con date "gg/mm/aaaa": mostra solo mese e anno, "ott 25"
        "@meseDaData": v => {
            const [, mm, aaaa] = String(v).split("/");
            return mm ? `${mesi[Number(mm) - 1]} ${aaaa.slice(2)}` : v;
        }
    };

    function sostituisci(valore) {
        if (typeof valore === "string") return formattatori[valore] ?? valore;
        if (Array.isArray(valore)) return valore.map(sostituisci);
        if (valore && typeof valore === "object") {
            const copia = {};
            for (const [chiave, v] of Object.entries(valore)) copia[chiave] = sostituisci(v);
            return copia;
        }
        return valore;
    }

    return {
        // componente: il Grafico.razor da avvisare ai clic sulle serie cliccabili (null se non ce ne sono)
        disegna(elemento, opzioni, componente) {
            let istanza = istanze.get(elemento);
            if (!istanza) {
                // SVG invece di canvas: resta nitido con lo zoom dell'interfaccia
                const grafico = echarts.init(elemento, null, { renderer: "svg" });
                const osservatore = new ResizeObserver(() => grafico.resize());
                osservatore.observe(elemento);
                istanza = { grafico, osservatore, componente: null };
                istanze.set(elemento, istanza);

                // un clic su un elemento di una serie con cursor "pointer" passa al componente Blazor il nome
                // della categoria (per le barre per gruppo, il nome del gruppo), il nome della serie e la
                // posizione dell'elemento nei dati (per le colonne per mese, quale mese)
                grafico.on("click", p => {
                    if (p.componentType === "series" && istanza.componente)
                        istanza.componente.invokeMethodAsync("Clic", String(p.name ?? ""), p.seriesName ?? "", p.dataIndex ?? -1);
                });
            }
            istanza.componente = componente ?? null;
            // ECharts mette la manina su barre, linee e punti, come se fossero cliccabili: di solito qui non
            // lo sono, quindi il cursore resta quello normale. La legenda tiene la manina: cliccandola si
            // nasconde o si mostra una serie. Le serie cliccabili indicano cursor: "pointer" nelle opzioni.
            const finali = sostituisci(opzioni);
            for (const serie of [].concat(finali.series ?? []))
                serie.cursor ??= "default";
            istanza.opzioni = finali;
            istanza.grafico.setOption(finali, { notMerge: true });
        },

        // nasconde o mostra gli importi e ridisegna i grafici aperti con le stesse opzioni
        impostaNascosti(valore) {
            if (nascosti === valore) return;
            nascosti = valore;
            for (const istanza of istanze.values())
                if (istanza.opzioni) istanza.grafico.setOption(istanza.opzioni, { notMerge: true });
        },

        distruggi(elemento) {
            const istanza = istanze.get(elemento);
            if (!istanza) return;
            istanza.osservatore.disconnect();
            istanza.grafico.dispose();
            istanze.delete(elemento);
        }
    };
})();
