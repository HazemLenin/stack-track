import { Routes } from '@angular/router';
import { AdminUsersPage } from './users/admin-users-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', component: AdminUsersPage },
];
