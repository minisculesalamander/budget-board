import { Group, Stack } from "@mantine/core";
import {
  ActionIcon,
  Badge,
  BodyText,
  Button,
  Card,
} from "@teelur/budget-board-ui";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { PencilIcon, PlayIcon, TrashIcon } from "lucide-react";
import React from "react";
import {
  hasEmptyRecurringAction,
  hasEmptyTagAction,
  toAutomaticRuleRequest,
} from "~/helpers/automaticRules";
import {
  IAutomaticRuleBehavior,
  IAutomaticRuleResponse,
  IRuleParameterEdit,
} from "~/models/automaticRule";
import AutomaticRulePreview from "../AutomaticRulePreview/AutomaticRulePreview";
import ConditionItem from "./ConditionItem/ConditionItem";
import ActionItem from "./ActionItem/ActionItem";
import EditableAutomaticRuleContent from "../EditableAutomaticRuleContent/EditableAutomaticRuleContent";
import { useTransactionCategories } from "~/providers/TransactionCategoryProvider/TransactionCategoryProvider";
import { useTranslation } from "react-i18next";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";
import { useAccountsQuery } from "~/hooks/queries/useAccountsQuery";
import { useDeleteAutomaticRuleMutation } from "~/hooks/mutations/automaticRules/useDeleteAutomaticRuleMutation";
import { useUpdateAutomaticRuleMutation } from "~/hooks/mutations/automaticRules/useUpdateAutomaticRuleMutation";
import { useRecurringRulesQuery } from "~/hooks/queries/useRecurringRulesQuery";

interface AutomaticRuleCardProps {
  rule: IAutomaticRuleResponse;
}

const AutomaticRuleCard = (props: AutomaticRuleCardProps) => {
  const [isSelected, setIsSelected] = React.useState(false);

  const [conditionItems, setConditionItems] = React.useState<
    IRuleParameterEdit[]
  >(props.rule.conditions ?? []);
  const [actionItems, setActionItems] = React.useState<IRuleParameterEdit[]>(
    props.rule.actions ?? [],
  );
  const [behavior, setBehavior] = React.useState<IAutomaticRuleBehavior>({
    matchMode: props.rule.matchMode ?? "all",
    priority: props.rule.priority ?? null,
    stopProcessing: props.rule.stopProcessing ?? false,
  });
  const [previewOpened, setPreviewOpened] = React.useState(false);

  const { t } = useTranslation();
  const { allTransactionCategories: transactionCategories } =
    useTransactionCategories();
  const { preferredCurrency } = useUserSettings();
  const accountsQuery = useAccountsQuery();
  const recurringRulesQuery = useRecurringRulesQuery();
  const deleteAutomaticRuleMutation = useDeleteAutomaticRuleMutation();
  const updateAutomaticRuleMutation = useUpdateAutomaticRuleMutation();

  const savedBehavior = (): IAutomaticRuleBehavior => ({
    matchMode: props.rule.matchMode ?? "all",
    priority: props.rule.priority ?? null,
    stopProcessing: props.rule.stopProcessing ?? false,
  });

  const hasValidActions = (): boolean => {
    if (hasEmptyRecurringAction(actionItems)) {
      showNotification({
        message: t("recurring_rule_required"),
        type: NotificationType.Error,
      });
      return false;
    }

    if (!hasEmptyTagAction(actionItems)) {
      return true;
    }

    showNotification({
      message: t("at_least_one_tag_required"),
      type: NotificationType.Error,
    });
    return false;
  };

  if (isSelected) {
    return (
      <Card>
        <Stack gap="0.5rem">
          <EditableAutomaticRuleContent
            conditionItems={conditionItems}
            actionItems={actionItems}
            behavior={behavior}
            setConditionItems={setConditionItems}
            setActionItems={setActionItems}
            setBehavior={setBehavior}
            surface="sunken"
          />
          <Group w="100%">
            <Button
              variant="filled"
              color="neutral"
              size="compact-sm"
              flex="1 1 0"
              onClick={() => setIsSelected(false)}
            >
              {t("cancel")}
            </Button>
            <Button
              variant="filled"
              color="primary"
              size="compact-sm"
              flex="1 1 0"
              loading={updateAutomaticRuleMutation.isPending}
              onClick={() => {
                if (!hasValidActions()) {
                  return;
                }

                updateAutomaticRuleMutation.mutate(
                  {
                    id: props.rule.id,
                    ...toAutomaticRuleRequest(
                      conditionItems,
                      actionItems,
                      behavior,
                    ),
                    conditions: conditionItems.map((item) => ({
                      id: item.id ?? "",
                      value: item.value,
                      field: item.field,
                      operator: item.operator,
                    })),
                    actions: actionItems.map((item) => ({
                      id: item.id ?? "",
                      value: item.value,
                      field: item.field,
                      operator: item.operator,
                    })),
                  },
                  {
                    onSuccess: () => {
                      setIsSelected(false);
                    },
                  },
                );
              }}
            >
              {t("save")}
            </Button>
          </Group>
        </Stack>
      </Card>
    );
  }

  return (
    <Card>
      <Group gap={0} justify="space-between" wrap="nowrap">
        <Stack>
          <Group gap="0.25rem">
            <Badge variant="outline" color="secondary" size="xs">
              {t("rule_priority_value", {
                priority: props.rule.priority ?? 0,
              })}
            </Badge>
            {props.rule.stopProcessing ? (
              <Badge variant="filled" color="accent" size="xs">
                {t("stop_processing")}
              </Badge>
            ) : null}
          </Group>
          <Group gap="0.25rem">
            <BodyText component="span" tone="secondary" size="sm" pr="0.5rem">
              {t("if")}
            </BodyText>
            {(props.rule.conditions ?? []).map((condition, index) => (
              <React.Fragment key={index}>
                <ConditionItem
                  key={condition.id}
                  condition={condition}
                  categories={transactionCategories}
                  currency={preferredCurrency}
                  accounts={accountsQuery.data ?? []}
                />
                {index < (props.rule.conditions ?? []).length - 1 && (
                  <BodyText component="span" tone="secondary" size="sm">
                    {t(props.rule.matchMode === "any" ? "or" : "and")}
                  </BodyText>
                )}
              </React.Fragment>
            ))}
          </Group>
          <Group gap="0.25rem">
            <BodyText component="span" tone="secondary" size="sm" pr="0.5rem">
              {t("then")}
            </BodyText>
            {(props.rule.actions ?? []).map((action, index) => (
              <React.Fragment key={index}>
                <ActionItem
                  key={action.id}
                  action={action}
                  categories={transactionCategories}
                  currency={preferredCurrency}
                  accounts={accountsQuery.data ?? []}
                  recurringRules={recurringRulesQuery.data ?? []}
                />
                {index < (props.rule.actions ?? []).length - 1 && (
                  <BodyText component="span" tone="secondary" size="sm">
                    {t("and")}
                  </BodyText>
                )}
              </React.Fragment>
            ))}
          </Group>
        </Stack>
        <Group style={{ alignSelf: "stretch" }} gap="0.5rem" wrap="nowrap">
          <ActionIcon
            variant="filled"
            color="secondary"
            size="compact-sm"
            aria-label={t("preview_matches")}
            onClick={() => {
              setPreviewOpened(true);
            }}
            h="100%"
          >
            <PlayIcon size="1rem" />
          </ActionIcon>
          <ActionIcon
            variant="outline"
            color="primary"
            size="compact-sm"
            h="100%"
            onClick={() => {
              setConditionItems(props.rule.conditions ?? []);
              setActionItems(props.rule.actions ?? []);
              setBehavior(savedBehavior());
              setIsSelected(true);
            }}
          >
            <PencilIcon size="1rem" />
          </ActionIcon>
          <ActionIcon
            variant="filled"
            color="error"
            size="compact-sm"
            loading={deleteAutomaticRuleMutation.isPending}
            h="100%"
            onClick={(e) => {
              e.stopPropagation();
              deleteAutomaticRuleMutation.mutate(props.rule.id);
            }}
          >
            <TrashIcon size="1rem" />
          </ActionIcon>
        </Group>
      </Group>
      <AutomaticRulePreview
        opened={previewOpened}
        rule={toAutomaticRuleRequest(
          props.rule.conditions ?? [],
          props.rule.actions ?? [],
          savedBehavior(),
        )}
        onClose={() => setPreviewOpened(false)}
        onApplied={() => setPreviewOpened(false)}
      />
    </Card>
  );
};

export default AutomaticRuleCard;
