import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('GroupOrdersService', () => {
  let service: GroupOrdersService;
  let requests: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    requests = TestBed.inject(HttpTestingController);
  });
  afterEach(() => requests.verify());

  it('loads owner metadata using the reservation ID', () => {
    let identifier = '';
    service
      .getOwnerSession('reservation one')
      .subscribe((result) => (identifier = result.guestSessionId));
    const request = requests.expectOne(
      'https://test.api/reservations/reservation%20one/guest-session-id'
    );
    expect(request.request.method).toBe('GET');
    request.flush({ guestSessionId: 'owner-session', groupReservationId: 'group', status: 'Open' });
    expect(identifier).toBe('owner-session');
  });
  it('loads a typed snapshot including participant totals', () => {
    let total = 0;
    service.getSessions('group').subscribe((result) => (total = result.total));
    const request = requests.expectOne('https://test.api/guest/groups/group/sessions');
    expect(request.request.method).toBe('GET');
    request.flush({ participants: [], total: 580 });
    expect(total).toBe(580);
  });
  it('submits only the reservation reference rather than client totals', () => {
    service.submit('group', 'reservation').subscribe();
    const request = requests.expectOne('https://test.api/group-reservations/group/submit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reservationId: 'reservation' });
    request.flush([{ dishId: 'salad', quantity: 3 }]);
  });
  it('explicitly prepares a missing owner session and empty order', () => {
    service.prepare('reservation').subscribe();
    const request = requests.expectOne('https://test.api/reservations/reservation/group-pre-order');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ guestSessionId: 'owner-session', groupReservationId: 'group', status: 'Open' });
  });
  it('replaces the snapshot from the DELETE response', () => {
    let remaining = -1;
    service
      .removeSession('group', 'session/one')
      .subscribe((result) => (remaining = result.participants.length));
    const request = requests.expectOne(
      'https://test.api/guest/groups/group/sessions/session%2Fone'
    );
    expect(request.request.method).toBe('DELETE');
    request.flush({ participants: [], total: 0 });
    expect(remaining).toBe(0);
  });
  it('propagates an ownership failure', () => {
    let status = 0;
    service.getSessions('group').subscribe({ error: (error) => (status = error.status) });
    requests
      .expectOne('https://test.api/guest/groups/group/sessions')
      .flush({ error: 'Only owner' }, { status: 403, statusText: 'Forbidden' });
    expect(status).toBe(403);
  });
});
