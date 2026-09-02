import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../config/tokens';
import { AppTheme } from '../models/enums';
import { ThemeService } from './theme.service';

/**
 * The theme belongs to the restaurant, but the sign-in screen does not yet know which restaurant it
 * is looking at. What is tested here is the consequence of that: the application paints in the last
 * thing this device saw, and the server's answer is what actually decides.
 */
describe('ThemeService', () => {
  const BASE = 'http://api.test';
  const KEY = 'digitalregistry.pos.theme';

  let service: ThemeService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-dr-theme');

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: BASE },
      ],
    });

    service = TestBed.inject(ThemeService);
    http = TestBed.inject(HttpTestingController);
  });

  function attribute(): string | null {
    return document.documentElement.getAttribute('data-dr-theme');
  }

  it('falls back to the default when this device has seen nothing yet', () => {
    service.restoreCached();

    expect(attribute()).toBe('petrol');
    expect(service.current()).toBe(AppTheme.Petrol);
  });

  it('paints in what this device saw last, before anything has been asked of the server', () => {
    localStorage.setItem(KEY, String(AppTheme.Forest));

    service.restoreCached();

    expect(attribute()).toBe('forest');
  });

  it('ignores a stored value that is not a theme rather than painting in nothing', () => {
    // An older build's spelling, or something edited by hand. Either way it is not a palette that
    // has been checked, and a till drawn in an unknown one is worse than a till drawn in the default.
    localStorage.setItem(KEY, 'charcoal');

    service.restoreCached();

    expect(attribute()).toBe('petrol');
  });

  it('lets the server overrule the guess', () => {
    localStorage.setItem(KEY, String(AppTheme.Sand));
    service.restoreCached();
    expect(attribute()).toBe('sand');

    service.load().subscribe();
    http.expectOne(`${BASE}/api/settings`).flush({ restaurantName: 'Demo', theme: AppTheme.Charcoal });

    expect(attribute()).toBe('charcoal');
    expect(localStorage.getItem(KEY)).toBe(String(AppTheme.Charcoal));
  });

  it('remembers what was saved, so the next start-up guesses right', () => {
    service.save(AppTheme.Forest).subscribe();

    const request = http.expectOne(`${BASE}/api/settings/theme`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ theme: AppTheme.Forest });

    request.flush({ restaurantName: 'Demo', theme: AppTheme.Forest });

    expect(attribute()).toBe('forest');
    expect(localStorage.getItem(KEY)).toBe(String(AppTheme.Forest));
  });

  it('previews without remembering, so an unconfirmed choice does not follow the owner around', () => {
    localStorage.setItem(KEY, String(AppTheme.Petrol));
    service.restoreCached();

    service.preview(AppTheme.Charcoal);

    expect(attribute()).toBe('charcoal');
    expect(localStorage.getItem(KEY)).toBe(String(AppTheme.Petrol));

    service.revertPreview();

    expect(attribute()).toBe('petrol');
  });
});
