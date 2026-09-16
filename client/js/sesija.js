const KLJUC_SESIJE = 'teretana.sesija';

const slusaoci = new Set();
let sesija = ucitajSacuvanu();

export function trenutniKorisnik() {
  return vazecaSesija()?.korisnik ?? null;
}

export function token() {
  return vazecaSesija()?.token ?? null;
}

/** Počinje sesiju iz odgovora POST /api/auth/prijava. */
export function zapocni({ token: noviToken, istice, korisnik }) {
  sesija = { token: noviToken, istice, korisnik };
  sacuvaj();
  obavesti(null);
}

export function azurirajKorisnika(korisnik) {
  if (sesija === null) {
    return;
  }

  sesija = { ...sesija, korisnik };
  sacuvaj();
  obavesti(null);
}

/** @param {'odjava' | 'sesija-istekla' | 'nalog-obrisan'} razlog */
export function zavrsi(razlog) {
  if (sesija === null) {
    return;
  }

  sesija = null;
  sacuvaj();
  obavesti(razlog);
}

export function naPromenuSesije(slusalac) {
  slusaoci.add(slusalac);
}

function vazecaSesija() {
  if (sesija !== null && new Date(sesija.istice) <= new Date()) {
    sesija = null;
    sacuvaj();
  }

  return sesija;
}

function obavesti(razlog) {
  for (const slusalac of slusaoci) {
    slusalac(razlog);
  }
}

function ucitajSacuvanu() {
  try {
    return JSON.parse(localStorage.getItem(KLJUC_SESIJE));
  } catch {
    // Nedostupan ili oštećen localStorage znači da korisnik mora ponovo da se prijavi.
    return null;
  }
}

function sacuvaj() {
  try {
    if (sesija === null) {
      localStorage.removeItem(KLJUC_SESIJE);
    } else {
      localStorage.setItem(KLJUC_SESIJE, JSON.stringify(sesija));
    }
  } catch {
    // Bez localStorage-a sesija i dalje radi u memoriji, samo ne preživljava osvežavanje stranice.
  }
}
