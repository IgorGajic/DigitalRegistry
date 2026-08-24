import { CdkDrag, CdkDragEnd, DragDropModule } from '@angular/cdk/drag-drop';
import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSliderModule } from '@angular/material/slider';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ConfirmDialog,
  ConfirmDialogData,
  FloorPlanDto,
  FloorPlanTableDto,
  PromptDialog,
  PromptDialogData,
  RoomDto,
  TableShape,
  TableLayoutRequest,
  TillApiService,
} from 'shared';

/** A table being arranged, in room coordinates. */
interface Placed {
  table: FloorPlanTableDto;
  x: number;
  y: number;
  width: number;
  height: number;
  shape: TableShape;
}

/**
 * Where the owner draws the room.
 *
 * The whole room is submitted on save, never individual moves. Dragging produces a stream of
 * positions, and persisting each one would put hundreds of writes behind one gesture and leave the
 * stored layout half-moved whenever the network dropped mid-drag.
 *
 * It follows from that shape that a table dragged out of the room is simply left out of the next
 * save — which is how removal works here, with no separate endpoint for it.
 */
@Component({
  selector: 'pos-layout-editor',
  imports: [
    DragDropModule,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
    MatTooltipModule,
  ],
  template: `
    <div class="dr-page">
      <header class="ed__header">
        <h1>Raspored stolova</h1>
        <span class="dr-toolbar-spacer"></span>

        @if (rooms().length) {
          <mat-form-field appearance="outline" class="ed__room-select">
            <mat-label>Prostorija</mat-label>
            <mat-select [ngModel]="roomId()" (ngModelChange)="selectRoom($event)">
              @for (room of rooms(); track room.id) {
                <mat-option [value]="room.id">{{ room.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }

        <button mat-stroked-button (click)="addRoom()">
          <mat-icon>add</mat-icon>
          Nova prostorija
        </button>

        @if (room(); as current) {
          <button mat-stroked-button color="warn" (click)="removeRoom(current)">
            <mat-icon>delete</mat-icon>
            Obriši prostoriju
          </button>
        }

        <button mat-flat-button [disabled]="!dirty() || !room()" (click)="save()">
          <mat-icon>save</mat-icon>
          Sačuvaj raspored
        </button>
      </header>

      @if (!rooms().length) {
        <div class="dr-empty">
          <p>Nema nijedne prostorije. Napravite prvu da biste rasporedili stolove.</p>
        </div>
      } @else if (room(); as current) {
        <div class="ed__layout">
          <div
            #canvas
            class="ed__canvas"
            [style.aspect-ratio]="current.canvasWidth + ' / ' + current.canvasHeight"
          >
            @for (item of placed(); track item.table.id) {
              <div
                cdkDrag
                cdkDragBoundary=".ed__canvas"
                (cdkDragEnded)="moved(item, $event)"
                class="ed__table"
                [class.ed__table--round]="item.shape === TableShape.Round"
                [class.ed__table--selected]="selectedId() === item.table.id"
                [style.left.%]="percent(item.x, current.canvasWidth)"
                [style.top.%]="percent(item.y, current.canvasHeight)"
                [style.width.%]="percent(item.width, current.canvasWidth)"
                [style.height.%]="percent(item.height, current.canvasHeight)"
                (click)="selectedId.set(item.table.id)"
              >
                <span class="ed__number">{{ item.table.tableNumber }}</span>
                <span class="ed__seats">{{ item.table.capacity }}</span>
              </div>
            }
          </div>

          <aside class="ed__side">
            <mat-card>
              <mat-card-header>
                <mat-card-title>Odabrani sto</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                @if (selected(); as item) {
                  <p><strong>Sto {{ item.table.tableNumber }}</strong> — {{ item.table.capacity }} mesta</p>

                  <mat-button-toggle-group
                    [value]="item.shape"
                    (change)="setShape(item, $any($event).value)"
                  >
                    <mat-button-toggle [value]="TableShape.Round">Okrugao</mat-button-toggle>
                    <mat-button-toggle [value]="TableShape.Square">Kvadratni</mat-button-toggle>
                    <mat-button-toggle [value]="TableShape.Rectangle">Pravougaoni</mat-button-toggle>
                  </mat-button-toggle-group>

                  <label class="ed__slider-label">Širina: {{ item.width }}</label>
                  <mat-slider min="40" max="300" step="10" discrete>
                    <input matSliderThumb [value]="item.width" (valueChange)="setWidth(item, $event)" />
                  </mat-slider>

                  <label class="ed__slider-label">Visina: {{ item.height }}</label>
                  <mat-slider min="40" max="300" step="10" discrete>
                    <input
                      matSliderThumb
                      [value]="item.height"
                      (valueChange)="setHeight(item, $event)"
                    />
                  </mat-slider>

                  <button mat-stroked-button color="warn" (click)="unplace(item)">
                    <mat-icon>logout</mat-icon>
                    Izbaci iz prostorije
                  </button>
                } @else {
                  <p class="dr-muted">Kliknite na sto da mu promenite oblik ili veličinu.</p>
                }
              </mat-card-content>
            </mat-card>

            <mat-card>
              <mat-card-header>
                <mat-card-title>Neraspoređeni ({{ unplaced().length }})</mat-card-title>
                <mat-card-subtitle>Stolovi koji nisu ni u jednoj prostoriji</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                @for (table of unplaced(); track table.id) {
                  <button mat-stroked-button class="ed__unplaced" (click)="place(table)">
                    Sto {{ table.tableNumber }} ({{ table.capacity }})
                    <mat-icon>add_circle_outline</mat-icon>
                  </button>
                }

                <div class="ed__new-table">
                  <mat-form-field appearance="outline">
                    <mat-label>Broj</mat-label>
                    <input matInput type="number" min="1" [(ngModel)]="newNumber" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Mesta</mat-label>
                    <input matInput type="number" min="1" [(ngModel)]="newCapacity" />
                  </mat-form-field>
                  <button mat-flat-button (click)="createTable()">Nov sto</button>
                </div>
              </mat-card-content>
            </mat-card>
          </aside>
        </div>

        @if (dirty()) {
          <p class="ed__dirty">
            <mat-icon inline>edit</mat-icon>
            Imate nesačuvane izmene.
          </p>
        }
      }
    </div>
  `,
  styles: `
    .ed__header {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .ed__room-select {
      width: 200px;
    }

    .ed__layout {
      display: grid;
      grid-template-columns: 1fr minmax(280px, 340px);
      gap: var(--dr-gap);
      align-items: start;
    }

    .ed__canvas {
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

    .ed__table {
      position: absolute;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      border: 2px solid var(--mat-sys-primary);
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      border-radius: 8px;
      cursor: move;
      user-select: none;
    }

    .ed__table--round {
      border-radius: 50%;
    }

    .ed__table--selected {
      outline: 3px solid var(--dr-reserved);
      outline-offset: 2px;
    }

    .ed__number {
      font-weight: 700;
      font-size: clamp(0.8rem, 1.4vw, 1.2rem);
    }

    .ed__seats {
      font-size: 0.65rem;
      opacity: 0.8;
    }

    .ed__side {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .ed__side mat-button-toggle-group {
      width: 100%;
      margin-bottom: 12px;
    }

    .ed__slider-label {
      display: block;
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant);
    }

    mat-slider {
      width: 100%;
    }

    .ed__unplaced {
      width: 100%;
      margin-bottom: 6px;
      justify-content: space-between;
    }

    .ed__new-table {
      display: flex;
      gap: 8px;
      align-items: center;
      margin-top: 12px;
    }

    .ed__new-table mat-form-field {
      width: 90px;
    }

    .ed__dirty {
      margin-top: 12px;
      color: var(--dr-reserved);
    }

    @media (max-width: 1000px) {
      .ed__layout {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class LayoutEditorPage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly canvas = viewChild<ElementRef<HTMLElement>>('canvas');

  protected readonly TableShape = TableShape;

  protected readonly plan = signal<FloorPlanDto | null>(null);
  protected readonly roomId = signal<string | null>(null);
  protected readonly placed = signal<Placed[]>([]);
  protected readonly unplaced = signal<FloorPlanTableDto[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly dirty = signal(false);

  protected newNumber = 1;
  protected newCapacity = 4;

  protected readonly rooms = computed<RoomDto[]>(() => this.plan()?.rooms ?? []);

  protected readonly room = computed<RoomDto | null>(
    () => this.rooms().find((candidate) => candidate.id === this.roomId()) ?? null,
  );

  protected readonly selected = computed<Placed | null>(
    () => this.placed().find((item) => item.table.id === this.selectedId()) ?? null,
  );

  constructor() {
    this.load();
  }

  protected percent(value: number, extent: number): number {
    return extent > 0 ? (value / extent) * 100 : 0;
  }

  protected selectRoom(id: string): void {
    if (!this.dirty()) {
      this.openRoom(id);
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Nesačuvane izmene',
      message: 'Raspored ove prostorije nije sačuvan. Ako pređete na drugu, izmene se gube.',
      confirmText: 'Napusti izmene',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (confirmed) {
          this.openRoom(id);
        }
      });
  }

  private openRoom(id: string): void {
    this.roomId.set(id);
    this.reset();
  }

  /**
   * Converts a drag, which the CDK reports in screen pixels, back into room coordinates.
   *
   * The canvas is scaled to whatever width the viewport gives it, so the ratio between the two is
   * what makes a layout arranged on a desktop land in the same place on a tablet.
   */
  protected moved(item: Placed, event: CdkDragEnd): void {
    const room = this.room();
    const element = this.canvas()?.nativeElement;

    if (!room || !element) {
      return;
    }

    const scale = element.clientWidth / room.canvasWidth;
    const shift = event.distance;

    const x = Math.max(0, Math.min(room.canvasWidth - item.width, item.x + shift.x / scale));
    const y = Math.max(0, Math.min(room.canvasHeight - item.height, item.y + shift.y / scale));

    // The CDK leaves the element transformed where it was dropped; positions are stored on the model
    // instead, so the transform is reset and the element re-rendered from its new coordinates.
    event.source.reset();

    this.update(item, { x: Math.round(x), y: Math.round(y) });
    this.selectedId.set(item.table.id);
  }

  protected setShape(item: Placed, shape: TableShape): void {
    this.update(item, { shape });
  }

  protected setWidth(item: Placed, width: number): void {
    this.update(item, { width: this.fit(width, item.x, this.room()?.canvasWidth ?? 0) });
  }

  protected setHeight(item: Placed, height: number): void {
    this.update(item, { height: this.fit(height, item.y, this.room()?.canvasHeight ?? 0) });
  }

  protected place(table: FloorPlanTableDto): void {
    this.placed.update((items) => [
      ...items,
      { table, x: 40, y: 40, width: 80, height: 80, shape: TableShape.Round },
    ]);
    this.unplaced.update((items) => items.filter((candidate) => candidate.id !== table.id));
    this.selectedId.set(table.id);
    this.dirty.set(true);
  }

  protected unplace(item: Placed): void {
    this.placed.update((items) => items.filter((candidate) => candidate.table.id !== item.table.id));
    this.unplaced.update((items) => [...items, item.table]);
    this.selectedId.set(null);
    this.dirty.set(true);
  }

  protected addRoom(): void {
    const data: PromptDialogData = {
      title: 'Nova prostorija',
      label: 'Naziv',
      placeholder: 'npr. Bašta',
      hint: 'Ime pod kojim će stajati u tabovima sale.',
      confirmText: 'Napravi',
    };

    this.dialog
      .open(PromptDialog, { data })
      .afterClosed()
      .subscribe((name: string | undefined) => {
        if (name) {
          this.api.createRoom({ name }).subscribe(() => this.load());
        }
      });
  }

  protected removeRoom(room: RoomDto): void {
    const data: ConfirmDialogData = {
      title: `Obrisati „${room.name}“?`,
      message:
        'Stolovi ostaju — samo prestaju da pripadaju ovoj prostoriji i mogu se rasporediti drugde. '
        + 'Prostorija sa otvorenim računima se ne briše.',
      confirmText: 'Obriši prostoriju',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }

        this.api.deleteRoom(room.id).subscribe(() => {
          this.roomId.set(null);
          this.load();
        });
      });
  }

  protected createTable(): void {
    this.api
      .createTable({ tableNumber: this.newNumber, capacity: this.newCapacity })
      .subscribe(() => {
        this.newNumber += 1;
        this.load();
      });
  }

  protected save(): void {
    const room = this.room();

    if (!room) {
      return;
    }

    const tables: TableLayoutRequest[] = this.placed().map((item) => ({
      tableId: item.table.id,
      positionX: item.x,
      positionY: item.y,
      width: item.width,
      height: item.height,
      shape: item.shape,
      rotation: item.table.rotation,
    }));

    this.api.saveRoomLayout(room.id, tables).subscribe(() => {
      this.snackBar.open('Raspored je sačuvan.', 'U redu', { duration: 4000 });
      this.dirty.set(false);
      this.load();
    });
  }

  private update(item: Placed, changes: Partial<Placed>): void {
    this.placed.update((items) =>
      items.map((candidate) =>
        candidate.table.id === item.table.id ? { ...candidate, ...changes } : candidate,
      ),
    );
    this.dirty.set(true);
  }

  /** Keeps a resize inside the room; the API refuses a table that would hang over the edge. */
  private fit(size: number, position: number, extent: number): number {
    return extent > 0 ? Math.min(size, extent - position) : size;
  }

  private load(): void {
    // Inactive tables are included: the editor has to be able to see and move them, even though the
    // floor screen does not show them.
    this.api.floorPlan(true).subscribe((plan) => {
      this.plan.set(plan);

      if (!this.roomId() && plan.rooms.length) {
        this.roomId.set(plan.rooms[0].id);
      }

      this.reset();

      const highest = [...plan.rooms.flatMap((r) => r.tables), ...plan.unplacedTables]
        .map((table) => table.tableNumber)
        .reduce((max, number) => Math.max(max, number), 0);

      this.newNumber = highest + 1;
    });
  }

  private reset(): void {
    const room = this.room();
    const plan = this.plan();

    this.placed.set(
      (room?.tables ?? []).map((table) => ({
        table,
        x: table.positionX,
        y: table.positionY,
        width: table.width,
        height: table.height,
        shape: table.shape,
      })),
    );

    // Tables sitting in other rooms are not offered here: moving one between rooms means taking it
    // out of the first, which is a separate save.
    this.unplaced.set(plan?.unplacedTables ?? []);
    this.selectedId.set(null);
    this.dirty.set(false);
  }
}
