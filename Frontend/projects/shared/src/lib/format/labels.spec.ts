import { describe, expect, it } from 'vitest';

import {
  accountsLabel,
  assignmentsLabel,
  billsLabel,
  daysLabel,
  itemsLabel,
  plural,
  seatsLabel,
  tablesLabel,
} from './labels';

/**
 * Serbian has three plural forms where English has two, and the teens invert the rule that governs
 * every other number. A two-way test would pass on 1 and 5 and be wrong on everything the till
 * actually shows — a table with three drinks on it, a licence with twenty-two days left.
 */
describe('plural', () => {
  const form = (count: number) => plural(count, 'stavka', 'stavke', 'stavki');

  it('takes the singular for 1 and for anything else ending in 1', () => {
    expect(form(1)).toBe('stavka');
    expect(form(21)).toBe('stavka');
    expect(form(101)).toBe('stavka');
  });

  it('takes the middle form for 2 to 4, and for anything else ending in them', () => {
    expect(form(2)).toBe('stavke');
    expect(form(3)).toBe('stavke');
    expect(form(4)).toBe('stavke');
    expect(form(23)).toBe('stavke');
  });

  it('takes the plural from 5 up, and for zero', () => {
    expect(form(0)).toBe('stavki');
    expect(form(5)).toBe('stavki');
    expect(form(19)).toBe('stavki');
    expect(form(100)).toBe('stavki');
  });

  it('gives the teens the plural, which is where the naive rule breaks', () => {
    expect(form(11)).toBe('stavki');
    expect(form(12)).toBe('stavki');
    expect(form(13)).toBe('stavki');
    expect(form(14)).toBe('stavki');
    expect(form(111)).toBe('stavki');
    expect(form(112)).toBe('stavki');
  });
});

describe('itemsLabel and daysLabel', () => {
  it("reads a table's tab the way somebody would say it", () => {
    expect(`1 ${itemsLabel(1)}`).toBe('1 stavka');
    expect(`3 ${itemsLabel(3)}`).toBe('3 stavke');
    expect(`7 ${itemsLabel(7)}`).toBe('7 stavki');
  });

  it('reads a licence countdown the same way', () => {
    expect(`1 ${daysLabel(1)}`).toBe('1 dan');
    expect(`3 ${daysLabel(3)}`).toBe('3 dana');
    expect(`11 ${daysLabel(11)}`).toBe('11 dana');
    expect(`21 ${daysLabel(21)}`).toBe('21 dan');
  });
});

/**
 * The nouns the screens count besides items and days. Each was written out by hand in a template
 * before this — "3 sto(lova)", "2 nalog(a)" — which is what a parenthesis in a user interface
 * always means: the plural was known to be wrong and left that way.
 */
describe('the counted nouns on the screens', () => {
  it('counts tables, which change stem in the plural', () => {
    expect(`1 ${tablesLabel(1)}`).toBe('1 sto');
    expect(`2 ${tablesLabel(2)}`).toBe('2 stola');
    expect(`5 ${tablesLabel(5)}`).toBe('5 stolova');
    expect(`21 ${tablesLabel(21)}`).toBe('21 sto');
  });

  it('counts seats, where the middle and plural forms coincide', () => {
    expect(`1 ${seatsLabel(1)}`).toBe('1 mesto');
    expect(`4 ${seatsLabel(4)}`).toBe('4 mesta');
    expect(`8 ${seatsLabel(8)}`).toBe('8 mesta');
  });

  it('counts staff accounts', () => {
    expect(`1 ${accountsLabel(1)}`).toBe('1 nalog');
    expect(`3 ${accountsLabel(3)}`).toBe('3 naloga');
    expect(`12 ${accountsLabel(12)}`).toBe('12 naloga');
  });

  it('counts bills', () => {
    expect(`1 ${billsLabel(1)}`).toBe('1 račun');
    expect(`3 ${billsLabel(3)}`).toBe('3 računa');
    expect(`9 ${billsLabel(9)}`).toBe('9 računa');
    expect(`21 ${billsLabel(21)}`).toBe('21 račun');
  });

  it('counts assignments, a feminine noun that declines differently', () => {
    expect(`1 ${assignmentsLabel(1)}`).toBe('1 dodela');
    expect(`2 ${assignmentsLabel(2)}`).toBe('2 dodele');
    expect(`5 ${assignmentsLabel(5)}`).toBe('5 dodela');
    expect(`11 ${assignmentsLabel(11)}`).toBe('11 dodela');
  });
});
