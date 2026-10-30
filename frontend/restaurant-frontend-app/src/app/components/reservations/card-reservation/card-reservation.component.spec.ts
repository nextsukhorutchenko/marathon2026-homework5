import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { provideRouter } from '@angular/router';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { Roles } from '@app/shared/constants/roles';

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: API_BASE_URL, useValue: 'https://test.api' }],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('r', {
      id: 'reservation', tableId: 'table', startTime: new Date('2027-01-01T18:00:00Z'), endTime: new Date('2027-01-01T19:00:00Z'),
      guestCount: 3, status: ReservationStatusCustomer.Reserved, restaurantName: 'Green & Tasty', locationAddress: 'Address', waiterId: 'waiter'
    } satisfies ReservationCustomer);
    fixture.detectChanges();
  });

  it('links a customer group reservation to the owner screen', () => {
    expect(fixture.nativeElement.querySelector('a[href="/reservations/reservation/group-pre-order"]')).not.toBeNull();
  });

  it('does not offer owner management to a waiter', () => {
    fixture.componentRef.setInput('role', Roles.WAITER);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a[href*="group-pre-order"]')).toBeNull();
  });
});
