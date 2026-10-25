import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthResponse, AuthService } from './auth.service';

const response: AuthResponse = {
  accessToken: 'jwt-1',
  accessTokenExpiresAt: '2026-10-25T12:15:00Z',
  user: { id: '1', email: 'sara@example.com', displayName: 'Sara Ahmed', role: 'Member' },
};

const FLAG = 'stacktrack.signed-in';

const request = {
  displayName: 'Sara Ahmed',
  email: 'sara@example.com',
  password: 'Valid1234',
  confirmPassword: 'Valid1234',
};

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  // Node 26 shadows jsdom's localStorage with an unusable stub, so tests bring their own.
  const store = new Map<string, string>();
  const session = new Map<string, string>();

  afterEach(() => vi.unstubAllGlobals());

  beforeEach(() => {
    store.clear();
    session.clear();
    vi.stubGlobal('sessionStorage', {
      getItem: (key: string) => session.get(key) ?? null,
      setItem: (key: string, value: string) => session.set(key, value),
      removeItem: (key: string) => session.delete(key),
    });
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => store.set(key, value),
      removeItem: (key: string) => store.delete(key),
    });
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

  it('never writes tokens to web storage, only a signed-in flag', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    expect([...store.keys()]).toEqual([FLAG]);
    expect(session.size).toBe(0);
    expect(JSON.stringify([...store])).not.toContain('jwt-1');
  });

  it('flags the session as expired when refresh is refused after an earlier sign in', () => {
    store.set(FLAG, '1');

    service.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(service.sessionExpired()).toBe(true);
    expect(store.has(FLAG)).toBe(false);
  });

  it('does not flag expiry for someone who never signed in', () => {
    service.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(service.sessionExpired()).toBe(false);
  });

  it('does not flag expiry when refresh fails for a non-401 reason', () => {
    store.set(FLAG, '1');

    service.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/refresh').flush({}, { status: 503, statusText: 'Unavailable' });

    expect(service.sessionExpired()).toBe(false);
  });

  it('signing out forgets the signed-in flag', () => {
    store.set(FLAG, '1');

    service.signOut().subscribe();
    http.expectOne('/api/auth/sign-out').flush(null);

    expect(store.has(FLAG)).toBe(false);
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
    expect(localStorage.getItem(FLAG)).toBeNull();
  });

  it('a refresh still in flight when the user signs out does not bring the session back', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    service.refresh().subscribe({ error: () => undefined });
    const refresh = http.expectOne('/api/auth/refresh');
    service.signOut().subscribe();
    // Signed out locally at once; the request waits for the refresh so it revokes the cookie that refresh rotated to.
    expect(service.user()).toBeNull();
    http.expectNone('/api/auth/sign-out');
    // A refresh asked for while sign out is pending reuses the stale one: no second POST to the rotating cookie.
    service.refresh().subscribe({ error: () => undefined });
    http.expectNone('/api/auth/refresh');
    refresh.flush(response);
    http.expectOne('/api/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });

    expect(service.user()).toBeNull();
    expect(localStorage.getItem(FLAG)).toBeNull();
  });

  it('a refresh refused after the user signed out is not reported as an expired session', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    service.refresh().subscribe({ error: () => undefined });
    const refresh = http.expectOne('/api/auth/refresh');
    service.signOut().subscribe();
    refresh.flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });

    expect(service.sessionExpired()).toBe(false);
  });

  it('signIn posts the credentials and keeps the session in memory', () => {
    service.signIn('sara@example.com', 'Valid1234').subscribe();

    const req = http.expectOne('/api/auth/sign-in');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'sara@example.com', password: 'Valid1234' });
    req.flush(response);

    expect(service.user()?.displayName).toBe('Sara Ahmed');
    expect(service.accessToken()).toBe('jwt-1');
  });

  it('refresh replaces the access token', () => {
    service.refresh().subscribe();
    const req = http.expectOne('/api/auth/refresh');
    expect(req.request.method).toBe('POST');
    req.flush({ ...response, accessToken: 'jwt-2' });

    expect(service.accessToken()).toBe('jwt-2');
  });

  it('concurrent refresh calls share one request', () => {
    service.refresh().subscribe();
    service.refresh().subscribe();

    http.expectOne('/api/auth/refresh').flush(response);
    expect(service.accessToken()).toBe('jwt-1');
  });

  it('a failed refresh clears the session', () => {
    service.register(request).subscribe();
    http.expectOne('/api/auth/register').flush(response);

    service.refresh().subscribe({ error: () => undefined });
    http.expectOne('/api/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(service.user()).toBeNull();
  });

  it('a later refresh makes a new request after the first one finished', () => {
    service.refresh().subscribe();
    http.expectOne('/api/auth/refresh').flush(response);

    service.refresh().subscribe();
    http.expectOne('/api/auth/refresh').flush(response);
  });

  it('restore signs the user in from the refresh cookie', async () => {
    const done = service.restore();
    http.expectOne('/api/auth/refresh').flush(response);
    await done;

    expect(service.user()?.email).toBe('sara@example.com');
  });

  it('restore without a valid cookie stays signed out and does not throw', async () => {
    const done = service.restore();
    http.expectOne('/api/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    await expect(done).resolves.toBeUndefined();
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
