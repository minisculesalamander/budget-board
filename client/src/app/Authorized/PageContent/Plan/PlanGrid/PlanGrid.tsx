import { ChevronDownIcon, ChevronRightIcon } from "lucide-react";
import React from "react";
import { useTranslation } from "react-i18next";
import { CategoryTypes } from "~/models/category";
import {
  IYearPlan,
  IYearPlanCell,
  IYearPlanMonthTotal,
} from "~/models/yearPlan";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import PlanCell from "./PlanCell";
import classes from "./PlanGrid.module.css";

interface PlanGridProps {
  plan: IYearPlan;
  savingCellKey: string | null;
  onCommit: (category: string, month: string, amount: string | null) => void;
}

const sumPlanned = (cells: IYearPlanCell[]): number | null => {
  const values = cells.flatMap((cell) =>
    cell.planned == null ? [] : [cell.planned],
  );
  if (values.length === 0) {
    return null;
  }

  return values.reduce((sum, value) => sum + value, 0);
};

const sumActual = (cells: IYearPlanCell[]): number =>
  cells.reduce((sum, cell) => sum + cell.actual, 0);

const cellKey = (category: string, month: string) => `${category}|${month}`;

const PlanGrid = (props: PlanGridProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();
  const [expanded, setExpanded] = React.useState<Set<string>>(() => new Set());

  const monthLabel = (month: string) => {
    const date = dayjs(month);
    if (
      props.plan.yearCount > 1 ||
      props.plan.financialYearStartMonth !== 1
    ) {
      return date.format("MMM YY");
    }

    return date.format("MMM");
  };

  const toggle = (category: string) => {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(category)) {
        next.delete(category);
      } else {
        next.add(category);
      }

      return next;
    });
  };

  const totalFor = (
    month: string,
    kind: "income" | "expense" | "savings",
  ): { planned: number; actual: number } => {
    const total = props.plan.monthTotals.find((item) => item.month === month);
    if (!total) {
      return { planned: 0, actual: 0 };
    }

    if (kind === "income") {
      return { planned: total.plannedIncome, actual: total.actualIncome };
    }

    if (kind === "expense") {
      return { planned: total.plannedExpense, actual: total.actualExpense };
    }

    return { planned: total.plannedSavings, actual: total.actualSavings };
  };

  const rangeTotal = (kind: "income" | "expense" | "savings") =>
    props.plan.monthTotals.reduce(
      (sum, month: IYearPlanMonthTotal) => {
        if (kind === "income") {
          return {
            planned: sum.planned + month.plannedIncome,
            actual: sum.actual + month.actualIncome,
          };
        }

        if (kind === "expense") {
          return {
            planned: sum.planned + month.plannedExpense,
            actual: sum.actual + month.actualExpense,
          };
        }

        return {
          planned: sum.planned + month.plannedSavings,
          actual: sum.actual + month.actualSavings,
        };
      },
      { planned: 0, actual: 0 },
    );

  return (
    <div className={classes.scroll}>
      <table className={classes.table}>
        <thead>
          <tr>
            <th className={classes.categoryHead}>{t("plan")}</th>
            {props.plan.months.map((month) => (
              <th key={month}>{monthLabel(month)}</th>
            ))}
            <th>{t("total")}</th>
          </tr>
        </thead>
        <tbody>
          {props.plan.sections.map((section) => {
            const sectionClass =
              section.kind === CategoryTypes.Income
                ? classes.income
                : classes.expense;
            const totalKind =
              section.kind === CategoryTypes.Income ? "income" : "expense";

            return (
              <React.Fragment key={section.kind}>
                <tr className={`${classes.section} ${sectionClass}`}>
                  <th className={classes.category} scope="row">
                    {section.kind === CategoryTypes.Income
                      ? t("income")
                      : t("expense")}
                  </th>
                  <td colSpan={props.plan.months.length + 1} />
                </tr>
                {section.parents.map((parent) => {
                  const isOpen = expanded.has(parent.category);
                  const hasChildren = parent.children.length > 0;

                  return (
                    <React.Fragment key={parent.category}>
                      <tr>
                        <th className={classes.category} scope="row">
                          {hasChildren ? (
                            <button
                              type="button"
                              className={classes.expand}
                              aria-expanded={isOpen}
                              onClick={() => toggle(parent.category)}
                            >
                              {isOpen ? (
                                <ChevronDownIcon size={14} />
                              ) : (
                                <ChevronRightIcon size={14} />
                              )}
                              {parent.category}
                            </button>
                          ) : (
                            parent.category
                          )}
                        </th>
                        {parent.cells.map((cell) => (
                          <td key={cell.month}>
                            <PlanCell
                              category={parent.category}
                              monthLabel={monthLabel(cell.month)}
                              kind={section.kind}
                              planned={cell.planned}
                              actual={cell.actual}
                              editable
                              saving={
                                props.savingCellKey ===
                                cellKey(parent.category, cell.month)
                              }
                              onCommit={(amount) =>
                                props.onCommit(parent.category, cell.month, amount)
                              }
                            />
                          </td>
                        ))}
                        <td>
                          <PlanCell
                            category={parent.category}
                            monthLabel={t("total")}
                            kind={section.kind}
                            planned={sumPlanned(parent.cells)}
                            actual={sumActual(parent.cells)}
                          />
                        </td>
                      </tr>
                      {isOpen &&
                        parent.children.map((child) => (
                          <tr key={child.category} className={classes.child}>
                            <th className={classes.category} scope="row">
                              {child.category}
                            </th>
                            {child.cells.map((cell) => (
                              <td key={cell.month}>
                                <PlanCell
                                  category={child.category}
                                  monthLabel={monthLabel(cell.month)}
                                  kind={section.kind}
                                  planned={cell.planned}
                                  actual={cell.actual}
                                  editable
                                  saving={
                                    props.savingCellKey ===
                                    cellKey(child.category, cell.month)
                                  }
                                  onCommit={(amount) =>
                                    props.onCommit(
                                      child.category,
                                      cell.month,
                                      amount,
                                    )
                                  }
                                />
                              </td>
                            ))}
                            <td>
                              <PlanCell
                                category={child.category}
                                monthLabel={t("total")}
                                kind={section.kind}
                                planned={sumPlanned(child.cells)}
                                actual={sumActual(child.cells)}
                              />
                            </td>
                          </tr>
                        ))}
                    </React.Fragment>
                  );
                })}
                <tr className={classes.total}>
                  <th className={classes.category} scope="row">
                    {t("total")}
                  </th>
                  {props.plan.months.map((month) => {
                    const total = totalFor(month, totalKind);
                    return (
                      <td key={month}>
                        <PlanCell
                          category={t("total")}
                          monthLabel={monthLabel(month)}
                          kind={section.kind}
                          planned={total.planned}
                          actual={total.actual}
                        />
                      </td>
                    );
                  })}
                  <td>
                    <PlanCell
                      category={t("total")}
                      monthLabel={t("total")}
                      kind={section.kind}
                      planned={rangeTotal(totalKind).planned}
                      actual={rangeTotal(totalKind).actual}
                    />
                  </td>
                </tr>
              </React.Fragment>
            );
          })}
          <tr className={classes.savings}>
            <th className={classes.category} scope="row">
              {t("savings")}
            </th>
            {props.plan.months.map((month) => {
              const total = totalFor(month, "savings");
              return (
                <td key={month}>
                  <PlanCell
                    category={t("savings")}
                    monthLabel={monthLabel(month)}
                    kind="savings"
                    planned={total.planned}
                    actual={total.actual}
                  />
                </td>
              );
            })}
            <td>
              <PlanCell
                category={t("savings")}
                monthLabel={t("total")}
                kind="savings"
                planned={rangeTotal("savings").planned}
                actual={rangeTotal("savings").actual}
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  );
};

export default PlanGrid;
