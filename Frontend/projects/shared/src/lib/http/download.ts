import { HttpResponse } from '@angular/common/http';

/**
 * Hands a downloaded file to the browser.
 *
 * The name is the server's, taken from `Content-Disposition`: it carries the period the report
 * covers, and two exports of different weeks landing in Downloads under one name are one export.
 * The fallback is used only when the header is absent or unreadable.
 *
 * The object URL is revoked on the next turn rather than immediately — Safari has not started
 * reading it when the click returns, and revoking too early produces a download of nothing.
 */
export function saveBlobResponse(response: HttpResponse<Blob>, fallbackName: string): void {
  const body = response.body;

  if (!body) {
    return;
  }

  const url = URL.createObjectURL(body);
  const link = document.createElement('a');

  link.href = url;
  link.download = fileNameFrom(response.headers.get('content-disposition')) ?? fallbackName;

  document.body.appendChild(link);
  link.click();
  link.remove();

  setTimeout(() => URL.revokeObjectURL(url));
}

/**
 * Reads the file name out of a `Content-Disposition` header.
 *
 * `filename*` is preferred and tried first: it is the percent-encoded UTF-8 form, and it is the only
 * one that survives a name with diacritics in it.
 */
function fileNameFrom(header: string | null): string | null {
  if (!header) {
    return null;
  }

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);

  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // A malformed header is not worth failing a download over; fall through to the plain form.
    }
  }

  const plain = /filename="?([^";]+)"?/i.exec(header);

  return plain ? plain[1].trim() : null;
}
