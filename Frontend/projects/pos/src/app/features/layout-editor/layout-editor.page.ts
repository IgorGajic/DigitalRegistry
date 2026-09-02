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
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSliderModule } from '@angular/material/slider';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  FixtureKind,
  FixtureLayoutRequest,
  FixtureShape,
  FixtureTone,
  FloorPlanDto,
  FloorPlanTableDto,
  LoadingState,
  RoomDto,
  TableShape,
  TableLayoutRequest,
  TillApiService,
  fixtureDefaults,
  fixtureKindLabels,
  fixtureToneLabels,
  seatsLabel,
} from 'shared';
import {
  ConfirmDialog,
  ConfirmDialogData,
  PromptDialog,
  PromptDialogData,
} from 'shared/ui';

import { QrSheetDialog, QrSheetDialogData } from './qr-sheet.dialog';
import { RoomDialog, RoomDialogResult } from './room.dialog';
import { TableDialog, TableDialogResult } from './table.dialog';

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
 * A fixture being arranged.
 *
 * `id` is null until the room is saved, so it cannot be the identity the editor tracks by — a newly
 * drawn bar would share `null` with every other new one and Angular would confuse them mid-drag.
 * `key` is a client-side handle that exists for exactly as long as this editing session.
 */
interface PlacedFixture {
  key: string;
  id: string | null;
  kind: FixtureKind;
  label: string;
  shape: FixtureShape;
  tone: FixtureTone;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
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
    MatProgressBarModule,
    MatSelectModule,
    MatSliderModule,
    MatTooltipModule,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" class="dr-no-print" />
    }

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
          <button mat-stroked-button (click)="editRoom(current)">
            <mat-icon>edit</mat-icon>
            Izmeni prostoriju
          </button>

          <button mat-stroked-button color="warn" (click)="removeRoom(current)">
            <mat-icon>delete</mat-icon>
            Obriši prostoriju
          </button>
        }

        <button mat-stroked-button (click)="printQrCodes()" matTooltip="Kodovi koje gosti skeniraju">
          <mat-icon>qr_code_2</mat-icon>
          QR kodovi
        </button>

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
            <!-- Under the tables, always. A bar drawn over a table would hide what it owes. -->
            @for (item of fixtures(); track item.key) {
              <div
                cdkDrag
                cdkDragBoundary=".ed__canvas"
                (cdkDragEnded)="movedFixture(item, $event)"
                class="ed__fixture"
                [class.ed__fixture--round]="item.shape === FixtureShape.Ellipse"
                [class.ed__fixture--selected]="selectedFixtureKey() === item.key"
                [attr.data-tone]="item.tone"
                [style.left.%]="percent(item.x, current.canvasWidth)"
                [style.top.%]="percent(item.y, current.canvasHeight)"
                [style.width.%]="percent(item.width, current.canvasWidth)"
                [style.height.%]="percent(item.height, current.canvasHeight)"
                [style.transform]="'rotate(' + item.rotation + 'deg)'"
                (click)="selectFixture(item.key)"
              >
                <span class="ed__fixture-label">{{ item.label }}</span>
              </div>
            }

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
                (click)="selectTable(item.table.id)"
              >
                <span class="ed__number">{{ item.table.tableNumber }}</span>
                <span class="ed__seats">{{ item.table.capacity }}</span>
              </div>
            }
          </div>

          <aside class="ed__side">
            <mat-card>
              <mat-card-header>
                <mat-card-title>Dodaj element</mat-card-title>
                <mat-card-subtitle>Šank, toalet i ostalo što nije sto</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content class="ed__kinds">
                @for (kind of fixtureKinds; track kind) {
                  <button mat-stroked-button (click)="addFixture(kind)">
                    {{ fixtureKindLabels[kind] }}
                  </button>
                }
              </mat-card-content>
            </mat-card>

            @if (selectedFixture(); as item) {
              <mat-card>
                <mat-card-header>
                  <mat-card-title>Odabrani element</mat-card-title>
                </mat-card-header>
                <mat-card-content>
                  <mat-form-field appearance="outline" class="ed__field">
                    <mat-label>Naziv</mat-label>
                    <input
                      matInput
                      maxlength="30"
                      [ngModel]="item.label"
                      (ngModelChange)="updateFixture(item, { label: $event })"
                      name="fixtureLabel"
                    />
                  </mat-form-field>

                  <mat-form-field appearance="outline" class="ed__field">
                    <mat-label>Boja</mat-label>
                    <mat-select
                      [ngModel]="item.tone"
                      (ngModelChange)="updateFixture(item, { tone: $event })"
                      name="fixtureTone"
                    >
                      @for (tone of fixtureTones; track tone) {
                        <mat-option [value]="tone">{{ fixtureToneLabels[tone] }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-button-toggle-group
                    [value]="item.shape"
                    (change)="updateFixture(item, { shape: $any($event).value })"
                  >
                    <mat-button-toggle [value]="FixtureShape.Rectangle">Pravougaonik</mat-button-toggle>
                    <mat-button-toggle [value]="FixtureShape.Ellipse">Krug</mat-button-toggle>
                  </mat-button-toggle-group>

                  <label class="ed__slider-label">Širina: {{ item.width }}</label>
                  <mat-slider min="20" max="1000" step="10" discrete>
                    <input
                      matSliderThumb
                      [value]="item.width"
                      (valueChange)="updateFixture(item, { width: $event })"
                    />
                  </mat-slider>

                  <label class="ed__slider-label">Visina: {{ item.height }}</label>
                  <mat-slider min="20" max="1000" step="10" discrete>
                    <input
                      matSliderThumb
                      [value]="item.height"
                      (valueChange)="updateFixture(item, { height: $event })"
                    />
                  </mat-slider>

                  @if (item.shape === FixtureShape.Rectangle) {
                    <div class="ed__rotate">
                      <button mat-stroked-button (click)="rotateFixture(item, -45)">
                        <mat-icon>rotate_left</mat-icon>
                        45°
                      </button>
                      <button mat-stroked-button (click)="rotateFixture(item, 45)">
                        <mat-icon>rotate_right</mat-icon>
                        45°
                      </button>
                    </div>

                    @if (item.rotation) {
                      <p class="dr-muted ed__rotation">Zaokrenut {{ item.rotation }}°</p>
                    }
                  }

                  <button mat-stroked-button color="warn" (click)="removeFixture(item)">
                    <mat-icon>delete_forever</mat-icon>
                    Ukloni element
                  </button>
                </mat-card-content>
              </mat-card>
            }

            <mat-card>
              <mat-card-header>
                <mat-card-title>Odabrani sto</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                @if (selected(); as item) {
                  <p>
                    <strong>Sto {{ item.table.tableNumber }}</strong> —
                    {{ item.table.capacity }} {{ seatsLabel(item.table.capacity) }}
                  </p>

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

                  <button mat-stroked-button (click)="editTable(item)">
                    <mat-icon>edit</mat-icon>
                    Broj, mesta, upotreba
                  </button>

                  <button mat-stroked-button (click)="printQrCodes(item.table.id)">
                    <mat-icon>qr_code_2</mat-icon>
                    QR kod ovog stola
                  </button>

                  <button mat-stroked-button (click)="rotateQr(item)">
                    <mat-icon>autorenew</mat-icon>
                    Obnovi QR kod
                  </button>

                  <button mat-stroked-button color="warn" (click)="unplace(item)">
                    <mat-icon>logout</mat-icon>
                    Izbaci iz prostorije
                  </button>

                  <button mat-stroked-button color="warn" (click)="removeTable(item)">
                    <mat-icon>delete_forever</mat-icon>
                    Obriši sto
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

    /* The same quiet treatment the floor screen gives them, plus a handle: here they are dragged,
       there they are scenery. Tones come from the one place they are defined, so the editor cannot
       drift from what a waiter will actually see. */
    .ed__fixture {
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid;
      border-radius: 4px;
      padding: 2px;
      overflow: hidden;
      cursor: move;
      user-select: none;
      color: var(--dr-tone-ink);
    }

    .ed__fixture[data-tone='1'] {
      background: var(--dr-tone-wood);
      border-color: var(--dr-tone-wood-line);
    }

    .ed__fixture[data-tone='2'] {
      background: var(--dr-tone-slate);
      border-color: var(--dr-tone-slate-line);
    }

    .ed__fixture[data-tone='3'] {
      background: var(--dr-tone-stone);
      border-color: var(--dr-tone-stone-line);
    }

    .ed__fixture[data-tone='4'] {
      background: var(--dr-tone-glass);
      border-color: var(--dr-tone-glass-line);
    }

    .ed__fixture--round {
      border-radius: 50%;
    }

    .ed__fixture--selected {
      outline: 2px solid var(--mat-sys-tertiary);
      outline-offset: 2px;
    }

    .ed__fixture-label {
      font-family: var(--dr-font-brand);
      font-size: 0.68rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      text-align: center;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      max-width: 100%;
    }

    /* A wrapping row of one-word buttons, not a column: seven of them stacked would push the
       selected-item panel below the fold on the screen where it is being used.
       The width override has to out-specify the rule further down that stacks the *action* buttons
       on purpose — those are sentences, these are single words. */
    .ed__kinds {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }

    .ed__side mat-card-content.ed__kinds > button {
      width: auto;
      margin-top: 0;
    }

    .ed__field {
      width: 100%;
    }

    /* The two turns sit side by side: they are a pair, and reading one above the other would
       suggest they do different things rather than opposite ones. */
    .ed__rotate {
      display: flex;
      gap: 6px;
      margin-top: 8px;
    }

    .ed__side mat-card-content .ed__rotate > button {
      flex: 1;
      width: auto;
      margin-top: 0;
    }

    .ed__rotation {
      margin: 6px 0 0;
      font-size: 0.8rem;
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
      font-family: var(--dr-font-mono);
      font-weight: 500;
      font-size: clamp(0.8rem, 1.4vw, 1.2rem);
      letter-spacing: -0.03em;
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

    /* The selected table's actions stack: two buttons side by side truncate on a tablet. */
    .ed__side mat-card-content > button {
      width: 100%;
      margin-top: 8px;
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

  protected readonly loading = new LoadingState();
  protected readonly TableShape = TableShape;
  protected readonly seatsLabel = seatsLabel;

  protected readonly plan = signal<FloorPlanDto | null>(null);
  protected readonly roomId = signal<string | null>(null);
  protected readonly placed = signal<Placed[]>([]);
  protected readonly unplaced = signal<FloorPlanTableDto[]>([]);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly fixtures = signal<PlacedFixture[]>([]);

  /**
   * Which fixture is being edited, if any.
   *
   * Selection is exclusive with {@link selectedId}: the side panel shows one thing, and an owner
   * pressing delete needs to be in no doubt which of the two it applies to.
   */
  protected readonly selectedFixtureKey = signal<string | null>(null);
  protected readonly dirty = signal(false);

  /** Printed on every code, so a sheet found loose on a desk says which venue it belongs to. */
  protected readonly venueName = signal('');

  protected newNumber = 1;
  protected newCapacity = 4;

  protected readonly rooms = computed<RoomDto[]>(() => this.plan()?.rooms ?? []);

  protected readonly room = computed<RoomDto | null>(
    () => this.rooms().find((candidate) => candidate.id === this.roomId()) ?? null,
  );

  protected readonly selected = computed<Placed | null>(
    () => this.placed().find((item) => item.table.id === this.selectedId()) ?? null,
  );

  protected readonly selectedFixture = computed<PlacedFixture | null>(
    () => this.fixtures().find((item) => item.key === this.selectedFixtureKey()) ?? null,
  );

  protected readonly fixtureKinds = Object.values(FixtureKind)
    .filter((value): value is FixtureKind => typeof value === 'number');

  protected readonly fixtureTones = Object.values(FixtureTone)
    .filter((value): value is FixtureTone => typeof value === 'number');

  protected readonly FixtureKind = FixtureKind;
  protected readonly FixtureShape = FixtureShape;
  protected readonly fixtureKindLabels = fixtureKindLabels;
  protected readonly fixtureToneLabels = fixtureToneLabels;

  constructor() {
    this.load();

    this.api.licenseStatus().subscribe({
      next: (status) => this.venueName.set(status.restaurantName),
      error: () => this.venueName.set(''),
    });
  }

  protected percent(value: number, extent: number): number {
    return extent > 0 ? (value / extent) * 100 : 0;
  }

  protected selectRoom(id: string): void {
    this.guardUnsaved(
      'Raspored ove prostorije nije sačuvan. Ako pređete na drugu, izmene se gube.',
      () => this.openRoom(id),
    );
  }

  /**
   * Runs something that will re-read the plan, first making sure nothing is lost by it.
   *
   * Every one of these actions ends in `load()`, and `load()` calls `reset()`, which rebuilds the
   * canvas from the server and drops whatever was dragged since the last save. Without this the
   * owner arranges a room, renames it, and silently finds the arrangement back where it started.
   */
  /**
   * The sentence to append when an action that already asks for confirmation will also discard the
   * arrangement. Stacking a second dialog on top of a deletion prompt would only train the owner to
   * dismiss both unread.
   */
  private unsavedNote(): string {
    return this.dirty()
      ? ' Napomena: raspored koji niste sačuvali biće izgubljen.'
      : '';
  }

  private guardUnsaved(message: string, action: () => void): void {
    if (!this.dirty()) {
      action();
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Nesačuvane izmene',
      message,
      confirmText: 'Napusti izmene',
      cancelText: 'Nazad',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (confirmed) {
          action();
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
    this.selectTable(item.table.id);
  }

  /** Same arithmetic as {@link moved}, against the fixture layer. */
  protected movedFixture(item: PlacedFixture, event: CdkDragEnd): void {
    const room = this.room();
    const element = this.canvas()?.nativeElement;

    if (!room || !element) {
      return;
    }

    const scale = element.clientWidth / room.canvasWidth;
    const shift = event.distance;

    const x = Math.max(0, Math.min(room.canvasWidth - item.width, item.x + shift.x / scale));
    const y = Math.max(0, Math.min(room.canvasHeight - item.height, item.y + shift.y / scale));

    event.source.reset();

    this.updateFixture(item, { x: Math.round(x), y: Math.round(y) });
    this.selectFixture(item.key);
  }

  protected selectTable(id: string): void {
    this.selectedId.set(id);
    this.selectedFixtureKey.set(null);
  }

  protected selectFixture(key: string): void {
    this.selectedFixtureKey.set(key);
    this.selectedId.set(null);
  }

  /**
   * Draws a new fixture of the given kind, near the top-left where there is usually room.
   *
   * The kind seeds the label, shape, tone and size; all four are then editable in the side panel.
   * It is placed rather than dragged in because the canvas scales to the viewport and a drop point
   * computed from a palette outside it would land somewhere the owner did not aim.
   */
  protected addFixture(kind: FixtureKind): void {
    const room = this.room();

    if (!room) {
      return;
    }

    const defaults = fixtureDefaults[kind];

    // Stagger, so drawing three in a row does not stack them into one apparent shape.
    const offset = this.fixtures().length % 6;

    const width = Math.min(defaults.width, room.canvasWidth);
    const height = Math.min(defaults.height, room.canvasHeight);

    this.fixtures.update((current) => [
      ...current,
      {
        key: `new-${Date.now()}-${current.length}`,
        id: null,
        kind,
        label: defaults.label,
        shape: defaults.shape,
        tone: defaults.tone,
        x: Math.min(40 + offset * 24, Math.max(0, room.canvasWidth - width)),
        y: Math.min(40 + offset * 24, Math.max(0, room.canvasHeight - height)),
        width,
        height,
        rotation: 0,
      },
    ]);

    this.dirty.set(true);
    this.selectFixture(this.fixtures()[this.fixtures().length - 1].key);
  }

  protected updateFixture(item: PlacedFixture, patch: Partial<PlacedFixture>): void {
    this.fixtures.update((current) =>
      current.map((candidate) => (candidate.key === item.key ? { ...candidate, ...patch } : candidate)),
    );

    this.dirty.set(true);
  }

  /**
   * Turns a fixture by a step, left or right.
   *
   * Offered only on rectangles, because that is the only shape a turn changes: an ellipse of equal
   * sides is a circle, and one of unequal sides is the same ellipse every 180°.
   *
   * Wrapped into 0–359 rather than allowed to run negative or past a full turn. The API accepts
   * that range and nothing else, and "-45" and "315" are the same bar drawn the same way — letting
   * both exist would only give the same arrangement two spellings.
   */
  protected rotateFixture(item: PlacedFixture, degrees: number): void {
    this.updateFixture(item, { rotation: (item.rotation + degrees + 360) % 360 });
  }

  /**
   * Takes a fixture off the plan.
   *
   * No confirmation: nothing is lost that cannot be redrawn in one click, and the removal is not
   * written until the arrangement is saved. Asking here would be the kind of prompt that teaches
   * people to dismiss prompts.
   */
  protected removeFixture(item: PlacedFixture): void {
    this.fixtures.update((current) => current.filter((candidate) => candidate.key !== item.key));
    this.selectedFixtureKey.set(null);
    this.dirty.set(true);
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

  /**
   * A table's own properties: its number, how many it seats, and whether it is in service.
   *
   * Separate from the canvas because they go to a different endpoint. Position and size are part of
   * the room's arrangement and are saved with it; these three belong to the table wherever it sits,
   * and saving them immediately is right — there is no half-finished state to hold on to.
   */
  protected editTable(item: Placed): void {
    const plan = this.plan();
    const taken = [...(plan?.rooms.flatMap((r) => r.tables) ?? []), ...(plan?.unplacedTables ?? [])]
      .filter((table) => table.id !== item.table.id)
      .map((table) => table.tableNumber);

    this.dialog
      .open(TableDialog, { data: { table: item.table, takenNumbers: taken } })
      .afterClosed()
      .subscribe((result: TableDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.loading.track(this.api.updateTable(item.table.id, result)).subscribe(() => {
          this.snackBar.open('Sto je izmenjen.', 'U redu', { duration: 4000 });

          // Patched in place rather than re-read: none of these three touch the arrangement, and
          // reloading here would throw away an unsaved drag for no reason.
          const updated: FloorPlanTableDto = { ...item.table, ...result };

          this.placed.update((items) =>
            items.map((candidate) =>
              candidate.table.id === item.table.id ? { ...candidate, table: updated } : candidate,
            ),
          );
        });
      });
  }

  /**
   * Deletes a table outright.
   *
   * Only ever succeeds for a table that has never been used: the API refuses one with order or
   * reservation history and says to deactivate it instead, which is what the properties dialog is
   * for. The confirmation says as much, so the refusal is expected rather than puzzling.
   */
  protected removeTable(item: Placed): void {
    const data: ConfirmDialogData = {
      title: `Obrisati sto ${item.table.tableNumber}?`,
      message:
        'Briše se samo sto koji nikada nije korišćen. Ako je na njemu bio ijedan račun ili '
        + 'rezervacija, brisanje se odbija — takav sto se isključuje iz upotrebe, da istorija '
        + 'ostane čitljiva.'
        + this.unsavedNote(),
      confirmText: 'Obriši sto',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }

        this.loading.track(this.api.deleteTable(item.table.id)).subscribe({
          next: () => {
            this.snackBar.open('Sto je obrisan.', 'U redu', { duration: 4000 });
            this.selectedId.set(null);
            this.load();
          },
          // A 409 means it has history. The interceptor has said so; this keeps the canvas honest.
          error: () => this.load(),
        });
      });
  }

  /** The room's name, the size of its floor, and where its tab sits. */
  protected editRoom(room: RoomDto): void {
    this.guardUnsaved(
      'Raspored nije sačuvan. Izmena prostorije ponovo učitava salu i nesačuvano se gubi.',
      () => this.openRoomDialog(room),
    );
  }

  private openRoomDialog(room: RoomDto): void {
    this.dialog
      .open(RoomDialog, { data: { room } })
      .afterClosed()
      .subscribe((result: RoomDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.loading.track(this.api.updateRoom(room.id, result)).subscribe(() => {
          this.snackBar.open('Prostorija je izmenjena.', 'U redu', { duration: 4000 });
          this.load();
        });
      });
  }

  protected addRoom(): void {
    this.guardUnsaved(
      'Raspored nije sačuvan. Nova prostorija ponovo učitava salu i nesačuvano se gubi.',
      () => this.openNewRoomPrompt(),
    );
  }

  private openNewRoomPrompt(): void {
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
        + 'Prostorija sa otvorenim računima se ne briše.'
        + this.unsavedNote(),
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

        this.loading.track(this.api.deleteRoom(room.id)).subscribe(() => {
          this.roomId.set(null);
          this.load();
        });
      });
  }

  protected createTable(): void {
    this.guardUnsaved(
      'Raspored nije sačuvan. Novi sto ponovo učitava salu i nesačuvano se gubi.',
      () =>
        this.loading
          .track(this.api.createTable({ tableNumber: this.newNumber, capacity: this.newCapacity }))
          .subscribe(() => {
            this.newNumber += 1;
            this.load();
          }),
    );
  }

  /**
   * Opens the printable sheet of QR codes.
   *
   * Room by room by default, because that is how the sheet is cut up and taped down; passing a table
   * id narrows it to one, for replacing a single code that has been damaged or rotated.
   */
  protected printQrCodes(tableId?: string): void {
    const room = this.room();

    // One named table is printed even when it is out of service — that is a code being replaced,
    // not a new one being handed out.
    this.loading
      .track(this.api.tableQrCodes(tableId ? undefined : (room?.id ?? undefined), tableId !== undefined))
      .subscribe((tables) => {
        const chosen = tableId ? tables.filter((table) => table.tableId === tableId) : tables;

        if (chosen.length === 0) {
          this.snackBar.open('Nema aktivnih stolova za štampu.', 'U redu', { duration: 4000 });
          return;
        }

        const data: QrSheetDialogData = {
          tables: chosen,
          restaurantName: this.venueName(),
        };

        this.dialog.open(QrSheetDialog, { data, width: '900px', maxWidth: '95vw' });
      });
  }

  /**
   * Issues the table a new QR token, and offers the replacement code for printing straight away.
   *
   * Destructive in a way that is easy to miss: every code already printed and taped to that table
   * stops working the moment this returns, so the confirmation says exactly that. The sheet is
   * opened on success because a rotated token with no printed code means a table nobody can order
   * from — the two steps belong together.
   */
  protected rotateQr(item: Placed): void {
    const data: ConfirmDialogData = {
      title: `Obnoviti QR kod za sto ${item.table.tableNumber}?`,
      message:
        'Stari kod prestaje da važi odmah — sve što je odštampano i zalepljeno na taj sto postaje '
        + 'neupotrebljivo. Novi kod se otvara za štampu čim se obnovi.',
      confirmText: 'Obnovi kod',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }

        this.loading.track(this.api.rotateQrCode(item.table.id)).subscribe(() => {
          this.snackBar.open(
            `Sto ${item.table.tableNumber}: kod obnovljen. Odštampajte novi i zamenite stari.`,
            'U redu',
            { duration: 8000 },
          );

          this.printQrCodes(item.table.id);
        });
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

    const fixtures: FixtureLayoutRequest[] = this.fixtures().map((item, index) => ({
      id: item.id,
      kind: item.kind,
      label: item.label.trim() || fixtureDefaults[item.kind].label,
      shape: item.shape,
      tone: item.tone,
      positionX: item.x,
      positionY: item.y,
      width: item.width,
      height: item.height,
      rotation: item.rotation,
      // Drawing order is the order they sit in the list, which is the order they were added.
      displayOrder: index,
    }));

    this.loading.track(this.api.saveRoomLayout(room.id, tables, fixtures)).subscribe(() => {
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
    this.loading.track(this.api.floorPlan(true)).subscribe((plan) => {
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
    this.fixtures.set(
      (room?.fixtures ?? []).map((fixture) => ({
        key: fixture.id,
        id: fixture.id,
        kind: fixture.kind,
        label: fixture.label,
        shape: fixture.shape,
        tone: fixture.tone,
        x: fixture.positionX,
        y: fixture.positionY,
        width: fixture.width,
        height: fixture.height,
        rotation: fixture.rotation,
      })),
    );

    this.unplaced.set(plan?.unplacedTables ?? []);
    this.selectedId.set(null);
    this.selectedFixtureKey.set(null);
    this.dirty.set(false);
  }
}
