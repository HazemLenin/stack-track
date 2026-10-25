import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/api-base-url';

export type UserRole = 'Admin' | 'Member';

export interface AdminUser {
  id: string;
  displayName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface UserPage {
  items: AdminUser[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListUsersParams {
  search: string;
  sort: 'name' | 'created';
  dir: 'asc' | 'desc';
  page: number;
}

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/admin/users`;

  list(p: ListUsersParams): Observable<UserPage> {
    const params = new HttpParams()
      .set('search', p.search)
      .set('sort', p.sort)
      .set('dir', p.dir)
      .set('page', p.page);
    return this.http.get<UserPage>(this.url, { params });
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/deactivate`, null);
  }

  reactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/reactivate`, null);
  }

  changeRole(id: string, role: UserRole): Observable<void> {
    return this.http.put<void>(`${this.url}/${id}/role`, { role });
  }
}
