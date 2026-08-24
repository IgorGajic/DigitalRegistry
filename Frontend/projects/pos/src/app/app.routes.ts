import { Routes } from '@angular/router';
import { UserRole, authGuard, roleGuard } from 'shared';

/**
 * The till's screens.
 *
 * Route guards mirror the API's authorization matrix, which remains the authority. They exist so a
 * waiter is not offered a reports screen that would answer every request with 403.
 */
export const routes: Routes = [
  {
    path: 'prijava',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    // Where a table's QR code leads. Outside the shell and outside the guard: whoever scans it has
    // no account, and the token in the address is the entire session.
    path: 'gost/:token',
    loadComponent: () => import('./features/guest/guest-menu.page').then((m) => m.GuestMenuPage),
  },
  {
    path: 'licenca',
    loadComponent: () =>
      import('./features/license/license-expired.page').then((m) => m.LicenseExpiredPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'sala' },
      {
        path: 'sala',
        loadComponent: () => import('./features/floor/floor.page').then((m) => m.FloorPage),
      },
      {
        path: 'sala/:tableId',
        loadComponent: () => import('./features/order/order.page').then((m) => m.OrderPage),
      },
      {
        path: 'rezervacije',
        loadComponent: () =>
          import('./features/reservations/reservations.page').then((m) => m.ReservationsPage),
      },
      {
        path: 'raspored',
        canActivate: [roleGuard(UserRole.Owner)],
        loadComponent: () =>
          import('./features/layout-editor/layout-editor.page').then((m) => m.LayoutEditorPage),
      },
      {
        path: 'jelovnik',
        canActivate: [roleGuard(UserRole.Manager, UserRole.Owner)],
        loadComponent: () => import('./features/menu/menu.page').then((m) => m.MenuPage),
      },
      {
        path: 'magacin',
        canActivate: [roleGuard(UserRole.Manager, UserRole.Owner)],
        loadComponent: () =>
          import('./features/inventory/inventory.page').then((m) => m.InventoryPage),
      },
      {
        path: 'smene',
        canActivate: [roleGuard(UserRole.Manager, UserRole.Owner)],
        loadComponent: () => import('./features/schedule/schedule.page').then((m) => m.SchedulePage),
      },
      {
        path: 'izvestaji',
        canActivate: [roleGuard(UserRole.Owner)],
        loadComponent: () => import('./features/reports/reports.page').then((m) => m.ReportsPage),
      },
      {
        path: 'zaposleni',
        canActivate: [roleGuard(UserRole.Owner)],
        loadComponent: () => import('./features/staff/staff.page').then((m) => m.StaffPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
