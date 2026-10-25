import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SignUpPage } from './sign-up-page';

describe('SignUpPage', () => {
  let fixture: ComponentFixture<SignUpPage>;
  let http: HttpTestingController;
  let el: HTMLElement;

  const input = (id: string) => el.querySelector<HTMLInputElement>('#' + id)!;
  const submit = () => el.querySelector<HTMLButtonElement>('button[type=submit]')!;
  const rule = (id: string) => el.querySelector(`[data-rule=${id}]`)!;

  async function type(id: string, value: string) {
    input(id).value = value;
    input(id).dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function fillValid(overrides: Partial<Record<string, string>> = {}) {
    const values = {
      'display-name': '  Sara Ahmed ',
      email: 'sara@example.com',
      password: 'Valid1234',
      'confirm-password': 'Valid1234',
      ...overrides,
    };
    for (const [id, value] of Object.entries(values)) await type(id, value!);
  }

  async function submitForm() {
    submit().click();
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignUpPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SignUpPage);
    http = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement;
    await fixture.whenStable();
  });

  it('marks unmet password rules as the user types', async () => {
    await type('password', 'Short1');

    expect(rule('len').classList).toContain('fail');
    expect(rule('up').classList).toContain('ok');
    expect(rule('lo').classList).toContain('ok');
    expect(rule('di').classList).toContain('ok');

    await type('password', 'Longenough1');
    expect(rule('len').classList).toContain('ok');
  });

  it('links to sign in', () => {
    expect(el.querySelector('a[href="/auth/sign-in"]')).toBeTruthy();
  });

  it('shows no rule failures before the user types', () => {
    expect(el.querySelectorAll('[data-rule].fail').length).toBe(0);
  });

  it('does not submit a weak password', async () => {
    await fillValid({ password: 'Short1', 'confirm-password': 'Short1' });
    await submitForm();

    http.expectNone('/api/auth/register');
  });

  it('shows an error when the passwords differ and does not submit', async () => {
    await fillValid({ 'confirm-password': 'Different1' });
    await submitForm();

    http.expectNone('/api/auth/register');
    expect(el.textContent).toContain('Passwords do not match');
  });

  it('registers with a trimmed name and goes to My boards', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    await fillValid();
    await submitForm();

    const req = http.expectOne('/api/auth/register');
    expect(req.request.body).toEqual({
      displayName: 'Sara Ahmed',
      email: 'sara@example.com',
      password: 'Valid1234',
      confirmPassword: 'Valid1234',
    });
    req.flush({
      accessToken: 'jwt',
      accessTokenExpiresAt: '2026-10-25T12:15:00Z',
      user: { id: '1', email: 'sara@example.com', displayName: 'Sara Ahmed', role: 'Member' },
    });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/boards');
  });

  it('shows the duplicate-email message under the email field', async () => {
    await fillValid();
    await submitForm();
    http
      .expectOne('/api/auth/register')
      .flush(
        { status: 409, detail: 'An account with this email already exists' },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    const error = input('email').closest('mat-form-field')!.querySelector('mat-error');
    expect(error?.textContent).toContain('An account with this email already exists');
  });

  it('disables the submit button while the request is pending', async () => {
    await fillValid();
    await submitForm();

    expect(submit().disabled).toBe(true);
    http.expectOne('/api/auth/register');
  });
});
