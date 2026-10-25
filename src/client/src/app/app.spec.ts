import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { AuthService } from './features/auth/auth.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  const text = (el: HTMLElement) => el.querySelector('mat-toolbar')?.textContent ?? '';

  function signIn() {
    TestBed.inject(AuthService)
      .register({ displayName: 'Sara Ahmed', email: 's@x.com', password: 'Valid1234', confirmPassword: 'Valid1234' })
      .subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/register')
      .flush({
        accessToken: 'jwt',
        accessTokenExpiresAt: '2026-10-25T12:15:00Z',
        user: { id: '1', email: 's@x.com', displayName: 'Sara Ahmed', role: 'Member' },
      });
  }

  it('renders the brand and router outlet', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(text(el)).toContain('Stack Track');
    expect(el.querySelector('main router-outlet')).toBeTruthy();
  });

  it('offers sign in and hides the user name when signed out', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('a[href="/auth/sign-in"]')?.textContent).toContain('Sign in');
    expect(el.querySelector('.user')).toBeNull();
    expect(text(el)).not.toContain('Sign out');
  });

  it('shows the display name and a sign out button when signed in', async () => {
    const fixture = TestBed.createComponent(App);
    signIn();
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.user')?.textContent).toContain('Sara Ahmed');
    expect(text(el)).toContain('Sign out');
    expect(el.querySelector('a[href="/auth/sign-in"]')).toBeNull();
  });

  it('signs out through the API and returns to sign in', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(App);
    signIn();
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.sign-out')!.click();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(TestBed.inject(AuthService).user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/auth/sign-in');
  });
});
