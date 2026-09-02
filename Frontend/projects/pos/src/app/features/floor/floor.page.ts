import { CurrencyPipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import {
  FixtureShape,
  FixtureTone,
  FloorPlanDto,
  FloorPlanTableDto,
  RoomDto,
  RoomFixtureDto,
  ServiceTicketDto,
  TableStatus,
  TillApiService,
  elapsedSince,
  seatsLabel,
  tableStatusLabels,
  tablesLabel,
} from 'shared';
import { RealtimeService } from 'shared/realtime';

/**
 * The till's main screen: the room as it actually is.
 *
 * Tables are drawn at the coordinates the owner arranged them at, scaled to whatever screen this is.
 * Colour carries the state, because a waiter crossing the room needs to read it at a glance rather
 * than compare numbers.
 */
/** Breathing room under the plan, so it does not sit flush against the bottom of the window. */
const BOTTOM_GUTTER = 16;

/**
 * Never shrink the room below this, whatever the arithmetic says.
 *
 * Only a guard against absurd viewports, where the sum could reach zero or go negative and there
 * would be no plan at all. It is deliberately lower than any height worth working at: every pixel
 * this floor is raised is a pixel the page scrolls on a window that short, and not scrolling is the
 * point. Measured at 520 px of viewport — a small laptop window — a floor of 260 was itself the
 * whole overflow.
 */
const MIN_CANVAS_HEIGHT = 200;

@Component({
  selector: 'pos-floor',
  imports: [CurrencyPipe, MatButtonModule, MatIconModule, MatProgressBarModule, MatTabsModule],
  template: `
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <h1 class="floor__title">Sala</h1>

      @if (plan(); as data) {
        @if (data.rooms.length === 0) {
          <div class="dr-empty">
            <mat-icon class="floor__empty-icon">table_restaurant</mat-icon>
            <p>Nijedna prostorija još nije napravljena.</p>
            <button mat-flat-button (click)="goToLayout()">Uredi raspored</button>
          </div>
        } @else {
          <div class="floor__with-queue">
          <mat-tab-group
            class="floor__rooms"
            [selectedIndex]="activeRoom()"
            (selectedIndexChange)="activeRoom.set($event)"
            animationDuration="120ms"
          >
            @for (room of data.rooms; track room.id) {
              <mat-tab [label]="room.name + ' (' + room.tables.length + ')'">
                <ng-template matTabContent>
                  <div class="floor__legend">
                    @for (state of legend; track state.status) {
                      <span class="floor__legend-item">
                        <i class="floor__swatch" [style.background]="state.colour"></i>
                        {{ tableStatusLabels[state.status] }}
                      </span>
                    }
                    <span class="dr-toolbar-spacer"></span>
                    <button mat-button (click)="reload()">
                      <mat-icon>refresh</mat-icon>
                      Osveži
                    </button>
                  </div>

                  <!-- The canvas keeps the room's aspect ratio and scales to the viewport, so a
                       layout arranged on a desktop reads the same on a waiter's tablet. -->
                  <div
                    class="floor__canvas"
                    [style.aspect-ratio]="room.canvasWidth + ' / ' + room.canvasHeight"
                    [style.max-width.px]="fitWidth(room)"
                  >
                    <!--
                      Landmarks first, so they paint under the tables. Drawn as plain divs and
                      hidden from assistive technology on purpose: a waiter must be able to open a
                      table and must never be able to open the toilet, and a screen reader reading
                      out the furniture would bury the tables that matter.

                      Unlabelled here, and named only in the editor. Staff know their own room —
                      the bar is the bar — so on the working screen the words would be repeating
                      what the shape and its place already say, in the one place where every other
                      piece of text is a table number or an amount owed.
                    -->
                    @for (fixture of room.fixtures; track fixture.id) {
                      <div
                        class="floor__fixture"
                        [class.floor__fixture--round]="fixture.shape === FixtureShape.Ellipse"
                        [style.left.%]="percent(fixture.positionX, room.canvasWidth)"
                        [style.top.%]="percent(fixture.positionY, room.canvasHeight)"
                        [style.width.%]="percent(fixture.width, room.canvasWidth)"
                        [style.height.%]="percent(fixture.height, room.canvasHeight)"
                        [style.transform]="'rotate(' + fixture.rotation + 'deg)'"
                        [style.background]="toneFill(fixture)"
                        [style.border-color]="toneLine(fixture)"
                        aria-hidden="true"
                      ></div>
                    }

                    @for (table of room.tables; track table.id) {
                      <button
                        type="button"
                        class="floor__table"
                        [class.floor__table--round]="table.shape === 1"
                        [style.left.%]="percent(table.positionX, room.canvasWidth)"
                        [style.top.%]="percent(table.positionY, room.canvasHeight)"
                        [style.width.%]="percent(table.width, room.canvasWidth)"
                        [style.height.%]="percent(table.height, room.canvasHeight)"
                        [style.transform]="'rotate(' + table.rotation + 'deg)'"
                        [style.background]="background(table)"
                        [style.border-color]="colour(table)"
                        [style.color]="colour(table)"
                        (click)="open(table)"
                        [attr.aria-label]="describe(table)"
                      >
                        <span class="floor__number">{{ table.tableNumber }}</span>
                        <span class="floor__seats">{{ table.capacity }} {{ seatsLabel(table.capacity) }}</span>

                        @if (table.openOrderIds.length > 0) {
                          <span class="floor__total">
                            {{ table.openOrderTotal | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                          </span>
                          @if (table.oldestOpenOrderAtUtc; as since) {
                            <span class="floor__since">{{ elapsed(since) }}</span>
                          }
                          @if (table.openOrderIds.length > 1) {
                            <span class="floor__badge">{{ table.openOrderIds.length }}</span>
                          }
                        }
                      </button>
                    }
                  </div>
                </ng-template>
              </mat-tab>
            }
          </mat-tab-group>

          <!--
            The runner's queue. A round ordered from a phone has nobody attached to it — no
            waiter took it, and the table may be one nobody has looked at for an hour — so
            unless it is written down somewhere it waits until somebody happens to notice.
            It sits beside the plan rather than on it: the plan says where, this says what.
          -->
          <aside class="floor__queue" [style.max-height.px]="available()">
            <h2 class="floor__queue-title">
              Za iznošenje
              @if (queue().length) {
                <span class="floor__queue-count">{{ queue().length }}</span>
              }
            </h2>

            <div class="floor__queue-list">
              @for (ticket of queue(); track ticket.id) {
                <article class="floor__ticket">
                  <header class="floor__ticket-head">
                    <span class="floor__ticket-table">Sto {{ ticket.tableNumber }}</span>
                    @if (ticket.roomName) {
                      <span class="floor__ticket-room">{{ ticket.roomName }}</span>
                    }
                    <span class="dr-toolbar-spacer"></span>
                    <span class="floor__ticket-since">{{ elapsed(ticket.placedAtUtc) }}</span>
                  </header>

                  <ul class="floor__ticket-items">
                    @for (line of ticket.items; track line.menuItemName) {
                      <li>
                        <span class="floor__ticket-qty">{{ line.quantity }}×</span>
                        {{ line.menuItemName }}
                      </li>
                    }
                  </ul>

                  <button
                    mat-flat-button
                    class="floor__ticket-done"
                    [disabled]="serving() === ticket.id"
                    (click)="markServed(ticket)"
                  >
                    <mat-icon>check</mat-icon>
                    Izneto
                  </button>
                </article>
              } @empty {
                <p class="dr-muted floor__queue-empty">Nema porudžbina na čekanju.</p>
              }
            </div>
          </aside>
          </div>

          @if (data.unplacedTables.length) {
            <p class="dr-muted floor__unplaced">
              <mat-icon inline>info</mat-icon>
              {{ data.unplacedTables.length }} {{ tablesLabel(data.unplacedTables.length) }}
              {{ data.unplacedTables.length === 1 ? 'nije raspoređen' : 'nije raspoređeno' }} ni u
              jednu prostoriju.
            </p>
          }
        }
      }
    </div>
  `,
  styles: `
    /* Visually quiet: the room itself is what the waiter reads, not the word above it. The heading
       is here so the page announces itself to a screen reader like every other one does. */
    .floor__title {
      margin: 0 0 4px;
      font-size: 1.5rem;
    }

    .floor__legend {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 12px 4px;
      flex-wrap: wrap;
    }

    .floor__legend-item {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 0.875rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .floor__swatch {
      width: 14px;
      height: 14px;
      border-radius: 4px;
      display: inline-block;
    }

    /* Plan and queue side by side. The plan keeps its own ceiling and centres in what is left; the
       queue takes a fixed column so it does not grow and shrink as tickets come and go. */
    .floor__with-queue {
      display: flex;
      align-items: flex-start;
      gap: var(--dr-gap);
    }

    /* The rooms take whatever the queue leaves. min-width 0 so this flex child may shrink below its
       content — without it the tab group refuses to give the column room and the pair overflows. */
    .floor__rooms {
      flex: 1;
      min-width: 0;
    }

    .floor__canvas {
      position: relative;
      width: 100%;
      margin-inline: auto;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      background-image:
        linear-gradient(var(--mat-sys-outline-variant) 1px, transparent 1px),
        linear-gradient(90deg, var(--mat-sys-outline-variant) 1px, transparent 1px);
      background-size: 5% 5%;
      overflow: hidden;
    }

    /* Architecture, not state. Quiet fill, thin line, no shadow — everything the tables use to
       demand attention is deliberately absent here. */
    .floor__fixture {
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid;
      border-radius: 4px;
      padding: 2px;
      overflow: hidden;
      color: var(--dr-tone-ink);
      /* Never intercepts a tap meant for a table sitting on top of it. */
      pointer-events: none;
    }

    .floor__fixture--round {
      border-radius: 50%;
    }

    .floor__table {
      position: absolute;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1px;
      border: 2px solid;
      border-radius: 8px;
      cursor: pointer;
      font: inherit;
      line-height: 1.1;
      padding: 2px;
      transition: filter 120ms ease;
    }

    .floor__table:hover {
      filter: brightness(0.95);
    }

    .floor__table--round {
      border-radius: 50%;
    }

    .floor__number {
      font-family: var(--dr-font-mono);
      font-size: clamp(0.9rem, 1.6vw, 1.4rem);
      font-weight: 500;
      letter-spacing: -0.03em;
    }

    .floor__seats {
      font-size: 0.65rem;
      opacity: 0.75;
    }

    .floor__total {
      font-size: 0.75rem;
      font-weight: 600;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .floor__since {
      font-size: 0.6rem;
      opacity: 0.75;
    }

    .floor__badge {
      position: absolute;
      top: 2px;
      right: 4px;
      font-size: 0.65rem;
      font-weight: 700;
    }

    .floor__empty-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
    }

    .floor__unplaced {
      margin-top: 12px;
    }

    /* ------------------------------------------------------------------------ runner's queue */

    .floor__queue {
      display: flex;
      flex-direction: column;
      flex: 0 0 260px;
      min-height: 0;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      background: var(--mat-sys-surface);
      overflow: hidden;
    }

    .floor__queue-title {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      padding: 10px 12px;
      font-family: var(--dr-font-brand);
      font-size: 0.8rem;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .floor__queue-count {
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font-family: var(--dr-font-mono);
    }

    /* This is the thing that scrolls — not the page. The floor plan must stay where it is while a
       waiter works down the list, and the list is the only part that can outgrow the window. */
    .floor__queue-list {
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      padding: 8px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .floor__queue-empty {
      margin: 8px 4px;
      font-size: 0.85rem;
    }

    .floor__ticket {
      border: 1px solid var(--mat-sys-outline-variant);
      border-left: 3px solid var(--dr-reserved);
      border-radius: var(--dr-radius-sm);
      padding: 8px 10px;
      background: var(--mat-sys-surface-container-low);
    }

    .floor__ticket-head {
      display: flex;
      align-items: baseline;
      gap: 6px;
      margin-bottom: 6px;
    }

    .floor__ticket-table {
      font-family: var(--dr-font-brand);
      font-weight: 700;
    }

    .floor__ticket-room,
    .floor__ticket-since {
      font-size: 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .floor__ticket-items {
      margin: 0 0 8px;
      padding: 0;
      list-style: none;
      font-size: 0.85rem;
      line-height: 1.5;
    }

    .floor__ticket-qty {
      font-family: var(--dr-font-mono);
      font-weight: 600;
    }

    .floor__ticket-done {
      width: 100%;
    }

    /* Below the breakpoint the pair stacks: a 260 px column beside a plan on a tablet held upright
       leaves neither enough room to be read. The queue keeps its own scroll either way. */
    @media (max-width: 900px) {
      .floor__with-queue {
        flex-direction: column;
      }

      .floor__queue {
        flex: none;
        width: 100%;
        max-height: 320px;
      }
    }
  `,
})
export class FloorPage {
  private readonly api = inject(TillApiService);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);

  protected readonly tableStatusLabels = tableStatusLabels;
  protected readonly seatsLabel = seatsLabel;
  protected readonly tablesLabel = tablesLabel;
  protected readonly plan = signal<FloorPlanDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly activeRoom = signal(0);

  protected readonly FixtureShape = FixtureShape;

  /** Rounds ordered from a phone that nobody has carried out yet, oldest first. */
  protected readonly queue = signal<ServiceTicketDto[]>([]);

  /** The ticket whose button has been pressed, so it cannot be pressed twice. */
  protected readonly serving = signal<string | null>(null);

  protected readonly legend = [
    { status: TableStatus.Available, colour: 'var(--dr-free)' },
    { status: TableStatus.Occupied, colour: 'var(--dr-occupied)' },
    { status: TableStatus.Reserved, colour: 'var(--dr-reserved)' },
  ];

  protected readonly rooms = computed<RoomDto[]>(() => this.plan()?.rooms ?? []);

  private readonly host = inject(ElementRef<HTMLElement>);

  /**
   * How tall the room may be drawn without pushing the page into a scrollbar.
   *
   * This screen is glanced at, not read: a waiter crossing the floor looks up and finds a table.
   * A room that runs off the bottom turns that glance into a scroll, and the table they wanted is
   * the one below the fold. So the plan is fitted to the window instead of the window to the plan.
   *
   * Measured rather than assumed. What sits above the canvas — toolbar, heading, tabs, legend —
   * changes height with the viewport, and the legend wraps on a narrow one; a constant subtracted
   * from the window height would be wrong the first time any of that moved, and wrong silently.
   */
  protected readonly available = signal(0);

  /** Bumped by anything that can change the measurement but is not itself state on this page. */
  private readonly viewportTick = signal(0);

  constructor() {
    this.reload();

    // Any hub event means somebody else changed something on the floor. The plan is re-read rather
    // than patched from the payload: it is one cheap request, and reconstructing state from a stream
    // of deltas is how two waiters end up seeing different totals for the same table.
    effect(() => {
      if (this.realtime.lastEvent()) {
        this.reload();
      }
    });

    // Re-measured after the browser has laid the page out, and again whenever anything that could
    // move the canvas changes: a different room, a plan that arrived, a resized window. Reading in
    // the render phase is the only point at which the box on screen is the box being measured.
    afterRenderEffect({
      read: () => {
        this.plan();
        this.activeRoom();
        this.viewportTick();
        this.measure();
      },
    });

    const remeasure = () => this.viewportTick.update((tick) => tick + 1);

    window.addEventListener('resize', remeasure);
    inject(DestroyRef).onDestroy(() => window.removeEventListener('resize', remeasure));
  }

  protected reload(): void {
    this.loading.set(true);

    this.api.floorPlan().subscribe({
      next: (plan) => {
        this.plan.set(plan);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    // Read alongside the plan and on the same triggers. Failure is silent on purpose: an empty
    // queue and an unreachable one look the same, and a snackbar over the floor screen every time
    // the network hiccups would be worse than either.
    this.api.serviceQueue().subscribe({
      next: (tickets) => this.queue.set(tickets),
      error: () => undefined,
    });
  }

  /**
   * Takes a round off the queue once it has been carried out.
   *
   * The card is removed here rather than waiting for the round trip, because the waiter is holding
   * a tray and has already moved on. The request still runs, and the next reload — which any hub
   * event brings — is what settles the truth if it failed.
   */
  protected markServed(ticket: ServiceTicketDto): void {
    this.serving.set(ticket.id);
    this.queue.update((tickets) => tickets.filter((candidate) => candidate.id !== ticket.id));

    this.api.markOrderServed(ticket.id).subscribe({
      next: () => this.serving.set(null),
      error: () => {
        // Put it back. Somebody else may have served it a moment ago, in which case the reload
        // takes it away again — but a card that vanished on a failure is a drink nobody carries.
        this.serving.set(null);
        this.reload();
      },
    });
  }

  protected open(table: FloorPlanTableDto): void {
    void this.router.navigate(['/sala', table.id]);
  }

  protected goToLayout(): void {
    void this.router.navigate(['/raspored']);
  }

  /**
   * The widest the room may be drawn and still fit the height available.
   *
   * The canvas keeps the room's aspect ratio, so capping its width is how its height is capped:
   * width = height x ratio. Returns null before the first measurement, which leaves the canvas at
   * its natural full width for that one frame rather than collapsing it to nothing.
   */
  protected fitWidth(room: RoomDto): number | null {
    const height = this.available();

    if (height <= 0 || room.canvasHeight <= 0) {
      return null;
    }

    return Math.round((height * room.canvasWidth) / room.canvasHeight);
  }

  /**
   * Reads how much room is left for the plan.
   *
   * Everything below the canvas is measured as one span — from its bottom edge to the bottom of the
   * page — rather than itemised. The note about unplaced tables comes and goes, and its margins are
   * a paragraph's, not this screen's; adding up the parts we happen to know about is how a reserve
   * ends up a little short and the page scrolls by exactly that much. The span does not care what
   * is in it, and it does not move when the canvas resizes, because both edges move with it.
   */
  private measure(): void {
    const element = this.host.nativeElement as HTMLElement;
    const canvas = element.querySelector('.floor__canvas');

    if (!canvas) {
      return;
    }

    const box = canvas.getBoundingClientRect();
    const below = Math.max(0, element.getBoundingClientRect().bottom - box.bottom);

    const next = Math.max(
      MIN_CANVAS_HEIGHT,
      Math.round(window.innerHeight - box.top - below - BOTTOM_GUTTER),
    );

    // Rounded, and only taken when it actually moves. Narrowing the canvas can shorten the page
    // enough to retire a scrollbar, which changes the viewport, which would re-measure: without a
    // dead band the two could trade places forever.
    if (Math.abs(next - this.available()) > 1) {
      this.available.set(next);
    }
  }

  protected percent(value: number, extent: number): number {
    return extent > 0 ? (value / extent) * 100 : 0;
  }

  protected colour(table: FloorPlanTableDto): string {
    switch (table.status) {
      case TableStatus.Occupied:
        return 'var(--dr-occupied)';
      case TableStatus.Reserved:
        return 'var(--dr-reserved)';
      case TableStatus.OutOfService:
        return 'var(--dr-out-of-service)';
      default:
        return 'var(--dr-free)';
    }
  }

  protected background(table: FloorPlanTableDto): string {
    switch (table.status) {
      case TableStatus.Occupied:
        return 'var(--dr-occupied-bg)';
      case TableStatus.Reserved:
        return 'var(--dr-reserved-bg)';
      case TableStatus.OutOfService:
        return 'var(--dr-out-of-service-bg)';
      default:
        return 'var(--dr-free-bg)';
    }
  }

  /**
   * A fixture's fill and outline, resolved from its named tone.
   *
   * Named rather than stored as a colour so the venue's chosen theme can restate it. The mapping is
   * a switch and not a lookup object for the same reason the table colours are: an exhaustive switch
   * fails to compile when a tone is added, and a silent default would draw the new one as stone.
   */
  protected toneFill(fixture: RoomFixtureDto): string {
    switch (fixture.tone) {
      case FixtureTone.Wood:
        return 'var(--dr-tone-wood)';
      case FixtureTone.Slate:
        return 'var(--dr-tone-slate)';
      case FixtureTone.Glass:
        return 'var(--dr-tone-glass)';
      default:
        return 'var(--dr-tone-stone)';
    }
  }

  /** @see toneFill */
  protected toneLine(fixture: RoomFixtureDto): string {
    switch (fixture.tone) {
      case FixtureTone.Wood:
        return 'var(--dr-tone-wood-line)';
      case FixtureTone.Slate:
        return 'var(--dr-tone-slate-line)';
      case FixtureTone.Glass:
        return 'var(--dr-tone-glass-line)';
      default:
        return 'var(--dr-tone-stone-line)';
    }
  }

  protected elapsed(isoUtc: string): string {
    return elapsedSince(isoUtc);
  }

  protected describe(table: FloorPlanTableDto): string {
    const state = tableStatusLabels[table.status];

    return table.openOrderIds.length > 0
      ? `Sto ${table.tableNumber}, ${state}, račun ${table.openOrderTotal} RSD`
      : `Sto ${table.tableNumber}, ${state}`;
  }
}
