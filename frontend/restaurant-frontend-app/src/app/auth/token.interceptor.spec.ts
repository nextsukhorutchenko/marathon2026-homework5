import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthenticationService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('adds the owner token to group session listing', () => {
    authService.getTokens.and.returnValue({ idToken: 'owner-jwt', accessToken: 'access' });
    http.get('https://test.api/guest/groups/group/sessions').subscribe();
    const request = httpMock.expectOne('https://test.api/guest/groups/group/sessions');
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-jwt');
    request.flush({ participants: [], total: 0 });
  });

  it('adds the owner token to removing a participant', () => {
    authService.getTokens.and.returnValue({ idToken: 'owner-jwt', accessToken: 'access' });
    http.delete('https://test.api/guest/groups/group/sessions/session').subscribe();
    const request = httpMock.expectOne('https://test.api/guest/groups/group/sessions/session');
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-jwt');
    request.flush({ participants: [], total: 0 });
  });

  it('preserves the separate guest session token and request body', () => {
    authService.getTokens.and.returnValue({ idToken: 'owner-jwt', accessToken: 'access' });
    const payload = { dishId: 'salad', quantity: 2 };
    http.post('https://test.api/guest/sessions/session/dishes', payload, { headers: { Authorization: 'Bearer guest-token' } }).subscribe();
    const request = httpMock.expectOne('https://test.api/guest/sessions/session/dishes');
    expect(request.request.headers.get('Authorization')).toBe('Bearer guest-token');
    expect(request.request.body).toEqual(payload);
    request.flush({ message: 'Added' });
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('keeps background group status failures silent in the guest branch', () => {
    let status = 0;
    const url = 'https://test.api/guest/groups/group/status';
    http.get(url, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).subscribe({ error: error => status = error.status });
    httpMock.expectOne(url).flush({ error: 'Unavailable' }, { status: 500, statusText: 'Server Error' });
    expect(status).toBe(500);
    expect(toastService.error).not.toHaveBeenCalled();
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });
});
