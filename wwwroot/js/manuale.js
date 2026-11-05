// Collegamenti del manuale. Il testo è HTML prodotto da Markdig, quindi i clic sui link non passano
// da Blazor: li intercetta questo script e li gira alla pagina (PaginaManuale.Collegamento).
// "cap:id" apre un capitolo, "app:pagina" una pagina del programma. Nessun link deve far navigare
// la WebView altrove, quindi il comportamento normale del browser è sempre bloccato.
window.manuale = {
    collega(elemento, pagina) {
        elemento.addEventListener("click", e => {
            const link = e.target.closest("a");
            if (!link) return;
            e.preventDefault();
            const destinazione = link.getAttribute("href") ?? "";
            if (destinazione.startsWith("cap:") || destinazione.startsWith("app:"))
                pagina.invokeMethodAsync("Collegamento", destinazione);
        });
    },

    // cambiando capitolo si riparte dall'inizio del testo
    inCima() {
        window.scrollTo({ top: 0 });
    }
};
