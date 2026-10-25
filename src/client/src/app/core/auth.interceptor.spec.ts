import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthResponse, AuthService } from '../features/auth/auth.service';
import { authInterceptor } from './auth.interceptor';

const refreshed: AuthResponse = {
  accessToken: 'jwt-2',
  accessTokenExpiresAt: '2026-10-25T12:30:00Z',
  user: { id: '1', email: 's@x.com', displayName: 'Sara', role: 'Member' },
};

describe('authInterceptor', () => {
  const state = { token: null as string | null };
  const refresh = vi.fn();
  let http: HttpTestingController;
  let client: HttpClient;
  let router: Router;

  beforeEach(() => {
    state.token = null;
    refresh.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { accessToken: () => state.token, refresh } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    router = TestBed.inject(Router);
  });

  function send(url: string) {
    client.get(url).subscribe();
    const req = http.expectOne(url);
    req.flush({});
    return req.request;
  }

  it('does not add a header when signed out', () => {
    expect(send('/api/x').headers.has('Authorization')).toBe(false);
  });

  it('adds the bearer token to API requests when signed in', () => {
    state.token = 'jwt-1';
    expect(send('/api/x').headers.get('Authorization')).toBe('Bearer jwt-1');
  });

  it('never sends the token to other hosts', () => {
    state.token = 'jwt-1';
    expect(send('https://example.com/api/x').headers.has('Authorization')).toBe(false);
  });

  it('refreshes once on 401 and replays the request with the new token', () => {
    state.token = 'jwt-1';
    refresh.mockReturnValue(of(refreshed));
    let result: unknown;
    client.get('/api/boards').subscribe((r) => (result = r));

    http.expectOne('/api/boards').flush({}, { status: 401, statusText: 'Unauthorized' });
    const retry = http.expectOne('/api/boards');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer jwt-2');
    retry.flush({ ok: true });

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(result).toEqual({ ok: true });
  });

  it('redirects with reason=expired when refresh fails', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/boards/42');
    refresh.mockReturnValue(throwError(() => ({ status: 401 })));
    let error: { status: number } | undefined;
    client.get('/api/boards').subscribe({ error: (e) => (error = e) });

    http.expectOne('/api/boards').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(error?.status).toBe(401);
    expect(navigate).toHaveBeenCalledWith(['/auth/sign-in'], {
      queryParams: { returnUrl: '/boards/42', reason: 'expired' },
    });
  });

  it('does not refresh when a retried request fails for another reason', () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    refresh.mockReturnValue(of(refreshed));
    client.get('/api/boards').subscribe({ error: () => undefined });

    http.expectOne('/api/boards').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/boards').flush({}, { status: 500, statusText: 'Server Error' });

    expect(navigate).not.toHaveBeenCalled();
  });

  it('never refreshes for auth endpoints', () => {
    client.post('/api/auth/sign-in', {}).subscribe({ error: () => undefined });

    http.expectOne('/api/auth/sign-in').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(refresh).not.toHaveBeenCalled();
  });

  it('does not refresh for other hosts', () => {
    client.get('https://example.com/x').subscribe({ error: () => undefined });

    http.expectOne('https://example.com/x').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(refresh).not.toHaveBeenCalled();
  });
});

describe('authInterceptor with the real AuthService', () => {
  it('parallel 401s share one refresh and all succeed', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const client = TestBed.inject(HttpClient);
    const urls = ['/api/a', '/api/b', '/api/c'];
    const results: unknown[] = [];
    urls.forEach((url) => client.get(url).subscribe((r) => results.push(r)));

    urls.forEach((url) => http.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' }));
    http.expectOne('/api/auth/refresh').flush(refreshed);
    urls.forEach((url) => {
      const retry = http.expectOne(url);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer jwt-2');
      retry.flush({ ok: url });
    });

    expect(results).toHaveLength(3);
    http.verify();
  });
});
