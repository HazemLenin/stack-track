import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { API_BASE_URL } from './api-base-url';
import { boardAccessInterceptor } from './board-access.interceptor';
import { appInterceptors } from '../app.config';
import { ToastService } from './toast.service';

describe('boardAccessInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let show: ReturnType<typeof vi.fn>;
  let navigateByUrl: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    show = vi.fn();
    navigateByUrl = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([boardAccessInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        { provide: ToastService, useValue: { show } },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  const fail = (url: string, status: number) => {
    http.get(url).subscribe({ error: () => undefined });
    ctrl.expectOne(url).flush('', { status, statusText: 'x' });
  };

  it('404 on a board request shows the message and goes to /boards', () => {
    fail('/api/boards/abc/lists', 404);
    expect(show).toHaveBeenCalledWith('This board is no longer available');
    expect(navigateByUrl).toHaveBeenCalledWith('/boards');
  });

  it('other 404s are ignored', () => {
    fail('/api/users/abc', 404);
    fail('/api/boardsX/1', 404);
    expect(show).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('non-404 errors on boards are ignored', () => {
    fail('/api/boards/abc', 500);
    expect(show).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('is the last toast when wired with the error interceptor as in the app', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors(appInterceptors)),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        { provide: ToastService, useValue: { show } },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });
    TestBed.inject(HttpClient).get('/api/boards/abc').subscribe({ error: () => undefined });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/boards/abc')
      .flush({ detail: 'The board was not found.' }, { status: 404, statusText: 'Not Found' });
    expect(show).toHaveBeenLastCalledWith('This board is no longer available');
  });
});
