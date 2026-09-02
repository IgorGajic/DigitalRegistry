import { Component, computed, input, signal } from '@angular/core';

/** One bar, as the screen that owns the data describes it. */
export interface BarPoint {
  /** What goes under the bar on the axis. Kept short: the axis has room for a date, not a sentence. */
  readonly label: string;
  readonly value: number;
  /** The tooltip's heading — the full date, the month and year. */
  readonly title: string;
  /** Further tooltip lines, already worded by the caller. */
  readonly notes?: readonly string[];
}

/** One slot on the plot, in the chart's own coordinate space. */
interface Bar {
  readonly point: BarPoint;
  readonly index: number;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

const VIEW_WIDTH = 960;
const VIEW_HEIGHT = 260;
const PAD_LEFT = 64;
const PAD_RIGHT = 12;
const PAD_TOP = 22;
const PAD_BOTTOM = 34;

/** A bar wider than this stops reading as a bar and starts reading as a panel. */
const MAX_BAR = 56;

/** Between bars, so two full slots never touch and read as one block. */
const BAR_GAP = 8;

/** Two ticks closer together than this print their labels on top of each other. */
const TICK_CLEARANCE = 16;

/** Default axis ticks: thousands are the unit both applications count in. */
function compactTick(value: number): string {
  return Math.abs(value) >= 1000 ? `${Math.round(value / 1000)}k` : `${Math.round(value)}`;
}

/**
 * Magnitude over an ordinal axis: takings by day, licence revenue by month.
 *
 * Shared because both applications ask the same question of different data, and answering it twice
 * produced two charts that behaved differently — one an SVG with a zero baseline and a tooltip, the
 * other a row of divs whose height was a percentage of the largest value. A reader moving between
 * them had to learn the second one.
 *
 * The caller supplies the points and the wording; everything about the geometry — the scale, the
 * zero line, which labels survive, where the tooltip sits — lives here, once.
 */
@Component({
  selector: 'dr-bar-chart',
  template: `
    <figure class="bar">
      <figcaption class="bar__head">
        <span class="dr-eyebrow">{{ eyebrow() }}</span>
        @if (peak(); as best) {
          @if (peakLabel()) {
            <span class="bar__peak dr-muted">
              {{ peakLabel() }} {{ best.point.label }} ·
              <span class="dr-figure">{{ formatValue()(best.point.value) }}</span>
            </span>
          }
        }
      </figcaption>

      <div class="bar__plot">
        <svg
          [attr.viewBox]="'0 0 ' + viewWidth + ' ' + viewHeight"
          class="bar__svg"
          role="img"
          [attr.aria-label]="ariaLabel()"
        >
          <!-- Grid first, so bars sit on top of it rather than under a line. -->
          @for (line of gridLines(); track line.value) {
            <line
              class="bar__grid"
              [class.bar__grid--zero]="line.value === 0"
              [attr.x1]="padLeft"
              [attr.x2]="viewWidth - padRight"
              [attr.y1]="line.y"
              [attr.y2]="line.y"
            />
            <text class="bar__tick" [attr.x]="padLeft - 10" [attr.y]="line.y + 4">
              {{ formatTick()(line.value) }}
            </text>
          }

          @for (bar of bars(); track bar.index) {
            <!-- Rounded at the data end only: the foot stays square on the baseline, so the bar
                 reads as measured from zero rather than floating. -->
            <path
              class="bar__mark"
              [class.bar__mark--hot]="hovered() === bar.index"
              [attr.d]="barPath(bar)"
            />

            <!-- The hit target is the whole column, far easier to land on than a mark that may be
                 a few pixels tall on a quiet day. -->
            <rect
              class="bar__hit"
              [attr.x]="bar.x"
              [attr.y]="padTop"
              [attr.width]="bar.width"
              [attr.height]="viewHeight - padTop - padBottom"
              (mouseenter)="hovered.set(bar.index)"
              (mouseleave)="hovered.set(null)"
            />

            @if (showLabel(bar)) {
              <text class="bar__label" [attr.x]="bar.x + bar.width / 2" [attr.y]="viewHeight - 12">
                {{ bar.point.label }}
              </text>
            }
          }
        </svg>

        @if (active(); as bar) {
          <!--
            Anchored to the bar's own data end rather than to the top of the plot. Pinned to the top
            it sat nowhere near a short bar, and nowhere near a negative one at all — the reader
            looked at a figure floating above a different column.
          -->
          <div
            class="bar__tip"
            [style.left.%]="(bar.x + bar.width / 2) / viewWidth * 100"
            [style.top.%]="bar.y / viewHeight * 100"
            [class.bar__tip--right]="bar.x > viewWidth / 2"
            [class.bar__tip--below]="tipBelow(bar)"
          >
            <strong>{{ bar.point.title }}</strong>
            <span class="dr-figure bar__tip-total">{{ formatValue()(bar.point.value) }}</span>
            @for (note of bar.point.notes ?? []; track $index) {
              <span class="dr-muted">{{ note }}</span>
            }
          </div>
        }
      </div>
    </figure>
  `,
  styles: `
    .bar {
      margin: 0;
    }

    .bar__head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 12px;
      flex-wrap: wrap;
      margin-bottom: 4px;
    }

    .bar__peak {
      font-size: 0.8rem;
    }

    .bar__plot {
      position: relative;
    }

    .bar__svg {
      width: 100%;
      height: auto;
      display: block;
      overflow: visible;
    }

    /* Recessive: the grid is a reference, not content. */
    .bar__grid {
      stroke: var(--mat-sys-outline-variant);
      stroke-width: 1;
    }

    .bar__grid--zero {
      stroke: var(--mat-sys-outline);
    }

    .bar__tick,
    .bar__label {
      font-family: var(--dr-font-mono);
      font-size: 11px;
      fill: var(--mat-sys-on-surface-variant);
    }

    .bar__tick {
      text-anchor: end;
    }

    .bar__label {
      text-anchor: middle;
    }

    .bar__mark {
      fill: var(--mat-sys-primary);
      transition: fill 120ms ease;
    }

    .bar__mark--hot {
      fill: var(--mat-sys-primary-container);
      stroke: var(--mat-sys-primary);
      stroke-width: 1.5;
    }

    .bar__hit {
      fill: transparent;
      cursor: default;
    }

    .bar__tip {
      position: absolute;
      transform: translate(-50%, calc(-100% - 8px));
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 8px 10px;
      border-radius: var(--dr-radius);
      background: var(--mat-sys-inverse-surface);
      color: var(--mat-sys-inverse-on-surface);
      font-size: 0.78rem;
      white-space: nowrap;
      pointer-events: none;
      z-index: 2;
    }

    /* Past the halfway mark the tooltip would run off the right edge. */
    .bar__tip--right {
      transform: translate(-100%, calc(-100% - 8px));
    }

    /* A tall bar leaves no room above it, so the tooltip drops below its data end instead. */
    .bar__tip--below {
      transform: translate(-50%, 8px);
    }

    .bar__tip--below.bar__tip--right {
      transform: translate(-100%, 8px);
    }

    .bar__tip .dr-muted {
      color: inherit;
      opacity: 0.75;
    }

    .bar__tip-total {
      font-size: 1.1rem;
    }
  `,
})
export class BarChart {
  readonly points = input.required<readonly BarPoint[]>();

  /** The chart's own title, set above the plot. */
  readonly eyebrow = input.required<string>();

  /** How a value is written out in the tooltip and the peak note — with its currency, usually. */
  readonly formatValue = input.required<(value: number) => string>();

  /** Axis ticks. Their unit is stated by the tooltip, so they are written short. */
  readonly formatTick = input<(value: number) => string>(compactTick);

  /** Names the strongest point in the header — "najbolji dan", "najjači mesec". Empty to omit. */
  readonly peakLabel = input('');

  /** What a screen reader is told the plot shows. */
  readonly summary = input('');

  protected readonly viewWidth = VIEW_WIDTH;
  protected readonly viewHeight = VIEW_HEIGHT;
  protected readonly padLeft = PAD_LEFT;
  protected readonly padRight = PAD_RIGHT;
  protected readonly padTop = PAD_TOP;
  protected readonly padBottom = PAD_BOTTOM;

  protected readonly hovered = signal<number | null>(null);

  /**
   * The scale, which has to admit negative values.
   *
   * A day whose reversals outweigh its takings is net negative, and clamping the floor at zero would
   * draw a flat day where the venue actually gave money back.
   */
  private readonly scale = computed(() => {
    const values = this.points().map((point) => point.value);
    const top = Math.max(0, ...values);
    const bottom = Math.min(0, ...values);
    const span = top - bottom || 1;
    const height = VIEW_HEIGHT - PAD_TOP - PAD_BOTTOM;

    return {
      top,
      bottom,
      zero: PAD_TOP + (top / span) * height,
      y: (value: number) => PAD_TOP + ((top - value) / span) * height,
    };
  });

  readonly bars = computed<Bar[]>(() => {
    const points = this.points();

    if (points.length === 0) {
      return [];
    }

    const scale = this.scale();
    const usable = VIEW_WIDTH - PAD_LEFT - PAD_RIGHT;
    const slot = usable / points.length;
    const width = Math.min(MAX_BAR, Math.max(3, slot - BAR_GAP));

    return points.map((point, index) => {
      const centre = PAD_LEFT + slot * index + slot / 2;
      const y = scale.y(point.value);

      return {
        point,
        index,
        x: centre - width / 2,
        y: Math.min(y, scale.zero),
        width,
        height: Math.abs(scale.zero - y),
      };
    });
  });

  protected readonly active = computed(() => {
    const index = this.hovered();

    return index === null ? null : (this.bars()[index] ?? null);
  });

  /** The strongest point, which is the one worth naming in the header. */
  readonly peak = computed(() => {
    const bars = this.bars();

    return bars.length
      ? bars.reduce((top, bar) => (bar.point.value > top.point.value ? bar : top))
      : null;
  });

  /**
   * The grid, thinned so no two ticks print on top of each other.
   *
   * A period with one small negative value puts the floor a few pixels under the zero line, and the
   * two labels landed on each other. Zero always survives: it is the line the bars are measured
   * from.
   */
  readonly gridLines = computed(() => {
    const scale = this.scale();
    const { top, bottom } = scale;
    const steps = bottom < 0 ? [top, top / 2, 0, bottom] : [top, top / 2, 0];
    const kept: { value: number; y: number }[] = [];

    for (const value of steps) {
      const y = scale.y(value);
      const collides = kept.find((line) => Math.abs(line.y - y) < TICK_CLEARANCE);

      if (!collides) {
        kept.push({ value, y });
        continue;
      }

      if (value === 0) {
        kept.splice(kept.indexOf(collides), 1, { value, y });
      }
    }

    return kept;
  });

  protected readonly ariaLabel = computed(() => this.summary() || this.eyebrow());

  /** A bar rounded at the data end only, so it still reads as measured from the baseline. */
  barPath(bar: Bar): string {
    const radius = Math.min(4, bar.width / 2, bar.height);
    const negative = bar.point.value < 0;

    if (bar.height <= 0.5) {
      // A slot with nothing in it still gets a hairline, so the gap is visibly a zero rather than a
      // missing bar.
      return `M ${bar.x} ${bar.y} h ${bar.width} v 0.75 h ${-bar.width} Z`;
    }

    const { x, y, width, height } = bar;

    return negative
      ? `M ${x} ${y} h ${width} v ${height - radius} a ${radius} ${radius} 0 0 1 ${-radius} ${radius}`
        + ` h ${-(width - 2 * radius)} a ${radius} ${radius} 0 0 1 ${-radius} ${-radius} Z`
      : `M ${x} ${y + radius} a ${radius} ${radius} 0 0 1 ${radius} ${-radius}`
        + ` h ${width - 2 * radius} a ${radius} ${radius} 0 0 1 ${radius} ${radius}`
        + ` v ${height - radius} h ${-width} Z`;
  }

  /**
   * Which slots get a label under them.
   *
   * Never all of them: at thirty the labels collide into a grey band. The ends anchor the period and
   * every few slots carry the rest.
   */
  showLabel(bar: { index: number }): boolean {
    const count = this.bars().length;
    const every = count <= 10 ? 1 : count <= 20 ? 2 : 5;

    return bar.index === 0 || bar.index === count - 1 || bar.index % every === 0;
  }

  /** Whether a bar is too close to the ceiling to hang its tooltip above it. */
  protected tipBelow(bar: Bar): boolean {
    return bar.y < 76;
  }
}
