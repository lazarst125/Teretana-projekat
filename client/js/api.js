import { token, zavrsi } from './sesija.js';

const PODRAZUMEVANE_PORUKE = {
  0: 'Server nije dostupan. Proverite vezu i pokušajte ponovo.',
  400: 'Proverite unete podatke.',
  401: 'Potrebno je da se prijavite.',
  403: 'Nemate pristup ovoj operaciji.',
  404: 'Traženi podatak ne postoji.',
};

/**
 * Greška iz API-ja. Poruka je naslov ProblemDetails-a kada server pošalje kod greške (naslovi domenskih
 * grešaka su na srpskom); za ostale greške koristi se podrazumevana poruka za status.
 */
export class ApiGreska extends Error {
  constructor(status, problem) {
    const kod = typeof problem?.code === 'string' ? problem.code : null;
    const imaDomenskuPoruku = kod !== null && kod !== 'validacija' && typeof problem.title === 'string';
    super(imaDomenskuPoruku ? problem.title : PODRAZUMEVANE_PORUKE[status] ?? 'Došlo je do neočekivane greške.');
    this.name = 'ApiGreska';
    this.status = status;
    this.kod = kod;
    this.greskePolja = problem?.errors ?? {};
  }
}

export async function pozovi(putanja, { metod = 'GET', telo, parametri } = {}) {
  const adresa = new URL(putanja, window.location.origin);
  for (const [naziv, vrednost] of Object.entries(parametri ?? {})) {
    if (vrednost !== undefined && vrednost !== null && vrednost !== '') {
      adresa.searchParams.set(naziv, vrednost);
    }
  }

  const zaglavlja = { Accept: 'application/json' };
  const aktivniToken = token();
  if (aktivniToken !== null) {
    zaglavlja.Authorization = `Bearer ${aktivniToken}`;
  }

  if (telo !== undefined) {
    zaglavlja['Content-Type'] = 'application/json';
  }

  let odgovor;
  try {
    odgovor = await fetch(adresa, { method: metod, headers: zaglavlja, body: telo === undefined ? undefined : JSON.stringify(telo) });
  } catch {
    throw new ApiGreska(0, null);
  }

  const podaci = await procitajTelo(odgovor);
  if (odgovor.ok) {
    return podaci;
  }

  if (odgovor.status === 401 && aktivniToken !== null) {
    zavrsi('sesija-istekla');
  }

  throw new ApiGreska(odgovor.status, podaci);
}

async function procitajTelo(odgovor) {
  const tip = odgovor.headers.get('Content-Type') ?? '';
  return odgovor.status === 204 || !tip.includes('json') ? null : odgovor.json();
}
