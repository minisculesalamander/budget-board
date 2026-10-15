import classes from "./Envelopes.module.css";

import { Group, SimpleGrid, Skeleton, Stack } from "@mantine/core";
import { AmountText, BodyText, Button, Card, Checkbox, HeadingText } from "@teelur/budget-board-ui";
import { AxiosError } from "axios";
import React from "react";
import { useTranslation } from "react-i18next";
import MonthToolcards from "~/components/MonthToolcards/MonthToolcards";
import { convertNumberToCurrency, SignDisplay } from "~/helpers/currency";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { translateAxiosError } from "~/helpers/requests";
import { useSetEnvelopeEnabledMutation } from "~/hooks/mutations/envelopes/useSetEnvelopeEnabledMutation";
import { useSetOnBudgetAccountsMutation } from "~/hooks/mutations/envelopes/useSetOnBudgetAccountsMutation";
import { useEnvelopeMonthQuery } from "~/hooks/queries/useEnvelopeMonthQuery";
import { IEnvelopeMonth } from "~/models/envelope";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { usePrivacyMode } from "~/providers/PrivacyModeProvider/PrivacyModeProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";
import EnvelopeGroups from "./EnvelopeGroups";

const Envelopes = (): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();
  const [selectedDates, setSelectedDates] = React.useState<Date[]>([
    dayjs().startOf("month").toDate(),
  ]);
  const month = selectedDates[0] ?? dayjs().startOf("month").toDate();
  const envelopeQuery = useEnvelopeMonthQuery(month);
  const enabledMutation = useSetEnvelopeEnabledMutation();

  React.useEffect(() => {
    if (envelopeQuery.isError) {
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(envelopeQuery.error as AxiosError),
      });
    }
  }, [envelopeQuery.isError, envelopeQuery.error]);

  return (
    <Stack w="100%" maw={1100} gap="0.75rem">
      <HeadingText level={3}>{t("envelopes")}</HeadingText>
      <MonthToolcards
        selectedDates={[month]}
        setSelectedDates={setSelectedDates}
        timeToMonthlyTotalsMap={new Map()}
        isPending={envelopeQuery.isPending}
        allowSelectMultiple={false}
        allowFutureMonths
      />
      {envelopeQuery.isPending && (
        <Stack gap="0.5rem">
          <Skeleton h={96} />
          <Skeleton h={180} />
        </Stack>
      )}
      {envelopeQuery.data && !envelopeQuery.data.enabled && (
        <Card w="100%" p="1rem">
          <Stack gap="0.75rem">
            <HeadingText level={4}>{t("envelope_budgeting")}</HeadingText>
            <BodyText tone="secondary">{t("envelope_budgeting_description")}</BodyText>
            <BodyText tone="secondary">{t("envelope_coexists_with_budgets")}</BodyText>
            <Group>
              <Button
                variant="filled"
                color="primary"
                onClick={() => enabledMutation.mutate(true)}
                loading={enabledMutation.isPending}
              >
                {t("envelope_enable")}
              </Button>
            </Group>
          </Stack>
        </Card>
      )}
      {envelopeQuery.data?.enabled && (
        <EnvelopeBudget
          month={envelopeQuery.data}
          monthDate={month}
          busy={enabledMutation.isPending}
          onDisable={() => enabledMutation.mutate(false)}
        />
      )}
    </Stack>
  );
};

interface EnvelopeBudgetProps {
  month: IEnvelopeMonth;
  monthDate: Date;
  busy: boolean;
  onDisable: () => void;
}

const EnvelopeBudget = (props: EnvelopeBudgetProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs, intlLocale } = useLocale();
  const { isPrivacyModeEnabled } = usePrivacyMode();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const accountsMutation = useSetOnBudgetAccountsMutation();

  const money = (amount: number) => (
    <AmountText
      amount={amount}
      size="md"
      isSensitive={isPrivacyModeEnabled}
      locale={intlLocale}
      currency={preferredCurrency}
      decimalPlaces={decimalPlaces}
    />
  );

  const toggleAccount = (accountId: string, onBudget: boolean) => {
    const accountIds = props.month.accounts
      .filter((account) =>
        account.id === accountId ? onBudget : account.onBudget,
      )
      .map((account) => account.id);
    accountsMutation.mutate(accountIds);
  };

  const accountLabel = (account: IEnvelopeMonth["accounts"][number]) => {
    const name =
      account.institutionName.length > 0
        ? `${account.name} · ${account.institutionName}`
        : account.name;
    if (isPrivacyModeEnabled) {
      return name;
    }

    return `${name} (${convertNumberToCurrency(
      account.balance,
      decimalPlaces,
      preferredCurrency,
      SignDisplay.Auto,
      intlLocale,
    )})`;
  };

  return (
    <Stack gap="0.75rem">
      <Group justify="space-between" align="flex-start">
        <BodyText tone="secondary" size="sm">
          {t("envelope_month_hint", {
            month: dayjs(props.monthDate).format("MMMM YYYY"),
          })}
        </BodyText>
        <Button
          variant="outline"
          color="primary"
          size="compact-sm"
          onClick={props.onDisable}
          loading={props.busy}
        >
          {t("envelope_disable")}
        </Button>
      </Group>
      <div className={classes.summary}>
        <Card p="0.75rem">
          <Stack gap={0}>
            <BodyText size="sm" tone="secondary">
              {t("envelope_to_be_assigned")}
            </BodyText>
            {money(props.month.toBeAssigned)}
            <BodyText size="xs" tone="secondary">
              {t("envelope_to_be_assigned_hint")}
            </BodyText>
          </Stack>
        </Card>
        <Card p="0.75rem">
          <Stack gap={0}>
            <BodyText size="sm" tone="secondary">
              {t("envelope_on_budget_balance")}
            </BodyText>
            {money(props.month.onBudgetBalance)}
          </Stack>
        </Card>
        <Card p="0.75rem">
          <Stack gap={0}>
            <BodyText size="sm" tone="secondary">
              {t("envelope_income_this_month")}
            </BodyText>
            {money(props.month.income)}
          </Stack>
        </Card>
      </div>
      {props.month.outsideActivity !== 0 && (
        <Group gap="0.35rem" align="baseline">
          <BodyText size="sm" tone="secondary">
            {t("envelope_outside_activity")}
          </BodyText>
          {money(props.month.outsideActivity)}
        </Group>
      )}
      <Card p="0.75rem">
        <Stack gap="0.5rem">
          <HeadingText level={5}>{t("envelope_on_budget_accounts")}</HeadingText>
          <BodyText size="sm" tone="secondary">
            {t("envelope_on_budget_accounts_hint")}
          </BodyText>
          {props.month.accounts.length === 0 && (
            <BodyText size="sm">{t("envelope_no_accounts")}</BodyText>
          )}
          <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="0.5rem">
            {props.month.accounts.map((account) => (
              <Checkbox
                key={account.id}
                checked={account.onBudget}
                disabled={accountsMutation.isPending}
                onChange={(event) =>
                  toggleAccount(account.id, event.currentTarget.checked)
                }
                label={accountLabel(account)}
              />
            ))}
          </SimpleGrid>
        </Stack>
      </Card>
      <EnvelopeGroups
        month={dayjs(props.monthDate).startOf("month").format("YYYY-MM-DD")}
        groups={props.month.groups}
        disabled={accountsMutation.isPending}
      />
    </Stack>
  );
};

export default Envelopes;
