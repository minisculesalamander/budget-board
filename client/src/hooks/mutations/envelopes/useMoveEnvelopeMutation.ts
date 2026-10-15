import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { envelopesQueryKey, translateAxiosError } from "~/helpers/requests";
import { IEnvelopeMoveRequest } from "~/models/envelope";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useMoveEnvelopeMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (move: IEnvelopeMoveRequest) =>
      await request({
        url: "/api/envelopes/move",
        method: "POST",
        data: move,
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
