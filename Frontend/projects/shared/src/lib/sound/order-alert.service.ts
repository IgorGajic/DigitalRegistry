import { Injectable, signal } from '@angular/core';

/**
 * The noise a new round makes.
 *
 * A card appearing on the floor screen only works if somebody is looking at the floor screen. In a
 * full room nobody is: the tablet stands at the till and the waiters are at the tables. This is the
 * half of that alert which reaches across the room.
 *
 * Synthesised rather than played from a file. Two short sine tones, a fifth apart, at a fifth of
 * full scale — an interval carries over noise better than a single pitch, and a pair reads as a
 * signal rather than as a fault, which a single beep does. It also means no asset to ship, nothing
 * to fetch before the first alert can sound, and a tone that cannot be mistaken for another
 * application's notification.
 *
 * Off is a real answer. A quiet café with the till behind the bar does not want it, so the switch
 * is on the settings screen and the choice is kept per device — the tablet on the floor and the
 * laptop in the office are not in the same room and should not have to agree.
 */
@Injectable({ providedIn: 'root' })
export class OrderAlertService {
  private static readonly STORAGE_KEY = 'digitalregistry.pos.order-alert';

  /** Concert A and the E above it. Consonant, so two of them together do not sound like an error. */
  private static readonly NOTES = [880, 1318.5];

  private static readonly NOTE_SECONDS = 0.16;

  /** Kept between alerts: browsers cap how many contexts a page may open. */
  private context: AudioContext | null = null;

  readonly enabled = signal<boolean>(this.restore());

  /** Sounds the alert, unless this device has asked for silence. */
  play(): void {
    if (!this.enabled()) {
      return;
    }

    this.chime();
  }

  /**
   * Sounds it regardless of the switch.
   *
   * For the settings screen, where the point is to hear what is being turned on. It is also what
   * gets the audio context out of its suspended state, since it necessarily follows a press.
   */
  preview(): void {
    this.chime();
  }

  setEnabled(enabled: boolean): void {
    this.enabled.set(enabled);

    try {
      localStorage.setItem(OrderAlertService.STORAGE_KEY, enabled ? 'on' : 'off');
    } catch {
      // A browser refusing storage costs the setting at next start-up and nothing else.
    }
  }

  private chime(): void {
    const context = this.audio();

    if (!context) {
      return;
    }

    // A page that has not been interacted with yet gets a suspended context. Resuming is a promise
    // nobody waits on here: by the time an order arrives the waiter has signed in, which is a press.
    if (context.state === 'suspended') {
      void context.resume();
    }

    OrderAlertService.NOTES.forEach((frequency, index) => {
      const startsAt = context.currentTime + index * OrderAlertService.NOTE_SECONDS;
      const endsAt = startsAt + OrderAlertService.NOTE_SECONDS;

      const oscillator = context.createOscillator();
      const gain = context.createGain();

      oscillator.type = 'sine';
      oscillator.frequency.value = frequency;

      // Ramped rather than switched. A gain that jumps to its value clicks, and a click in a quiet
      // room is the part people ask to have turned off.
      gain.gain.setValueAtTime(0.0001, startsAt);
      gain.gain.exponentialRampToValueAtTime(0.2, startsAt + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, endsAt);

      oscillator.connect(gain).connect(context.destination);
      oscillator.start(startsAt);
      oscillator.stop(endsAt);
    });
  }

  /**
   * The audio context, opened on first use.
   *
   * Not in the constructor: a context created before any interaction starts suspended, and some
   * browsers count it against the page whether it ever makes a sound or not. Returns null where
   * there is no Web Audio at all, which is the case in the unit tests' DOM.
   */
  private audio(): AudioContext | null {
    if (this.context) {
      return this.context;
    }

    const constructor =
      typeof AudioContext === 'function'
        ? AudioContext
        : (globalThis as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;

    if (!constructor) {
      return null;
    }

    try {
      this.context = new constructor();
    } catch {
      this.context = null;
    }

    return this.context;
  }

  /** On unless this device has said otherwise: an alert nobody knew about is worth more than silence. */
  private restore(): boolean {
    try {
      return localStorage.getItem(OrderAlertService.STORAGE_KEY) !== 'off';
    } catch {
      return true;
    }
  }
}
