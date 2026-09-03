import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  AuthService,
  LicenseStatusDto,
  ThemeService,
  TillApiService,
  UserRole,
  daysLabel,
  userRoleLabels,
} from 'shared';
import { RealtimeService } from 'shared/realtime';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles: UserRole[];
}

@Component({
  selector: 'pos-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
  ],
  template: `
    <mat-toolbar class="shell__bar dr-no-print">
      <span class="shell__brand">{{ venue() }}</span>

      <nav class="shell__nav">
        @for (item of visibleNav(); track item.path) {
          <a
            mat-button
            [routerLink]="item.path"
            routerLinkActive="shell__link--active"
            [matTooltip]="item.label"
          >
            <mat-icon>{{ item.icon }}</mat-icon>
            <span class="shell__link-text">{{ item.label }}</span>
          </a>
        }
      </nav>

      <span class="dr-toolbar-spacer"></span>

      @if (realtime.connected()) {
        <mat-icon class="shell__live" matTooltip="Uživo povezano">wifi</mat-icon>
      } @else {
        <mat-icon class="shell__offline" matTooltip="Nema veze uživo — osvežite ručno">
          wifi_off
        </mat-icon>
      }

      <button mat-button [matMenuTriggerFor]="account">
        <mat-icon>account_circle</mat-icon>
        <span class="shell__link-text">{{ auth.displayName() }}</span>
      </button>

      <mat-menu #account="matMenu">
        <div class="shell__account">
          <strong>{{ auth.displayName() }}</strong>
          <span class="dr-muted">{{ roleLabel() }}</span>
          <span class="dr-muted">{{ auth.restaurantSlug() }}</span>
        </div>
        <button mat-menu-item (click)="auth.logout()">
          <mat-icon>logout</mat-icon>
          Odjavi se
        </button>
      </mat-menu>
    </mat-toolbar>

    @if (license(); as status) {
      @if (status.isExpiringSoon) {
        <div class="shell__banner dr-no-print" role="status">
          <mat-icon>schedule</mat-icon>
          Licenca ističe za {{ status.daysRemaining }} {{ daysLabel(status.daysRemaining) }}.
          Obratite se administratoru platforme.
        </div>
      }
    }

    <router-outlet />
  `,
  styles: `
    .shell__bar {
      position: sticky;
      top: 0;
      z-index: 10;
      gap: 8px;
      background: var(--mat-sys-surface);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .shell__brand {
      font-weight: 600;
      letter-spacing: 0.02em;
      margin-right: 8px;
    }

    .shell__nav {
      display: flex;
      gap: 2px;
      overflow-x: auto;
      /*
        Not redundant. Setting overflow-x to anything but visible makes overflow-y compute to auto
        as well, so the bar became a vertical scrolling box too — and a 40 px row of 40 px buttons
        overflows it by a hair. Windows drew the scrollbar for that: two stray triangles sitting in
        the middle of the toolbar on every screen of the till, at every width, beside the last menu
        item, where they read as a broken control. They also ate 15 px of the bar.
      */
      overflow-y: hidden;
    }

    .shell__link--active {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    /* The connection indicator is a 24 px glyph and has to stay one. The toolbar is a flex row, so
       on a tablet it was being squeezed to 18 and drawn distorted — the one element on screen whose
       whole job is to be recognised at a glance, quietly deformed at the width it matters most. */
    .shell__live,
    .shell__offline {
      flex: none;
    }

    .shell__live {
      color: var(--dr-free);
    }

    .shell__offline {
      color: var(--mat-sys-on-surface-variant);
    }

    .shell__account {
      display: flex;
      flex-direction: column;
      padding: 8px 16px;
      min-width: 200px;
    }

    .shell__banner {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 16px;
      background: var(--dr-reserved-bg);
      color: var(--dr-reserved);
      font-weight: 500;
    }

    /* Labels are dropped before the icons are: on a tablet the icons alone still navigate. */
    @media (max-width: 1100px) {
      .shell__link-text {
        display: none;
      }
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly realtime = inject(RealtimeService);
  private readonly theme = inject(ThemeService);
  private readonly api = inject(TillApiService);

  protected readonly license = signal<LicenseStatusDto | null>(null);

  /**
   * What the top-left corner says.
   *
   * The venue's own name rather than the product's. Whoever is looking at this screen knows what
   * application they are in — they are standing in front of it for a whole shift — and what is
   * worth a permanent corner is which restaurant this till belongs to.
   *
   * Falls back to the slug, which is on the token and therefore known the instant the session is,
   * while the name has to be fetched. Not to the product name: that would put the word being
   * replaced back on screen for exactly as long as one request, which is the flicker this avoids.
   * The slug is what the person typed to get in, so it names the right venue either way.
   */
  protected readonly venue = computed(
    () => this.license()?.restaurantName || this.auth.restaurantSlug(),
  );
  protected readonly daysLabel = daysLabel;

  private readonly nav: NavItem[] = [
    { path: '/sala', label: 'Sala', icon: 'table_restaurant', roles: [UserRole.Waiter, UserRole.Manager, UserRole.Owner] },
    { path: '/racuni', label: 'Računi', icon: 'receipt_long', roles: [UserRole.Waiter, UserRole.Manager, UserRole.Owner] },
    { path: '/rezervacije', label: 'Rezervacije', icon: 'event_available', roles: [UserRole.Waiter, UserRole.Manager, UserRole.Owner] },
    { path: '/smene', label: 'Smene', icon: 'event_note', roles: [UserRole.Manager, UserRole.Owner] },
    { path: '/magacin', label: 'Magacin', icon: 'inventory_2', roles: [UserRole.Manager, UserRole.Owner] },
    { path: '/jelovnik', label: 'Jelovnik', icon: 'restaurant_menu', roles: [UserRole.Manager, UserRole.Owner] },
    { path: '/raspored', label: 'Raspored', icon: 'dashboard_customize', roles: [UserRole.Owner] },
    { path: '/izvestaji', label: 'Izveštaji', icon: 'insights', roles: [UserRole.Owner] },
    { path: '/zaposleni', label: 'Zaposleni', icon: 'badge', roles: [UserRole.Owner] },
    { path: '/podesavanja', label: 'Podešavanja', icon: 'palette', roles: [UserRole.Owner] },
  ];

  protected readonly visibleNav = computed(() => {
    const role = this.auth.role();

    return role === null ? [] : this.nav.filter((item) => item.roles.includes(role));
  });

  protected readonly roleLabel = computed(() => {
    const role = this.auth.role();

    return role === null ? '' : userRoleLabels[role];
  });

  constructor() {
    void this.realtime.start();

    // Whatever this device guessed at start-up, the venue's own answer settles it. Failure is
    // deliberately silent: the guess is already on screen and a colour is not worth a snackbar.
    this.theme.load().subscribe({ error: () => undefined });

    // The banner is the only warning an owner gets before the till stops working, so it is fetched
    // once on entry rather than being left to whichever screen happens to ask.
    this.api.licenseStatus().subscribe({
      next: (status) => this.license.set(status),
      error: () => this.license.set(null),
    });
  }
}
