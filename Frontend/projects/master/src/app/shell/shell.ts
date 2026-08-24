import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from 'shared';

@Component({
  selector: 'mstr-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <mat-toolbar class="shell__bar">
      <span class="shell__brand">DigitalRegistry</span>
      <span class="shell__tag">administracija</span>

      <nav class="shell__nav">
        <a mat-button routerLink="/pregled" routerLinkActive="shell__link--active">
          <mat-icon>insights</mat-icon>
          Pregled
        </a>
        <a mat-button routerLink="/restorani" routerLinkActive="shell__link--active">
          <mat-icon>storefront</mat-icon>
          Restorani
        </a>
      </nav>

      <span class="dr-toolbar-spacer"></span>

      <span class="dr-muted">{{ auth.displayName() }}</span>
      <button mat-icon-button (click)="auth.logout()" aria-label="Odjavi se">
        <mat-icon>logout</mat-icon>
      </button>
    </mat-toolbar>

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
    }

    .shell__tag {
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .shell__nav {
      display: flex;
      gap: 2px;
      margin-left: 16px;
    }

    .shell__link--active {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
}
