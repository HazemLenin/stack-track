import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../core/api-base-url';
import { AdminUsersService } from './admin-users.service';

describe('AdminUsersService', () => {
  let service: AdminUsersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '/api' }],
    });
    service = TestBed.inject(AdminUsersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists users with query params', () => {
    service.list({ search: 'sa', sort: 'created', dir: 'desc', page: 2 }).subscribe();
    const req = http.expectOne((r) => r.url === '/api/admin/users');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('sa');
    expect(req.request.params.get('sort')).toBe('created');
    expect(req.request.params.get('dir')).toBe('desc');
    expect(req.request.params.get('page')).toBe('2');
    req.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('posts deactivate', () => {
    service.deactivate('u1').subscribe();
    const req = http.expectOne('/api/admin/users/u1/deactivate');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('posts reactivate', () => {
    service.reactivate('u1').subscribe();
    const req = http.expectOne('/api/admin/users/u1/reactivate');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('puts the role', () => {
    service.changeRole('u1', 'Admin').subscribe();
    const req = http.expectOne('/api/admin/users/u1/role');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ role: 'Admin' });
    req.flush(null);
  });
});
