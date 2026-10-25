import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { toUserMessage } from './error-message';
import { ToastService } from './toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const message = toUserMessage(err);
      if (message) toast.show(message);
      return throwError(() => err);
    }),
  );
};
