using System.Security.Cryptography;

namespace EasyCashFlow.Services;

/// <summary>
/// Aiuti per scegliere la password (docs/07-sicurezza.md, sezione 3): una frase di parole a caso,
/// facile da ricordare e molto più robusta di una parola con numeri e simboli, e una valutazione
/// semplice della robustezza da mostrare sotto il campo.
/// </summary>
public static class ConsigliPassword
{
    public enum Livello { TroppoCorta, Debole, Discreta, Buona, Ottima }

    // parole comuni, senza accenti né doppie difficili: 256 parole, 8 bit ciascuna; 5 parole sono 40 bit,
    // che con Argon2 a mezzo secondo per tentativo vogliono dire migliaia di anni di prove
    private static readonly string[] Parole =
    [
        "acqua", "aereo", "albero", "alba", "amaca", "amico", "ancora", "anello", "angolo", "ape",
        "arancia", "arco", "argento", "armadio", "asino", "autunno", "balcone", "balena", "banco", "barca",
        "basilico", "battello", "becco", "bicchiere", "binario", "biscotto", "bosco", "bottone", "brezza", "bruco",
        "bussola", "cactus", "caffe", "calza", "camino", "campana", "candela", "canguro", "cantina", "capanna",
        "cappello", "carota", "carretto", "cartolina", "castagna", "castello", "cavallo", "cavolo", "cesto", "cielo",
        "ciliegia", "cipolla", "collina", "colomba", "coniglio", "conchiglia", "corda", "cortile", "cuscino", "delfino",
        "divano", "duna", "edera", "elefante", "elica", "fagiolo", "falco", "farfalla", "faro", "fata",
        "fenice", "ferro", "fiume", "foglia", "fontana", "forchetta", "formica", "fragola", "freccia", "fulmine",
        "fungo", "gabbiano", "galassia", "gelato", "geranio", "ghianda", "giardino", "giraffa", "girasole", "gomitolo",
        "gondola", "grano", "grillo", "grotta", "gufo", "imbuto", "inverno", "isola", "lampada", "lampione",
        "lanterna", "lattuga", "lavagna", "leone", "libro", "limone", "lucciola", "luna", "lupo", "magnete",
        "mandorla", "mantello", "mappa", "mare", "margherita", "martello", "matita", "melograno", "mercato", "miele",
        "mirtillo", "molla", "montagna", "mulino", "muschio", "nave", "nebbia", "neve", "nido", "nocciola",
        "nodo", "nuvola", "oca", "ombrello", "onda", "orologio", "orso", "ortica", "pagina", "paglia",
        "palazzo", "palla", "panchina", "pantofola", "papavero", "passero", "pavone", "pentola", "pepe", "pera",
        "pesca", "pettine", "piano", "pianta", "pigna", "pinguino", "pioggia", "pirata", "piuma", "polenta",
        "pollo", "ponte", "porta", "prato", "primavera", "prugna", "pulcino", "quaderno", "quercia", "radice",
        "ragno", "rana", "rastrello", "razzo", "riccio", "rondine", "rosa", "rubinetto", "ruota", "sabbia",
        "salice", "salvia", "sapone", "sasso", "scala", "scarpa", "scatola", "scoglio", "secchio", "sedia",
        "sentiero", "serpente", "sirena", "slitta", "sole", "sorriso", "spazzola", "specchio", "spiga", "stella",
        "stivale", "strada", "tamburo", "tappeto", "tartaruga", "tazza", "teiera", "tenda", "terrazza", "tigre",
        "timone", "topo", "torre", "tramonto", "treno", "trottola", "tulipano", "uccello", "uliva", "uovo",
        "uva", "valigia", "vaso", "vela", "ventaglio", "vento", "vespa", "vetro", "viola", "violino",
        "vite", "volpe", "vulcano", "zaino", "zampa", "zebra", "zucca", "zucchero", "abete", "acero",
        "airone", "alloro", "anatra", "aquilone", "ariete", "astuccio", "avena", "bambola", "barattolo", "biglia",
        "borraccia", "bruma", "cammello", "carciofo", "cascata", "cicogna"
    ];

    // le più usate in Italia e nel mondo, anche allungate: una password così si indovina subito
    private static readonly string[] Comuni =
    [
        "password", "passw0rd", "1234567890", "123456789", "qwertyuiop", "qwertyuiopasdf", "abcdefghij",
        "iloveyou", "ciaociao", "amoremio", "tiamo", "juventus", "forzainter", "forzamilan", "napoli",
        "0000000000", "1111111111", "1234512345", "password123", "passwordpassword"
    ];

    /// <summary>Una frase di 5 parole a caso, separate da spazi.</summary>
    public static string SuggerisciFrase() =>
        string.Join(" ", Enumerable.Range(0, 5).Select(_ => Parole[RandomNumberGenerator.GetInt32(Parole.Length)]));

    public static Livello Valuta(string? password)
    {
        var p = (password ?? "").Trim();
        if (p.Length < ChiaviService.LunghezzaMinimaPassword) return Livello.TroppoCorta;

        var compatta = new string(p.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        // una password comune, anche con qualche cifra in fondo ("juventus2024"), o fatta di pochi caratteri ripetuti
        var senzaCifreFinali = compatta.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (Comuni.Contains(compatta) || Comuni.Contains(senzaCifreFinali) || compatta.Distinct().Count() <= 3)
            return Livello.Debole;

        var parole = p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var tipi = new[] { p.Any(char.IsLower), p.Any(char.IsUpper), p.Any(char.IsDigit), p.Any(c => !char.IsLetterOrDigit(c) && c != ' ') }
            .Count(x => x);

        if (p.Length >= 24 || (parole >= 4 && p.Length >= 20)) return Livello.Ottima;
        if (p.Length >= 16 || (p.Length >= 12 && tipi >= 3)) return Livello.Buona;
        return Livello.Discreta;
    }

    public static string Testo(Livello livello) => livello switch
    {
        Livello.TroppoCorta => $"Troppo corta: servono almeno {ChiaviService.LunghezzaMinimaPassword} caratteri",
        Livello.Debole => "Debole: è una password molto comune o ripetitiva",
        Livello.Discreta => "Discreta: più lunga sarebbe meglio",
        Livello.Buona => "Buona",
        _ => "Ottima"
    };
}
