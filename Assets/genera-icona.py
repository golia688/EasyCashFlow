"""Disegna l'icona di EasyCashFlow (proposta B: euro dentro due frecce circolari) e salva il .ico.

Si disegna a 1024 px e si riduce, così i bordi sono morbidi. Sotto i 32 px le frecce non si leggono:
per 16 e 24 px c'è una versione semplificata con il solo simbolo dell'euro, più grande.
Uso, dalla cartella del progetto: python Assets\genera-icona.py Assets
(crea Assets\easycashflow.ico, wwwroot\img\easycashflow.png e, nella cartella del progetto, anteprima-icona.png da controllare e poi cancellare)
"""
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

S = 1024
SFONDO = "#38746A"      # Salvia, tonalità del tema chiaro
FRECCE = "#F7F6F2"      # avorio della superficie chiara
EURO = "#F2B98F"        # Pesca, tonalità del tema scuro
FONT = r"C:\Windows\Fonts\seguisb.ttf"


def base():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.22), fill=SFONDO)
    return img, d


def euro(d, dimensione, colore, dy=0):
    font = ImageFont.truetype(FONT, dimensione)
    d.text((S / 2, S / 2 + dy), "€", font=font, fill=colore, anchor="mm")


def punta(d, x, y, direzione, lato):
    # triangolo con la punta nella direzione del moto (angolo in gradi, 0 = destra, 90 = giù)
    a = math.radians(direzione)
    px, py = x + math.cos(a) * lato * 0.55, y + math.sin(a) * lato * 0.55
    b1 = (x + math.cos(a + 2.3) * lato * 0.55, y + math.sin(a + 2.3) * lato * 0.55)
    b2 = (x + math.cos(a - 2.3) * lato * 0.55, y + math.sin(a - 2.3) * lato * 0.55)
    d.polygon([(px, py), b1, b2], fill=FRECCE)


def completa():
    img, d = base()
    c, r, w = S / 2, S * 0.34, int(S * 0.09)
    box = (c - r, c - r, c + r, c + r)
    # due archi in senso orario: dall'alto a destra e dal basso a sinistra, lasciando spazio alle punte
    d.arc(box, start=270, end=345, fill=FRECCE, width=w)
    d.arc(box, start=90, end=165, fill=FRECCE, width=w)
    # punte alla fine degli archi, nel verso del moto (tangente al cerchio)
    for fine in (345, 165):
        a = math.radians(fine)
        x, y = c + math.cos(a) * (r - w / 2), c + math.sin(a) * (r - w / 2)
        punta(d, x, y, fine + 90, S * 0.23)
    euro(d, int(S * 0.5), EURO, dy=-S * 0.015)
    return img


def semplice():
    img, d = base()
    euro(d, int(S * 0.78), FRECCE, dy=-S * 0.02)
    return img


def main(cartella):
    cartella = Path(cartella)
    cartella.mkdir(parents=True, exist_ok=True)
    grande, piccola = completa(), semplice()

    dimensioni = [256, 128, 64, 48, 32, 24, 16]
    immagini = [(piccola if n < 32 else grande).resize((n, n), Image.LANCZOS) for n in dimensioni]
    immagini[0].save(cartella / "easycashflow.ico", format="ICO",
                     sizes=[(n, n) for n in dimensioni], append_images=immagini[1:])

    # la stessa icona in PNG per l'interfaccia (finestra Informazioni): la WebView non usa il .ico
    png = cartella.parent / "wwwroot" / "img" / "easycashflow.png"
    png.parent.mkdir(parents=True, exist_ok=True)
    grande.resize((256, 256), Image.LANCZOS).save(png)

    # e per la schermata di avvio nativa (MainWindow.xaml): dal .ico WPF prenderebbe un'immagine piccola
    grande.resize((256, 256), Image.LANCZOS).save(cartella / "easycashflow.png")

    # anteprima per controllare il risultato: tutte le dimensioni affiancate
    anteprima = Image.new("RGBA", (sum(dimensioni) + 20 * len(dimensioni), 276), (55, 55, 64, 255))
    x = 10
    for im in immagini:
        anteprima.paste(im, (x, 10 + (256 - im.width) // 2), im)
        x += im.width + 20
    anteprima.save(cartella.parent / "anteprima-icona.png")


if __name__ == "__main__":
    main(sys.argv[1])
