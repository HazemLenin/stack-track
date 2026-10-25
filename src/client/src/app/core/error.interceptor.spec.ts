import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { errorInterceptor } from './error.interceptor';
import { ToastService } from './toast.service';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let show: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    show = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: { show } },
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  it('toasts once and rethrows the original error when the server is unreachable', () => {
    let caught: unknown;
    http.get('/api/x').subscribe({ error: (e) => (caught = e) });

    ctrl.expectOne('/api/x').error(new ProgressEvent('error'), { status: 0 });

    expect(show).toHaveBeenCalledTimes(1);
    expect(show).toHaveBeenCalledWith("Can't reach the server. Please try again in a moment.");
    expect(caught).toBeInstanceOf(HttpErrorResponse);
    expect((caught as HttpErrorResponse).status).toBe(0);
  });

  it('does not toast on success', () => {
    http.get('/api/x').subscribe();
    ctrl.expectOne('/api/x').flush({});
    expect(show).not.toHaveBeenCalled();
  });

  it('does not toast on 401', () => {
    http.get('/api/x').subscribe({ error: () => undefined });
    ctrl.expectOne('/api/x').flush('', { status: 401, statusText: 'Unauthorized' });
    expect(show).not.toHaveBeenCalled();
  });
});
