import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { Subscription, finalize, forkJoin, interval, switchMap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { Participant } from '@app/models/group-orders/participant.model';
import { SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';
import {
  GroupReservationStatus,
  GroupStatusResponse,
} from '@app/models/group-orders/group-status.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private polling: Subscription | null = null;
  private acceptedDishes: SubmittedDish[] | null = null;
  readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly status = signal<GroupReservationStatus>('Open');
  readonly capacity = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly canPrepare = signal(false);
  readonly busy = signal(false);
  readonly refreshing = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly refreshError = signal(false);
  readonly finalOrderPending = signal(false);
  readonly confirmingSubmit = signal(false);
  readonly pendingRemoval = signal<Participant | null>(null);
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly pendingCount = computed(
    () => this.participants().filter((person) => !person.isSubmitted).length
  );
  readonly hasDishes = computed(() =>
    this.participants().some((person) => person.items.length > 0)
  );
  readonly editable = computed(() => this.status() === 'Open');
  readonly canSubmit = computed(
    () =>
      this.editable() && this.consolidated().items.length > 0 && !this.busy() && !this.refreshing()
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading() || this.busy() || this.refreshing()) return;
    this.stopPolling();
    this.loading.set(true);
    this.error.set('');
    this.canPrepare.set(false);
    this.service
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) =>
          forkJoin({
            ownerId: this.service.getOwnerSessionId$(this.reservationId),
            sessions: this.service.getSessions$(invite.GroupReservationId),
            group: this.service.getStatus$(invite.GroupReservationId),
          }).pipe(switchMap((result) => [{ ...result, invite }]))
        ),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (result) => {
          this.invite.set(result.invite);
          this.ownerSessionId.set(result.ownerId);
          this.participants.set(result.sessions.participants);
          this.applyStatus(result.group);
          this.updatedAt.set(new Date());
          this.refreshError.set(false);
          this.startPolling();
        },
        error: (error: unknown) => {
          const message = error instanceof HttpErrorResponse ? getServerErrorMessage(error) : '';
          this.canPrepare.set(
            error instanceof HttpErrorResponse &&
              error.status === 404 &&
              /owner session/i.test(message)
          );
          this.error.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Group reservation not found'
              : message || 'Could not load the group pre-order.'
          );
        },
      });
  }

  prepareGroup(): void {
    if (this.busy() || this.loading() || !this.canPrepare()) return;
    this.busy.set(true);
    this.service
      .prepare(this.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.load();
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.error.set(
            (error instanceof HttpErrorResponse ? getServerErrorMessage(error) : '') ||
              'Could not prepare the group pre-order.'
          );
        },
      });
  }

  refresh(): void {
    const groupId = this.invite()?.GroupReservationId;
    if (
      !groupId ||
      this.status() === 'Closed' ||
      this.loading() ||
      this.busy() ||
      this.refreshing()
    )
      return;
    if (this.status() === 'Submitted') {
      this.reconcileSubmitted(groupId);
      return;
    }
    this.refreshing.set(true);
    forkJoin({
      sessions: this.service.getSessions$(groupId, { silent: true }),
      group: this.service.getStatus$(groupId, { silent: true }),
    })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.refreshing.set(false))
      )
      .subscribe({
        next: (result) => {
          this.participants.set(result.sessions.participants);
          this.applyStatus(result.group);
          this.updatedAt.set(new Date());
          this.refreshError.set(false);
        },
        error: () => this.refreshError.set(true),
      });
  }

  openSubmit(): void {
    if (this.canSubmit()) this.confirmingSubmit.set(true);
  }

  submit(): void {
    const groupId = this.invite()?.GroupReservationId;
    if (!groupId || !this.confirmingSubmit() || !this.canSubmit()) return;
    this.busy.set(true);
    this.service
      .submit$(groupId, this.reservationId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false))
      )
      .subscribe({
        next: (accepted) => {
          this.acceptedDishes = accepted;
          this.status.set('Submitted');
          this.stopPolling();
          this.confirmingSubmit.set(false);
          this.toast.success('Pre-order sent to the restaurant');
          // The server may have accepted a guest cart submitted after our last poll.
          if (!this.matchesAcceptedOrder()) {
            this.finalOrderPending.set(true);
            this.reconcileSubmitted(groupId);
          }
        },
        error: () => this.confirmingSubmit.set(false),
      });
  }

  requestRemoval(person: Participant): void {
    if (person.type === 'Guest' && this.editable() && !this.busy() && !this.refreshing())
      this.pendingRemoval.set(person);
  }

  removeGuest(): void {
    const person = this.pendingRemoval();
    const groupId = this.invite()?.GroupReservationId;
    if (
      !person ||
      person.type !== 'Guest' ||
      !groupId ||
      !this.editable() ||
      this.busy() ||
      this.refreshing()
    )
      return;
    this.busy.set(true);
    this.service
      .removeSession$(groupId, person.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false))
      )
      .subscribe({
        next: (result) => {
          this.participants.set(result.participants);
          this.updatedAt.set(new Date());
          this.pendingRemoval.set(null);
          this.toast.success('Participant removed');
        },
        error: () => this.pendingRemoval.set(null),
      });
  }

  closeDialogs(): void {
    if (this.busy()) return;
    this.pendingRemoval.set(null);
    this.confirmingSubmit.set(false);
  }

  private matchesAcceptedOrder(): boolean {
    const items = this.consolidated().items;
    return (
      this.acceptedDishes === null ||
      (this.acceptedDishes.length === items.length &&
        this.acceptedDishes.every((accepted) =>
          items.some(
            (item) => item.dish.id === accepted.dishId && item.quantity === accepted.quantity
          )
        ))
    );
  }

  private reconcileSubmitted(groupId: string): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.service
      .getSessions$(groupId, { silent: true })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.refreshing.set(false))
      )
      .subscribe({
        next: (snapshot) => {
          this.participants.set(snapshot.participants);
          const reconciled = this.matchesAcceptedOrder();
          this.finalOrderPending.set(!reconciled);
          this.refreshError.set(!reconciled);
          if (reconciled) this.updatedAt.set(new Date());
        },
        error: () => this.refreshError.set(true),
      });
  }

  private applyStatus(group: GroupStatusResponse): void {
    this.status.set(group.status);
    this.capacity.set(group.maxCapacity);
    if (group.status !== 'Open') {
      this.stopPolling();
      this.confirmingSubmit.set(false);
      this.pendingRemoval.set(null);
    }
  }

  private startPolling(): void {
    if (!this.editable() || this.polling) return;
    this.polling = interval(15_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refresh());
  }

  private stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = null;
  }
}
