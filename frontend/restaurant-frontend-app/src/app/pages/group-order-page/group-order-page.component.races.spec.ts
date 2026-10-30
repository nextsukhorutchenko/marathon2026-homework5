import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Owner synchronization races', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  const sessions: GroupSessionsResponse = { total: 10, participants: [{ id: 'guest', name: 'Guest', type: 'Guest', isSubmitted: true, total: 10, items: [{ dish: { id: 'dish', name: 'Dish', price: 10 }, quantity: 1 }] }] };
  beforeEach(() => {
    service = jasmine.createSpyObj('GroupOrdersService', ['getGroupInvite$', 'getOwnerSessionId$', 'getSessions$', 'getStatus$', 'submit$', 'removeSession$']);
    service.getGroupInvite$.and.returnValue(of({ GroupReservationId: 'group', StartTime: '2026-11-01T12:00:00Z', EndTime: '2026-11-01T13:00:00Z', MaxGuestCount: 5 } as GuestInvite));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getSessions$.and.returnValue(of(sessions));
    service.getStatus$.and.returnValue(of({ status: 'Open', currentParticipants: 1, maxCapacity: 5 }));
    TestBed.configureTestingModule({ imports: [GroupOrderPageComponent], providers: [
      { provide: GroupOrdersService, useValue: service },
      { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) } } },
    ] });
  });
  function create() { const fixture = TestBed.createComponent(GroupOrderPageComponent); fixture.detectChanges(); return fixture; }

  it('does not overlap slow requests or manual refresh and keeps cards visible', fakeAsync(() => {
    const fixture = create();
    const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    tick(15_000); fixture.componentInstance.refresh(); tick(45_000); fixture.detectChanges();
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('app-participant-card').length).toBe(1);
    fixture.componentInstance.openSubmit(); expect(fixture.componentInstance.confirmingSubmit()).toBeFalse();
    fixture.destroy(); pending.next({ total: 0, participants: [] }); pending.complete();
    expect(fixture.componentInstance.participants().length).toBe(1);
  }));
  it('does not refresh while a submit request is pending', fakeAsync(() => {
    const fixture = create(); const pending = new Subject<{ dishId: string; quantity: number }[]>();
    service.submit$.and.returnValue(pending);
    fixture.componentInstance.openSubmit(); fixture.componentInstance.submit(); tick(45_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    pending.next([{ dishId: 'dish', quantity: 1 }]); pending.complete(); tick(30_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.status()).toBe('Submitted'); fixture.destroy();
  }));
  it('stops after another browser closes the group', fakeAsync(() => {
    const fixture = create();
    service.getStatus$.and.returnValue(of({ status: 'Closed', currentParticipants: 1, maxCapacity: 5 }));
    tick(15_000); tick(45_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    expect(fixture.componentInstance.editable()).toBeFalse(); fixture.destroy();
  }));
  it('retry creates only one polling cycle', fakeAsync(() => {
    const invite = service.getGroupInvite$.calls;
    service.getSessions$.and.returnValue(throwError(() => new Error('Unavailable')));
    const fixture = create();
    service.getSessions$.and.returnValue(of(sessions)); fixture.componentInstance.load();
    tick(30_000);
    expect(invite.count()).toBe(2);
    expect(service.getSessions$).toHaveBeenCalledTimes(4); fixture.destroy();
  }));
});
