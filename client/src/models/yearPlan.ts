export interface IYearPlanCell {
  month: string;
  budgetId: string | null;
  planned: number | null;
  actual: number;
}

export interface IYearPlanChild {
  category: string;
  cells: IYearPlanCell[];
}

export interface IYearPlanParent {
  category: string;
  cells: IYearPlanCell[];
  children: IYearPlanChild[];
}

export interface IYearPlanSection {
  kind: string;
  parents: IYearPlanParent[];
}

export interface IYearPlanMonthTotal {
  month: string;
  financialYear: number;
  plannedIncome: number;
  actualIncome: number;
  plannedExpense: number;
  actualExpense: number;
  plannedSavings: number;
  actualSavings: number;
}

export interface IYearPlanYearTotal {
  financialYear: number;
  plannedIncome: number;
  actualIncome: number;
  plannedExpense: number;
  actualExpense: number;
  plannedSavings: number;
  actualSavings: number;
}

export interface IYearPlan {
  financialYearStartMonth: number;
  startYear: number;
  yearCount: number;
  months: string[];
  sections: IYearPlanSection[];
  monthTotals: IYearPlanMonthTotal[];
  yearTotals: IYearPlanYearTotal[];
}

export interface IYearPlanCellUpdate {
  month: string;
  category: string;
  amount: string | null;
}
