import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../features/auth/auth.service';

// Session restore runs at startup (see app.config.ts), so the user signal is settled before guards run.
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).user()
    ? true
    : inject(Router).createUrlTree(['/auth/sign-in'], { queryParams: { returnUrl: state.url } });

export const guestGuard: CanActivateFn = () =>
  inject(AuthService).user() ? inject(Router).createUrlTree(['/boards']) : true;
