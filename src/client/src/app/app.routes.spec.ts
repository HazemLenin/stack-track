import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { AuthService, UserDto } from './features/auth/auth.service';

describe('routes', () => {
  const user = signal<UserDto | null>(null);
  const signedIn: UserDto = { id: '1', email: 's@x.com', displayName: 'Sara', role: 'Member' };
  let harness: RouterTestingHarness;
  const h1 = () => (harness.routeNativeElement as HTMLElement).querySelector('h1')?.textContent;
  const url = () => TestBed.inject(Router).url;

  beforeEach(async () => {
    user.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user, accessToken: () => null, sessionExpired: () => false } },
      ],
    });
    harness = await RouterTestingHarness.create();
  });

  describe('signed out', () => {
    for (const path of ['/', '/boards', '/admin/users', '/my-cards']) {
      it(`sends ${path} to sign in and remembers the page`, async () => {
        await harness.navigateByUrl(path);
        const wanted = path === '/' ? '/boards' : path;
        expect(url()).toBe(`/auth/sign-in?returnUrl=${encodeURIComponent(wanted)}`);
        expect(h1()).toBe('Welcome back');
      });
    }

    it('keeps the query string of the page the user wanted', async () => {
      await harness.navigateByUrl('/boards?tab=cards');
      expect(url()).toBe('/auth/sign-in?returnUrl=%2Fboards%3Ftab%3Dcards');
    });

    it('redirects /auth to sign in', async () => {
      await harness.navigateByUrl('/auth');
      expect(url()).toBe('/auth/sign-in');
      expect(h1()).toBe('Welcome back');
    });

    it('renders sign up', async () => {
      await harness.navigateByUrl('/auth/sign-up');
      expect(h1()).toBe('Create your account');
    });

    it('does not guard the not found page', async () => {
      await harness.navigateByUrl('/does/not/exist');
      expect(h1()).toBe('Page not found');
      expect(document.title).toBe('Page not found - Stack Track');
    });
  });

  describe('signed in', () => {
    beforeEach(() => user.set(signedIn));

    it('redirects / to boards', async () => {
      await harness.navigateByUrl('/');
      expect(url()).toBe('/boards');
      expect(h1()).toBe('My boards');
    });

    it('renders boards with the app title', async () => {
      await harness.navigateByUrl('/boards');
      expect(h1()).toBe('My boards');
      expect(document.title).toBe('Stack Track');
    });

    it('renders my-cards', async () => {
      await harness.navigateByUrl('/my-cards');
      expect(h1()).toBe('my-cards');
    });

    it('sends a Member away from /admin/users to My boards', async () => {
      await harness.navigateByUrl('/admin/users');
      expect(url()).toBe('/boards');
      expect(h1()).toBe('My boards');
    });

    it('lets an Admin into /admin/users and redirects /admin there', async () => {
      user.set({ ...signedIn, role: 'Admin' });
      await harness.navigateByUrl('/admin');
      expect(url()).toBe('/admin/users');
      expect(h1()).toBe('Users');
    });

    it('after sign out, navigating back to /boards redirects to sign in', async () => {
      await harness.navigateByUrl('/boards');
      expect(url()).toBe('/boards');
      user.set(null);
      await harness.navigateByUrl('/auth/sign-in');
      await harness.navigateByUrl('/boards');
      expect(url()).toBe('/auth/sign-in?returnUrl=%2Fboards');
      expect(h1()).not.toBe('My boards');
    });

    for (const page of ['/auth/sign-in', '/auth/sign-up', '/auth']) {
      it(`keeps the user away from ${page}`, async () => {
        await harness.navigateByUrl(page);
        expect(url()).toBe('/boards');
      });
    }

    for (const missing of ['/boards/xyz/nope', '/admin/zzz']) {
      it(`shows not found for ${missing}`, async () => {
        await harness.navigateByUrl(missing);
        expect(h1()).toBe('Page not found');
      });
    }
  });
});
