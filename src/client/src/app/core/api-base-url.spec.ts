import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from './api-base-url';

describe('API_BASE_URL', () => {
  it('defaults to /api', () => {
    expect(TestBed.inject(API_BASE_URL)).toBe('/api');
  });

  it('can be overridden', () => {
    TestBed.configureTestingModule({ providers: [{ provide: API_BASE_URL, useValue: 'http://x' }] });
    expect(TestBed.inject(API_BASE_URL)).toBe('http://x');
  });
});
