import { HttpErrorResponse } from '@angular/common/http';

export function toUserMessage(e: HttpErrorResponse): string | null {
  if (e.status === 401) return null;
  if ([0, 502, 503, 504].includes(e.status)) return "Can't reach the server. Please try again in a moment.";
  if (e.status >= 500) return 'Something went wrong on our side. Please try again.';

  // Only trust string fields of a ProblemDetails object; anything else (HTML, nested JSON) stays hidden.
  const body = e.error;
  if (body && typeof body === 'object') {
    for (const v of [body.detail, body.title]) {
      if (typeof v === 'string' && v) return v;
    }
  }
  return 'The request could not be completed.';
}
