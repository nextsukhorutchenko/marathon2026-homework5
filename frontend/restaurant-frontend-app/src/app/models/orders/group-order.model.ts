export interface GroupOrderDish {
  id: string;
  name: string;
  price: number;
}

export interface GroupOrderParticipant {
  id: string;
  type: 'Owner' | 'Guest';
  name: string;
  items: { dish: GroupOrderDish; quantity: number }[];
  total: number;
  isSubmitted: boolean;
}

export interface GroupOrderSnapshot {
  participants: GroupOrderParticipant[];
  total: number;
}

export interface OwnerGroupSession {
  guestSessionId: string;
  groupReservationId: string;
  status: 'Open' | 'Submitted' | 'Closed';
}

export interface SubmittedGroupDish {
  dishId: string;
  quantity: number;
}
