import classes from "./Envelopes.module.css";

import { Group, Stack } from "@mantine/core";
import { AmountText, BodyText, Button, Card, HeadingText, TextInput } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { useAssignEnvelopeMutation } from "~/hooks/mutations/envelopes/useAssignEnvelopeMutation";
import { IEnvelopeCategory, IEnvelopeGroup } from "~/models/envelope";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { usePrivacyMode } from "~/providers/PrivacyModeProvider/PrivacyModeProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";
import AssignedInput from "./AssignedInput";
import MoveMoneyModal from "./MoveMoneyModal";

interface EnvelopeGroupsProps {
  month: string;
  groups: IEnvelopeGroup[];
  disabled: boolean;
}

const hasMoney = (envelope: IEnvelopeCategory) =>
  envelope.leftover !== 0 ||
  envelope.assigned !== 0 ||
  envelope.activity !== 0 ||
  envelope.available !== 0;

const EnvelopeGroups = (props: EnvelopeGroupsProps): React.ReactNode => {
  const { t } = useTranslation();
  const { intlLocale } = useLocale();
  const { isPrivacyModeEnabled } = usePrivacyMode();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const assignMutation = useAssignEnvelopeMutation();
  const [search, setSearch] = React.useState("");
  const envelopes = props.groups.flatMap((group) => group.envelopes);
  const query = search.trim().toLocaleLowerCase();

  const money = (amount: number) => (
    <AmountText
      amount={amount}
      size="sm"
      isSensitive={isPrivacyModeEnabled}
      locale={intlLocale}
      currency={preferredCurrency}
      decimalPlaces={decimalPlaces}
    />
  );

  const metric = (label: string, amount: number) => (
    <div className={classes.metric}>
      <BodyText size="xs" tone="secondary">
        {label}
      </BodyText>
      {money(amount)}
    </div>
  );

  const visibleGroups = props.groups
    .map((group) => ({
      ...group,
      envelopes: group.envelopes.filter((envelope) => {
        if (query.length === 0) {
          return hasMoney(envelope);
        }

        return (
          envelope.category.toLocaleLowerCase().includes(query) ||
          group.name.toLocaleLowerCase().includes(query)
        );
      }),
    }))
    .filter((group) => group.envelopes.length > 0);

  return (
    <Stack gap="0.75rem" w="100%">
      <TextInput
        placeholder={t("envelope_search")}
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
        aria-label={t("envelope_search")}
      />
      {visibleGroups.length === 0 && (
        <BodyText tone="secondary" size="sm">
          {t("envelope_search_empty")}
        </BodyText>
      )}
      {visibleGroups.map((group) => (
        <EnvelopeGroupCard
          key={group.name}
          group={group}
          month={props.month}
          envelopes={envelopes}
          disabled={props.disabled || assignMutation.isPending}
          forceOpen={query.length > 0}
          metric={metric}
          onAssign={(category, assigned) =>
            assignMutation.mutate({
              month: props.month,
              category,
              assigned,
            })
          }
        />
      ))}
    </Stack>
  );
};

interface EnvelopeGroupCardProps {
  group: IEnvelopeGroup;
  month: string;
  envelopes: IEnvelopeCategory[];
  disabled: boolean;
  forceOpen: boolean;
  metric: (label: string, amount: number) => React.ReactNode;
  onAssign: (category: string, assigned: number) => void;
}

const EnvelopeGroupCard = (props: EnvelopeGroupCardProps): React.ReactNode => {
  const { t } = useTranslation();
  const active = props.group.envelopes.some(hasMoney);
  const standalone =
    props.group.envelopes.length === 1 &&
    props.group.envelopes[0]?.category === props.group.name;
  const [openedOverride, setOpenedOverride] = React.useState<boolean | null>(null);
  const opened = props.forceOpen || standalone || (openedOverride ?? active);

  const total = (pick: (envelope: IEnvelopeCategory) => number) =>
    props.group.envelopes.reduce((sum, envelope) => sum + pick(envelope), 0);

  return (
    <Card w="100%" p="0.75rem">
      <Stack gap="0.75rem">
        {!standalone && (
          <Group justify="space-between" align="flex-start" wrap="nowrap">
            <Stack gap="0.35rem" flex="1">
              <HeadingText level={5}>{props.group.name}</HeadingText>
              <div className={classes.row}>
                <BodyText size="sm" tone="secondary" className={classes.name}>
                  {t("envelope_group_total")}
                </BodyText>
                {props.metric(t("envelope_leftover"), total((envelope) => envelope.leftover))}
                {props.metric(t("envelope_assigned"), total((envelope) => envelope.assigned))}
                {props.metric(t("envelope_activity"), total((envelope) => envelope.activity))}
                {props.metric(t("envelope_available"), total((envelope) => envelope.available))}
              </div>
            </Stack>
            <Button
              variant="outline"
              color="primary"
              size="compact-sm"
              onClick={() => setOpenedOverride(!opened)}
            >
              {opened ? t("envelope_hide_categories") : t("envelope_show_categories")}
            </Button>
          </Group>
        )}
        {opened &&
          props.group.envelopes.map((envelope) => (
            <div key={envelope.category} className={classes.row}>
              <Stack gap={0} className={classes.name}>
                <BodyText fw={600}>{envelope.category}</BodyText>
                {envelope.available < 0 && (
                  <BodyText size="xs" tone="secondary">
                    {t("envelope_overspent")}
                  </BodyText>
                )}
              </Stack>
              {props.metric(t("envelope_leftover"), envelope.leftover)}
              <div className={classes.metric}>
                <BodyText size="xs" tone="secondary">
                  {t("envelope_assigned")}
                </BodyText>
                <AssignedInput
                  category={envelope.category}
                  value={envelope.assigned}
                  disabled={props.disabled}
                  onSave={(assigned) => props.onAssign(envelope.category, assigned)}
                />
              </div>
              {props.metric(t("envelope_activity"), envelope.activity)}
              {props.metric(t("envelope_available"), envelope.available)}
              <div className={classes.move}>
                <MoveMoneyModal
                  month={props.month}
                  from={envelope}
                  envelopes={props.envelopes}
                  disabled={props.disabled}
                />
              </div>
            </div>
          ))}
      </Stack>
    </Card>
  );
};

export default EnvelopeGroups;
