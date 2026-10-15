import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { translateAxiosError, yearPlanQueryKey } from "~/helpers/requests";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useUpdatePlanningSettingsMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (financialYearStartMonth: number) =>
      await request({
        url: "/api/yearplan/settings",
        method: "PUT",
        data: { financialYearStartMonth },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [yearPlanQueryKey] });
    },
    onError: (error: AxiosError) =>
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(error),
      }),
  });
};
