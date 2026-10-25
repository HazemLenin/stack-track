import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../features/auth/auth.service';
import { API_BASE_URL } from './api-base-url';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const baseUrl = inject(API_BASE_URL);

  // Only our own API gets the token, never third-party hosts.
  if (!req.url.startsWith(baseUrl + '/')) return next(req);

  const withToken = (token: string | null) =>
    token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  const isAuthCall = req.url.startsWith(`${baseUrl}/auth/`);

  return next(withToken(auth.accessToken())).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isAuthCall) return throwError(() => error);

      // Access token expired: refresh once and replay. If the refresh itself fails the session is over.
      return auth.refresh().pipe(
        catchError(() => {
          router.navigate(['/auth/sign-in'], { queryParams: { returnUrl: router.url } });
          return throwError(() => error);
        }),
        switchMap((response) => next(withToken(response.accessToken))),
      );
    }),
  );
};
