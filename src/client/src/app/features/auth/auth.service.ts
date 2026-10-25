import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, firstValueFrom, Observable, shareReplay, tap, throwError } from 'rxjs';
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

  private refreshing: Observable<AuthResponse> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/register`, request)
      .pipe(tap((response) => this.session.set(response)));
  }

  signIn(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/sign-in`, { email, password })
      .pipe(tap((response) => this.session.set(response)));
  }

  // Trades the HttpOnly refresh cookie for a new access token. Concurrent callers share one request,
  // because the API rotates the cookie and a second use of the old one would be treated as theft.
  refresh(): Observable<AuthResponse> {
    this.refreshing ??= this.http.post<AuthResponse>(`${this.baseUrl}/auth/refresh`, null).pipe(
      tap((response) => this.session.set(response)),
      catchError((error) => {
        this.session.set(null);
        return throwError(() => error);
      }),
      finalize(() => (this.refreshing = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshing;
  }

  // Runs at startup: a valid cookie signs the user back in after a reload. Failure just means signed out.
  async restore(): Promise<void> {
    try {
      await firstValueFrom(this.refresh());
    } catch {
      // not signed in
    }
  }

  signOut(): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/auth/sign-out`, null)
      .pipe(finalize(() => this.session.set(null)));
  }
}
