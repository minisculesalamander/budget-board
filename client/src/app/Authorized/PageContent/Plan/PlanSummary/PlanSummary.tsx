import { SimpleGrid, Stack } from "@mantine/core";
import { BodyText, Card, HeadingText } from "@teelur/budget-board-ui";
import { useTranslation } from "react-i18next";
import { SignDisplay } from "~/helpers/currency";
import { useSensitiveAmountFormatter } from "~/hooks/useSensitiveAmountFormatter";
import { IYearPlan } from "~/models/yearPlan";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";

interface PlanSummaryProps {
  plan: IYearPlan;
}

const PlanSummary = (props: PlanSummaryProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();
  const formatAmount = useSensitiveAmountFormatter();

  const started = props.plan.monthTotals.filter(
    (month) => !dayjs(month.month).isAfter(dayjs(), "month"),
  );
  const plannedSavings = props.plan.yearTotals.reduce(
    (sum, year) => sum + year.plannedSavings,
    0,
  );
  const plannedToDate = started.reduce(
    (sum, month) => sum + month.plannedSavings,
    0,
  );
  const actualToDate = started.reduce((sum, month) => sum + month.actualSavings, 0);
  const variance = started.length === 0 ? null : actualToDate - plannedToDate;

  const cards = [
    { label: t("planned_savings"), value: plannedSavings },
    { label: t("actual_savings_so_far"), value: started.length === 0 ? null : actualToDate },
    { label: t("plan_variance"), value: variance },
  ];

  return (
    <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="0.75rem">
      {cards.map((card) => (
        <Card key={card.label}>
          <Stack gap={0}>
            <BodyText tone="muted" size="xs">
              {card.label}
            </BodyText>
            <HeadingText level={4}>
              {card.value == null
                ? "—"
                : formatAmount(card.value, SignDisplay.Auto, undefined, 0)}
            </HeadingText>
          </Stack>
        </Card>
      ))}
    </SimpleGrid>
  );
};

export default PlanSummary;
