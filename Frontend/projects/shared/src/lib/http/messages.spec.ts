import { describe, expect, it } from 'vitest';

import { toSerbian } from './messages';

/**
 * The rule this file exists to protect: an untranslated message must survive intact. The fallback is
 * the failure mode — the backend gets reworded and this map goes stale — so it is tested first.
 */
describe('toSerbian', () => {
  it('hands back anything it does not recognise, unchanged', () => {
    const unknown = 'Some message this map has never seen.';

    expect(toSerbian(unknown)).toBe(unknown);
  });

  it('translates the message that prompted all of this', () => {
    expect(toSerbian('This order is Paid; its lines can no longer be voided. A settled bill is reversed instead.')).toBe(
      'Ovaj račun je zatvoren, pa mu se stavke više ne storniraju. '
        + 'Plaćen račun se stornira protivstavkom, sa otiska računa.',
    );
  });

  it('keeps interpolated values rather than dropping them', () => {
    expect(toSerbian('Table number 12 is already in use.')).toBe('Sto broj 12 već postoji.');

    expect(toSerbian("A room called 'Bašta' already exists.")).toBe('Prostorija „Bašta“ već postoji.');

    expect(
      toSerbian('Table 7 would fall outside a 800×600 room. Move it first, or choose a larger area.'),
    ).toBe('Sto 7 bi ostao izvan prostorije 800×600. Prvo ga pomerite, ili izaberite veće platno.');
  });

  it('declines the status word inside a sentence', () => {
    expect(toSerbian('This order is already Paid.')).toBe('Račun je već plaćen.');
    expect(toSerbian('This order is already Cancelled.')).toBe('Račun je već otkazan.');
  });

  it('gets the plural right in the reason-length message', () => {
    expect(toSerbian('Give a reason of at least 3 characters.')).toBe(
      'Razlog mora imati bar 3 znaka.',
    );
    expect(toSerbian('Give a reason of at least 10 characters.')).toBe(
      'Razlog mora imati bar 10 znakova.',
    );
  });

  it('translates the titles the API puts on problem responses', () => {
    expect(toSerbian('One or more validation errors occurred.')).toBe('Uneti podaci nisu ispravni.');
    expect(toSerbian('The request conflicts with the current state.')).toBe(
      'Radnja se kosi sa trenutnim stanjem podataka.',
    );
  });

  it('covers the messages a waiter meets most often', () => {
    expect(toSerbian('A payment has already been recorded for this order.')).toBe(
      'Ovaj račun je već naplaćen.',
    );
    expect(toSerbian('An empty order cannot be paid.')).toBe('Prazan račun se ne naplaćuje.');
    expect(toSerbian('Table 4 is not in service.')).toBe('Sto 4 je van upotrebe.');
    expect(toSerbian("'Espresso' is currently unavailable.")).toBe(
      '„Espresso“ trenutno nije u ponudi.',
    );
  });

  it('tolerates surrounding whitespace', () => {
    expect(toSerbian('  No such order.  ')).toBe('Račun nije pronađen.');
  });
});
