import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import {
  catchError,
  concat,
  EMPTY,
  finalize,
  firstValueFrom,
  ignoreElements,
  Observable,
  shareReplay,
  tap,
  throwError,
  timeout,
} from 'rxjs';
import { API_BASE_URL } from '../../core/api-base-url';

const SIGNED_IN_FLAG = 'stacktrack.signed-in';

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

  // Bumped on sign out so a refresh that was already in flight cannot bring the session back.
  private generation = 0;

  // True once a session that existed (this tab or an earlier visit) was refused by the API.
  readonly sessionExpired = signal(false);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/register`, request)
      .pipe(tap((response) => this.begin(response)));
  }

  signIn(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/auth/sign-in`, { email, password })
      .pipe(tap((response) => this.begin(response)));
  }

  // Trades the HttpOnly refresh cookie for a new access token. Concurrent callers share one request,
  // because the API rotates the cookie and a second use of the old one would be treated as theft.
  refresh(): Observable<AuthResponse> {
    if (this.refreshing) return this.refreshing;
    const generation = this.generation;
    const shared: Observable<AuthResponse> = this.http.post<AuthResponse>(`${this.baseUrl}/auth/refresh`, null).pipe(
      timeout(15000), // a hung request must not make every later refresh() reuse it
      tap((response) => {
        if (generation !== this.generation) throw new Error('signed out during refresh');
        this.start(response);
      }),
      catchError((error: HttpErrorResponse) => {
        // A refresh from before a sign out is stale: it must not touch the session or flag of whoever signed in since.
        if (generation !== this.generation) return throwError(() => error);
        this.session.set(null);
        // Refused (401) after someone had signed in: the session ran out, which is not the same as never signing in.
        if (error.status === 401 && this.signedBefore()) {
          this.sessionExpired.set(true);
          this.setSignedBefore(false);
        }
        return throwError(() => error);
      }),
      finalize(() => {
        if (this.refreshing === shared) this.refreshing = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.refreshing = shared;
    return shared;
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
    // Signed out locally at once, whatever the API does. A refresh already in flight is kept (late callers reuse it
    // and its result is dropped) and awaited, so the request revokes the cookie that refresh rotated to.
    this.generation++;
    this.session.set(null);
    this.setSignedBefore(false);
    // ponytail: waits at most 5s for a hung refresh, then revokes whatever cookie the browser holds
    const settled = this.refreshing?.pipe(timeout(5000), catchError(() => EMPTY), ignoreElements()) ?? EMPTY;
    return concat(settled, this.http.post<void>(`${this.baseUrl}/auth/sign-out`, null));
  }

  // A fresh sign in or registration: any refresh still shared from an earlier session is not reused.
  private begin(response: AuthResponse): void {
    this.refreshing = null;
    this.start(response);
  }

  private start(response: AuthResponse): void {
    this.session.set(response);
    this.sessionExpired.set(false);
    this.setSignedBefore(true);
  }

  // A non-secret flag (no token) so a reopened tab can tell "session ran out" from "never signed in".
  private signedBefore(): boolean {
    try {
      return localStorage.getItem(SIGNED_IN_FLAG) !== null;
    } catch {
      return false;
    }
  }

  private setSignedBefore(value: boolean): void {
    try {
      if (value) localStorage.setItem(SIGNED_IN_FLAG, '1');
      else localStorage.removeItem(SIGNED_IN_FLAG);
    } catch {
      // storage blocked: the message is skipped, nothing else depends on it
    }
  }
}
