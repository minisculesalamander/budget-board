import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import {
  budgetsQueryKey,
  translateAxiosError,
  yearPlanQueryKey,
} from "~/helpers/requests";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useCopyYearPlanMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (fromYear: number) =>
      await request({
        url: "/api/yearplan/copy",
        method: "POST",
        params: { fromYear },
      }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: [yearPlanQueryKey] }),
        queryClient.invalidateQueries({ queryKey: [budgetsQueryKey] }),
      ]);
    },
    onError: (error: AxiosError) =>
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(error),
      }),
  });
};
