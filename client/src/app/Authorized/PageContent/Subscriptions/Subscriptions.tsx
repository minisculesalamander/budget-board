import { Group, Skeleton, Stack } from "@mantine/core";
import {
  ActionIcon,
  AmountText,
  Badge,
  BodyText,
  Button,
  Card,
  HeadingText,
  Modal,
} from "@teelur/budget-board-ui";
import { useDisclosure } from "@mantine/hooks";
import { CheckIcon, PencilIcon, PlusIcon, XIcon } from "lucide-react";
import React from "react";
import { useTranslation } from "react-i18next";
import RecurringRuleForm from "~/components/RecurringRuleForm/RecurringRuleForm";
import SubscriptionRenewalAlertControl from "./SubscriptionRenewalAlertControl";
import { getRecurringCadenceLabel } from "~/helpers/recurringRules";
import { useAcceptRecurringSuggestionMutation } from "~/hooks/mutations/recurringRules/useAcceptRecurringSuggestionMutation";
import { useDismissRecurringSuggestionMutation } from "~/hooks/mutations/recurringRules/useDismissRecurringSuggestionMutation";
import { useSubscriptionsQuery } from "~/hooks/queries/useSubscriptionsQuery";
import {
  IRecurringRuleResponse,
  RecurringRuleSuggestionStatuses,
} from "~/models/recurringRule";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { usePrivacyMode } from "~/providers/PrivacyModeProvider/PrivacyModeProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";

const DUE_SOON_DAYS = 7;

interface SubscriptionCardProps {
  rule: IRecurringRuleResponse;
  dueSoon: boolean;
  onEdit: (rule: IRecurringRuleResponse) => void;
}

const SubscriptionCard = (props: SubscriptionCardProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs, longDateFormat, intlLocale } = useLocale();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const { isPrivacyModeEnabled } = usePrivacyMode();
  const formatDate = (value: string) => dayjs(value).format(longDateFormat);

  return (
    <Card>
      <Stack gap="0.5rem">
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <Stack gap="0.25rem" miw={0}>
          <Group gap="0.5rem">
            <BodyText tone="primary" size="md" fw={500}>
              {props.rule.merchantName || t("any_merchant")}
            </BodyText>
            <Badge variant="outline" color="primary" size="xs">
              {getRecurringCadenceLabel(props.rule.cadence, t)}
            </Badge>
            {props.dueSoon && (
              <Badge variant="filled" color="warning" size="xs">
                {t("subscriptions_due_soon")}
              </Badge>
            )}
            {!props.rule.isActive && (
              <Badge variant="filled" color="error" size="xs">
                {t("inactive")}
              </Badge>
            )}
          </Group>
          <BodyText tone="secondary" size="sm">
            {props.rule.accountName}
          </BodyText>
          <Group gap="0.75rem">
            <AmountText
              amount={props.rule.forecastAmount}
              size="sm"
              disableStatusColor
              isSensitive={isPrivacyModeEnabled}
              locale={intlLocale}
              currency={preferredCurrency}
              decimalPlaces={decimalPlaces}
            />
            {props.rule.lastChargeDate && (
              <BodyText tone="secondary" size="sm">
                {t("subscriptions_last_charge", {
                  date: formatDate(props.rule.lastChargeDate),
                })}
              </BodyText>
            )}
            {props.rule.nextOccurrenceDate && (
              <BodyText tone="secondary" size="sm">
                {t("subscriptions_next_charge", {
                  date: formatDate(props.rule.nextOccurrenceDate),
                })}
              </BodyText>
            )}
          </Group>
        </Stack>
        <ActionIcon
          variant="outline"
          color="primary"
          size="compact-sm"
          h="100%"
          aria-label={t("edit")}
          onClick={() => props.onEdit(props.rule)}
        >
          <PencilIcon size="1rem" />
        </ActionIcon>
      </Group>
      <SubscriptionRenewalAlertControl recurringRuleID={props.rule.id} />
      </Stack>
    </Card>
  );
};

interface SuggestionCardProps {
  rule: IRecurringRuleResponse;
}

const SuggestionCard = (props: SuggestionCardProps): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs, longDateFormat, intlLocale } = useLocale();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const { isPrivacyModeEnabled } = usePrivacyMode();
  const acceptMutation = useAcceptRecurringSuggestionMutation();
  const dismissMutation = useDismissRecurringSuggestionMutation();
  const formatDate = (value: string) => dayjs(value).format(longDateFormat);

  return (
    <Card>
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <Stack gap="0.25rem" miw={0}>
          <Group gap="0.5rem">
            <BodyText tone="primary" size="md" fw={500}>
              {props.rule.merchantName || t("any_merchant")}
            </BodyText>
            <Badge variant="outline" color="primary" size="xs">
              {getRecurringCadenceLabel(props.rule.cadence, t)}
            </Badge>
            <Badge variant="filled" color="secondary" size="xs">
              {t("subscriptions_suggested")}
            </Badge>
          </Group>
          <Group gap="0.75rem">
            <AmountText
              amount={props.rule.forecastAmount}
              size="sm"
              disableStatusColor
              isSensitive={isPrivacyModeEnabled}
              locale={intlLocale}
              currency={preferredCurrency}
              decimalPlaces={decimalPlaces}
            />
            {props.rule.lastChargeDate && (
              <BodyText tone="secondary" size="sm">
                {t("subscriptions_last_charge", {
                  date: formatDate(props.rule.lastChargeDate),
                })}
              </BodyText>
            )}
            {props.rule.nextOccurrenceDate && (
              <BodyText tone="secondary" size="sm">
                {t("subscriptions_next_charge", {
                  date: formatDate(props.rule.nextOccurrenceDate),
                })}
              </BodyText>
            )}
          </Group>
        </Stack>
        <Group gap="0.25rem" wrap="nowrap">
          <Button
            variant="filled"
            color="primary"
            size="compact-sm"
            loading={acceptMutation.isPending}
            rightSection={<CheckIcon size="1rem" />}
            onClick={() => acceptMutation.mutate(props.rule.id)}
          >
            {t("subscriptions_accept")}
          </Button>
          <Button
            variant="outline"
            color="error"
            size="compact-sm"
            loading={dismissMutation.isPending}
            rightSection={<XIcon size="1rem" />}
            onClick={() => dismissMutation.mutate(props.rule.id)}
          >
            {t("subscriptions_dismiss")}
          </Button>
        </Group>
      </Group>
    </Card>
  );
};

const byNextDueDate = (
  first: IRecurringRuleResponse,
  second: IRecurringRuleResponse,
): number => {
  if (!first.nextOccurrenceDate && !second.nextOccurrenceDate) {
    return (first.merchantName ?? "").localeCompare(second.merchantName ?? "");
  }
  if (!first.nextOccurrenceDate) {
    return 1;
  }
  if (!second.nextOccurrenceDate) {
    return -1;
  }

  return (
    first.nextOccurrenceDate.localeCompare(second.nextOccurrenceDate) ||
    (first.merchantName ?? "").localeCompare(second.merchantName ?? "")
  );
};

const Subscriptions = (): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs } = useLocale();
  const [opened, { open, close }] = useDisclosure(false);
  const [editingRule, setEditingRule] =
    React.useState<IRecurringRuleResponse>();
  const subscriptionsQuery = useSubscriptionsQuery();
  const rules = subscriptionsQuery.data ?? [];
  const suggestions = rules.filter(
    (rule) =>
      rule.suggestionStatus === RecurringRuleSuggestionStatuses.Suggested,
  );
  const subscriptions = rules
    .filter(
      (rule) =>
        rule.suggestionStatus !== RecurringRuleSuggestionStatuses.Suggested,
    )
    .slice()
    .sort(byNextDueDate);
  const today = dayjs().startOf("day");
  const isDueSoon = (rule: IRecurringRuleResponse) => {
    if (!rule.nextOccurrenceDate || !rule.isActive) {
      return false;
    }

    const daysUntil = dayjs(rule.nextOccurrenceDate)
      .startOf("day")
      .diff(today, "day");
    return daysUntil >= 0 && daysUntil <= DUE_SOON_DAYS;
  };
  const dueSoonCount = subscriptions.filter(isDueSoon).length;

  const openCreate = () => {
    setEditingRule(undefined);
    open();
  };

  const openEdit = (rule: IRecurringRuleResponse) => {
    setEditingRule(rule);
    open();
  };

  return (
    <Stack w="100%" maw={1400} gap="0.75rem">
      <Group justify="space-between" align="flex-end">
        <Stack gap="0.25rem">
          <HeadingText tone="heading" level={4}>
            {t("subscriptions")}
          </HeadingText>
          <BodyText tone="secondary" size="sm">
            {t("subscriptions_description")}
          </BodyText>
          {dueSoonCount > 0 && (
            <BodyText tone="secondary" size="sm">
              {t("subscriptions_due_within_week", { count: dueSoonCount })}
            </BodyText>
          )}
        </Stack>
        <Button
          variant="filled"
          color="primary"
          size="compact-sm"
          rightSection={<PlusIcon size="1rem" />}
          onClick={openCreate}
        >
          {t("subscriptions_add")}
        </Button>
      </Group>
      {subscriptionsQuery.isPending ? (
        <Skeleton height={80} />
      ) : (
        <Stack gap="0.75rem">
          {suggestions.length > 0 && (
            <Stack gap="0.5rem">
              <Stack gap="0.25rem">
                <HeadingText tone="heading" level={5}>
                  {t("subscriptions_suggestions")}
                </HeadingText>
                <BodyText tone="secondary" size="sm">
                  {t("subscriptions_suggestions_description")}
                </BodyText>
              </Stack>
              {suggestions.map((rule) => (
                <SuggestionCard key={rule.id} rule={rule} />
              ))}
            </Stack>
          )}
          {subscriptions.length > 0 ? (
            subscriptions.map((rule) => (
              <SubscriptionCard
                key={rule.id}
                rule={rule}
                dueSoon={isDueSoon(rule)}
                onEdit={openEdit}
              />
            ))
          ) : (
            <Group justify="center" p="1rem">
              <BodyText tone="muted" size="sm">
                {t("subscriptions_empty")}
              </BodyText>
            </Group>
          )}
        </Stack>
      )}
      <Modal
        opened={opened}
        onClose={close}
        title={
          editingRule ? t("subscriptions_edit") : t("subscriptions_add")
        }
      >
        <RecurringRuleForm
          key={editingRule?.id ?? "new"}
          rule={editingRule}
          onSuccess={close}
          onCancel={close}
        />
      </Modal>
    </Stack>
  );
};

export default Subscriptions;
