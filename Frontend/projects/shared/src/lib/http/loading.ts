import { Signal, computed, signal } from '@angular/core';
import { Observable, defer, finalize } from 'rxjs';

/**
 * Whether a screen is still waiting on the API.
 *
 * A counter rather than a boolean, because several screens fire more than one request at a time —
 * reports asks for turnover, top sellers and voids together, the store asks for valuation and
 * movements. With a boolean the first response to land clears the bar while the others are still in
 * flight, which is worse than no bar at all: it says the screen is finished when it is not.
 *
 * Held as a plain object rather than a service: each screen wants its own count, and two screens
 * sharing one would have a background refresh on the first spin the second.
 *
 * ```ts
 * protected readonly loading = new LoadingState();
 *
 * this.loading.track(this.api.turnover(from, to)).subscribe(...);
 * ```
 * ```html
 * @if (loading.active()) { <mat-progress-bar mode="indeterminate" /> }
 * ```
 */
export class LoadingState {
  private readonly pending = signal(0);

  /** True while at least one tracked call is outstanding. */
  readonly active: Signal<boolean> = computed(() => this.pending() > 0);

  /**
   * Counts a call in for as long as it runs.
   *
   * `defer` means the count starts when somebody subscribes, not when the observable is built, so a
   * call that is prepared and never subscribed does not leave the bar spinning forever. `finalize`
   * covers all three ways it can end — a value, an error, or the screen being closed underneath it.
   */
  track<T>(work: Observable<T>): Observable<T> {
    return defer(() => {
      this.pending.update((count) => count + 1);

      return work.pipe(finalize(() => this.pending.update((count) => Math.max(0, count - 1))));
    });
  }
}
