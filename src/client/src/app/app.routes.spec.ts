import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('routes', () => {
  let harness: RouterTestingHarness;
  const h1 = () => (harness.routeNativeElement as HTMLElement).querySelector('h1')?.textContent;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    harness = await RouterTestingHarness.create();
  });

  it('redirects / to boards', async () => {
    await harness.navigateByUrl('/');
    expect(TestBed.inject(Router).url).toBe('/boards');
    expect(h1()).toBe('boards');
  });

  for (const name of ['auth', 'boards', 'admin', 'my-cards']) {
    it(`renders ${name}`, async () => {
      await harness.navigateByUrl('/' + name);
      expect(h1()).toBe(name);
      if (name === 'boards') expect(document.title).toBe('Stack Track');
    });
  }

  for (const url of ['/does/not/exist', '/boards/xyz/nope', '/admin/zzz']) {
    it(`shows not found for ${url}`, async () => {
      await harness.navigateByUrl(url);
      expect(h1()).toBe('Page not found');
      expect(document.title).toBe('Page not found - Stack Track');
    });
  }
});
