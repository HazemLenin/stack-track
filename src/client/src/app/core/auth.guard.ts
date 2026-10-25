import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../features/auth/auth.service';

// Session restore runs at startup (see app.config.ts), so the user signal is settled before guards run.
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.user()) return true;
  const queryParams = auth.sessionExpired() ? { returnUrl: state.url, reason: 'expired' } : { returnUrl: state.url };
  return inject(Router).createUrlTree(['/auth/sign-in'], { queryParams });
};

export const guestGuard: CanActivateFn = () =>
  inject(AuthService).user() ? inject(Router).createUrlTree(['/boards']) : true;
