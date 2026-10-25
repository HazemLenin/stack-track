import { Routes } from '@angular/router';
import { AdminPage } from './admin-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', component: AdminPage },
];
