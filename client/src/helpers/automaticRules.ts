import dayjs from "dayjs";
import { convertNumberToCurrency, SignDisplay } from "./currency";
import { getFormattedCategoryValue } from "./category";
import { ICategory } from "~/models/category";
import { IAccountResponse } from "~/models/account";
import { getTrimmedUniqueTags } from "~/helpers/tags";
import {
  ActionTransactionFields,
  getActionOperators,
  IAutomaticRuleBehavior,
  IAutomaticRuleCreateRequest,
  IRuleParameterEdit,
} from "~/models/automaticRule";
import { isNumericLiteral } from "./automaticRuleExpressions";

export const getDefaultValue = (field: string): string => {
  switch (field) {
    case "merchant":
    case "description":
    case "transfer":
    case "recurringRule":
      return "";
    case "amount":
      return "0";
    case "date":
      return dayjs().format("YYYY-MM-DD");
    case "category":
      return "";
    case "note":
      return "";
    case "tags":
      return "[]";
    default:
      return "";
  }
};

export const getDefaultAction = (): IRuleParameterEdit => {
  const field =
    ActionTransactionFields.find((item) => item.value === "merchant")?.value ??
    "merchant";

  return {
    field,
    operator: getActionOperators(field)[0]?.value ?? "set",
    value: getDefaultValue(field),
  };
};

export const serializeActionTags = (tags: string[]): string =>
  JSON.stringify(getTrimmedUniqueTags(tags));

export const deserializeActionTags = (value: string): string[] => {
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed)
      ? getTrimmedUniqueTags(
          parsed.filter((tag): tag is string => typeof tag === "string"),
        )
      : [];
  } catch {
    return [];
  }
};

export const hasEmptyTagAction = (
  actions: Pick<IRuleParameterEdit, "field" | "value">[],
): boolean =>
  actions.some(
    (action) =>
      action.field === "tags" &&
      deserializeActionTags(action.value).length === 0,
  );

export const defaultAutomaticRuleBehavior = (): IAutomaticRuleBehavior => ({
  matchMode: "all",
  priority: null,
  stopProcessing: false,
});

export const toAutomaticRuleRequest = (
  conditions: IRuleParameterEdit[],
  actions: IRuleParameterEdit[],
  behavior: IAutomaticRuleBehavior,
): IAutomaticRuleCreateRequest => ({
  conditions: conditions.map((item) => ({
    field: item.field,
    operator: item.operator,
    value: item.value,
  })),
  actions: actions.map((item) => ({
    field: item.field,
    operator: item.operator,
    value: item.value,
  })),
  matchMode: behavior.matchMode,
  priority: behavior.priority,
  stopProcessing: behavior.stopProcessing,
});

export const hasEmptyRecurringAction = (
  actions: Pick<IRuleParameterEdit, "field" | "value">[],
): boolean =>
  actions.some(
    (action) => action.field === "recurringRule" && action.value.trim() === "",
  );

export const formatAmountRange = (
  value: string,
  formatAmount: (amount: string) => string,
): string => {
  const [minimum, maximum] = value.split("|");
  if (minimum === undefined || maximum === undefined) {
    return value;
  }

  return `${formatAmount(minimum)} – ${formatAmount(maximum)}`;
};

export const getFormattedValue = (
  field: string,
  value: string,
  currency: string,
  decimalPlaces: number,
  categories: ICategory[],
  formatDate: (dateStr: string) => string,
  intlLocale: string,
  accounts: IAccountResponse[] = [],
  recurringRules: { id: string; merchantName: string | null; accountName: string }[] = [],
): string => {
  switch (field) {
    case "merchant":
    case "description":
      return value;
    case "amount":
      if (value.includes("|")) {
        return formatAmountRange(value, (amount) =>
          !isNumericLiteral(amount) || !Number.isFinite(Number(amount))
            ? amount
            : convertNumberToCurrency(
                Number(amount),
                decimalPlaces,
                currency,
                SignDisplay.Auto,
                intlLocale,
              ),
        );
      }

      if (!isNumericLiteral(value) || !Number.isFinite(Number(value))) {
        return value;
      }

      return convertNumberToCurrency(
        Number(value),
        decimalPlaces,
        currency,
        SignDisplay.Auto,
        intlLocale,
      );
    case "date":
      return formatDate(value);
    case "category":
      return getFormattedCategoryValue(value, categories);
    case "note":
      return value;
    case "transfer": {
      const accountName = accounts.find((account) => account.id === value)?.name;
      return accountName ?? value;
    }
    case "recurringRule": {
      const rule = recurringRules.find((candidate) => candidate.id === value);
      if (!rule) {
        return value;
      }

      return [rule.merchantName, rule.accountName].filter(Boolean).join(" · ");
    }
    case "tags":
      return deserializeActionTags(value).join(", ");
    case "account": {
      const accountNames = accounts
        .filter((account) => value.split(",").includes(account.id))
        .map((account) => account.name);
      return accountNames.join(", ");
    }
    default:
      return value;
  }
};
