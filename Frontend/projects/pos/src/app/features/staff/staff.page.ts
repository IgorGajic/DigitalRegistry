import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ConfirmDialog,
  ConfirmDialogData,
  LoadingState,
  StaffMemberDto,
  TillApiService,
  UserRole,
  userRoleLabels,
} from 'shared';

import { StaffDialog, StaffDialogResult } from './staff.dialog';

/**
 * The venue's own people.
 *
 * Owner only. The owner's account arrives with the restaurant; everybody else is created here, which
 * is what lets a venue have more than one person in it at all.
 */
@Component({
  selector: 'pos-staff',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <header class="staff__header">
        <h1>Zaposleni</h1>
        <span class="dr-toolbar-spacer"></span>
        <mat-slide-toggle [(ngModel)]="includeDisabled" (change)="load()">
          Prikaži ugašene
        </mat-slide-toggle>
        <button mat-flat-button (click)="create()">
          <mat-icon>person_add</mat-icon>
          Novi zaposleni
        </button>
      </header>

      <mat-card>
        <table mat-table [dataSource]="staff()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Ime</th>
            <td mat-cell *matCellDef="let member">
              {{ member.fullName }}
              @if (!member.isEnabled) {
                <mat-icon class="staff__off" matTooltip="Nalog je ugašen">block</mat-icon>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="role">
            <th mat-header-cell *matHeaderCellDef>Uloga</th>
            <td mat-cell *matCellDef="let member">{{ roleLabel(member.role) }}</td>
          </ng-container>

          <ng-container matColumnDef="email">
            <th mat-header-cell *matHeaderCellDef>Email</th>
            <td mat-cell *matCellDef="let member">{{ member.email }}</td>
          </ng-container>

          <ng-container matColumnDef="userName">
            <th mat-header-cell *matHeaderCellDef>Korisničko ime</th>
            <td mat-cell *matCellDef="let member">
              <code>{{ member.userName }}</code>
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let member" class="staff__actions">
              <button mat-icon-button (click)="edit(member)" matTooltip="Izmeni">
                <mat-icon>edit</mat-icon>
              </button>
              <button mat-icon-button (click)="resetPassword(member)" matTooltip="Nova lozinka">
                <mat-icon>key</mat-icon>
              </button>
              @if (member.role !== UserRole.Owner) {
                @if (member.isEnabled) {
                  <button
                    mat-icon-button
                    color="warn"
                    (click)="setEnabled(member, false)"
                    matTooltip="Ugasi nalog"
                  >
                    <mat-icon>person_off</mat-icon>
                  </button>
                } @else {
                  <button mat-icon-button (click)="setEnabled(member, true)" matTooltip="Vrati nalog">
                    <mat-icon>how_to_reg</mat-icon>
                  </button>
                }
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" [class.staff__row--off]="!row.isEnabled"></tr>
        </table>

        @if (staff().length === 0) {
          <p class="dr-empty">Nema zaposlenih za prikaz.</p>
        }
      </mat-card>

      <p class="dr-muted staff__note">
        <mat-icon inline>info</mat-icon>
        Nalozi se gase, ne brišu — ime ostaje na svim računima i smenama koje je ta osoba radila.
      </p>
    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    .staff__header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    table {
      width: 100%;
    }

    .staff__actions {
      white-space: nowrap;
      text-align: right;
    }

    .staff__row--off {
      opacity: 0.55;
    }

    .staff__off {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--mat-sys-on-surface-variant);
    }

    .staff__note {
      margin-top: 16px;
    }

    code {
      font-size: 0.8rem;
    }

    @include rt.labels((
      name: 'Ime',
      role: 'Uloga',
      email: 'Email',
      userName: 'Korisničko ime',
    ));
  `,
})
export class StaffPage {
  private readonly api = inject(TillApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly loading = new LoadingState();
  protected readonly UserRole = UserRole;
  protected readonly columns = ['name', 'role', 'email', 'userName', 'actions'];

  protected readonly staff = signal<StaffMemberDto[]>([]);
  protected includeDisabled = false;

  constructor() {
    this.load();
  }

  /** Template rows arrive untyped from the Material table, so the lookup is done here instead. */
  protected roleLabel(role: UserRole): string {
    return userRoleLabels[role];
  }

  protected load(): void {
    this.loading
      .track(this.api.staff(this.includeDisabled))
      .subscribe((rows) => this.staff.set(rows));
  }

  protected create(): void {
    this.dialog
      .open(StaffDialog, { data: { member: null }, width: '460px' })
      .afterClosed()
      .subscribe((result: StaffDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.api
          .createStaff({
            email: result.email,
            password: result.password!,
            firstName: result.firstName,
            lastName: result.lastName,
            role: result.role,
          })
          .subscribe((member) => {
            // Shown once, because the owner has to pass it on and it is never retrievable again.
            this.snackBar.open(`Nalog kreiran. Prijava: ${member.userName}`, 'U redu', {
              duration: 10000,
            });
            this.load();
          });
      });
  }

  protected edit(member: StaffMemberDto): void {
    this.dialog
      .open(StaffDialog, { data: { member }, width: '460px' })
      .afterClosed()
      .subscribe((result: StaffDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.api
          .updateStaff(member.id, {
            firstName: result.firstName,
            lastName: result.lastName,
            role: result.role,
          })
          .subscribe(() => this.load());
      });
  }

  protected resetPassword(member: StaffMemberDto): void {
    this.dialog
      .open(StaffDialog, { data: { member, passwordOnly: true }, width: '460px' })
      .afterClosed()
      .subscribe((result: StaffDialogResult | undefined) => {
        if (!result?.password) {
          return;
        }

        this.api.resetStaffPassword(member.id, result.password).subscribe(() => {
          this.snackBar.open('Lozinka je promenjena.', 'U redu', { duration: 4000 });
        });
      });
  }

  /**
   * Switches an account off, or back on.
   *
   * Only switching off asks: it locks somebody out mid-shift, and the person clicking is not
   * necessarily the person who will find out. Turning an account back on takes nothing away, so
   * making that one ask too would only train the owner to dismiss the dialog unread.
   */
  protected setEnabled(member: StaffMemberDto, enabled: boolean): void {
    if (enabled) {
      this.api.setStaffEnabled(member.id, true).subscribe(() => this.load());
      return;
    }

    const data: ConfirmDialogData = {
      title: `Ugasiti nalog: ${member.fullName}?`,
      message:
        'Prijava prestaje odmah — ako je osoba na smeni, izgubiće kasu iz ruku. '
        + 'Ime ostaje na svim računima i smenama koje je radila, i nalog se kasnije vraća.',
      confirmText: 'Ugasi nalog',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (confirmed) {
          this.api.setStaffEnabled(member.id, false).subscribe(() => this.load());
        }
      });
  }
}
