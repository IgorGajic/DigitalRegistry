/*
 * What the till and the master application may use from this library.
 *
 * Deliberately free of anything that draws: both applications import from here before they have a
 * session — interceptors, tokens, guards — so whatever sits here is in their initial bundle.
 * Components live in `shared/ui`, the hub connection in `shared/realtime`.
 */

export * from './lib/config/tokens';

export * from './lib/models/enums';
export * from './lib/models/dtos';

export * from './lib/auth/auth.service';
export * from './lib/auth/guards';

export * from './lib/http/interceptors';
export * from './lib/http/loading';
export * from './lib/http/messages';

export * from './lib/api/till-api.service';
export * from './lib/api/platform-api.service';

export * from './lib/theme/theme.service';

export * from './lib/format/labels';
export * from './lib/format/dates';
