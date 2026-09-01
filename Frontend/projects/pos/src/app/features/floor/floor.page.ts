import { CurrencyPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import {
  FloorPlanDto,
  FloorPlanTableDto,
  RealtimeService,
  RoomDto,
  TableStatus,
  TillApiService,
  elapsedSince,
  seatsLabel,
  tableStatusLabels,
  tablesLabel,
} from 'shared';

/**
 * The till's main screen: the room as it actually is.
 *
 * Tables are drawn at the coordinates the owner arranged them at, scaled to whatever screen this is.
 * Colour carries the state, because a waiter crossing the room needs to read it at a glance rather
 * than compare numbers.
 */
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
          <mat-tab-group
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
                  >
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

    .floor__canvas {
      position: relative;
      width: 100%;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      background-image:
        linear-gradient(var(--mat-sys-outline-variant) 1px, transparent 1px),
        linear-gradient(90deg, var(--mat-sys-outline-variant) 1px, transparent 1px);
      background-size: 5% 5%;
      overflow: hidden;
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

  protected readonly legend = [
    { status: TableStatus.Available, colour: 'var(--dr-free)' },
    { status: TableStatus.Occupied, colour: 'var(--dr-occupied)' },
    { status: TableStatus.Reserved, colour: 'var(--dr-reserved)' },
  ];

  protected readonly rooms = computed<RoomDto[]>(() => this.plan()?.rooms ?? []);

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
  }

  protected open(table: FloorPlanTableDto): void {
    void this.router.navigate(['/sala', table.id]);
  }

  protected goToLayout(): void {
    void this.router.navigate(['/raspored']);
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
