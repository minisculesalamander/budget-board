import { useQuery } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { normalizeRecurringCadence } from "~/helpers/recurringRules";
import {
  recurringRulesQueryKey,
  translateAxiosError,
} from "~/helpers/requests";
import {
  IRecurringRuleResponse,
  RecurringRuleSuggestionStatuses,
} from "~/models/recurringRule";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

const normalizeRule = (rule: IRecurringRuleResponse): IRecurringRuleResponse => ({
  ...rule,
  cadence: normalizeRecurringCadence(rule.cadence),
  suggestionStatus:
    rule.suggestionStatus ?? RecurringRuleSuggestionStatuses.Accepted,
  lastChargeDate: rule.lastChargeDate ?? null,
  forecastAmount: rule.forecastAmount ?? rule.amount,
});

export const useSubscriptionsQuery = () => {
  const { request } = useAuth();

  return useQuery({
    queryKey: [recurringRulesQueryKey, "subscriptions"],
    queryFn: async (): Promise<IRecurringRuleResponse[]> => {
      try {
        await request({
          url: "/api/recurringRule/suggestions",
          method: "POST",
        });
      } catch (error) {
        showNotification({
          message: translateAxiosError(error as AxiosError),
          type: NotificationType.Error,
        });
      }

      const response = await request({
        url: "/api/recurringRule",
        method: "GET",
      });

      return (response.data as IRecurringRuleResponse[]).map(normalizeRule);
    },
  });
};
