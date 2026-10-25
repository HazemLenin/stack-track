import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService, UserDto } from '../features/auth/auth.service';
import { authGuard, guestGuard } from './auth.guard';

describe('guards', () => {
  const user = signal<UserDto | null>(null);
  const run = (guard: typeof authGuard, url = '/boards') =>
    TestBed.runInInjectionContext(() => guard({} as never, { url } as RouterStateSnapshot));

  beforeEach(() => {
    user.set(null);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { user } }],
    });
  });

  it('authGuard lets a signed-in user through', () => {
    user.set({ id: '1', email: 's@x.com', displayName: 'Sara', role: 'Member' });
    expect(run(authGuard)).toBe(true);
  });

  it('authGuard sends a guest to sign in and keeps the full URL with its query', () => {
    const result = run(authGuard, '/boards/42?tab=cards') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe(
      '/auth/sign-in?returnUrl=%2Fboards%2F42%3Ftab%3Dcards',
    );
  });

  it('guestGuard lets a guest through', () => {
    expect(run(guestGuard, '/auth/sign-in')).toBe(true);
  });

  it('guestGuard sends a signed-in user to My boards', () => {
    user.set({ id: '1', email: 's@x.com', displayName: 'Sara', role: 'Member' });
    const result = run(guestGuard, '/auth/sign-in') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/boards');
  });
});
