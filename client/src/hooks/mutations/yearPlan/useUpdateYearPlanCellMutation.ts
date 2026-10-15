import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import {
  budgetsQueryKey,
  translateAxiosError,
  yearPlanQueryKey,
} from "~/helpers/requests";
import { IYearPlanCellUpdate } from "~/models/yearPlan";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useUpdateYearPlanCellMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (cell: IYearPlanCellUpdate) =>
      await request({
        url: "/api/yearplan/cell",
        method: "PUT",
        data: cell,
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
