import { Skeleton, Stack } from "@mantine/core";
import { AxiosError } from "axios";
import React from "react";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { translateAxiosError } from "~/helpers/requests";
import { useCopyYearPlanMutation } from "~/hooks/mutations/yearPlan/useCopyYearPlanMutation";
import { useUpdatePlanningSettingsMutation } from "~/hooks/mutations/yearPlan/useUpdatePlanningSettingsMutation";
import { useUpdateYearPlanCellMutation } from "~/hooks/mutations/yearPlan/useUpdateYearPlanCellMutation";
import { useYearPlanQuery } from "~/hooks/queries/useYearPlanQuery";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import PlanCharts from "./PlanCharts/PlanCharts";
import PlanGrid from "./PlanGrid/PlanGrid";
import PlanHeader from "./PlanHeader/PlanHeader";
import PlanSummary from "./PlanSummary/PlanSummary";

const Plan = (): React.ReactNode => {
  const { dayjs } = useLocale();
  const [startYear, setStartYear] = React.useState(() => dayjs().year());
  const [yearCount, setYearCount] = React.useState(1);
  const [financialYearStartMonth, setFinancialYearStartMonth] = React.useState(1);
  const aligned = React.useRef(false);
  const planQuery = useYearPlanQuery({ startYear, yearCount });
  const updateCell = useUpdateYearPlanCellMutation();
  const updateSettings = useUpdatePlanningSettingsMutation();
  const copyYear = useCopyYearPlanMutation();

  React.useEffect(() => {
    if (planQuery.isError) {
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(planQuery.error as AxiosError),
      });
    }
  }, [planQuery.isError]);

  React.useEffect(() => {
    if (planQuery.data) {
      setFinancialYearStartMonth(planQuery.data.financialYearStartMonth);
    }
  }, [planQuery.data]);

  React.useEffect(() => {
    if (!planQuery.data || aligned.current) {
      return;
    }

    aligned.current = true;
    const startMonth = planQuery.data.financialYearStartMonth;
    const now = dayjs();
    const currentFinancialYear =
      now.month() + 1 >= startMonth ? now.year() : now.year() - 1;
    if (currentFinancialYear !== startYear) {
      setStartYear(currentFinancialYear);
    }
  }, [dayjs, planQuery.data, startYear]);

  const plan = planQuery.data;
  const rangeLabel = plan
    ? `${dayjs(plan.months[0]).format("MMM YYYY")} – ${dayjs(plan.months[plan.months.length - 1]).format("MMM YYYY")}`
    : String(startYear);
  const savingCellKey = updateCell.isPending
    ? `${updateCell.variables?.category}|${updateCell.variables?.month}`
    : null;

  return (
    <Stack w="100%" gap="0.85rem">
      <PlanHeader
        startYear={startYear}
        yearCount={yearCount}
        financialYearStartMonth={financialYearStartMonth}
        rangeLabel={rangeLabel}
        copying={copyYear.isPending}
        savingSettings={updateSettings.isPending}
        onStartYearChange={setStartYear}
        onYearCountChange={setYearCount}
        onFinancialYearStartChange={(month) => {
          setFinancialYearStartMonth(month);
          updateSettings.mutate(month);
        }}
        onCopy={() => copyYear.mutate(startYear)}
      />
      {planQuery.isPending || !plan ? (
        <Stack gap="0.75rem">
          <Skeleton h={72} radius="lg" />
          <Skeleton h={280} radius="lg" />
          <Skeleton h={360} radius="lg" />
        </Stack>
      ) : (
        <>
          <PlanSummary plan={plan} />
          <PlanCharts plan={plan} />
          <PlanGrid
            plan={plan}
            savingCellKey={savingCellKey}
            onCommit={(category, month, amount) =>
              updateCell.mutate({ category, month, amount })
            }
          />
        </>
      )}
    </Stack>
  );
};

export default Plan;
