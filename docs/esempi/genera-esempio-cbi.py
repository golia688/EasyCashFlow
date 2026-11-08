"""Genera un export CBI "saldi e movimenti" con dati INVENTATI, per le schermate e per provare il programma.

Due conti di due banche immaginarie (ABI 99001 e 99002, che non esistono), dal 01/10/2025 al 04/11/2026,
una rendicontazione per ogni giorno lavorativo e conto (61, 62+63, 64, 65), come prevede CBI-RND-001.
Nomi, aziende, importi e riferimenti sono di fantasia. Il generatore è deterministico (seme fisso).

Con un secondo nome di file scrive anche gli stessi movimenti nella rendicontazione XML del CBI
(STRN-MO-001 v.00.01.02, versione giornaliera), come la esporta l'home banking: un blocco Stmt per ogni
giornata con movimenti, stessi riferimenti e stesse causali del file a 120 caratteri (dal 08/11/2026).

Uso: python genera-esempio-cbi.py esempio-cbi-due-conti.txt [esempio-cbi-due-conti.xml]
"""
import random
import sys
from datetime import date, timedelta

random.seed(20261106)


def campo(riga, pos, testo):
    for i, c in enumerate(testo):
        riga[pos - 1 + i] = c


def riga_vuota():
    return [" "] * 120


def importo(v):
    # 15 caratteri, virgola decimale, senza segno
    return f"{abs(v):015.2f}".replace(".", ",")


def gg(d):
    return d.strftime("%d%m%y")


def iban(cin, abi, cab, conto):
    # cifre di controllo vere, così il programma ricava l'IBAN (modulo 97)
    bban = cin + abi + cab + conto
    s = "".join(str(int(c, 36)) for c in (bban + "IT00"))
    return f"IT{98 - int(s) % 97:02d}"


CONTI = [
    dict(nome="casa", cin="X", abi="99001", cab="01600", conto="000000123456", saldo=8420.00),
    dict(nome="online", cin="Y", abi="99002", cab="01000", conto="000000654321", saldo=2150.00),
]
for c in CONTI:
    c["codice"] = c["abi"] + c["cab"] + c["conto"]
    c["iban4"] = iban(c["cin"], c["abi"], c["cab"], c["conto"])

INIZIO, FINE = date(2025, 10, 1), date(2026, 11, 4)
negozi = ["SUPERMERCATO DEL CORSO", "FARMACIA CENTRALE", "PANIFICIO LA SPIGA", "DISTRIBUTORE ROSSI", "LIBRERIA PAGINA 9",
          "BAR DELLA PIAZZA", "FERRAMENTA BIANCHI", "OTTICA LUCE", "PIZZERIA DA GINO", "MERCATO RIONALE"]
riferimento = [100000]


def nuovo_rif(segno):
    riferimento[0] += random.randint(3, 97)
    return f"{riferimento[0]:011d}{'0001' if segno < 0 else '0002'}"


def movimenti_del_giorno(c, d):
    """(causale, importo con segno, descrizione breve, righe 63) per un conto in un giorno."""
    m = []
    lavorativo_dopo = lambda n: d.day == n or (d.weekday() == 0 and d.day in (n + 1, n + 2))
    if c["nome"] == "casa":
        if lavorativo_dopo(27):
            m.append(("2700", 2300.00 + (380.00 if d.month == 12 else 0), "Emolumenti",
                      ["*ACME INFORMATICA SRL STIPENDIO " + d.strftime("%m-%Y")]))
        if lavorativo_dopo(5):
            m.append(("1510", -645.30, "Addebito rata mutuo", ["RATA MUTUO N. 0000123 SCADENZA " + d.strftime("%d/%m/%Y")]))
        if lavorativo_dopo(10):
            luce = round(random.uniform(48, 95) * (1.5 if d.month in (12, 1, 2) else 1), 2)
            m.append(("1129", -luce, "SDD Utenze-Servizi", ["ENERGIA CHIARA SPA FORNITURA LUCE E GAS"]))
        if lavorativo_dopo(18):
            m.append(("3122", -29.99, "SDD Core - Richiesta Incasso SEPA", ["TELEFONIA PIU SPA ABBONAMENTO CASA"]))
        if lavorativo_dopo(15):
            m.append(("4500", -round(random.uniform(260, 540), 2), "Utilizzo carta di credito", ["CARTA **** 1234 ESTRATTO CONTO MENSILE"]))
        if lavorativo_dopo(20):
            m.append(("3126", -150.00, "SDD Finanziario", ["FONDI FUTURO SGR PIANO DI ACCUMULO"]))
        if lavorativo_dopo(28):
            m.append(("3400", -200.00, "Giroconto", ["GIROCONTO VERSO CONTO ONLINE"]))
        if d.weekday() < 5 and random.random() < 0.55:
            m.append(("4310", -round(random.uniform(8, 85), 2), "Pagamento POS", ["POS " + random.choice(negozi)]))
        if d.weekday() == 4 and random.random() < 0.35:
            m.append(("0500", -100.00, "Prelievo Bancomat", ["PRELIEVO ATM FILIALE CENTRO"]))
        if d.day in (2, 3, 4) and d.month in (1, 4, 7, 10) and d.weekday() < 5 and not any(x[0] == "1905" for x in m):
            m.append(("1905", -8.55, "Imposta di bollo", ["@IMPOSTA DI BOLLO TRIMESTRALE"]))
        if random.random() < 0.025:
            m.append(("2611", -round(random.uniform(40, 380), 2), "Bonifico tramite Internet Banking",
                      ["*" + random.choice(["IDRAULICO VERDI", "ASSOCIAZIONE SPORTIVA", "SCUOLA DI MUSICA", "STUDIO DENTISTICO"]) + " SALDO FATTURA"]))
        if random.random() < 0.012:
            # un bonifico ricevuto con i 63 strutturati YYY e YY2, come quelli veri
            m.append(("4800", round(random.uniform(30, 250), 2), "Bonifico a vostro favore",
                      [("YYY", d.strftime("%d%m%Y"), "LUCA NERI", "MILANO"), ("YY2", "VIA DEI TIGLI 12 MILANO"),
                       "*RIMBORSO SPESE CONDIVISE"]))
        if d == date(2026, 6, 16):
            m.append(("2611", -3200.00, "Bonifico tramite Internet Banking", ["*CLIMA SERVICE SRL IMPIANTO DI CONDIZIONAMENTO"]))
    else:
        if lavorativo_dopo(28):
            m.append(("3400", 200.00, "Giroconto", ["GIROCONTO DA CONTO DI CASA"]))
        if lavorativo_dopo(12):
            m.append(("5011", -12.99, "Addebito diretto SDD", ["STREAMING PLUS ABBONAMENTO MENSILE"]))
        if d.weekday() < 5 and random.random() < 0.12:
            m.append(("4317", -round(random.uniform(15, 120), 2), "Acquisto online carta", ["E-COMMERCE " + random.choice(["LIBRI VOLANTI", "SPORT LAB", "CASA E GIARDINO"])]))
        if d.day <= 3 and d.month in (1, 4, 7, 10) and d.weekday() < 5 and not any(x[0] == "1801" for x in m):
            m.append(("1801", round(random.uniform(4, 9), 2), "Interessi creditori", ["@INTERESSI CREDITORI TRIMESTRE"]))
    return m


def riga63(prog, num, info):
    r = riga_vuota()
    campo(r, 2, "63")
    campo(r, 4, f"{prog:07d}")
    campo(r, 11, f"{num:03d}")
    if isinstance(info, tuple) and info[0] == "YYY":
        campo(r, 14, "YYY"); campo(r, 17, info[1]); campo(r, 41, info[2]); campo(r, 81, info[3])
    elif isinstance(info, tuple) and info[0] == "YY2":
        campo(r, 14, "YY2"); campo(r, 17, info[1])
    else:
        campo(r, 14, info[:107])
    return "".join(r)


righe = []
r = riga_vuota(); campo(r, 2, "RH"); campo(r, 4, "99001"); campo(r, 9, "ESEMP"); campo(r, 14, gg(FINE)); campo(r, 20, "ESEMPIO EASYCASHFLOW")
righe.append("".join(r))
prog = 0
giornate_xml = []      # per l'XML: (conto, giorno, saldo iniziale, saldo finale, movimenti)
giorno = INIZIO
gia_usati = set()
while giorno <= FINE:
    if giorno.weekday() < 5:
        for c in CONTI:
            prog += 1
            mov = movimenti_del_giorno(c, giorno)
            iniziale = c["saldo"]
            r = riga_vuota(); campo(r, 2, "61"); campo(r, 4, f"{prog:07d}"); campo(r, 29, "93001")
            campo(r, 52, c["cin"]); campo(r, 53, c["codice"]); campo(r, 75, "EUR"); campo(r, 78, gg(giorno))
            campo(r, 84, "C" if iniziale >= 0 else "D"); campo(r, 85, importo(iniziale)); campo(r, 100, c["iban4"])
            righe.append("".join(r))
            del_giorno = []
            for num, (causale, valore, breve, info) in enumerate(mov, start=1):
                valuta = giorno - timedelta(days=random.choice([0, 0, 0, 1, 2])) if causale in ("4310", "4317") else giorno
                rif = nuovo_rif(valore)
                r = riga_vuota(); campo(r, 2, "62"); campo(r, 4, f"{prog:07d}"); campo(r, 11, f"{num:03d}")
                campo(r, 14, gg(valuta)); campo(r, 20, gg(giorno)); campo(r, 26, "C" if valore > 0 else "D")
                campo(r, 27, importo(valore)); campo(r, 42, causale); campo(r, 62, rif); campo(r, 87, breve[:34])
                righe.append("".join(r))
                for parte in info:
                    righe.append(riga63(prog, num, parte))
                c["saldo"] = round(c["saldo"] + valore, 2)
                del_giorno.append((causale, valore, breve, info, valuta, rif))
            if del_giorno:
                giornate_xml.append((c, giorno, iniziale, c["saldo"], del_giorno))
            r = riga_vuota(); campo(r, 2, "64"); campo(r, 4, f"{prog:07d}"); campo(r, 11, "EUR"); campo(r, 14, gg(giorno))
            campo(r, 20, "C" if c["saldo"] >= 0 else "D"); campo(r, 21, importo(c["saldo"]))
            campo(r, 36, "C" if c["saldo"] >= 0 else "D"); campo(r, 37, importo(c["saldo"]))
            righe.append("".join(r))
            r = riga_vuota(); campo(r, 2, "65"); campo(r, 4, f"{prog:07d}")
            for k in range(5):
                futuro = giorno + timedelta(days=k + 1)
                campo(r, 11 + 22 * k, gg(futuro) + ("C" if c["saldo"] >= 0 else "D") + importo(c["saldo"]))
            righe.append("".join(r))
    giorno += timedelta(days=1)

r = riga_vuota(); campo(r, 2, "EF"); campo(r, 4, "99001"); campo(r, 9, "ESEMP"); campo(r, 14, gg(FINE)); campo(r, 20, "ESEMPIO EASYCASHFLOW")
campo(r, 46, f"{prog:07d}"); campo(r, 83, f"{len(righe) + 1:07d}")
righe.append("".join(r))

for x in righe:
    assert len(x) == 120, (len(x), x)
with open(sys.argv[1], "w", encoding="latin-1", newline="\r\n") as f:
    f.write("\n".join(righe) + "\n")
print(f"{len(righe)} righe, {prog} rendicontazioni; saldi finali: " + ", ".join(f"{c['nome']} {c['saldo']:.2f}" for c in CONTI))


def testo_xml(breve, info, giorno):
    """Il testo del movimento come nell'XML della banca: la descrizione breve seguita dai record 63; il
    bonifico ricevuto ha l'ordinante nelle "Info aggiuntive", come negli export veri."""
    liberi = [p for p in info if isinstance(p, str)]
    testo = " ".join([breve] + liberi)
    yyy = next((p for p in info if isinstance(p, tuple) and p[0] == "YYY"), None)
    yy2 = next((p for p in info if isinstance(p, tuple) and p[0] == "YY2"), None)
    if yyy:
        testo += (f" Info aggiuntive: Data ordine: {giorno:%d/%m/%Y} Localita ordinante: {yyy[3]}"
                  f" Ragione sociale ordinante: {yyy[2]}" + (f" Indirizzo ordinante: {yy2[1]}" if yy2 else ""))
    return testo.replace("&", "&amp;").replace("<", "&lt;")


def saldo_xml(tipo, valore, giorno):
    return (f"<ns5:Bal><ns5:Tp><ns5:CdOrPrtry><ns5:Cd>{tipo}</ns5:Cd></ns5:CdOrPrtry></ns5:Tp>"
            f"<ns5:Amt Ccy=\"EUR\">{abs(valore):.2f}</ns5:Amt><ns5:CdtDbtInd>{'CRDT' if valore >= 0 else 'DBIT'}</ns5:CdtDbtInd>"
            f"<ns5:Dt><ns5:Dt>{giorno.isoformat()}+01:00</ns5:Dt></ns5:Dt></ns5:Bal>")


if len(sys.argv) > 2:
    x = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
         '<ns4:CBIBdyBkToCstmrStmtReq xmlns:ns4="urn:CBI:xsd:CBIBdyBkToCstmrStmtReq.00.01.02" '
         'xmlns:ns5="urn:CBI:xsd:CBIDlyStmtReqLogMsg.00.01.02" xmlns:ns6="urn:CBI:xsd:CBIBkToCstmrStmtReqLogMsg.00.01.02">',
         '<ns4:PhyMsgInf><ns4:PhyMsgTpCd>INF-STMT-01</ns4:PhyMsgTpCd><ns4:NbOfLogMsg>1</ns4:NbOfLogMsg></ns4:PhyMsgInf>',
         '<ns4:CBIEnvelBkToCstmrStmtReqLogMsg><ns4:CBIBkToCstmrStmtReqLogMsg><ns6:CBIDlyStmtReqLogMsg>',
         f'<ns5:GrpHdr><ns5:MsgId>1</ns5:MsgId><ns5:CreDtTm>{FINE.isoformat()}T18:00:00.000+01:00</ns5:CreDtTm></ns5:GrpHdr>']
    for n, (c, g, iniziale, finale, movs) in enumerate(giornate_xml, start=1):
        iban_completo = c["iban4"] + c["cin"] + c["codice"]
        x.append(f'<ns5:Stmt><ns5:Id>{n}</ns5:Id><ns5:ElctrncSeqNb>{n}</ns5:ElctrncSeqNb>'
                 f'<ns5:CreDtTm>{FINE.isoformat()}T18:00:00.000+01:00</ns5:CreDtTm>'
                 f'<ns5:Acct><ns5:Id><ns5:IBAN>{iban_completo}</ns5:IBAN></ns5:Id><ns5:Tp><ns5:Cd>CACC</ns5:Cd></ns5:Tp>'
                 f'<ns5:Ccy>EUR</ns5:Ccy><ns5:Svcr><ns5:FinInstnId><ns5:ClrSysMmbId><ns5:MmbId>{c["abi"]}</ns5:MmbId>'
                 f'</ns5:ClrSysMmbId></ns5:FinInstnId></ns5:Svcr></ns5:Acct>')
        x.append(saldo_xml("CLBD", finale, g) + saldo_xml("OPBD", iniziale, g) + saldo_xml("CLAV", finale, g))
        somma = sum(abs(m[1]) for m in movs)
        netto = sum(m[1] for m in movs)
        x.append(f'<ns5:TxsSummry><ns5:TtlNtries><ns5:NbOfNtries>{len(movs)}</ns5:NbOfNtries><ns5:Sum>{somma:.2f}</ns5:Sum>'
                 f'<ns5:TtlNetNtryAmt>{abs(netto):.2f}</ns5:TtlNetNtryAmt><ns5:CdtDbtInd>{"CRDT" if netto >= 0 else "DBIT"}</ns5:CdtDbtInd>'
                 f'</ns5:TtlNtries></ns5:TxsSummry>')
        for k, (causale, valore, breve, info, valuta, rif) in enumerate(movs, start=1):
            x.append(f'<ns5:Ntry><ns5:NtryRef>{k}</ns5:NtryRef><ns5:Amt Ccy="EUR">{abs(valore):.2f}</ns5:Amt>'
                     f'<ns5:CdtDbtInd>{"CRDT" if valore > 0 else "DBIT"}</ns5:CdtDbtInd><ns5:Sts>BOOK</ns5:Sts>'
                     f'<ns5:BookgDt><ns5:Dt>{g.isoformat()}+01:00</ns5:Dt></ns5:BookgDt>'
                     f'<ns5:ValDt><ns5:Dt>{valuta.isoformat()}+01:00</ns5:Dt></ns5:ValDt>'
                     f'<ns5:AcctSvcrRef>{rif}</ns5:AcctSvcrRef>'
                     f'<ns5:BkTxCd><ns5:Prtry><ns5:Cd>{causale[:2]}//{causale[2:]}</ns5:Cd><ns5:Issr>CBI</ns5:Issr></ns5:Prtry></ns5:BkTxCd>'
                     f'<ns5:NtryDtls><ns5:TxDtls><ns5:AddtlTxInf>{testo_xml(breve[:34], info, g)}</ns5:AddtlTxInf>'
                     f'</ns5:TxDtls></ns5:NtryDtls></ns5:Ntry>')
        x.append('</ns5:Stmt>')
    x.append('</ns6:CBIDlyStmtReqLogMsg></ns4:CBIBkToCstmrStmtReqLogMsg></ns4:CBIEnvelBkToCstmrStmtReqLogMsg></ns4:CBIBdyBkToCstmrStmtReq>')
    with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(x) + "\n")
    print(f"XML: {len(giornate_xml)} giornate con movimenti, {sum(len(g[4]) for g in giornate_xml)} movimenti")
