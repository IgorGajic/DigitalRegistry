import { describe, expect, it } from 'vitest';
import { Subject, throwError } from 'rxjs';

import { LoadingState } from './loading';

/**
 * The counting is the whole point: a boolean would pass the first two tests here and fail the third,
 * which is the case the screens actually hit — reports fires three calls at once.
 */
describe('LoadingState', () => {
  it('is idle until something is subscribed', () => {
    const loading = new LoadingState();
    const source = new Subject<number>();

    expect(loading.active()).toBe(false);

    // Preparing a call must not start the bar; only subscribing does.
    loading.track(source);
    expect(loading.active()).toBe(false);
  });

  it('is active while one call runs, and idle once it completes', () => {
    const loading = new LoadingState();
    const source = new Subject<number>();

    loading.track(source).subscribe();
    expect(loading.active()).toBe(true);

    source.next(1);
    expect(loading.active()).toBe(true);

    source.complete();
    expect(loading.active()).toBe(false);
  });

  it('stays active until the last of several parallel calls finishes', () => {
    const loading = new LoadingState();
    const first = new Subject<number>();
    const second = new Subject<number>();
    const third = new Subject<number>();

    loading.track(first).subscribe();
    loading.track(second).subscribe();
    loading.track(third).subscribe();
    expect(loading.active()).toBe(true);

    first.complete();
    expect(loading.active()).toBe(true);

    second.complete();
    expect(loading.active()).toBe(true);

    third.complete();
    expect(loading.active()).toBe(false);
  });

  it('clears when a call fails, so a dead server does not leave the bar spinning', () => {
    const loading = new LoadingState();

    loading.track(throwError(() => new Error('nema veze'))).subscribe({ error: () => undefined });

    expect(loading.active()).toBe(false);
  });

  it('clears when the screen is closed mid-request', () => {
    const loading = new LoadingState();
    const source = new Subject<number>();

    const subscription = loading.track(source).subscribe();
    expect(loading.active()).toBe(true);

    subscription.unsubscribe();
    expect(loading.active()).toBe(false);
  });
});
