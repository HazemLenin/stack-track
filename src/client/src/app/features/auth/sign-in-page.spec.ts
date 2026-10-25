import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { SignInPage } from './sign-in-page';

const authResponse = {
  accessToken: 'jwt',
  accessTokenExpiresAt: '2026-10-25T12:15:00Z',
  user: { id: '1', email: 'sara@example.com', displayName: 'Sara Ahmed', role: 'Member' },
};

describe('SignInPage', () => {
  let fixture: ComponentFixture<SignInPage>;
  let http: HttpTestingController;
  let el: HTMLElement;
  let navigate: ReturnType<typeof vi.spyOn>;

  const input = (id: string) => el.querySelector<HTMLInputElement>('#' + id)!;
  const submit = () => el.querySelector<HTMLButtonElement>('button[type=submit]')!;
  const banner = () => el.querySelector('[role=alert]')?.textContent?.trim();

  async function type(id: string, value: string) {
    input(id).value = value;
    input(id).dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function signIn() {
    await type('email', 'sara@example.com');
    await type('password', 'Valid1234');
    submit().click();
    await fixture.whenStable();
  }

  async function setup(returnUrl: string | null = null) {
    await TestBed.configureTestingModule({
      imports: [SignInPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}) } },
        },
      ],
    }).compileComponents();
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(SignInPage);
    http = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement;
    await fixture.whenStable();
  }

  it('goes to My boards after signing in', async () => {
    await setup();
    await signIn();

    const req = http.expectOne('/api/auth/sign-in');
    expect(req.request.body).toEqual({ email: 'sara@example.com', password: 'Valid1234' });
    req.flush(authResponse);
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/boards');
  });

  it('goes back to the page the user originally wanted', async () => {
    await setup('/boards/42?tab=cards');
    await signIn();
    http.expectOne('/api/auth/sign-in').flush(authResponse);
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/boards/42?tab=cards');
  });

  it('ignores an unsafe return URL', async () => {
    await setup('//evil.com');
    await signIn();
    http.expectOne('/api/auth/sign-in').flush(authResponse);
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/boards');
  });

  it('shows the generic message for a wrong password or unknown email', async () => {
    await setup();
    await signIn();
    http
      .expectOne('/api/auth/sign-in')
      .flush({ status: 401, detail: 'Email or password is incorrect' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(banner()).toBe('Email or password is incorrect');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows the deactivated message', async () => {
    await setup();
    await signIn();
    http
      .expectOne('/api/auth/sign-in')
      .flush(
        { status: 403, detail: 'Your account has been deactivated. Please contact an administrator.' },
        { status: 403, statusText: 'Forbidden' },
      );
    await fixture.whenStable();

    expect(banner()).toBe('Your account has been deactivated. Please contact an administrator.');
  });

  it('shows that the account is locked and when to try again', async () => {
    await setup();
    await signIn();
    const lockedUntil = '2026-10-25T12:15:00Z';
    http
      .expectOne('/api/auth/sign-in')
      .flush({ status: 423, lockedUntil }, { status: 423, statusText: 'Locked' });
    await fixture.whenStable();

    const time = new Date(lockedUntil).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    expect(banner()).toContain('temporarily locked');
    expect(banner()).toContain(time);
  });

  it('does not submit empty fields', async () => {
    await setup();
    submit().click();
    await fixture.whenStable();

    http.expectNone('/api/auth/sign-in');
  });

  it('disables the button while the request is pending and clears an old error on retry', async () => {
    await setup();
    await signIn();
    http.expectOne('/api/auth/sign-in').flush({ detail: 'x' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();
    expect(banner()).toBeTruthy();

    submit().click();
    await fixture.whenStable();

    expect(submit().disabled).toBe(true);
    expect(banner()).toBeUndefined();
    http.expectOne('/api/auth/sign-in');
  });

  it('links to sign up', async () => {
    await setup();
    expect(el.querySelector('a[href="/auth/sign-up"]')).toBeTruthy();
  });
});
