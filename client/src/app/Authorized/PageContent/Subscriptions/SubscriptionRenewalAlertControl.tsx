import { Group } from "@mantine/core";
import { NumberInput, Select } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { useUpdateRenewalAlertPreferenceMutation } from "~/hooks/mutations/renewalAlerts/useUpdateRenewalAlertPreferenceMutation";
import { useRenewalAlertPreferencesQuery } from "~/hooks/queries/useRenewalAlertPreferencesQuery";

type AlertMode = "default" | "custom" | "off";

interface SubscriptionRenewalAlertControlProps {
  recurringRuleID: string;
}

const SubscriptionRenewalAlertControl = (
  props: SubscriptionRenewalAlertControlProps,
): React.ReactNode => {
  const { t } = useTranslation();
  const preferencesQuery = useRenewalAlertPreferencesQuery();
  const updateMutation = useUpdateRenewalAlertPreferenceMutation();
  const preference = preferencesQuery.data?.preferences.find(
    (item) => item.recurringRuleID === props.recurringRuleID,
  );
  const defaultDays = preferencesQuery.data?.defaultDaysBefore ?? 3;
  const serverMode: AlertMode = !preference
    ? "default"
    : !preference.alertsEnabled
      ? "off"
      : preference.daysBefore === null
        ? "default"
        : "custom";
  const [mode, setMode] = React.useState<AlertMode>(serverMode);
  const [daysBefore, setDaysBefore] = React.useState(
    preference?.daysBefore ?? defaultDays,
  );

  React.useEffect(() => {
    setMode(serverMode);
    setDaysBefore(preference?.daysBefore ?? defaultDays);
  }, [serverMode, preference?.daysBefore, defaultDays, props.recurringRuleID]);

  if (!preferencesQuery.data) {
    return null;
  }

  const save = (nextMode: AlertMode, nextDays: number) => {
    if (nextMode === "default") {
      updateMutation.mutate({
        recurringRuleID: props.recurringRuleID,
        preference: { alertsEnabled: true, daysBefore: null },
      });
      return;
    }

    if (nextMode === "off") {
      updateMutation.mutate({
        recurringRuleID: props.recurringRuleID,
        preference: { alertsEnabled: false, daysBefore: null },
      });
      return;
    }

    if (!Number.isInteger(nextDays)) {
      return;
    }

    updateMutation.mutate({
      recurringRuleID: props.recurringRuleID,
      preference: { alertsEnabled: true, daysBefore: nextDays },
    });
  };

  return (
    <Group align="flex-end" gap="0.5rem" wrap="wrap">
      <Select
        label={t("subscriptions_alert_label")}
        data={[
          {
            value: "default",
            label: t("subscriptions_alert_default", { count: defaultDays }),
          },
          { value: "custom", label: t("subscriptions_alert_custom") },
          { value: "off", label: t("subscriptions_alert_off") },
        ]}
        value={mode}
        disabled={updateMutation.isPending}
        onChange={(value) => {
          if (value !== "default" && value !== "custom" && value !== "off") {
            return;
          }

          const nextDays = value === "custom" ? (daysBefore ?? defaultDays) : daysBefore;
          setMode(value);
          if (value === "custom") {
            setDaysBefore(nextDays);
          }
          save(value, nextDays);
        }}
        maw={280}
      />
      {mode === "custom" && (
        <NumberInput
          label={t("subscriptions_alert_days")}
          min={0}
          max={90}
          step={1}
          allowDecimal={false}
          allowNegative={false}
          value={daysBefore}
          disabled={updateMutation.isPending}
          onChange={(value) => {
            if (typeof value === "number" && Number.isInteger(value)) {
              setDaysBefore(value);
            }
          }}
          onBlur={() => save("custom", daysBefore)}
          maw={140}
        />
      )}
    </Group>
  );
};

export default SubscriptionRenewalAlertControl;
