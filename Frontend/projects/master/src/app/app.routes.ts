import { Routes } from '@angular/router';
import { authGuard } from 'shared';

export const routes: Routes = [
  {
    path: 'prijava',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'pregled' },
      {
        path: 'pregled',
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'restorani',
        loadComponent: () =>
          import('./features/restaurants/restaurants.page').then((m) => m.RestaurantsPage),
      },
      {
        path: 'restorani/:id',
        loadComponent: () =>
          import('./features/restaurants/restaurant-detail.page').then(
            (m) => m.RestaurantDetailPage,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
