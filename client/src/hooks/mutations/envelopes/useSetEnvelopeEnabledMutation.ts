import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { envelopesQueryKey, translateAxiosError } from "~/helpers/requests";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useSetEnvelopeEnabledMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (enabled: boolean) =>
      await request({
        url: "/api/envelopes/enabled",
        method: "PUT",
        data: { enabled },
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
