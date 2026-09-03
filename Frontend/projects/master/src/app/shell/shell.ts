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

      <span class="dr-muted shell__who">{{ auth.displayName() }}</span>
      <button mat-icon-button class="shell__out" (click)="auth.logout()" aria-label="Odjavi se">
        <mat-icon>logout</mat-icon>
      </button>
    </mat-toolbar>

    <router-outlet />
  `,
  styles: `
    /* The toolbar is a flex row that does not wrap, so whatever sits last gets pushed off the end
       when the row runs out of room. At 820 px that was the sign-out button — 14 px of it past the
       right edge of the window, on the one control that has to work when nothing else does.
       The name yields instead: it truncates, and disappears entirely on a narrow screen, where the
       one person who can be signed in already knows who they are. */
    .shell__who {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .shell__out {
      flex: none;
    }

    @media (max-width: 900px) {
      .shell__who {
        display: none;
      }
    }

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
