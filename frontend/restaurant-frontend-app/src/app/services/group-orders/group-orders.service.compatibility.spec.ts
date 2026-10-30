import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('Owner API compatibility', () => {
  let service: GroupOrdersService;
  let requests: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://api.test' }] });
    service = TestBed.inject(GroupOrdersService);
    requests = TestBed.inject(HttpTestingController);
  });
  afterEach(() => requests.verify());

  it('includes the existing Owner order without marking guest drafts submitted', () => {
    let submitted: boolean[] = [];
    service.getSessions$('group').subscribe(result => submitted = result.participants.map(person => person.isSubmitted));
    requests.expectOne('https://api.test/guest/groups/group/sessions').flush({ total: 400, participants: [
      { id: 'owner', type: 'Owner', isSubmitted: false, items: [{ dish: { id: 'salad', price: 180 }, quantity: 1 }] },
      { id: 'guest', type: 'Guest', isSubmitted: false, items: [{ dish: { id: 'soup', price: 220 }, quantity: 1 }] },
    ] });
    expect(submitted).toEqual([true, false]);
  });

  for (const message of ['Group reservation was submitted.', 'Guest limit exceeded for this reservation.']) {
    it(`loads authorized metadata when public invite says: ${message}`, () => {
      let groupId = '';
      service.getGroupInvite$('reservation').subscribe(invite => groupId = invite.GroupReservationId);
      requests.expectOne('https://api.test/reservations/reservation/invite-token').flush({ token: 'invite' });
      requests.expectOne('https://api.test/guest/invites/invite').flush({ error: message }, { status: 400, statusText: 'Bad Request' });
      requests.expectOne('https://api.test/reservations/reservation/guest-session-id').flush({ guestSessionId: 'owner', groupReservationId: 'group', status: 'Submitted' });
      requests.expectOne('https://api.test/reservations/reservation').flush({ restaurantName: 'Green & Tasty', locationAddress: 'Kyiv', startTime: '2026-11-01T12:00:00Z', endTime: '2026-11-01T14:00:00Z', guestCount: 5 });
      expect(groupId).toBe('group');
    });
  }

  it('does not mask a missing group with the metadata fallback', () => {
    let failure = 0;
    service.getGroupInvite$('missing').subscribe({ error: (error: HttpErrorResponse) => failure = error.status });
    requests.expectOne('https://api.test/reservations/missing/invite-token').flush({ error: 'Missing' }, { status: 404, statusText: 'Not Found' });
    requests.expectNone('https://api.test/reservations/missing/guest-session-id');
    expect(failure).toBe(404);
  });

  it('checks Owner authorization before requesting fallback reservation details', () => {
    let failure = 0;
    service.getGroupInvite$('other').subscribe({ error: (error: HttpErrorResponse) => failure = error.status });
    requests.expectOne('https://api.test/reservations/other/invite-token').flush({ error: 'Group reservation was submitted.' }, { status: 400, statusText: 'Bad Request' });
    requests.expectOne('https://api.test/reservations/other/guest-session-id').flush({ error: 'Only Owner' }, { status: 403, statusText: 'Forbidden' });
    requests.expectNone('https://api.test/reservations/other');
    expect(failure).toBe(403);
  });
});
