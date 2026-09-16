import { pozovi } from '../api.js';
import {
  el, formatirajVreme, greskaForme, link, naslovEkrana, napomena, porukaGreske, postaviNaslov, prikaziPoruku, saCekanjem,
  sakrij, stanjeGreske, stanjePrazno, stanjeUcitavanja, tabela, vremeTermina, znackaTermina,
} from '../ui.js';

export async function prikaziPolaznike(kontejner, kontekst) {
  const idTermina = kontekst.parametri.id;
  postaviNaslov('Polaznici');
  const naslov = naslovEkrana('Polaznici', 'polaznici-naslov');
  const telo = el('div', { 'data-testid': 'polaznici' });
  kontejner.append(el('p', { class: 'nazad' }, link('← Nazad na termin', `/termini/${idTermina}`, 'nazad-na-termin')), naslov, telo);

  await ucitaj();

  async function ucitaj() {
    telo.replaceChildren(stanjeUcitavanja('polaznici'));
    let termin;
    let polaznici;
    try {
      [termin, polaznici] = await Promise.all([pozovi(`/api/termini/${idTermina}`), pozovi(`/api/termini/${idTermina}/polaznici`)]);
    } catch (greska) {
      telo.replaceChildren(stanjeGreske('polaznici', greska, ucitaj));
      return;
    }

    naslov.textContent = `Polaznici: ${termin.naziv}`;
    postaviNaslov(naslov.textContent);
    const poceo = new Date(termin.pocetak) <= new Date();
    const greskaAkcije = greskaForme('polaznici-greska');

    telo.replaceChildren(
      el('p', {}, vremeTermina(termin.pocetak, termin.kraj), ' ', znackaTermina(termin)),
      greskaAkcije,
      el('section', { 'aria-labelledby': 'potvrdjeni-naslov' },
        el('h2', { id: 'potvrdjeni-naslov' }, `Potvrđeni polaznici (${polaznici.potvrdjeni.length} od ${termin.kapacitet})`),
        poceo ? null : napomena('Prisustvo se evidentira kada termin počne.', 'prisustvo-nedostupno'),
        polaznici.potvrdjeni.length === 0
          ? stanjePrazno('potvrdjeni', 'Nema potvrđenih polaznika.')
          : tabela(['Ime i prezime', 'Email', 'Prisustvo', 'Evidencija'], polaznici.potvrdjeni.map((polaznik) => redPotvrdjenog(polaznik, poceo, greskaAkcije)), 'potvrdjeni-tabela')),
      el('section', { 'aria-labelledby': 'cekanje-naslov' },
        el('h2', { id: 'cekanje-naslov' }, `Lista čekanja (${polaznici.listaCekanja.length})`),
        polaznici.listaCekanja.length === 0
          ? stanjePrazno('lista-cekanja', 'Niko ne čeka na mesto.')
          : tabela(['Pozicija', 'Ime i prezime', 'Email', 'Prijavljen'], polaznici.listaCekanja.map(redNaCekanju), 'lista-cekanja-tabela')));
  }

  function redPotvrdjenog(polaznik, poceo, greskaAkcije) {
    const [stanje, tekst] = polaznik.prisustvovao === true ? ['dosao', 'Došao']
      : polaznik.prisustvovao === false ? ['nije-dosao', 'Nije došao']
        : ['neevidentirano', 'Nije evidentirano'];
    const dugme = (prisustvovao, tekstDugmeta, testid) => el('button', {
      type: 'button',
      class: 'dugme-sekundarno dugme-malo',
      'data-testid': testid,
      disabled: !poceo,
      'aria-pressed': String(polaznik.prisustvovao === prisustvovao),
      onclick: (dogadjaj) => evidentiraj(dogadjaj.currentTarget, prisustvovao),
    }, tekstDugmeta);

    return el('tr', { 'data-testid': 'polaznik-red', 'data-rezervacija-id': polaznik.rezervacijaId },
      el('td', { 'data-testid': 'polaznik-ime' }, polaznik.imePrezime),
      el('td', {}, polaznik.email),
      el('td', { 'data-testid': 'polaznik-prisustvo', 'data-prisustvo': stanje }, tekst),
      el('td', {}, el('div', { class: 'akcije' },
        dugme(true, 'Došao', 'polaznik-dosao'),
        dugme(false, 'Nije došao', 'polaznik-nije-dosao'))));

    async function evidentiraj(dugmeAkcije, prisustvovao) {
      sakrij(greskaAkcije);
      await saCekanjem(dugmeAkcije, 'Čuvanje…', async () => {
        try {
          await pozovi(`/api/rezervacije/${polaznik.rezervacijaId}/prisustvo`, { metod: 'PUT', telo: { prisustvovao } });
          await ucitaj();
          kontekst.obavesti({ tekst: `Prisustvo je evidentirano: ${polaznik.imePrezime}.` });
        } catch (greska) {
          prikaziPoruku(greskaAkcije, porukaGreske(greska));
        }
      });
    }
  }
}

function redNaCekanju(polaznik) {
  return el('tr', { 'data-testid': 'cekanje-red', 'data-rezervacija-id': polaznik.rezervacijaId },
    el('td', { 'data-testid': 'cekanje-pozicija' }, polaznik.pozicija),
    el('td', { 'data-testid': 'cekanje-ime' }, polaznik.imePrezime),
    el('td', {}, polaznik.email),
    el('td', {}, formatirajVreme(polaznik.prijavljenAt)));
}
