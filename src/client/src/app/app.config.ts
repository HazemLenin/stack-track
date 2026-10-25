import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { boardAccessInterceptor } from './core/board-access.interceptor';
import { errorInterceptor } from './core/error.interceptor';
import { AuthService } from './features/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, boardAccessInterceptor])),
    // Settle the session before the first navigation so route guards see who is signed in.
    provideAppInitializer(() => inject(AuthService).restore()),
  ],
};
