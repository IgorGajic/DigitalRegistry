import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TableQrCodeSheetEntryDto } from 'shared';
import QRCode from 'qrcode';

export interface QrSheetDialogData {
  tables: TableQrCodeSheetEntryDto[];
  restaurantName: string;
}

/** One table's card, once its code has been drawn. */
interface QrCard {
  table: TableQrCodeSheetEntryDto;
  url: string;
  image: string;
}

/**
 * The sheet of QR codes that goes on the tables.
 *
 * The tokens and the screen they lead to both already existed; what was missing was any way to get
 * one onto a table. A token is not something a guest types, so it has to be printed — which is why
 * this is a print sheet rather than a list of links.
 *
 * The link is built from the browser's own origin. The code is scanned by a phone standing in the
 * restaurant, so it has to point at wherever this till is actually served from; a value configured
 * once and forgotten would send every guest to whichever host was current when it was written.
 *
 * Codes are drawn at 512 px and printed at 45 mm, so the printed module is comfortably above what a
 * phone camera resolves at arm's length even on an office printer.
 */
@Component({
  selector: 'pos-qr-sheet-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title class="dr-no-print">QR kodovi za stolove</h2>

    @if (drawing()) {
      <mat-progress-bar mode="indeterminate" class="dr-no-print" />
    }

    <mat-dialog-content>
      <p class="qr__hint dr-no-print">
        Odštampajte, isecite i zalepite na stolove. Kod vodi na jelovnik i vezan je za taj sto —
        ako se odštampani kod izgubi, obnovite token stola i odštampajte novi list.
      </p>

      <div class="qr__sheet dr-printable">
        @for (card of cards(); track card.table.tableId) {
          <figure class="qr__card">
            <span class="qr__venue">{{ data.restaurantName }}</span>
            <strong class="qr__table">Sto {{ card.table.tableNumber }}</strong>
            <img class="qr__image" [src]="card.image" [alt]="'QR kod za sto ' + card.table.tableNumber" />
            <figcaption class="qr__caption">
              Skenirajte za jelovnik i poručivanje
              <span class="qr__link">{{ card.url }}</span>
            </figcaption>
          </figure>
        }
      </div>

      @if (!drawing() && cards().length === 0) {
        <p class="dr-empty">Nema stolova za štampu. Napravite bar jedan sto.</p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end" class="dr-no-print">
      <button mat-button mat-dialog-close>Zatvori</button>
      <button mat-flat-button [disabled]="cards().length === 0" (click)="print()">
        <mat-icon>print</mat-icon>
        Štampaj list
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .qr__hint {
      max-width: 60ch;
      color: var(--mat-sys-on-surface-variant);
    }

    .qr__sheet {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 12px;
      background: #fff;
    }

    .qr__card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      margin: 0;
      padding: 12px;
      text-align: center;
      color: #000;
      background: #fff;
      /* Dashed, because the sheet is meant to be cut up. */
      border: 1px dashed #999;
      border-radius: 6px;
      /* A card split over two pages is a code that cannot be scanned. */
      break-inside: avoid;
      page-break-inside: avoid;
    }

    .qr__venue {
      font-size: 0.75rem;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: #555;
    }

    .qr__table {
      font-size: 1.15rem;
    }

    .qr__image {
      width: 100%;
      max-width: 180px;
      aspect-ratio: 1;
      image-rendering: pixelated;
    }

    .qr__caption {
      display: flex;
      flex-direction: column;
      gap: 2px;
      font-size: 0.72rem;
      color: #333;
    }

    .qr__link {
      /* The address is printed as a fallback for a phone that will not scan; it has to wrap. */
      font-family: var(--dr-font-mono);
      font-size: 0.6rem;
      color: #666;
      word-break: break-all;
    }

    @media print {
      .qr__sheet {
        /* Three across on A4 puts nine codes on a page and keeps each one at about 45 mm. */
        grid-template-columns: repeat(3, 1fr);
        gap: 6mm;
      }

      .qr__image {
        max-width: 45mm;
      }
    }
  `,
})
export class QrSheetDialog {
  protected readonly data = inject<QrSheetDialogData>(MAT_DIALOG_DATA);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly cards = signal<QrCard[]>([]);
  protected readonly drawing = signal(true);

  constructor() {
    void this.draw();
  }

  protected print(): void {
    window.print();
  }

  private async draw(): Promise<void> {
    try {
      const cards = await Promise.all(
        this.data.tables.map(async (table) => {
          const url = `${window.location.origin}/gost/${table.qrCodeToken}`;

          return {
            table,
            url,
            image: await QRCode.toDataURL(url, {
              width: 512,
              margin: 1,
              // A code taped to a table gets wet, scratched and partly covered by a glass; the
              // middle correction level recovers about 15% of it, at a size a phone still reads.
              errorCorrectionLevel: 'M',
              color: { dark: '#000000', light: '#ffffff' },
            }),
          };
        }),
      );

      this.cards.set(cards);
    } catch {
      this.snackBar.open('Kodovi se ne mogu iscrtati.', 'U redu', { duration: 6000 });
    } finally {
      this.drawing.set(false);
    }
  }
}
