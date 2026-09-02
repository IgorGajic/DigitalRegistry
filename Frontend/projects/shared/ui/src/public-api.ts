/*
 * Components that draw something, and so drag Angular Material in with them.
 *
 * A separate entry point because of where they are used. Everything a running application needs
 * before it knows who is looking at it — interceptors, tokens, guards — comes from `shared`, so
 * `shared` is in the eager graph of both applications. The bundler splits chunks per source file,
 * and a library is one file (`fesm2022/shared.mjs`): anything exported beside those interceptors
 * gets pulled into the initial bundle along with them, even when only a lazy route uses it. That
 * put the whole Material dialog stack in front of a login screen that has neither.
 */

export * from './lib/dialogs/prompt.dialog';
export * from './lib/dialogs/confirm.dialog';

export * from './lib/charts/bar-chart';
