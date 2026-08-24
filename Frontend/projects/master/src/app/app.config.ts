import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { registerLocaleData } from '@angular/common';
import localeSr from '@angular/common/locales/sr-Latn';

import {
  API_BASE_URL,
  LOGIN_ROUTE,
  STORAGE_KEY,
  authInterceptor,
  errorInterceptor,
} from 'shared';

import { environment } from '../environments/environment';
import { routes } from './app.routes';

registerLocaleData(localeSr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),

    { provide: LOCALE_ID, useValue: 'sr-Latn' },
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    // A separate key from the till, so running both on localhost does not have one signing the
    // other out.
    { provide: STORAGE_KEY, useValue: 'digitalregistry.master.session' },
    { provide: LOGIN_ROUTE, useValue: '/prijava' },

    // No LICENSE_ROUTE: this is the host that sells licences, and never receives a 402.
  ],
};
