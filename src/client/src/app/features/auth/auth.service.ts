import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../../core/api-base-url';

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserDto;
}

export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // The access token lives in memory only. The refresh token is an HttpOnly cookie the page cannot read.
  private readonly session = signal<AuthResponse | null>(null);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/register`, request)
      .pipe(tap((response) => this.session.set(response)));
  }

  signOut(): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/auth/sign-out`, null)
      .pipe(finalize(() => this.session.set(null)));
  }
}
