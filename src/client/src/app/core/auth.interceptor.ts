import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // placeholder: attach access token in Authentication epic
  return next(req);
};
