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

  function signIn(role = 'Member') {
    TestBed.inject(AuthService)
      .register({ displayName: 'Sara Ahmed', email: 's@x.com', password: 'Valid1234', confirmPassword: 'Valid1234' })
      .subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/register')
      .flush({
        accessToken: 'jwt',
        accessTokenExpiresAt: '2026-10-25T12:15:00Z',
        user: { id: '1', email: 's@x.com', displayName: 'Sara Ahmed', role },
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

  it('shows the display name on the user menu trigger when signed in', async () => {
    const fixture = TestBed.createComponent(App);
    signIn();
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('button.user')?.textContent).toContain('Sara Ahmed');
    expect(el.querySelector('a[href="/auth/sign-in"]')).toBeNull();
  });

  it('hides the Admin link for a Member', async () => {
    const fixture = TestBed.createComponent(App);
    signIn();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('a[href="/admin/users"]')).toBeNull();
  });

  it('shows the Admin link for an Admin', async () => {
    const fixture = TestBed.createComponent(App);
    signIn('Admin');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('a[href="/admin/users"]')?.textContent).toContain('Admin');
  });

  async function openMenuAndSignOut() {
    const fixture = TestBed.createComponent(App);
    signIn();
    await fixture.whenStable();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button.user')!.click();
    await fixture.whenStable();
    document.querySelector<HTMLButtonElement>('.cdk-overlay-container button.sign-out')!.click();
    return fixture;
  }

  it('opens the user menu and signs out through the API, then goes to sign in', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = await openMenuAndSignOut();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(TestBed.inject(AuthService).user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/auth/sign-in');
  });

  it('still signs out and goes to sign in when the API is unreachable', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = await openMenuAndSignOut();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/sign-out').flush(null, { status: 0, statusText: 'Unknown Error' });
    await fixture.whenStable();

    expect(TestBed.inject(AuthService).user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/auth/sign-in');
  });
});
