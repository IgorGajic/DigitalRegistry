import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { registerLocaleData } from '@angular/common';
import localeSr from '@angular/common/locales/sr-Latn';

import {
  API_BASE_URL,
  LICENSE_ROUTE,
  LOGIN_ROUTE,
  STORAGE_KEY,
  authInterceptor,
  errorInterceptor,
} from 'shared';

import { environment } from '../environments/environment';
import { routes } from './app.routes';

// Dates, currency and number grouping all follow the venue's locale rather than the browser's.
registerLocaleData(localeSr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimationsAsync(),

    // Order matters: the auth interceptor attaches the token on the way out, the error interceptor
    // catches what comes back — including the 402 that means the venue has not paid.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),

    { provide: LOCALE_ID, useValue: 'sr-Latn' },
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: STORAGE_KEY, useValue: 'digitalregistry.pos.session' },
    { provide: LOGIN_ROUTE, useValue: '/prijava' },
    { provide: LICENSE_ROUTE, useValue: '/licenca' },
  ],
};
