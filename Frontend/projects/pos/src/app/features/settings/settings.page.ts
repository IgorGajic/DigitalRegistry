import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  AppTheme,
  LoadingState,
  TableStatus,
  ThemeService,
  appThemeDescriptions,
  appThemeLabels,
  tableStatusLabels,
} from 'shared';

/**
 * Where the owner chooses what the till looks like.
 *
 * Choosing is done by wearing it. Picking a theme repaints the whole application at once rather than
 * filling in a swatch on this page, because a palette is a room to work a shift in and a 40 px
 * square says nothing about that. Nothing is written until it is confirmed, and leaving without
 * confirming puts back what was there.
 *
 * The four table states are shown against the chosen ground, and they are the reason this screen has
 * any content beyond four buttons: they are what the floor screen says everything in, and they are
 * what a change of background can quietly break.
 */
@Component({
  selector: 'pos-settings',
  imports: [MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page set__page">
      <h1>Podešavanja</h1>

      <mat-card>
        <mat-card-header>
          <mat-card-title>Tema</mat-card-title>
          <mat-card-subtitle>
            Boja pozadine cele kase. Važi za sve zaposlene u ovom restoranu.
          </mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <div class="set__themes">
            @for (theme of themes; track theme) {
              <button
                type="button"
                class="set__theme"
                [class.set__theme--chosen]="chosen() === theme"
                (click)="choose(theme)"
                [attr.aria-pressed]="chosen() === theme"
              >
                <span class="set__theme-name">
                  {{ appThemeLabels[theme] }}
                  @if (saved() === theme) {
                    <mat-icon inline class="set__theme-current">check_circle</mat-icon>
                  }
                </span>
                <span class="set__theme-note">{{ appThemeDescriptions[theme] }}</span>
              </button>
            }
          </div>

          <!--
            Not decoration. These four are the whole vocabulary of the floor screen, read from across
            a room, and a change of ground is exactly what can stop them being readable. Shown here
            so the owner is looking at the consequence while they choose.
          -->
          <p class="set__states-label">Kako stolovi izgledaju na ovoj temi</p>

          <div class="set__states">
            @for (state of states; track state) {
              <span
                class="set__state"
                [style.background]="background(state)"
                [style.border-color]="colour(state)"
                [style.color]="colour(state)"
              >
                {{ tableStatusLabels[state] }}
              </span>
            }
          </div>
        </mat-card-content>

        <mat-card-actions align="end">
          <button mat-button [disabled]="!dirty()" (click)="cancel()">Odustani</button>
          <button mat-flat-button [disabled]="!dirty() || loading.active()" (click)="save()">
            Sačuvaj temu
          </button>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: `
    .set__page {
      max-width: 760px;
    }

    h1 {
      margin: 0 0 16px;
      font-size: 1.5rem;
    }

    .set__themes {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 10px;
    }

    .set__theme {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 12px 14px;
      text-align: left;
      font: inherit;
      cursor: pointer;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
    }

    .set__theme--chosen {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .set__theme-name {
      display: flex;
      align-items: center;
      gap: 6px;
      font-family: var(--dr-font-brand);
      font-weight: 600;
    }

    .set__theme-current {
      color: var(--dr-free);
    }

    .set__theme-note {
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .set__states-label {
      margin: 20px 0 8px;
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .set__states {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .set__state {
      padding: 8px 14px;
      border: 2px solid;
      border-radius: var(--dr-radius);
      font-size: 0.85rem;
      font-weight: 600;
    }
  `,
})
export class SettingsPage {
  private readonly themeService = inject(ThemeService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly loading = new LoadingState();

  protected readonly appThemeLabels = appThemeLabels;
  protected readonly appThemeDescriptions = appThemeDescriptions;
  protected readonly tableStatusLabels = tableStatusLabels;

  protected readonly themes = [AppTheme.Petrol, AppTheme.Charcoal, AppTheme.Forest, AppTheme.Sand];

  /** Out of service is left out: the floor screen filters those tables away, so it never appears. */
  protected readonly states = [TableStatus.Available, TableStatus.Occupied, TableStatus.Reserved];

  /** What is stored, as against what is merely being tried on. */
  protected readonly saved = signal<AppTheme>(this.themeService.current());
  protected readonly chosen = signal<AppTheme>(this.themeService.current());

  protected readonly dirty = () => this.chosen() !== this.saved();

  constructor() {
    this.loading.track(this.themeService.load()).subscribe({
      next: (settings) => {
        this.saved.set(settings.theme);
        this.chosen.set(settings.theme);
      },
      // The application is already painted in something; a failed read is not worth a message here.
      error: () => undefined,
    });
  }

  protected choose(theme: AppTheme): void {
    this.chosen.set(theme);
    this.themeService.preview(theme);
  }

  protected cancel(): void {
    this.chosen.set(this.saved());
    this.themeService.revertPreview();
  }

  protected save(): void {
    this.loading.track(this.themeService.save(this.chosen())).subscribe((settings) => {
      this.saved.set(settings.theme);
      this.snackBar.open('Tema je sačuvana.', 'U redu', { duration: 4000 });
    });
  }

  protected colour(status: TableStatus): string {
    switch (status) {
      case TableStatus.Occupied:
        return 'var(--dr-occupied)';
      case TableStatus.Reserved:
        return 'var(--dr-reserved)';
      default:
        return 'var(--dr-free)';
    }
  }

  protected background(status: TableStatus): string {
    switch (status) {
      case TableStatus.Occupied:
        return 'var(--dr-occupied-bg)';
      case TableStatus.Reserved:
        return 'var(--dr-reserved-bg)';
      default:
        return 'var(--dr-free-bg)';
    }
  }
}
