import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'boards' },
  {
    path: 'auth',
    title: 'Stack Track',
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.routes),
  },
  {
    path: 'boards',
    title: 'Stack Track',
    canActivate: [authGuard],
    loadChildren: () => import('./features/boards/boards.routes').then((m) => m.routes),
  },
  {
    path: 'admin',
    title: 'Stack Track',
    canActivate: [authGuard],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.routes),
  },
  {
    path: 'my-cards',
    title: 'Stack Track',
    canActivate: [authGuard],
    loadChildren: () => import('./features/my-cards/my-cards.routes').then((m) => m.routes),
  },
  {
    path: '**',
    title: 'Page not found - Stack Track',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
