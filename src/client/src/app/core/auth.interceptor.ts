import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../features/auth/auth.service';
import { API_BASE_URL } from './api-base-url';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).accessToken();
  // Only our own API gets the token, never third-party hosts.
  if (!token || !req.url.startsWith(inject(API_BASE_URL) + '/')) return next(req);

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
