// Blocco per inattività (BloccoService): segnala che l'utente sta usando la finestra. Mouse, tasti,
// rotellina e tocchi bastano; per non chiamare .NET a ogni movimento del mouse si segnala al massimo
// ogni 15 secondi, che è anche l'intervallo con cui BloccoService controlla.
window.attivita = {
    osserva(servizio) {
        let ultima = 0;
        const segnala = () => {
            const ora = Date.now();
            if (ora - ultima < 15000) return;
            ultima = ora;
            servizio.invokeMethodAsync("Attivita");
        };
        for (const evento of ["pointerdown", "pointermove", "keydown", "wheel", "touchstart"])
            document.addEventListener(evento, segnala, { capture: true, passive: true });
    },

    // Ctrl+H nasconde o mostra gli importi (Main.CommutaImporti), da qualunque pagina
    scorciatoiaImporti(main) {
        document.addEventListener("keydown", e => {
            if (e.ctrlKey && !e.altKey && !e.shiftKey && e.key.toLowerCase() === "h") {
                e.preventDefault();
                main.invokeMethodAsync("CommutaImporti");
            }
        });
    }
};
