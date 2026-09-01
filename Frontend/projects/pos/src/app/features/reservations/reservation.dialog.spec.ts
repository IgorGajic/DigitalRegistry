import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { FloorPlanTableDto, TableShape, TableStatus } from 'shared';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  ReservationDialog,
  ReservationDialogData,
  ReservationDialogResult,
} from './reservation.dialog';

/**
 * A booking taken here goes down in the guest's name, not the name of whoever answered the phone —
 * which is the whole reason the desk could not take one before. The form therefore has to insist on
 * a name, and it has to refuse a table too small or a time already past before the caller is put on
 * hold to be told so by the API.
 */

function table(id: string, tableNumber: number, capacity: number): FloorPlanTableDto {
  return {
    id,
    tableNumber,
    capacity,
    status: TableStatus.Available,
    shape: TableShape.Round,
    positionX: 0,
    positionY: 0,
    width: 80,
    height: 80,
    rotation: 0,
    isActive: true,
    openOrderIds: [],
    openOrderTotal: 0,
    oldestOpenOrderAtUtc: null,
  };
}

/** Tomorrow, so the form's default seven-o'clock slot is always still ahead. */
function tomorrow(): Date {
  const date = new Date();
  date.setDate(date.getDate() + 1);

  return date;
}

describe('ReservationDialog', () => {
  let fixture: ComponentFixture<ReservationDialog>;
  /** The form's fields are protected, as they should be; the test reaches them by name. */
  let dialog: Record<string, any>;
  let close: ReturnType<typeof vi.fn>;

  function create(data: Partial<ReservationDialogData> = {}): void {
    close = vi.fn();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideNoopAnimations(),
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            tables: [table('t1', 1, 2), table('t2', 2, 6)],
            date: tomorrow(),
            ...data,
          } satisfies ReservationDialogData,
        },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });

    fixture = TestBed.createComponent(ReservationDialog);
    dialog = fixture.componentInstance as unknown as Record<string, any>;
  }

  beforeEach(() => create());

  it('refuses a booking with no name: that is what would file it under the waiter', () => {
    dialog['tableId'].set('t1');

    expect(dialog['problem']()).toBe('Unesite ime na koje se vodi rezervacija.');
  });

  it('offers only tables that can seat the party', () => {
    dialog['partySize'].set(4);

    expect(dialog['seatable']().map((t: FloorPlanTableDto) => t.id)).toEqual(['t2']);
  });

  it('drops a chosen table once the party outgrows it, rather than sending a doomed request', () => {
    dialog['name'].set('Marko Marković');
    dialog['tableId'].set('t1');
    dialog['partySize'].set(6);

    expect(dialog['problem']()).toBe('Izaberite sto koji prima toliko gostiju.');
  });

  it('refuses a booking in the past', () => {
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);

    create({ date: yesterday });
    dialog['name'].set('Marko Marković');
    dialog['tableId'].set('t2');
    dialog['partySize'].set(4);
    dialog['day'].set(yesterday);

    expect(dialog['problem']()).toBe('Rezervacija mora biti u budućnosti.');
  });

  it('accepts a complete booking and hands back the name the desk wrote down', () => {
    dialog['name'].set('  Marko Marković  ');
    dialog['phone'].set(' 060 111 222 ');
    dialog['partySize'].set(4);
    dialog['tableId'].set('t2');

    expect(dialog['problem']()).toBeNull();

    dialog['confirm']();

    const result = close.mock.calls[0][0] as ReservationDialogResult;
    expect(result.contactName).toBe('Marko Marković');
    expect(result.contactPhone).toBe('060 111 222');
    expect(result.tableId).toBe('t2');
    expect(result.partySize).toBe(4);
    expect(new Date(result.endTime).getTime()).toBeGreaterThan(new Date(result.startTime).getTime());
  });

  it('sends no number rather than an empty one when the desk did not take one', () => {
    dialog['name'].set('Ana Anić');
    dialog['tableId'].set('t1');
    dialog['confirm']();

    expect((close.mock.calls[0][0] as ReservationDialogResult).contactPhone).toBeNull();
  });

  it('carries a sitting past midnight into the following day instead of refusing it', () => {
    dialog['name'].set('Ana Anić');
    dialog['tableId'].set('t1');
    dialog['startTime'].set('23:00');
    dialog['endTime'].set('01:00');
    dialog['confirm']();

    const result = close.mock.calls[0][0] as ReservationDialogResult;
    const hours =
      (new Date(result.endTime).getTime() - new Date(result.startTime).getTime()) / 3600_000;

    expect(hours).toBe(2);
  });

  it('closes with nothing while the form is incomplete', () => {
    dialog['confirm']();

    expect(close).not.toHaveBeenCalled();
  });
});
