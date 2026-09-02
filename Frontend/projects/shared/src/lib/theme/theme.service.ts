import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { API_BASE_URL } from '../config/tokens';
import { RestaurantSettingsDto } from '../models/dtos';
import { AppTheme } from '../models/enums';

/**
 * What the venue's till is painted in.
 *
 * The theme belongs to the restaurant, not to the person, and that creates the one problem worth
 * describing here: the sign-in screen does not yet know which restaurant it is looking at. Painting
 * it in the venue's colours the moment the venue is known would mean the whole application changing
 * colour a beat after the password is accepted, which reads as a fault rather than as a preference.
 *
 * So the last theme seen on this device is kept and applied before anything is drawn, and the
 * server's answer replaces it as soon as it arrives. On the machine this is built for — one tablet
 * standing in one restaurant — the guess is right every time after the first. When it is wrong it
 * is wrong for about as long as one request takes, and it is never the thing that decides: the
 * answer from {@link load} always wins.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /**
   * The attribute the stylesheets key off.
   *
   * An attribute on the root element rather than a class, so a theme is a state the document is in
   * and cannot accumulate: setting one necessarily unsets the last.
   */
  private static readonly ATTRIBUTE = 'data-dr-theme';

  private static readonly STORAGE_KEY = 'digitalregistry.pos.theme';

  /** How each theme is spelled in the stylesheet. */
  private static readonly NAMES: Record<AppTheme, string> = {
    [AppTheme.Petrol]: 'petrol',
    [AppTheme.Charcoal]: 'charcoal',
    [AppTheme.Forest]: 'forest',
    [AppTheme.Sand]: 'sand',
  };

  readonly current = signal<AppTheme>(AppTheme.Petrol);

  /**
   * Paints in whatever this device saw last, before the first request has been made.
   *
   * Runs at start-up, ahead of the sign-in screen. A stored value that is not a theme — an older
   * build's spelling, or something edited by hand — is discarded rather than trusted.
   */
  restoreCached(): void {
    this.apply(this.cached() ?? AppTheme.Petrol);
  }

  /** Asks the server what this venue is painted in, and repaints if it disagrees. */
  load(): Observable<RestaurantSettingsDto> {
    return this.http
      .get<RestaurantSettingsDto>(`${this.base}/api/settings`)
      .pipe(tap((settings) => this.apply(settings.theme)));
  }

  /** Repaints the venue. Owner only; the API is what enforces that. */
  save(theme: AppTheme): Observable<RestaurantSettingsDto> {
    return this.http
      .put<RestaurantSettingsDto>(`${this.base}/api/settings/theme`, { theme })
      .pipe(tap((settings) => this.apply(settings.theme)));
  }

  /**
   * Paints without saving, for the picker's preview.
   *
   * The owner is choosing a room to work in, not a swatch, so the choice is shown on the actual
   * application. Leaving the store alone means an owner who changes their mind and navigates away
   * is not followed by a theme they never confirmed.
   */
  preview(theme: AppTheme): void {
    this.setAttribute(theme);
    this.current.set(theme);
  }

  /** Puts back whatever is actually stored, after a preview was not taken up. */
  revertPreview(): void {
    this.apply(this.cached() ?? AppTheme.Petrol);
  }

  private apply(theme: AppTheme): void {
    this.setAttribute(theme);
    this.current.set(theme);

    try {
      localStorage.setItem(ThemeService.STORAGE_KEY, String(theme));
    } catch {
      // A browser refusing storage costs the first-paint guess and nothing else.
    }
  }

  private setAttribute(theme: AppTheme): void {
    document.documentElement.setAttribute(ThemeService.ATTRIBUTE, ThemeService.NAMES[theme]);
  }

  private cached(): AppTheme | null {
    try {
      const stored = Number(localStorage.getItem(ThemeService.STORAGE_KEY));

      return stored in ThemeService.NAMES ? (stored as AppTheme) : null;
    } catch {
      return null;
    }
  }
}
