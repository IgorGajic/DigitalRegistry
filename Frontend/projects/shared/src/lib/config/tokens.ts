import { InjectionToken } from '@angular/core';

/**
 * The API this application talks to.
 *
 * A token rather than a constant because the two applications point at different hosts — the till at
 * the restaurant API, the master at the platform one — while sharing every service in this library.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

/**
 * Where the session is kept in `localStorage`.
 *
 * Distinct per application so that running both on `localhost` during development does not have one
 * signing the other out. In production they are different origins and it would not matter.
 */
export const STORAGE_KEY = new InjectionToken<string>('STORAGE_KEY');

/** Where to send somebody who is not signed in, or whose session has just been rejected. */
export const LOGIN_ROUTE = new InjectionToken<string>('LOGIN_ROUTE');

/**
 * Where to send a restaurant whose licence has lapsed.
 *
 * Only the till has such a screen; the master application never receives a 402, since it is the host
 * that sells the licences.
 */
export const LICENSE_ROUTE = new InjectionToken<string>('LICENSE_ROUTE');
