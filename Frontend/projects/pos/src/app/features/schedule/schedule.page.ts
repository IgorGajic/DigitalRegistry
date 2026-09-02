import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  LoadingState,
  ScheduledShiftDto,
  ShiftAssignmentDto,
  ShiftTemplateDto,
  StaffMemberDto,
  TillApiService,
  UserRole,
  WeekDays,
  WeeklyScheduleDto,
  addDays,
  assignmentsLabel,
  describeDays,
  shortTime,
  startOfWeek,
  toDateOnly,
  weekDayOrder,
} from 'shared';
import {
  ConfirmDialog,
  ConfirmDialogData,
} from 'shared/ui';

/**
 * The rota: named shifts, who works them, and turning that into an actual schedule.
 *
 * The three are deliberately separate steps. A manager sketching next month should not be writing
 * hundreds of rows they may still change their mind about, so nothing appears on the schedule until
 * it is generated.
 */
@Component({
  selector: 'pos-schedule',
  providers: [provideNativeDateAdapter()],
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatChipsModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <h1>Smene</h1>

      <mat-tab-group animationDuration="120ms">
        <mat-tab label="Nedeljni raspored">
          <mat-card class="sched__panel">
            <div class="sched__weeknav">
              <button mat-icon-button (click)="shiftWeek(-7)" aria-label="Prethodna nedelja">
                <mat-icon>chevron_left</mat-icon>
              </button>
              <strong>
                {{ week()?.weekStart | date: 'dd.MM.' }} –
                {{ lastDay() | date: 'dd.MM.yyyy.' }}
              </strong>
              <button mat-icon-button (click)="shiftWeek(7)" aria-label="Sledeća nedelja">
                <mat-icon>chevron_right</mat-icon>
              </button>
              <span class="dr-toolbar-spacer"></span>
              <button mat-button (click)="shiftWeek(0)">Ova nedelja</button>
            </div>

            @if (week(); as grid) {
              <div class="sched__scroll">
              <div class="sched__grid" [style.--columns]="grid.days.length">
                <div class="sched__cell sched__cell--head">Konobar</div>
                @for (day of grid.days; track day) {
                  <div class="sched__cell sched__cell--head">
                    {{ dayLabel(day) }}<br /><span class="dr-muted">{{ day | date: 'dd.MM.' }}</span>
                  </div>
                }
                <div class="sched__cell sched__cell--head dr-numeric">Sati</div>

                @for (row of grid.waiters; track row.waiterId) {
                  <div class="sched__cell sched__name">{{ row.waiterName }}</div>
                  @for (day of grid.days; track day) {
                    <div class="sched__cell">
                      @for (shift of shiftsOn(row.shifts, day); track shift.id) {
                        <span
                          class="sched__shift"
                          [class.sched__shift--adhoc]="!shift.isGenerated"
                          [matTooltip]="shiftTooltip(shift)"
                        >
                          {{ shift.shiftTemplateName ?? 'Smena' }}
                        </span>
                      }
                    </div>
                  }
                  <div class="sched__cell dr-numeric">{{ row.totalHours | number: '1.0-1' }}</div>
                }
              </div>
              </div>

              @if (grid.waiters.length === 0) {
                <p class="dr-empty">Nema konobara. Dodajte ih na ekranu „Zaposleni“.</p>
              }
            }
          </mat-card>

          <mat-card class="sched__panel">
            <mat-card-header>
              <mat-card-title>Generisanje rasporeda</mat-card-title>
              <mat-card-subtitle>
                Pravi konkretne smene iz stalnih dodela. Bezbedno je pokrenuti više puta — ono što
                već postoji se ne duplira.
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <div class="sched__fields">
                <mat-form-field appearance="outline">
                  <mat-label>Od</mat-label>
                  <input matInput [matDatepicker]="fromPicker" [(ngModel)]="generateFrom" />
                  <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
                  <mat-datepicker #fromPicker />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Do</mat-label>
                  <input matInput [matDatepicker]="toPicker" [(ngModel)]="generateTo" />
                  <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
                  <mat-datepicker #toPicker />
                </mat-form-field>

                <button mat-flat-button (click)="generate()">
                  <mat-icon>auto_awesome</mat-icon>
                  Generiši
                </button>
              </div>

              @if (conflicts().length) {
                <div class="sched__conflicts">
                  <h3>
                    <mat-icon inline>warning</mat-icon>
                    Nije upisano ({{ conflicts().length }})
                  </h3>
                  <ul>
                    @for (conflict of conflicts(); track $index) {
                      <li>
                        {{ conflict.date | date: 'dd.MM.' }} — {{ conflict.waiterName }},
                        {{ conflict.shiftTemplateName }}: {{ conflict.reason }}
                      </li>
                    }
                  </ul>
                </div>
              }
            </mat-card-content>
          </mat-card>
        </mat-tab>

        <mat-tab label="Stalne dodele">
          <mat-card class="sched__panel">
            <mat-card-header>
              <mat-card-title>Nova dodela</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="sched__fields">
                <mat-form-field appearance="outline">
                  <mat-label>Konobar</mat-label>
                  <mat-select [(ngModel)]="waiterId">
                    @for (member of waiters(); track member.id) {
                      <mat-option [value]="member.id">{{ member.fullName }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Smena</mat-label>
                  <mat-select [(ngModel)]="templateId">
                    @for (template of templates(); track template.id) {
                      <mat-option [value]="template.id">
                        {{ template.name }} ({{ time(template.startTime) }}–{{ time(template.endTime) }})
                      </mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Važi od</mat-label>
                  <input matInput [matDatepicker]="validFromPicker" [(ngModel)]="validFrom" />
                  <mat-datepicker-toggle matIconSuffix [for]="validFromPicker" />
                  <mat-datepicker #validFromPicker />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Važi do</mat-label>
                  <input matInput [matDatepicker]="validToPicker" [(ngModel)]="validTo" />
                  <mat-datepicker-toggle matIconSuffix [for]="validToPicker" />
                  <mat-datepicker #validToPicker />
                  <mat-hint>Ostavite prazno za dodelu bez roka</mat-hint>
                </mat-form-field>
              </div>

              <div class="sched__days">
                <span class="dr-muted">Dani:</span>
                @for (day of weekDayOrder; track day.flag) {
                  <button
                    mat-stroked-button
                    type="button"
                    [class.sched__day--on]="hasDay(day.flag)"
                    (click)="toggleDay(day.flag)"
                  >
                    {{ day.short }}
                  </button>
                }
                <button mat-button (click)="days.set(WeekDays.Weekdays)">Pon–Pet</button>
                <button mat-button (click)="days.set(WeekDays.All)">Svaki dan</button>
              </div>

              <button
                mat-flat-button
                class="sched__save"
                [disabled]="!waiterId || !templateId || days() === WeekDays.None"
                (click)="saveAssignment()"
              >
                Sačuvaj dodelu
              </button>
            </mat-card-content>
          </mat-card>

          <mat-card class="sched__panel">
            @if (assignments().length === 0) {
              <p class="dr-empty">Nema stalnih dodela.</p>
            } @else {
              <ul class="sched__assignments">
                @for (assignment of assignments(); track assignment.id) {
                  <li>
                    <div>
                      <strong>{{ assignment.waiterName }}</strong>
                      <span class="dr-muted">
                        {{ assignment.shiftTemplateName }}
                        ({{ time(assignment.startTime) }}–{{ time(assignment.endTime) }})
                      </span>
                    </div>
                    <mat-chip-set>
                      <mat-chip>{{ dayText(assignment.days) }}</mat-chip>
                      <mat-chip>
                        {{ assignment.validFrom | date: 'dd.MM.yy' }}
                        @if (assignment.validTo) {
                          – {{ assignment.validTo | date: 'dd.MM.yy' }}
                        } @else {
                          – bez roka
                        }
                      </mat-chip>
                    </mat-chip-set>
                    <button
                      mat-icon-button
                      color="warn"
                      (click)="removeAssignment(assignment)"
                      matTooltip="Ukini dodelu"
                    >
                      <mat-icon>delete</mat-icon>
                    </button>
                  </li>
                }
              </ul>
            }
          </mat-card>
        </mat-tab>

        <mat-tab label="Šabloni smena">
          <mat-card class="sched__panel">
            <mat-card-header>
              <mat-card-title>
                {{ editingTemplate() ? 'Izmena šablona' : 'Nov šablon' }}
              </mat-card-title>
              @if (editingTemplate()) {
                <mat-card-subtitle>
                  Izmena važi za smene koje se tek generišu. Već upisane smene zadržavaju vremena sa
                  kojima su napravljene.
                </mat-card-subtitle>
              }
            </mat-card-header>

            <mat-card-content>
              <div class="sched__fields">
                <mat-form-field appearance="outline">
                  <mat-label>Naziv</mat-label>
                  <input matInput [(ngModel)]="templateName" placeholder="npr. I smena" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Početak</mat-label>
                  <input matInput type="time" [(ngModel)]="templateStart" />
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Kraj</mat-label>
                  <input matInput type="time" [(ngModel)]="templateEnd" />
                  <mat-hint>Kraj pre početka znači da smena ide preko ponoći</mat-hint>
                </mat-form-field>

                <button mat-flat-button [disabled]="!templateName.trim()" (click)="saveTemplate()">
                  {{ editingTemplate() ? 'Sačuvaj izmenu' : 'Napravi šablon' }}
                </button>

                @if (editingTemplate()) {
                  <button mat-button (click)="newTemplate()">Odustani</button>
                }
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="sched__panel">
            <div class="sched__weeknav">
              <span class="dr-muted">Šabloni po kojima se generiše raspored</span>
              <span class="dr-toolbar-spacer"></span>
              <mat-slide-toggle [(ngModel)]="includeRetired" (change)="loadTemplates()">
                Prikaži povučene
              </mat-slide-toggle>
            </div>

            @if (templates().length === 0) {
              <p class="dr-empty">
                Nema nijednog šablona. Napravite prvi da biste dodeljivali smene.
              </p>
            } @else {
              <ul class="sched__templates">
                @for (template of templates(); track template.id) {
                  <li [class.sched__template--off]="!template.isActive">
                    <strong>{{ template.name }}</strong>
                    <span>{{ time(template.startTime) }}–{{ time(template.endTime) }}</span>
                    <span class="dr-muted">{{ template.durationHours }} h</span>
                    @if (template.crossesMidnight) {
                      <mat-chip>preko ponoći</mat-chip>
                    }
                    @if (!template.isActive) {
                      <mat-chip>povučen</mat-chip>
                    }
                    <span class="dr-muted">
                      {{ template.assignmentCount }} {{ assignmentsLabel(template.assignmentCount) }}
                    </span>

                    <span class="dr-toolbar-spacer"></span>

                    <button
                      mat-icon-button
                      (click)="editTemplate(template)"
                      matTooltip="Izmeni šablon"
                      aria-label="Izmeni šablon"
                    >
                      <mat-icon>edit</mat-icon>
                    </button>

                    @if (template.isActive) {
                      <button
                        mat-icon-button
                        color="warn"
                        (click)="setTemplateActive(template, false)"
                        matTooltip="Povuci iz upotrebe"
                        aria-label="Povuci iz upotrebe"
                      >
                        <mat-icon>block</mat-icon>
                      </button>
                    } @else {
                      <button
                        mat-icon-button
                        (click)="setTemplateActive(template, true)"
                        matTooltip="Vrati u upotrebu"
                        aria-label="Vrati u upotrebu"
                      >
                        <mat-icon>restart_alt</mat-icon>
                      </button>
                    }
                  </li>
                }
              </ul>
            }
          </mat-card>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    h1 {
      margin: 0 0 16px;
      font-size: 1.5rem;
    }

    .sched__panel {
      margin-top: 12px;
    }

    .sched__weeknav {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 4px 16px;
    }

    .sched__grid {
      display: grid;
      grid-template-columns: minmax(140px, 1fr) repeat(var(--columns), 1fr) 70px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      overflow: hidden;
    }

    .sched__cell {
      padding: 8px;
      border-right: 1px solid var(--mat-sys-outline-variant);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      min-height: 44px;
      font-size: 0.875rem;
    }

    .sched__cell--head {
      background: var(--mat-sys-surface-container);
      font-weight: 600;
      text-align: center;
    }

    .sched__name {
      font-weight: 500;
    }

    .sched__shift {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font-size: 0.75rem;
      white-space: nowrap;
    }

    .sched__shift--adhoc {
      background: var(--dr-reserved-bg);
      color: var(--dr-reserved);
    }

    .sched__fields {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      align-items: flex-start;
    }

    .sched__fields mat-form-field {
      min-width: 180px;
      flex: 1;
    }

    .sched__days {
      display: flex;
      gap: 6px;
      align-items: center;
      flex-wrap: wrap;
      margin: 8px 0 16px;
    }

    .sched__day--on {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .sched__assignments,
    .sched__templates {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .sched__assignments li {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 4px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .sched__assignments li > div {
      display: flex;
      flex-direction: column;
      min-width: 200px;
    }

    .sched__templates li {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 4px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    /* A retired template stays on the list: assignments already made from it still refer to it. */
    .sched__template--off {
      opacity: 0.55;
    }

    .sched__conflicts {
      margin-top: 16px;
      padding: 12px;
      border-radius: var(--dr-radius);
      background: var(--dr-reserved-bg);
      color: var(--dr-reserved);
    }

    .sched__conflicts h3 {
      margin: 0 0 6px;
      font-size: 0.95rem;
    }

    .sched__conflicts ul {
      margin: 0;
      padding-left: 20px;
    }

    /* The rota is waiters × days and cannot stack: a shift out of its day column means nothing.
       So this one scrolls sideways, and the scroll sits on the grid rather than on the page. */
    .sched__scroll {
      @include rt.scroll-below(760px);
    }

    @media (max-width: 900px) {
      .sched__fields mat-form-field {
        min-width: 0;
        flex: 1 1 100%;
      }

      .sched__assignments li,
      .sched__templates li {
        flex-wrap: wrap;
      }
    }
  `,
})
export class SchedulePage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly loading = new LoadingState();
  protected readonly WeekDays = WeekDays;
  protected readonly assignmentsLabel = assignmentsLabel;
  protected readonly weekDayOrder = weekDayOrder;

  protected readonly week = signal<WeeklyScheduleDto | null>(null);
  protected readonly templates = signal<ShiftTemplateDto[]>([]);
  protected readonly assignments = signal<ShiftAssignmentDto[]>([]);
  protected readonly waiters = signal<StaffMemberDto[]>([]);
  protected readonly conflicts = signal<
    { date: string; waiterName: string; shiftTemplateName: string; reason: string }[]
  >([]);

  protected weekStart = startOfWeek(new Date());

  protected generateFrom = startOfWeek(new Date());
  protected generateTo = addDays(startOfWeek(new Date()), 27);

  protected waiterId = '';
  protected templateId = '';
  protected validFrom: Date = new Date();
  protected validTo: Date | null = null;
  protected readonly days = signal<WeekDays>(WeekDays.Weekdays);

  protected templateName = '';
  protected templateStart = '07:00';
  protected templateEnd = '15:00';

  /** Set while an existing template is being corrected; null while a new one is being written. */
  protected readonly editingTemplate = signal<string | null>(null);
  protected includeRetired = false;

  protected readonly lastDay = computed(() => {
    const grid = this.week();

    return grid ? grid.days[grid.days.length - 1] : null;
  });

  constructor() {
    this.loadWeek();
    this.loadTemplates();
    this.loadAssignments();

    // Only waiters can be assigned a shift, which the API enforces; filtering here keeps the picker
    // from offering managers who would simply be refused.
    this.loading
      .track(this.api.staff())
      .subscribe((staff) => this.waiters.set(staff.filter((m) => m.role === UserRole.Waiter)));
  }

  protected time(timeOnly: string): string {
    return shortTime(timeOnly);
  }

  protected dayText(days: WeekDays): string {
    return describeDays(days);
  }

  protected dayLabel(isoDate: string): string {
    const index = (new Date(isoDate).getDay() + 6) % 7;

    return weekDayOrder[index].short;
  }

  protected shiftsOn(shifts: ScheduledShiftDto[], isoDate: string): ScheduledShiftDto[] {
    return shifts.filter((shift) => shift.date === isoDate);
  }

  protected shiftTooltip(shift: ScheduledShiftDto): string {
    const kind = shift.isGenerated ? 'iz rasporeda' : 'ručno dodata';

    return `${shift.hours} h, ${kind}`;
  }

  protected shiftWeek(days: number): void {
    this.weekStart = days === 0 ? startOfWeek(new Date()) : addDays(this.weekStart, days);
    this.loadWeek();
  }

  protected hasDay(flag: WeekDays): boolean {
    return (this.days() & flag) !== 0;
  }

  protected toggleDay(flag: WeekDays): void {
    this.days.update((current) => (current & flag ? current & ~flag : current | flag));
  }

  protected generate(): void {
    this.loading
      .track(this.api.generateSchedule(toDateOnly(this.generateFrom), toDateOnly(this.generateTo)))
      .subscribe((result) => {
        this.conflicts.set(result.conflicts);
        this.snackBar.open(
          `Kreirano ${result.created}, već postojalo ${result.alreadyPresent}.`,
          'U redu',
          { duration: 6000 },
        );
        this.loadWeek();
      });
  }

  protected saveAssignment(): void {
    this.api
      .saveShiftAssignment({
        waiterId: this.waiterId,
        shiftTemplateId: this.templateId,
        days: this.days(),
        validFrom: toDateOnly(this.validFrom),
        validTo: this.validTo ? toDateOnly(this.validTo) : null,
      })
      .subscribe(() => {
        this.loadAssignments();
        this.loadTemplates();
      });
  }

  /**
   * Ends a standing assignment.
   *
   * Shifts already generated from it survive — the API leaves them behind on purpose — so the
   * confirmation says so. Somebody expecting this to clear next month's rota would otherwise find
   * it still there and assume nothing happened.
   */
  protected removeAssignment(assignment: ShiftAssignmentDto): void {
    const data: ConfirmDialogData = {
      title: 'Ukinuti stalnu dodelu?',
      message:
        `${assignment.waiterName}, ${assignment.shiftTemplateName}, `
        + `${describeDays(assignment.days)}. Već generisane smene ostaju u rasporedu — ukida se `
        + 'samo pravilo po kojem se ubuduće generišu nove.',
      confirmText: 'Ukini dodelu',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }

        this.api.deleteShiftAssignment(assignment.id).subscribe(() => {
          this.loadAssignments();
          this.loadTemplates();
        });
      });
  }

  /**
   * Writes a template, creating one or correcting the one being edited.
   *
   * The API takes the same call for both and keys on the id, so there is one path here rather than
   * two. A template whose hours were mistyped could previously never be corrected: the only way out
   * was a second template beside the wrong one, and the wrong one stayed on the list forever.
   */
  protected saveTemplate(): void {
    const id = this.editingTemplate();
    const existing = id ? this.templates().find((template) => template.id === id) : undefined;

    this.loading
      .track(
        this.api.saveShiftTemplate({
          id,
          name: this.templateName.trim(),
          // The API takes a TimeOnly, which serialises as HH:mm:ss.
          startTime: `${this.templateStart}:00`,
          endTime: `${this.templateEnd}:00`,
          // Editing must not quietly revive a retired template.
          isActive: existing ? existing.isActive : true,
        }),
      )
      .subscribe(() => {
        this.snackBar.open(id ? 'Šablon je izmenjen.' : 'Šablon je napravljen.', 'U redu', {
          duration: 4000,
        });
        this.newTemplate();
        this.loadTemplates();
        this.loadAssignments();
      });
  }

  /** Loads a template into the form above the list. */
  protected editTemplate(template: ShiftTemplateDto): void {
    this.editingTemplate.set(template.id);
    this.templateName = template.name;
    this.templateStart = shortTime(template.startTime);
    this.templateEnd = shortTime(template.endTime);
  }

  /** Clears the form back to writing a new template. */
  protected newTemplate(): void {
    this.editingTemplate.set(null);
    this.templateName = '';
    this.templateStart = '07:00';
    this.templateEnd = '15:00';
  }

  /**
   * Retires a template, or brings it back.
   *
   * There is no delete: a template that shifts were generated from has to stay reachable, or the
   * rota would refer to something that no longer exists. Retiring keeps it out of the pickers while
   * leaving everything built from it intact — which is what "deleting" one actually means here, and
   * the confirmation says so rather than promising removal.
   */
  protected setTemplateActive(template: ShiftTemplateDto, active: boolean): void {
    const apply = () =>
      this.loading
        .track(
          this.api.saveShiftTemplate({
            id: template.id,
            name: template.name,
            startTime: template.startTime,
            endTime: template.endTime,
            isActive: active,
          }),
        )
        .subscribe(() => {
          this.snackBar.open(
            active ? 'Šablon je vraćen u upotrebu.' : 'Šablon je povučen iz upotrebe.',
            'U redu',
            { duration: 4000 },
          );
          this.loadTemplates();
        });

    if (active) {
      apply();
      return;
    }

    const data: ConfirmDialogData = {
      title: `Povući „${template.name}“ iz upotrebe?`,
      message:
        'Šablon se više ne nudi pri dodeli i po njemu se ne generišu nove smene. '
        + `${template.assignmentCount} ${assignmentsLabel(template.assignmentCount)} i sve već `
        + 'generisane smene ostaju netaknute, a šablon se istim potezom vraća u upotrebu.',
      confirmText: 'Povuci šablon',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (confirmed) {
          apply();
        }
      });
  }

  private loadWeek(): void {
    this.loading
      .track(this.api.weeklySchedule(toDateOnly(this.weekStart)))
      .subscribe((grid) => this.week.set(grid));
  }

  protected loadTemplates(): void {
    this.loading
      .track(this.api.shiftTemplates(this.includeRetired))
      .subscribe((rows) => this.templates.set(rows));
  }

  private loadAssignments(): void {
    this.loading.track(this.api.shiftAssignments()).subscribe((rows) => this.assignments.set(rows));
  }
}
