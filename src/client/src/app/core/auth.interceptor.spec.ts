import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../features/auth/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const token = { value: null as string | null };

  beforeEach(() => {
    token.value = null;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { accessToken: () => token.value } },
      ],
    });
  });

  function send(url: string) {
    TestBed.inject(HttpClient).get(url).subscribe();
    const req = TestBed.inject(HttpTestingController).expectOne(url);
    req.flush({});
    return req.request;
  }

  it('does not add a header when signed out', () => {
    expect(send('/api/x').headers.has('Authorization')).toBe(false);
  });

  it('adds the bearer token to API requests when signed in', () => {
    token.value = 'jwt-1';
    expect(send('/api/x').headers.get('Authorization')).toBe('Bearer jwt-1');
  });

  it('never sends the token to other hosts', () => {
    token.value = 'jwt-1';
    expect(send('https://example.com/api/x').headers.has('Authorization')).toBe(false);
  });
});
