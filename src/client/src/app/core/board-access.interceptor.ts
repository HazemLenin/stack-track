import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from './api-base-url';
import { ToastService } from './toast.service';

const pathOf = (url: string) => new URL(url, window.location.origin).pathname;

// A 404 on boards/* means the board is gone or the user lost access (the API answers both the same).
export const boardAccessInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const router = inject(Router);
  const boardsPath = `${pathOf(inject(API_BASE_URL)).replace(/\/$/, '')}/boards/`;
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 404 && pathOf(req.url).startsWith(boardsPath)) {
        toast.show('This board is no longer available');
        router.navigateByUrl('/boards');
      }
      return throwError(() => err);
    }),
  );
};
