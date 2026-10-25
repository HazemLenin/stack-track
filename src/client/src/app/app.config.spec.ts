import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { AuthService } from './features/auth/auth.service';

describe('appConfig', () => {
  it('restores the session from the refresh cookie before the app starts', async () => {
    TestBed.configureTestingModule({ providers: [...appConfig.providers, provideHttpClientTesting()] });
    const init = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);

    http.expectOne('/api/auth/refresh').flush({
      accessToken: 'jwt',
      accessTokenExpiresAt: '2026-10-25T12:15:00Z',
      user: { id: '1', email: 's@x.com', displayName: 'Sara', role: 'Member' },
    });
    await init.donePromise;

    expect(TestBed.inject(AuthService).user()?.displayName).toBe('Sara');
  });
});
