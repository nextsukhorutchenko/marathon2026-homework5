import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import {
  GroupOrderSnapshot,
  OwnerGroupSession,
  SubmittedGroupDish,
} from '@app/models/orders/group-order.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { catchError, map, Observable, switchMap, throwError } from 'rxjs';
import { GuestInvite, GroupInviteToken } from '@app/models/reservations/guest-invite.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    const reservationPath = `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`;
    return this.http.get<GroupInviteToken>(`${reservationPath}/invite-token`, this.options()).pipe(
      switchMap((result) =>
        this.http.get<GuestInvite>(
          `${this.baseUrl}/guest/invites/${encodeURIComponent(result.token)}`,
          this.options()
        )
      ),
      catchError((error: unknown) => {
        // A public invitation can close while its Owner still has permission to review the group.
        if (
          !(error instanceof HttpErrorResponse) ||
          error.status !== 400 ||
          !/submitted|expired|limit/i.test(getServerErrorMessage(error))
        )
          return throwError(() => error);
        return this.getOwnerSession(reservationId).pipe(
          switchMap((owner) =>
            this.http.get<ReservationCustomer>(reservationPath, this.options()).pipe(
              map((reservation) => ({
                GroupReservationId: owner.groupReservationId,
                LocationId: '',
                RestaurantName: reservation.restaurantName,
                LocationAddress: reservation.locationAddress,
                StartTime: String(reservation.startTime),
                EndTime: String(reservation.endTime),
                MaxGuestCount: reservation.guestCount,
                TableNumber: 0,
                InviterName: '',
              }))
            )
          )
        );
      })
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.getOwnerSession(reservationId).pipe(map((owner) => owner.guestSessionId));
  }

  getSessions$(groupId: string, options?: { silent?: boolean }): Observable<GroupSessionsResponse> {
    return this.http
      .get<GroupSessionsResponse>(
        `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions`,
        this.requestOptions(options)
      )
      .pipe(map((snapshot) => this.normalizeOwner(snapshot)));
  }

  getStatus$(groupId: string, options?: { silent?: boolean }): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/status`,
      this.requestOptions(options)
    );
  }

  submit$(groupId: string, reservationId: string): Observable<SubmittedGroupDish[]> {
    return this.http.post<SubmittedGroupDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupId)}/submit`,
      { reservationId }
    );
  }

  removeSession$(groupId: string, sessionId: string): Observable<GroupSessionsResponse> {
    return this.http
      .delete<GroupSessionsResponse>(
        `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions/${encodeURIComponent(sessionId)}`
      )
      .pipe(map((snapshot) => this.normalizeOwner(snapshot)));
  }

  private normalizeOwner(snapshot: GroupSessionsResponse): GroupSessionsResponse {
    return {
      ...snapshot,
      participants: snapshot.participants.map((participant) =>
        participant.type === 'Owner' && participant.items.length > 0
          ? { ...participant, isSubmitted: true }
          : participant
      ),
    };
  }

  private requestOptions(options?: { silent?: boolean }) {
    return { context: new HttpContext().set(SKIP_ERROR_TOAST, options?.silent ?? false) };
  }

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
