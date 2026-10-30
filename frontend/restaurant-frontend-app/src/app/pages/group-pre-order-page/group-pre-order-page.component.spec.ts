import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupPreOrderPageComponent } from './group-pre-order-page.component';

describe('Legacy Owner flow on the canonical homework screen', () => {
  let fixture: ComponentFixture<GroupPreOrderPageComponent>;
  let service: jasmine.SpyObj<GroupOrdersService>;
  const snapshot: GroupSessionsResponse = { total: 580, participants: [
    { id: 'andrii', type: 'Guest', name: 'Andrii', isSubmitted: true, total: 360, items: [{ dish: { id: 'salad', name: 'Salad', price: 180 }, quantity: 2 }] },
    { id: 'olena', type: 'Guest', name: 'Olena', isSubmitted: false, total: 220, items: [{ dish: { id: 'soup', name: 'Soup', price: 220 }, quantity: 1 }] },
  ] };
  const button = (text: string) => [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(element => element.textContent?.trim() === text)!;
  beforeEach(() => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', ['getGroupInvite$', 'getOwnerSessionId$', 'getSessions$', 'getStatus$', 'submit$', 'removeSession$', 'prepare']);
    service.getGroupInvite$.and.returnValue(of({ GroupReservationId: 'group', StartTime: '2026-11-01T12:00:00Z', EndTime: '2026-11-01T14:00:00Z', MaxGuestCount: 5 } as GuestInvite));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getSessions$.and.returnValue(of(snapshot));
    service.getStatus$.and.returnValue(of({ status: 'Open', currentParticipants: 2, maxCapacity: 5 }));
    TestBed.configureTestingModule({ imports: [GroupPreOrderPageComponent], providers: [
      { provide: GroupOrdersService, useValue: service },
      { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success','error']) },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) } } },
    ] });
    fixture = TestBed.createComponent(GroupPreOrderPageComponent);
  });
  it('distinguishes draft carts from the amount sent to the restaurant', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance.consolidated().total).toBe(360);
    expect(fixture.nativeElement.textContent).toContain('Andrii');
    expect(fixture.nativeElement.textContent).toContain('Olena');
  });
  it('prepares a missing Owner session only after an explicit click', () => {
    service.getOwnerSessionId$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404, error: { error: 'Owner session not found.' } })));
    service.prepare.and.callFake(() => {
      service.getOwnerSessionId$.and.returnValue(of('owner'));
      return of({ guestSessionId: 'owner', groupReservationId: 'group', status: 'Open' });
    });
    fixture.detectChanges(); expect(service.prepare).not.toHaveBeenCalled();
    button('Prepare group pre-order').click(); fixture.detectChanges();
    expect(service.prepare).toHaveBeenCalledOnceWith('reservation');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });
  it('shows loading until the group is available', () => {
    service.getSessions$.and.returnValue(new Subject<GroupSessionsResponse>());
    fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Loading group');
  });
  it('shows a failed load instead of a false empty group', () => {
    service.getOwnerSessionId$.and.returnValue(throwError(() => new Error('Forbidden')));
    fixture.detectChanges(); expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });
  it('disables submission when there are no participants', () => {
    service.getSessions$.and.returnValue(of({ participants: [], total: 0 }));
    fixture.detectChanges(); expect(button('Submit Pre-Order').disabled).toBeTrue();
  });
  it('replaces the participant list with the confirmed DELETE response', () => {
    service.removeSession$.and.returnValue(of({ participants: [snapshot.participants[1]], total: 220 }));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[aria-label="Remove Andrii"]') as HTMLButtonElement).click(); fixture.detectChanges();
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    [...dialog.querySelectorAll('button')].find(element => element.textContent?.trim() === 'Remove Guest')!.click(); fixture.detectChanges();
    expect(service.removeSession$).toHaveBeenCalledOnceWith('group', 'andrii');
    expect(fixture.componentInstance.participants().map(person => person.name)).toEqual(['Olena']);
  });
  it('submits directly and locks mutations without silently creating an Order', () => {
    service.submit$.and.returnValue(of([{ dishId: 'salad', quantity: 2 }]));
    fixture.detectChanges(); button('Submit Pre-Order').click(); fixture.detectChanges(); button('Submit').click(); fixture.detectChanges();
    expect(service.submit$).toHaveBeenCalledOnceWith('group', 'reservation'); expect(service.prepare).not.toHaveBeenCalled();
    expect(fixture.componentInstance.status()).toBe('Submitted');
    expect(fixture.nativeElement.querySelector('[aria-label="Remove Andrii"]')).toBeNull();
  });
});
