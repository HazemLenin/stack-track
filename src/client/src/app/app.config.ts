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

// The first interceptor is the outermost, so it sees an error last: the board message has to replace the generic toast.
export const appInterceptors = [boardAccessInterceptor, authInterceptor, errorInterceptor];

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors(appInterceptors)),
    // Settle the session before the first navigation so route guards see who is signed in.
    provideAppInitializer(() => inject(AuthService).restore()),
  ],
};
