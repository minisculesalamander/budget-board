import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { envelopesQueryKey, translateAxiosError } from "~/helpers/requests";
import { IEnvelopeAssignRequest } from "~/models/envelope";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useAssignEnvelopeMutation = () => {
  const queryClient = useQueryClient();
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (assignment: IEnvelopeAssignRequest) =>
      await request({
        url: "/api/envelopes/assign",
        method: "PUT",
        data: assignment,
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
