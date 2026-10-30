import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { GroupOrderSnapshot } from '@app/models/orders/group-order.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupPreOrderPageComponent } from './group-pre-order-page.component';

describe('GroupPreOrderPageComponent', () => {
  let fixture: ComponentFixture<GroupPreOrderPageComponent>;
  let service: jasmine.SpyObj<GroupOrdersService>;
  const snapshot: GroupOrderSnapshot = {
    total: 580,
    participants: [
      {
        id: 'session-one',
        type: 'Guest',
        name: 'Andrii',
        items: [{ dish: { id: 'salad', name: 'Salad', price: 180 }, quantity: 2 }],
        total: 360,
        isSubmitted: true,
      },
      {
        id: 'session-two',
        type: 'Guest',
        name: 'Olena',
        items: [{ dish: { id: 'soup', name: 'Soup', price: 220 }, quantity: 1 }],
        total: 220,
        isSubmitted: false,
      },
    ],
  };
  beforeEach(async () => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getOwnerSession',
      'getSessions',
      'removeSession',
      'submit',
      'prepare',
    ]);
    service.getOwnerSession.and.returnValue(
      of({ guestSessionId: 'owner-session', groupReservationId: 'group', status: 'Open' })
    );
    service.prepare.and.returnValue(
      of({ guestSessionId: 'owner-session', groupReservationId: 'group', status: 'Open' })
    );
    service.getSessions.and.returnValue(of(snapshot));
    await TestBed.configureTestingModule({
      imports: [GroupPreOrderPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) } },
        },
        { provide: GroupOrdersService, useValue: service },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']),
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(GroupPreOrderPageComponent);
  });
  const button = (
    fixture: ComponentFixture<GroupPreOrderPageComponent>,
    label: string
  ): HTMLButtonElement =>
    Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>
    ).find((element) => element.textContent?.trim() === label)!;

  it('renders participant dishes and distinguishes draft totals from submitted totals', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Andrii');
    expect(fixture.nativeElement.textContent).toContain('Olena');
    expect(fixture.nativeElement.textContent).toContain('Salad');
    expect(fixture.componentInstance.submittedTotal()).toBe(360);
    expect(fixture.componentInstance.snapshot()?.total).toBe(580);
  });
  it('prepares a legacy group explicitly after the missing Owner session response', () => {
    service.getOwnerSession.and.returnValue(
      throwError(
        () => new HttpErrorResponse({ status: 404, error: { error: 'Owner session not found.' } })
      )
    );
    fixture.detectChanges();
    button(fixture, 'Prepare group pre-order').click();
    fixture.detectChanges();
    expect(service.prepare).toHaveBeenCalledWith('reservation');
    expect(fixture.nativeElement.textContent).toContain('Andrii');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('shows loading until the group is available', () => {
    service.getSessions.and.returnValue(new Subject<GroupOrderSnapshot>());
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading group');
  });
  it('shows an error instead of a false empty group', () => {
    service.getOwnerSession.and.returnValue(throwError(() => new Error('Forbidden')));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('No participants yet');
  });
  it('shows the empty state and disables submission', () => {
    service.getSessions.and.returnValue(of({ participants: [], total: 0 }));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No participants yet');
    expect(button(fixture, 'Submit group pre-order').disabled).toBeTrue();
  });
  it('updates the displayed snapshot after confirming removal', () => {
    service.removeSession.and.returnValue(
      of({ participants: [snapshot.participants[1]], total: 220 })
    );
    fixture.detectChanges();
    (
      fixture.nativeElement.querySelector('[aria-label="Remove Andrii"]') as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    button(fixture, 'Confirm removal').click();
    fixture.detectChanges();
    expect(service.removeSession).toHaveBeenCalledWith('group', 'session-one');
    expect(fixture.componentInstance.snapshot()?.total).toBe(220);
    expect(fixture.nativeElement.textContent).not.toContain('Andrii');
  });
  it('submits through the consolidated API and locks further mutations', () => {
    service.submit.and.returnValue(of([{ dishId: 'salad', quantity: 2 }]));
    fixture.detectChanges();
    button(fixture, 'Submit group pre-order').click();
    fixture.detectChanges();
    button(fixture, 'Confirm submission').click();
    fixture.detectChanges();
    expect(service.submit).toHaveBeenCalledWith('group', 'reservation');
    expect(fixture.nativeElement.textContent).toContain('Group pre-order submitted');
    expect(fixture.nativeElement.querySelector('[aria-label="Remove Andrii"]')).toBeNull();
  });
});
