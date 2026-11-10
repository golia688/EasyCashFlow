// Trascinamento delle colonne del file Excel nelle caselle del modello (DialogoAbbinamentoExcel, dal 10/11).
// Non usa il trascinamento nativo del browser (draggable, dragstart, drop): dentro WebView2 in una finestra WPF
// passa da Windows (OLE) e il rilascio non arrivava mai, né con gli eventi di Blazor né con quelli della pagina.
// Qui il trascinamento è fatto con gli eventi del puntatore: si preme su una colonna, la si muove (una copia
// segue il cursore e la casella sotto si evidenzia), si rilascia su una casella; alla fine si avvisa la
// finestra (.NET) con la colonna e la casella. Le colonne hanno data-colonna, le caselle data-campo.
window.trascinaColonne = (function () {
    const SOGLIA = 5;          // pixel di movimento prima che una pressione diventi un trascinamento
    let finestra = null;       // DotNetObjectReference della finestra aperta
    let premuta = null;        // { colonna, x, y, elemento } dalla pressione sulla colonna
    let copia = null;          // la copia della colonna che segue il cursore
    let evidenziata = null;    // la casella sotto il cursore
    let ignoraClic = false;    // il clic che segue un trascinamento non deve selezionare la colonna

    function casellaSotto(x, y) {
        const el = document.elementFromPoint(x, y);
        return el && el.closest ? el.closest('[data-campo]') : null;
    }

    function evidenzia(casella) {
        if (evidenziata === casella) return;
        if (evidenziata) evidenziata.classList.remove('sopra');
        evidenziata = casella;
        if (evidenziata) evidenziata.classList.add('sopra');
    }

    function fine() {
        if (copia) copia.remove();
        copia = null;
        evidenzia(null);
        premuta = null;
        document.body.classList.remove('trascinamento-colonna');
    }

    document.addEventListener('pointerdown', e => {
        if (!finestra || e.button !== 0) return;
        const el = e.target && e.target.closest ? e.target.closest('[data-colonna]') : null;
        if (!el) return;
        premuta = { colonna: parseInt(el.dataset.colonna, 10), x: e.clientX, y: e.clientY, elemento: el };
    }, true);

    document.addEventListener('pointermove', e => {
        if (!premuta) return;
        if (!copia) {
            if (Math.abs(e.clientX - premuta.x) < SOGLIA && Math.abs(e.clientY - premuta.y) < SOGLIA) return;
            // comincia il trascinamento: una copia della colonna, che non intercetta il puntatore
            const r = premuta.elemento.getBoundingClientRect();
            copia = premuta.elemento.cloneNode(true);
            copia.removeAttribute('data-colonna');
            copia.classList.add('copia-trascinata');
            copia.style.width = r.width + 'px';
            document.body.appendChild(copia);
            document.body.classList.add('trascinamento-colonna');
        }
        e.preventDefault();
        copia.style.left = (e.clientX + 12) + 'px';
        copia.style.top = (e.clientY + 8) + 'px';
        evidenzia(casellaSotto(e.clientX, e.clientY));
    }, true);

    document.addEventListener('pointerup', e => {
        if (!premuta) return;
        if (copia) {
            const casella = casellaSotto(e.clientX, e.clientY);
            const colonna = premuta.colonna;
            ignoraClic = true;
            setTimeout(() => { ignoraClic = false; }, 0);
            fine();
            if (casella && finestra) finestra.invokeMethodAsync('LasciaColonna', colonna, casella.dataset.campo);
        } else {
            premuta = null;     // solo un clic: lo gestisce la finestra (seleziona la colonna)
        }
    }, true);

    document.addEventListener('pointercancel', fine, true);

    document.addEventListener('click', e => {
        if (!ignoraClic) return;
        e.stopPropagation();
        e.preventDefault();
        ignoraClic = false;
    }, true);

    return {
        registra: riferimento => { finestra = riferimento; },
        togli: () => { finestra = null; fine(); }
    };
})();
