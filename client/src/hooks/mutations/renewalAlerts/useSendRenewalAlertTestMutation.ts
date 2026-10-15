import { useMutation } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { translateAxiosError } from "~/helpers/requests";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useSendRenewalAlertTestMutation = () => {
  const { request } = useAuth();

  return useMutation({
    mutationFn: async () =>
      request({
        url: "/api/renewalAlert/test",
        method: "POST",
      }),
    onError: (error: AxiosError) => {
      showNotification({
        type: NotificationType.Error,
        message: translateAxiosError(error),
      });
    },
  });
};
