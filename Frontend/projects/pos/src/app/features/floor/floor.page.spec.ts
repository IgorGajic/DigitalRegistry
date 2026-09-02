import { provideZonelessChangeDetection, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router } from '@angular/router';
import {
  FixtureKind,
  FixtureShape,
  FixtureTone,
  FloorPlanDto,
  FloorPlanTableDto,
  RoomFixtureDto,
  TableShape,
  TableStatus,
  TillApiService,
} from 'shared';
import { RealtimeService } from 'shared/realtime';
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

function fixture_(overrides: Partial<RoomFixtureDto> = {}): RoomFixtureDto {
  return {
    id: 'f1',
    kind: FixtureKind.Bar,
    label: 'Šank',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Wood,
    positionX: 10,
    positionY: 10,
    width: 400,
    height: 60,
    rotation: 0,
    displayOrder: 0,
    ...overrides,
  };
}

const emptyPlan: FloorPlanDto = { rooms: [], unplacedTables: [] };

function planWith(fixtures: RoomFixtureDto[], tables: FloorPlanTableDto[] = []): FloorPlanDto {
  return {
    rooms: [
      {
        id: 'r1',
        name: 'Sala',
        displayOrder: 0,
        canvasWidth: 1200,
        canvasHeight: 800,
        tables,
        fixtures,
      },
    ],
    unplacedTables: [],
  };
}

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

  // ------------------------------------------------------------------------------- fixtures
  //
  // Landmarks exist so the plan reads like the room. They must never behave like a table: a waiter
  // reaching for a bill must not be able to hit the toilet instead, and a screen reader working
  // through the plan should meet tables, not furniture.

  it('gives every tone its own fill, so none falls through to stone', () => {
    const fill = page['toneFill'] as (f: RoomFixtureDto) => string;

    expect(fill(fixture_({ tone: FixtureTone.Wood }))).toBe('var(--dr-tone-wood)');
    expect(fill(fixture_({ tone: FixtureTone.Slate }))).toBe('var(--dr-tone-slate)');
    expect(fill(fixture_({ tone: FixtureTone.Stone }))).toBe('var(--dr-tone-stone)');
    expect(fill(fixture_({ tone: FixtureTone.Glass }))).toBe('var(--dr-tone-glass)');
  });

  it('pairs each fill with its own outline', () => {
    const line = page['toneLine'] as (f: RoomFixtureDto) => string;

    expect(line(fixture_({ tone: FixtureTone.Wood }))).toBe('var(--dr-tone-wood-line)');
    expect(line(fixture_({ tone: FixtureTone.Slate }))).toBe('var(--dr-tone-slate-line)');
    expect(line(fixture_({ tone: FixtureTone.Stone }))).toBe('var(--dr-tone-stone-line)');
    expect(line(fixture_({ tone: FixtureTone.Glass }))).toBe('var(--dr-tone-glass-line)');
  });

  it('draws a fixture, and never as something that can be pressed', async () => {
    floorPlan.mockReturnValue(of(planWith([fixture_()])));
    (page['reload'] as () => void)();
    await fixture.whenStable();

    const drawn = fixture.nativeElement.querySelector('.floor__fixture') as HTMLElement;

    expect(drawn).toBeTruthy();
    expect(drawn.tagName).toBe('DIV');
    expect(drawn.getAttribute('aria-hidden')).toBe('true');
  });

  it('leaves the fixture unnamed here, however it is named in the editor', async () => {
    floorPlan.mockReturnValue(of(planWith([fixture_({ label: 'Šank' })])));
    (page['reload'] as () => void)();
    await fixture.whenStable();

    const drawn = fixture.nativeElement.querySelector('.floor__fixture') as HTMLElement;

    // Every other word on this screen is a table number or an amount owed. Staff know their own
    // room, so the label would only be repeating what the shape and its position already say.
    expect(drawn.textContent?.trim()).toBe('');
  });

  it('caps the room by the height available, so the floor never falls below the fold', async () => {
    const room = { canvasWidth: 1200, canvasHeight: 800 } as never;
    // Bound, unlike the colour helpers above: this one reads a signal off the component.
    const fit = (page['fitWidth'] as (r: unknown) => number | null).bind(page);

    // Nothing measured yet: full width, rather than a canvas collapsed to nothing for a frame.
    expect(fit(room)).toBeNull();

    (page['available'] as { set(value: number): void }).set(400);

    // Width follows from the height it may take, because the canvas holds the room's aspect ratio.
    expect(fit(room)).toBe(600);
  });

  it('keeps fixtures underneath the tables in paint order', async () => {
    floorPlan.mockReturnValue(of(planWith([fixture_()], [table()])));
    (page['reload'] as () => void)();
    await fixture.whenStable();

    const canvas = fixture.nativeElement.querySelector('.floor__canvas') as HTMLElement;
    const drawn = [...canvas.children].map((child) => child.className.split(' ')[0]);

    // Both are absolutely positioned with no z-index, so document order is the stacking order: a
    // bar painted after a table would cover the amount that table owes.
    expect(drawn.indexOf('floor__fixture')).toBeLessThan(drawn.indexOf('floor__table'));
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
