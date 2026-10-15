import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { envelopesQueryKey, translateAxiosError } from "~/helpers/requests";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useSetOnBudgetAccountsMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (accountIds: string[]) =>
      await request({
        url: "/api/envelopes/accounts",
        method: "PUT",
        data: { accountIds },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [envelopesQueryKey] });
    },
    onError: (error: AxiosError) =>
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(error),
      }),
  });
};
