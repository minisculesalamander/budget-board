import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import {
  renewalAlertPreferencesQueryKey,
  translateAxiosError,
} from "~/helpers/requests";
import { ISubscriptionRenewalPreferenceUpdateRequest } from "~/models/renewalAlert";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useUpdateRenewalAlertPreferenceMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (input: {
      recurringRuleID: string;
      preference: ISubscriptionRenewalPreferenceUpdateRequest;
    }) =>
      request({
        url: `/api/renewalAlert/preferences/${input.recurringRuleID}`,
        method: "PUT",
        data: input.preference,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: [renewalAlertPreferencesQueryKey],
      });
    },
    onError: (error: AxiosError) => {
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(error),
      });
    },
  });
};
