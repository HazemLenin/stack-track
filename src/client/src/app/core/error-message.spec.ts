import { HttpErrorResponse } from '@angular/common/http';
import { toUserMessage } from './error-message';

const err = (status: number, error?: unknown) => new HttpErrorResponse({ status, error });

describe('toUserMessage', () => {
  it('maps status 0 to a friendly offline message', () => {
    expect(toUserMessage(err(0))).toBe("Can't reach the server. Please try again in a moment.");
  });

  it.each([500, 503])('maps %i to a generic server message', (status) => {
    expect(toUserMessage(err(status))).toBe('Something went wrong on our side. Please try again.');
  });

  it('uses ProblemDetails detail for 4xx', () => {
    expect(
      toUserMessage(err(400, { title: 'Validation failed', detail: 'Name is required' })),
    ).toBe('Name is required');
  });

  it('falls back to title when detail is missing', () => {
    expect(toUserMessage(err(400, { title: 'Validation failed' }))).toBe('Validation failed');
  });

  it('never shows non-string detail or HTML bodies', () => {
    const generic = 'The request could not be completed.';
    expect(toUserMessage(err(400, { detail: { x: 1 } }))).toBe(generic);
    expect(toUserMessage(err(400, '<html>Bad</html>'))).toBe(generic);
  });

  it('returns null for 401', () => {
    expect(toUserMessage(err(401))).toBeNull();
  });
});
