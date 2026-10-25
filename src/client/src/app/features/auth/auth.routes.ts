import { Routes } from '@angular/router';
import { SignUpPage } from './sign-up-page';

export const routes: Routes = [
  { path: 'sign-up', component: SignUpPage },
  { path: '', pathMatch: 'full', redirectTo: 'sign-up' },
];
