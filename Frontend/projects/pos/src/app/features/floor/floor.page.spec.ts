import { provideZonelessChangeDetection, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router } from '@angular/router';
import {
  FloorPlanDto,
  FloorPlanTableDto,
  RealtimeService,
  TableShape,
  TableStatus,
  TillApiService,
} from 'shared';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { FloorPage } from './floor.page';

/**
 * The floor screen is read at a glance from across a room, and everything it says is said in colour:
 * a waiter never reads the word "Zauzet", they see red. So the mapping from status to colour is the
 * screen's whole meaning, and a status silently falling through to the free colour would send
 * somebody to seat a party at an occupied table.
 *
 * The other rule tested here is that any hub event re-reads the plan rather than patching it. That
 * is the decision that keeps two waiters from seeing different totals for the same table, and it is
 * invisible until they do.
 */

function table(overrides: Partial<FloorPlanTableDto> = {}): FloorPlanTableDto {
  return {
    id: 't1',
    tableNumber: 5,
    capacity: 4,
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
    ...overrides,
  };
}

const emptyPlan: FloorPlanDto = { rooms: [], unplacedTables: [] };

describe('FloorPage', () => {
  let fixture: ComponentFixture<FloorPage>;
  let page: FloorPage & Record<string, unknown>;
  let floorPlan: ReturnType<typeof vi.fn>;
  let lastEvent: ReturnType<typeof signal<unknown>>;

  beforeEach(async () => {
    floorPlan = vi.fn().mockReturnValue(of(emptyPlan));
    lastEvent = signal<unknown>(null);

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideNoopAnimations(),
        { provide: TillApiService, useValue: { floorPlan } },
        { provide: RealtimeService, useValue: { lastEvent, connected: signal(true) } },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
      ],
    });

    fixture = TestBed.createComponent(FloorPage);
    page = fixture.componentInstance as FloorPage & Record<string, unknown>;
    await fixture.whenStable();
  });

  /** The colour rules are protected; the screen is what they are for, so they are reached by name. */
  function colour(status: TableStatus): string {
    return (page['colour'] as (t: FloorPlanTableDto) => string)(table({ status }));
  }

  function background(status: TableStatus): string {
    return (page['background'] as (t: FloorPlanTableDto) => string)(table({ status }));
  }

  it('gives every status its own colour, so none falls through to free', () => {
    expect(colour(TableStatus.Available)).toBe('var(--dr-free)');
    expect(colour(TableStatus.Occupied)).toBe('var(--dr-occupied)');
    expect(colour(TableStatus.Reserved)).toBe('var(--dr-reserved)');
    expect(colour(TableStatus.OutOfService)).toBe('var(--dr-out-of-service)');
  });

  it('pairs each colour with its own background', () => {
    expect(background(TableStatus.Available)).toBe('var(--dr-free-bg)');
    expect(background(TableStatus.Occupied)).toBe('var(--dr-occupied-bg)');
    expect(background(TableStatus.Reserved)).toBe('var(--dr-reserved-bg)');
    expect(background(TableStatus.OutOfService)).toBe('var(--dr-out-of-service-bg)');
  });

  it('reads a free table out as its state alone', () => {
    const describe = page['describe'] as (t: FloorPlanTableDto) => string;

    expect(describe(table())).toBe('Sto 5, Slobodan');
  });

  it('reads an occupied table out with what is on it, for a screen reader that sees no colour', () => {
    const describe = page['describe'] as (t: FloorPlanTableDto) => string;

    expect(
      describe(table({ status: TableStatus.Occupied, openOrderIds: ['o1'], openOrderTotal: 640 })),
    ).toBe('Sto 5, Zauzet, račun 640 RSD');
  });

  it('re-reads the whole plan on a hub event rather than patching from the payload', async () => {
    expect(floorPlan).toHaveBeenCalledTimes(1);

    lastEvent.set({ kind: 'orderCreated' });
    await fixture.whenStable();

    expect(floorPlan).toHaveBeenCalledTimes(2);
  });

  it('stops loading when the plan cannot be fetched, so the screen is not stuck', async () => {
    floorPlan.mockReturnValue(
      new (await import('rxjs')).Observable((subscriber) => subscriber.error(new Error('offline'))),
    );

    (page['reload'] as () => void)();
    await fixture.whenStable();

    expect((page['loading'] as () => boolean)()).toBe(false);
  });
});
