import { el, link, naslovEkrana, postaviNaslov } from '../ui.js';

export async function prikaziNematePristup(kontejner) {
  postaviNaslov('Nemate pristup');
  kontejner.append(el('section', { class: 'stanje stanje-greska', 'data-testid': 'nemate-pristup' },
    naslovEkrana('Nemate pristup'),
    el('p', {}, 'Ova stranica nije dostupna vašoj ulozi.'),
    link('Na raspored termina', '/termini', 'nemate-pristup-raspored', 'dugme-link')));
}

export async function prikaziNepostojecuStranicu(kontejner) {
  postaviNaslov('Stranica ne postoji');
  kontejner.append(el('section', { class: 'stanje', 'data-testid': 'stranica-ne-postoji' },
    naslovEkrana('Stranica ne postoji'),
    el('p', {}, 'Adresa koju ste otvorili ne postoji u aplikaciji.'),
    link('Na početnu stranu', '/', 'stranica-ne-postoji-pocetna', 'dugme-link')));
}
