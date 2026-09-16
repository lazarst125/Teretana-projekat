const rute = [];

/**
 * Registruje ekran za putanju. Delovi oblika :naziv prihvataju samo cele brojeve.
 * @param {{ prijava?: boolean, uloga?: string, samoAnonimno?: boolean }} pravila
 */
export function dodajRutu(sablon, prikazi, pravila = {}) {
  const nazivi = [];
  const izraz = new RegExp(`^${sablon.replace(/:(\w+)/g, (_, naziv) => {
    nazivi.push(naziv);
    return '(\\d+)';
  })}$`);
  rute.push({ izraz, nazivi, prikazi, ...pravila });
}

export function pronadjiRutu(putanja) {
  for (const ruta of rute) {
    const pogodak = ruta.izraz.exec(putanja);
    if (pogodak !== null) {
      return { ruta, parametri: Object.fromEntries(ruta.nazivi.map((naziv, indeks) => [naziv, Number(pogodak[indeks + 1])])) };
    }
  }

  return null;
}

export function trenutnaAdresa() {
  const [putanja, upit = ''] = window.location.hash.replace(/^#/, '').split('?');
  return { putanja: putanja || '/', upit: new URLSearchParams(upit) };
}

/** Prazne, null i false vrednosti upita se izostavljaju, da adresa sadrži samo zadate filtere. */
export function idiNa(putanja, upit) {
  const parametri = new URLSearchParams(
    Object.entries(upit ?? {}).filter(([, vrednost]) => vrednost !== '' && vrednost !== null && vrednost !== undefined && vrednost !== false),
  ).toString();
  const novaAdresa = `#${putanja}${parametri ? `?${parametri}` : ''}`;

  if (window.location.hash === novaAdresa) {
    window.dispatchEvent(new HashChangeEvent('hashchange'));
  } else {
    window.location.hash = novaAdresa;
  }
}
