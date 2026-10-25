import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it('keeps a relative app path with its query', () => {
    expect(safeReturnUrl('/boards/42?tab=cards')).toBe('/boards/42?tab=cards');
  });

  it('falls back to My boards when there is none', () => {
    expect(safeReturnUrl(null)).toBe('/boards');
    expect(safeReturnUrl('')).toBe('/boards');
  });

  for (const unsafe of ['//evil.com', '/\\evil.com', 'https://evil.com', 'javascript:alert(1)', 'boards', 'evil.com/x']) {
    it(`rejects ${unsafe}`, () => {
      expect(safeReturnUrl(unsafe)).toBe('/boards');
    });
  }
});
