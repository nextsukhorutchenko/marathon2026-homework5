import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Submit reconciles the order actually accepted by the restaurant', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  const salad = { id: 'salad', name: 'Salad', price: 180 };
  const soup = { id: 'soup', name: 'Soup', price: 220 };
  const before: GroupSessionsResponse = { total: 760, participants: [
    { id: 'owner', type: 'Owner', name: 'Owner', total: 180, isSubmitted: true, items: [{ dish: salad, quantity: 1 }] },
    { id: 'andrii', type: 'Guest', name: 'Andrii', total: 360, isSubmitted: true, items: [{ dish: salad, quantity: 2 }] },
    { id: 'olena', type: 'Guest', name: 'Olena', total: 220, isSubmitted: false, items: [{ dish: soup, quantity: 1 }] },
  ] };
  const after: GroupSessionsResponse = { ...before, participants: before.participants.map(person => ({ ...person, isSubmitted: true })) };
  beforeEach(() => {
    service = jasmine.createSpyObj('GroupOrdersService', ['getGroupInvite$', 'getSessions$', 'getOwnerSessionId$', 'getStatus$', 'submit$', 'removeSession$']);
    service.getGroupInvite$.and.returnValue(of({ GroupReservationId: 'group', StartTime: '2026-11-01T12:00:00Z', EndTime: '2026-11-01T13:00:00Z', MaxGuestCount: 5 } as GuestInvite));
    service.getSessions$.and.returnValue(of(before));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getStatus$.and.returnValue(of({ status: 'Open', currentParticipants: 3, maxCapacity: 5 }));
    service.submit$.and.returnValue(of([{ dishId: 'salad', quantity: 3 }, { dishId: 'soup', quantity: 1 }]));
    TestBed.configureTestingModule({ imports: [GroupOrderPageComponent], providers: [
      { provide: GroupOrdersService, useValue: service },
      { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) } } },
    ] });
  });
  function create() { const fixture = TestBed.createComponent(GroupOrderPageComponent); fixture.detectChanges(); return fixture; }

  it('shows $760 when a guest submits after the last poll, with no further polling', fakeAsync(() => {
    const fixture = create();
    expect(fixture.nativeElement.textContent).toContain('$540.00');
    service.getSessions$.and.returnValue(of(after));
    fixture.componentInstance.openSubmit(); fixture.componentInstance.submit(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('$760.00');
    expect(fixture.nativeElement.textContent).not.toContain("hasn't submitted");
    expect(fixture.componentInstance.status()).toBe('Submitted');
    tick(45_000); expect(service.getSessions$).toHaveBeenCalledTimes(2); fixture.destroy();
  }));

  it('does not present the stale total as final when reconciliation fails, and supports Refresh', () => {
    const fixture = create();
    service.getSessions$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    fixture.componentInstance.openSubmit(); fixture.componentInstance.submit(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("Couldn't refresh final order");
    expect(fixture.nativeElement.textContent).not.toContain('$540.00');
    expect(fixture.componentInstance.status()).toBe('Submitted');
    service.getSessions$.and.returnValue(of(after));
    const refresh = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(button => button.textContent?.trim() === 'Refresh')!;
    expect(refresh).toBeDefined(); refresh.click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('$760.00');
    expect(service.submit$).toHaveBeenCalledTimes(1);
  });

  it('keeps the group locked and hides the obsolete total during a slow final read', fakeAsync(() => {
    const fixture = create(); const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    fixture.componentInstance.openSubmit(); fixture.componentInstance.submit(); fixture.detectChanges();
    expect(fixture.componentInstance.editable()).toBeFalse();
    expect(fixture.nativeElement.textContent).not.toContain('$540.00');
    tick(45_000); expect(service.getSessions$).toHaveBeenCalledTimes(2); fixture.destroy();
  }));
});
