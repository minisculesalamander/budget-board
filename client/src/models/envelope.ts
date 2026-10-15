export interface IEnvelopeCategory {
  category: string;
  parent: string;
  leftover: number;
  assigned: number;
  activity: number;
  available: number;
}

export interface IEnvelopeGroup {
  name: string;
  envelopes: IEnvelopeCategory[];
}

export interface IEnvelopeAccount {
  id: string;
  name: string;
  institutionName: string;
  balance: number;
  onBudget: boolean;
}

export interface IEnvelopeMonth {
  enabled: boolean;
  month: string;
  toBeAssigned: number;
  onBudgetBalance: number;
  income: number;
  outsideActivity: number;
  accounts: IEnvelopeAccount[];
  groups: IEnvelopeGroup[];
}

export interface IEnvelopeAssignRequest {
  month: string;
  category: string;
  assigned: number;
}

export interface IEnvelopeMoveRequest {
  month: string;
  fromCategory: string;
  toCategory: string;
  amount: number;
}
