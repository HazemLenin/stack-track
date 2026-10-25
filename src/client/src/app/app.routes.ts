import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'boards' },
  { path: 'auth', loadChildren: () => import('./features/auth/auth.routes').then((m) => m.routes) },
  {
    path: 'boards',
    loadChildren: () => import('./features/boards/boards.routes').then((m) => m.routes),
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.routes),
  },
  {
    path: 'my-cards',
    loadChildren: () => import('./features/my-cards/my-cards.routes').then((m) => m.routes),
  },
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
