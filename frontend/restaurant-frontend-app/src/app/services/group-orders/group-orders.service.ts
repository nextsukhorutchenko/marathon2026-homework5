import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import {
  GroupOrderSnapshot,
  OwnerGroupSession,
  SubmittedGroupDish,
} from '@app/models/orders/group-order.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getOwnerSession(reservationId: string) {
    return this.http.get<OwnerGroupSession>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`,
      this.options()
    );
  }

  getSessions(groupReservationId: string) {
    return this.http.get<GroupOrderSnapshot>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
      this.options()
    );
  }

  prepare(reservationId: string) {
    return this.http.post<OwnerGroupSession>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-pre-order`,
      {},
      this.options()
    );
  }

  removeSession(groupReservationId: string, guestSessionId: string) {
    return this.http.delete<GroupOrderSnapshot>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
      this.options()
    );
  }

  submit(groupReservationId: string, reservationId: string) {
    return this.http.post<SubmittedGroupDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      { reservationId },
      this.options()
    );
  }

  private options() {
    return { context: new HttpContext().set(SKIP_ERROR_TOAST, true) };
  }
}
