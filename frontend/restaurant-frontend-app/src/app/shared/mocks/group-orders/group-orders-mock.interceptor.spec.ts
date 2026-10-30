import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { createDemoTokens } from '../auth/demo-auth';
import { groupOrdersMockInterceptor } from './group-orders-mock.interceptor';

describe('Demo HTTP isolation', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [
    provideHttpClient(withInterceptors([groupOrdersMockInterceptor])), provideHttpClientTesting(),
  ] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('never forwards a demo token to a real reservation API', fakeAsync(() => {
    let blocked = false;
    TestBed.inject(HttpClient).get('https://real.api/reservations', {
      headers: { Authorization: `Bearer ${createDemoTokens().idToken}` },
    }).subscribe({ error: () => blocked = true });
    TestBed.inject(HttpTestingController).expectNone('https://real.api/reservations');
    tick(400);
    expect(blocked).toBeTrue();
  }));

  it('forwards real non-demo requests unchanged', () => {
    TestBed.inject(HttpClient).get('https://real.api/reservations', { headers: { Authorization: 'Bearer real-token' } }).subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('https://real.api/reservations');
    expect(request.request.headers.get('Authorization')).toBe('Bearer real-token');
    request.flush([]);
  });
});
