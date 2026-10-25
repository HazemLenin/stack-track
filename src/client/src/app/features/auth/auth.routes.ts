import { Routes } from '@angular/router';
import { SignInPage } from './sign-in-page';
import { SignUpPage } from './sign-up-page';

export const routes: Routes = [
  { path: 'sign-in', component: SignInPage },
  { path: 'sign-up', component: SignUpPage },
  { path: '', pathMatch: 'full', redirectTo: 'sign-in' },
];
