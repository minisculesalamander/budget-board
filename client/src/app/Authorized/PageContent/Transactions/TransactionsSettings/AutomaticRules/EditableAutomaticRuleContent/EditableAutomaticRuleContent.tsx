import { Group, Stack } from "@mantine/core";
import {
  ActionIcon,
  BodyText,
  Checkbox,
  NumberInput,
  SegmentedControl,
} from "@teelur/budget-board-ui";
import { PlusIcon } from "lucide-react";
import React from "react";
import {
  AutomaticRuleMatchMode,
  ConditionTransactionFields,
  FieldToOperatorType,
  IAutomaticRuleBehavior,
  IRuleParameterEdit,
  Operators,
  OperatorTypes,
} from "~/models/automaticRule";
import { getDefaultAction } from "~/helpers/automaticRules";
import ActionItem from "./ActionItem/ActionItem";
import ConditionItem from "./ConditionItem/ConditionItem";
import { useTransactionCategories } from "~/providers/TransactionCategoryProvider/TransactionCategoryProvider";
import { useTranslation } from "react-i18next";

interface EditableAutomaticRuleContentProps {
  conditionItems: IRuleParameterEdit[];
  actionItems: IRuleParameterEdit[];
  behavior: IAutomaticRuleBehavior;
  setConditionItems: React.Dispatch<React.SetStateAction<IRuleParameterEdit[]>>;
  setActionItems: React.Dispatch<React.SetStateAction<IRuleParameterEdit[]>>;
  setBehavior: React.Dispatch<React.SetStateAction<IAutomaticRuleBehavior>>;
  surface?: "sunken" | "default";
}

const EditableAutomaticRuleContent = (
  props: EditableAutomaticRuleContentProps,
): React.ReactNode => {
  const defaultField =
    ConditionTransactionFields.find((field) => field.value === "merchant")
      ?.value ?? "";

  const { t } = useTranslation();
  const { allTransactionCategories: transactionCategories } =
    useTransactionCategories();

  const addNewCondition = () => {
    props.setConditionItems((prev) => [
      ...prev,
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
        type: "",
      },
    ]);
  };

  const removeCondition = (index: number) => {
    props.setConditionItems((prev) => prev.filter((_, i) => i !== index));
  };

  const addNewAction = () => {
    props.setActionItems((prev) => [...prev, getDefaultAction()]);
  };

  const removeAction = (index: number) => {
    props.setActionItems((prev) => prev.filter((_, i) => i !== index));
  };

  return (
    <Stack gap="0.5rem">
      <Group align="end" gap="0.75rem">
        <SegmentedControl
          color="secondary"
          size="compact-sm"
          value={props.behavior.matchMode}
          data={[
            { value: "all", label: t("match_all") },
            { value: "any", label: t("match_any") },
          ]}
          onChange={(value) =>
            props.setBehavior((current) => ({
              ...current,
              matchMode: value as AutomaticRuleMatchMode,
            }))
          }
          aria-label={t("match_all")}
        />
        <NumberInput
          label={t("rule_priority")}
          placeholder={t("rule_priority_next")}
          value={props.behavior.priority ?? ""}
          onChange={(value) =>
            props.setBehavior((current) => ({
              ...current,
              priority: typeof value === "number" ? value : null,
            }))
          }
          allowDecimal={false}
          w="8rem"
        />
        <Checkbox
          label={t("stop_processing")}
          checked={props.behavior.stopProcessing}
          onChange={(event) =>
            props.setBehavior((current) => ({
              ...current,
              stopProcessing: event.currentTarget.checked,
            }))
          }
        />
      </Group>
      <Stack gap="0.25rem">
        <Group align="center" justify="space-between">
          <BodyText tone="primary" size="sm" fw={500}>
            {t("if")}
          </BodyText>
          <ActionIcon
            variant="filled"
            color="primary"
            size="compact-xs"
            onClick={addNewCondition}
          >
            <PlusIcon size={16} />
          </ActionIcon>
        </Group>
        {props.conditionItems.map((item: IRuleParameterEdit, index: number) => (
          <ConditionItem
            key={index}
            ruleParameter={item}
            setRuleParameter={(newParameter) =>
              props.setConditionItems(
                (prev: IRuleParameterEdit[]): IRuleParameterEdit[] =>
                  prev.map((param, i) => (i === index ? newParameter : param)),
              )
            }
            allowDelete={props.conditionItems.length > 1}
            doDelete={removeCondition}
            index={index}
            categories={transactionCategories}
            surface={props.surface}
          />
        ))}
      </Stack>
      <Stack gap="0.25rem">
        <Group align="center" justify="space-between">
          <BodyText tone="primary" size="sm" fw={500}>
            {t("then")}
          </BodyText>
          <ActionIcon
            variant="filled"
            color="primary"
            size="compact-xs"
            onClick={addNewAction}
          >
            <PlusIcon size={16} />
          </ActionIcon>
        </Group>
        {props.actionItems.map((item: IRuleParameterEdit, index: number) => (
          <ActionItem
            key={index}
            ruleParameter={item}
            setRuleParameter={(newParameter) =>
              props.setActionItems(
                (prev: IRuleParameterEdit[]): IRuleParameterEdit[] =>
                  prev.map((param, i) => (i === index ? newParameter : param)),
              )
            }
            allowDelete={props.actionItems.length > 1}
            doDelete={removeAction}
            index={index}
            categories={transactionCategories}
            surface={props.surface}
          />
        ))}
      </Stack>
    </Stack>
  );
};

export default EditableAutomaticRuleContent;
