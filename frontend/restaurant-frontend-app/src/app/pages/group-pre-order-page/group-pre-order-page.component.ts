import { DecimalPipe } from '@angular/common';
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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, map, switchMap } from 'rxjs';
import {
  GroupOrderParticipant,
  GroupOrderSnapshot,
  OwnerGroupSession,
} from '@app/models/orders/group-order.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

@Component({
  selector: 'app-group-pre-order-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  templateUrl: './group-pre-order-page.component.html',
  styleUrl: './group-pre-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupPreOrderPageComponent implements OnInit {
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  readonly ownerSession = signal<OwnerGroupSession | null>(null);
  readonly snapshot = signal<GroupOrderSnapshot | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly pendingRemoval = signal<GroupOrderParticipant | null>(null);
  readonly confirmSubmit = signal(false);
  readonly canPrepare = signal(false);
  readonly editable = computed(() => this.ownerSession()?.status === 'Open');
  readonly submittedCount = computed(
    () =>
      this.snapshot()?.participants.filter(
        (participant) => participant.type === 'Guest' && participant.isSubmitted
      ).length ?? 0
  );
  readonly draftCount = computed(
    () =>
      this.snapshot()?.participants.filter(
        (participant) => participant.type === 'Guest' && !participant.isSubmitted
      ).length ?? 0
  );
  readonly submittedTotal = computed(
    () =>
      this.snapshot()
        ?.participants.filter(
          (participant) => participant.type === 'Owner' || participant.isSubmitted
        )
        .reduce((total, participant) => total + participant.total, 0) ?? 0
  );
  readonly hasSubmittedDishes = computed(
    () =>
      this.snapshot()?.participants.some(
        (participant) =>
          participant.isSubmitted && participant.items.some((item) => item.quantity > 0)
      ) ?? false
  );

  ngOnInit(): void {
    this.loadGroup();
  }

  loadGroup(): void {
    if (this.loading() || this.busy()) return;
    this.loading.set(true);
    this.canPrepare.set(false);
    this.error.set('');
    this.snapshot.set(null);
    this.ownerSession.set(null);
    this.confirmSubmit.set(false);
    this.pendingRemoval.set(null);
    this.service
      .getOwnerSession(this.reservationId)
      .pipe(
        switchMap((owner) =>
          this.service
            .getSessions(owner.groupReservationId)
            .pipe(map((snapshot) => ({ owner, snapshot })))
        ),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (result) => {
          this.ownerSession.set(result.owner);
          this.snapshot.set(result.snapshot);
        },
        error: (error) => {
          const needsPreparation = error instanceof HttpErrorResponse && error.status === 404;
          this.canPrepare.set(needsPreparation);
          if (needsPreparation) this.error.set(getServerErrorMessage(error));
          else this.showError(error, 'Could not load the group pre-order. Please try again.');
        },
      });
  }

  prepareGroup(): void {
    if (this.busy() || this.loading()) return;
    this.busy.set(true);
    this.error.set('');
    this.service
      .prepare(this.reservationId)
      .pipe(
        switchMap((owner) =>
          this.service
            .getSessions(owner.groupReservationId)
            .pipe(map((snapshot) => ({ owner, snapshot })))
        ),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false))
      )
      .subscribe({
        next: (result) => {
          this.ownerSession.set(result.owner);
          this.snapshot.set(result.snapshot);
          this.canPrepare.set(false);
        },
        error: (error) => this.showError(error, 'Could not prepare the group pre-order.'),
      });
  }

  removeParticipant(): void {
    const owner = this.ownerSession();
    const participant = this.pendingRemoval();
    if (!owner || !participant || participant.type === 'Owner' || !this.editable() || this.busy())
      return;
    this.busy.set(true);
    this.error.set('');
    this.service
      .removeSession(owner.groupReservationId, participant.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false))
      )
      .subscribe({
        next: (snapshot) => {
          this.snapshot.set(snapshot);
          this.pendingRemoval.set(null);
          this.toast.success('Participant removed.');
        },
        error: (error) => {
          this.pendingRemoval.set(null);
          this.showError(error, 'Could not remove the participant.');
        },
      });
  }

  submitGroup(): void {
    const owner = this.ownerSession();
    if (!owner || !this.editable() || this.busy() || !this.hasSubmittedDishes()) return;
    this.busy.set(true);
    this.error.set('');
    this.service
      .prepare(this.reservationId)
      .pipe(
        switchMap(() => this.service.submit(owner.groupReservationId, this.reservationId)),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false))
      )
      .subscribe({
        next: () => {
          this.ownerSession.set({ ...owner, status: 'Submitted' });
          this.confirmSubmit.set(false);
          this.toast.success('Group pre-order submitted to the restaurant.');
        },
        error: (error) => {
          this.confirmSubmit.set(false);
          this.showError(error, 'Could not submit the group pre-order.');
        },
      });
  }

  private showError(error: unknown, fallback: string): void {
    const message =
      error instanceof HttpErrorResponse ? getServerErrorMessage(error) || fallback : fallback;
    this.error.set(message);
    this.toast.error(message);
  }
}
