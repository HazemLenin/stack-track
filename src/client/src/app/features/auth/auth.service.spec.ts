import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthResponse, AuthService } from './auth.service';

const response: AuthResponse = {
  accessToken: 'jwt-1',
  accessTokenExpiresAt: '2026-10-25T12:15:00Z',
  user: { id: '1', email: 'sara@example.com', displayName: 'Sara Ahmed', role: 'Member' },
};

const request = {
  displayName: 'Sara Ahmed',
  email: 'sara@example.com',
  password: 'Valid1234',
  confirmPassword: 'Valid1234',
};

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('starts signed out', () => {
    expect(service.user()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('register posts the form and keeps the session in memory', () => {
    service.register(request).subscribe();

    const req = http.expectOne('/api/auth/register');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(response);

    expect(service.user()?.displayName).toBe('Sara Ahmed');
    expect(service.accessToken()).toBe('jwt-1');
  });

  it('never writes tokens to web storage', () => {
    const before = JSON.stringify([{ ...localStorage }, { ...sessionStorage }]);

    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    expect(JSON.stringify([{ ...localStorage }, { ...sessionStorage }])).toBe(before);
    expect(before).not.toContain('jwt-1');
  });

  it('failed register leaves the user signed out', () => {
    service.register(request).subscribe({ error: () => undefined });
    http.expectOne('/api/auth/register').flush({}, { status: 409, statusText: 'Conflict' });

    expect(service.user()).toBeNull();
  });

  it('signOut posts to the API and clears the session', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    service.signOut().subscribe();
    const req = http.expectOne('/api/auth/sign-out');
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.user()).toBeNull();
  });

  it('signOut clears the session even when the request fails', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    service.signOut().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/sign-out').flush({}, { status: 500, statusText: 'Server Error' });

    expect(service.user()).toBeNull();
  });
});
