import { Group, Stack } from "@mantine/core";
import { Button } from "@teelur/budget-board-ui";
import React from "react";
import { NotificationType, showNotification } from "~/helpers/notifications";
import {
  FieldToOperatorType,
  IAutomaticRuleBehavior,
  IRuleParameterEdit,
  Operators,
  OperatorTypes,
  TransactionFields,
} from "~/models/automaticRule";
import {
  defaultAutomaticRuleBehavior,
  getDefaultAction,
  hasEmptyRecurringAction,
  hasEmptyTagAction,
  toAutomaticRuleRequest,
} from "~/helpers/automaticRules";
import AutomaticRulePreview from "../AutomaticRulePreview/AutomaticRulePreview";

import EditableAutomaticRuleContent from "../EditableAutomaticRuleContent/EditableAutomaticRuleContent";
import { useTranslation } from "react-i18next";
import { useCreateAutomaticRuleMutation } from "~/hooks/mutations/automaticRules/useCreateAutomaticRuleMutation";
import { PlayIcon, PlusIcon } from "lucide-react";

const AddAutomaticRule = (): React.ReactNode => {
  const defaultField =
    TransactionFields.find((field) => field.value === "merchant")?.value ?? "";

  const [conditionItems, setConditionItems] = React.useState<
    IRuleParameterEdit[]
  >([
    {
      field: defaultField,
      operator:
        Operators.filter((op) =>
          op.type.includes(
            FieldToOperatorType.get(defaultField) ?? OperatorTypes.STRING,
          ),
        )
          .map((op) => op.value)
          .at(0) ?? "",
      value: "",
    },
  ]);

  const [actionItems, setActionItems] = React.useState<IRuleParameterEdit[]>([
    getDefaultAction(),
  ]);
  const [behavior, setBehavior] = React.useState<IAutomaticRuleBehavior>(
    defaultAutomaticRuleBehavior(),
  );
  const [previewOpened, setPreviewOpened] = React.useState(false);

  const { t } = useTranslation();
  const createAutomaticRuleMutation = useCreateAutomaticRuleMutation();

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

  const ruleRequest = toAutomaticRuleRequest(
    conditionItems,
    actionItems,
    behavior,
  );

  const onAddRuleClick = () => {
    if (!hasValidActions()) {
      return;
    }

    const newAutomaticRule = ruleRequest;

    createAutomaticRuleMutation.mutate(newAutomaticRule, {
      onSuccess: () => {
        setConditionItems([
          {
            field: defaultField,
            operator:
              Operators.filter((op) =>
                op.type.includes(
                  FieldToOperatorType.get(defaultField) ?? OperatorTypes.STRING,
                ),
              )
                .map((op) => op.value)
                .at(0) ?? "",
            value: "",
          },
        ]);
        setActionItems([getDefaultAction()]);
        setBehavior(defaultAutomaticRuleBehavior());
      },
    });
  };

  return (
    <Stack gap="0.5rem">
      <EditableAutomaticRuleContent
        conditionItems={conditionItems}
        actionItems={actionItems}
        behavior={behavior}
        setConditionItems={setConditionItems}
        setActionItems={setActionItems}
        setBehavior={setBehavior}
      />
      <Group w="100%">
        <Button
          variant="filled"
          color="primary"
          size="compact-sm"
          flex="1 1 auto"
          rightSection={<PlusIcon size="1rem" />}
          loading={createAutomaticRuleMutation.isPending}
          onClick={() => {
            onAddRuleClick();
          }}
        >
          {t("add_rule")}
        </Button>
        <Button
          variant="filled"
          color="secondary"
          size="compact-sm"
          flex="1 1 auto"
          rightSection={<PlayIcon size="0.8rem" />}
          onClick={() => {
            if (!hasValidActions()) {
              return;
            }

            setPreviewOpened(true);
          }}
        >
          {t("preview_matches")}
        </Button>
      </Group>
      <AutomaticRulePreview
        opened={previewOpened}
        rule={ruleRequest}
        onClose={() => setPreviewOpened(false)}
        onApplied={() => setPreviewOpened(false)}
      />
    </Stack>
  );
};

export default AddAutomaticRule;
