import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ScheduledShiftDto,
  ShiftAssignmentDto,
  ShiftTemplateDto,
  StaffMemberDto,
  TillApiService,
  UserRole,
  WeekDays,
  WeeklyScheduleDto,
  addDays,
  describeDays,
  shortTime,
  startOfWeek,
  toDateOnly,
  weekDayOrder,
} from 'shared';

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
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
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
                Sačuvaj
              </button>
            </div>

            <ul class="sched__templates">
              @for (template of templates(); track template.id) {
                <li>
                  <strong>{{ template.name }}</strong>
                  <span>{{ time(template.startTime) }}–{{ time(template.endTime) }}</span>
                  <span class="dr-muted">{{ template.durationHours }} h</span>
                  @if (template.crossesMidnight) {
                    <mat-chip>preko ponoći</mat-chip>
                  }
                  <span class="dr-muted">{{ template.assignmentCount }} dodela</span>
                </li>
              }
            </ul>
          </mat-card>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: `
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
  `,
})
export class SchedulePage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly WeekDays = WeekDays;
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
    this.api
      .staff()
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
    this.api
      .generateSchedule(toDateOnly(this.generateFrom), toDateOnly(this.generateTo))
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

  protected removeAssignment(assignment: ShiftAssignmentDto): void {
    this.api.deleteShiftAssignment(assignment.id).subscribe(() => {
      this.loadAssignments();
      this.loadTemplates();
    });
  }

  protected saveTemplate(): void {
    this.api
      .saveShiftTemplate({
        name: this.templateName.trim(),
        // The API takes a TimeOnly, which serialises as HH:mm:ss.
        startTime: `${this.templateStart}:00`,
        endTime: `${this.templateEnd}:00`,
        isActive: true,
      })
      .subscribe(() => {
        this.templateName = '';
        this.loadTemplates();
      });
  }

  private loadWeek(): void {
    this.api.weeklySchedule(toDateOnly(this.weekStart)).subscribe((grid) => this.week.set(grid));
  }

  private loadTemplates(): void {
    this.api.shiftTemplates().subscribe((rows) => this.templates.set(rows));
  }

  private loadAssignments(): void {
    this.api.shiftAssignments().subscribe((rows) => this.assignments.set(rows));
  }
}
