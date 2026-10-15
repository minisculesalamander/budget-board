import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import {
  renewalAlertPreferencesQueryKey,
  renewalAlertSettingsQueryKey,
  translateAxiosError,
} from "~/helpers/requests";
import {
  IRenewalAlertSettingsResponse,
  IRenewalAlertSettingsUpdateRequest,
} from "~/models/renewalAlert";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useUpdateRenewalAlertSettingsMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (
      settings: IRenewalAlertSettingsUpdateRequest,
    ): Promise<IRenewalAlertSettingsResponse> => {
      const response = await request({
        url: "/api/renewalAlert/settings",
        method: "PUT",
        data: settings,
      });

      return response.data as IRenewalAlertSettingsResponse;
    },
    onSuccess: async (settings) => {
      queryClient.setQueryData([renewalAlertSettingsQueryKey], settings);
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
