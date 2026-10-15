import { CompositeChart, LineChart } from "@mantine/charts";
import { SimpleGrid, Stack } from "@mantine/core";
import { BodyText, Card, HeadingText } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import ChartTooltip from "~/components/Charts/ChartTooltip/ChartTooltip";
import { SignDisplay } from "~/helpers/currency";
import { useSensitiveAmountFormatter } from "~/hooks/useSensitiveAmountFormatter";
import { IYearPlan } from "~/models/yearPlan";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";

interface PlanChartsProps {
  plan: IYearPlan;
}

const PlanCharts = (props: PlanChartsProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();
  const formatAmount = useSensitiveAmountFormatter();
  const formatChartAmount = (value: number) =>
    formatAmount(value, SignDisplay.Auto, undefined, 0);

  const monthLabel = (month: string) => {
    const date = dayjs(month);
    if (props.plan.yearCount > 1 || props.plan.financialYearStartMonth !== 1) {
      return date.format("MMM YY");
    }

    return date.format("MMM");
  };

  const isFutureMonth = (month: string) => dayjs(month).isAfter(dayjs(), "month");

  const monthlyData = props.plan.monthTotals.map((month) => {
    const future = isFutureMonth(month.month);
    return {
      month: monthLabel(month.month),
      plannedIncome: month.plannedIncome,
      actualIncome: future ? null : month.actualIncome,
      plannedExpense: month.plannedExpense,
      actualExpense: future ? null : month.actualExpense,
      plannedSavings: month.plannedSavings,
      actualSavings: future ? null : month.actualSavings,
    };
  });

  const yearlyData = props.plan.yearTotals.map((year) => {
    const months = props.plan.monthTotals.filter(
      (month) => month.financialYear === year.financialYear,
    );
    const started = months.some((month) => !isFutureMonth(month.month));
    return {
      year: String(year.financialYear),
      plannedIncome: year.plannedIncome,
      actualIncome: started ? year.actualIncome : null,
      plannedExpense: year.plannedExpense,
      actualExpense: started ? year.actualExpense : null,
    };
  });

  const yearlySeries = [
    { name: "plannedIncome", label: t("planned_income"), color: "teal.4", type: "bar" as const },
    { name: "actualIncome", label: t("actual_income"), color: "teal.7", type: "bar" as const },
    { name: "plannedExpense", label: t("planned_expenses"), color: "orange.4", type: "bar" as const },
    { name: "actualExpense", label: t("actual_expenses"), color: "red.6", type: "bar" as const },
  ];

  const monthlySeries = [
    { name: "plannedIncome", label: t("planned_income"), color: "teal.5", type: "line" as const },
    { name: "actualIncome", label: t("actual_income"), color: "teal.8", type: "line" as const },
    { name: "plannedExpense", label: t("planned_expenses"), color: "orange.5", type: "bar" as const },
    { name: "actualExpense", label: t("actual_expenses"), color: "red.6", type: "bar" as const },
  ];

  const savingsSeries = [
    { name: "plannedSavings", label: t("planned_savings"), color: "blue.5" },
    { name: "actualSavings", label: t("actual_savings"), color: "teal.6" },
  ];

  return (
    <Stack gap="0.5rem">
      <SimpleGrid cols={{ base: 1, lg: 3 }} spacing="0.75rem">
        <Card>
          <Stack gap="0.35rem">
            <HeadingText level={6}>{t("yearly_totals")}</HeadingText>
            <CompositeChart
              h={260}
              data={yearlyData}
              dataKey="year"
              series={yearlySeries}
              withLegend
              gridAxis="y"
              barProps={{ radius: [3, 3, 0, 0], maxBarSize: 22 }}
              valueFormatter={formatChartAmount}
              tooltipAnimationDuration={200}
              tooltipProps={{
                content: ({ label, payload }) => (
                  <ChartTooltip
                    label={label}
                    payload={payload}
                    series={yearlySeries}
                    valueFormatter={formatChartAmount}
                  />
                ),
              }}
            />
          </Stack>
        </Card>
        <Card>
          <Stack gap="0.35rem">
            <HeadingText level={6}>{t("monthly_breakdown")}</HeadingText>
            <CompositeChart
              h={260}
              data={monthlyData}
              dataKey="month"
              series={monthlySeries}
              withLegend
              withDots={false}
              connectNulls={false}
              gridAxis="y"
              curveType="monotone"
              barProps={{ radius: [3, 3, 0, 0], maxBarSize: 14 }}
              lineProps={{ strokeWidth: 2, dot: false }}
              xAxisProps={{ minTickGap: 18, tick: { fontSize: 11 } }}
              valueFormatter={formatChartAmount}
              tooltipAnimationDuration={200}
              tooltipProps={{
                content: ({ label, payload }) => (
                  <ChartTooltip
                    label={label}
                    payload={payload}
                    series={monthlySeries}
                    valueFormatter={formatChartAmount}
                  />
                ),
              }}
            />
          </Stack>
        </Card>
        <Card>
          <Stack gap="0.35rem">
            <HeadingText level={6}>{t("savings_over_time")}</HeadingText>
            <LineChart
              h={260}
              data={monthlyData}
              dataKey="month"
              series={savingsSeries}
              withLegend
              withDots={false}
              connectNulls={false}
              curveType="monotone"
              gridAxis="xy"
              strokeWidth={2}
              referenceLines={[{ y: 0, color: "gray.5" }]}
              xAxisProps={{ minTickGap: 18, tick: { fontSize: 11 } }}
              valueFormatter={formatChartAmount}
              tooltipAnimationDuration={200}
              tooltipProps={{
                content: ({ label, payload }) => (
                  <ChartTooltip
                    label={label}
                    payload={payload}
                    series={savingsSeries}
                    valueFormatter={formatChartAmount}
                  />
                ),
              }}
            />
          </Stack>
        </Card>
      </SimpleGrid>
      <BodyText tone="muted" size="xs">
        {t("plan_actuals_caption")}
      </BodyText>
    </Stack>
  );
};

export default PlanCharts;
