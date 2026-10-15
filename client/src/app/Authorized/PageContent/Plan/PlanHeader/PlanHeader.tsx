import { Group, Stack } from "@mantine/core";
import { Button, HeadingText, Select, SegmentedControl, BodyText } from "@teelur/budget-board-ui";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";

interface PlanHeaderProps {
  startYear: number;
  yearCount: number;
  financialYearStartMonth: number;
  rangeLabel: string;
  copying: boolean;
  savingSettings: boolean;
  onStartYearChange: (year: number) => void;
  onYearCountChange: (yearCount: number) => void;
  onFinancialYearStartChange: (month: number) => void;
  onCopy: () => void;
}

const PlanHeader = (props: PlanHeaderProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();

  const monthOptions = Array.from({ length: 12 }, (_, index) => ({
    value: String(index + 1),
    label: dayjs().month(index).format("MMMM"),
  }));

  return (
    <Stack gap="0.65rem">
      <Group justify="space-between" align="flex-end" wrap="wrap" gap="0.75rem">
        <Stack gap={0}>
          <HeadingText level={3}>{t("plan")}</HeadingText>
          <BodyText tone="muted" size="sm" maw={640}>
            {t("plan_ahead_description")}
          </BodyText>
        </Stack>
        <Group gap="0.5rem" wrap="wrap" align="flex-end">
          <Group gap="0.25rem" align="center">
            <Button
              variant="outline"
              color="neutral"
              size="xs"
              aria-label={t("previous_year")}
              onClick={() => props.onStartYearChange(props.startYear - 1)}
            >
              <ChevronLeftIcon size={16} />
            </Button>
            <BodyText size="sm" fw={600}>
              {props.rangeLabel}
            </BodyText>
            <Button
              variant="outline"
              color="neutral"
              size="xs"
              aria-label={t("next")}
              onClick={() => props.onStartYearChange(props.startYear + 1)}
            >
              <ChevronRightIcon size={16} />
            </Button>
          </Group>
          <SegmentedControl
            size="compact-sm"
            value={String(props.yearCount)}
            data={[
              { value: "1", label: t("1_year") },
              { value: "2", label: t("2_years") },
              { value: "3", label: t("3_years") },
            ]}
            onChange={(value) => props.onYearCountChange(Number(value))}
            aria-label={t("plan")}
          />
          <Select
            label={t("financial_year_starts")}
            data={monthOptions}
            value={String(props.financialYearStartMonth)}
            allowDeselect={false}
            onChange={(value) => {
              if (value) {
                props.onFinancialYearStartChange(Number(value));
              }
            }}
            disabled={props.savingSettings}
            w={180}
          />
          <Button
            variant="filled"
            color="primary"
            size="xs"
            loading={props.copying}
            onClick={props.onCopy}
          >
            {t("carry_plan_forward")}
          </Button>
        </Group>
      </Group>
    </Stack>
  );
};

export default PlanHeader;
