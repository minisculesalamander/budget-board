import { useMutation } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { translateAxiosError } from "~/helpers/requests";
import {
  IAutomaticRuleCreateRequest,
  IAutomaticRulePreviewResponse,
} from "~/models/automaticRule";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const usePreviewAutomaticRuleMutation = () => {
  const { request } = useAuth();

  return useMutation({
    mutationFn: async (automaticRule: IAutomaticRuleCreateRequest) => {
      const response = await request({
        url: "/api/automaticRule/preview",
        method: "POST",
        data: automaticRule,
      });

      return response.data as IAutomaticRulePreviewResponse;
    },
    onError: (error: AxiosError) => {
      showNotification({
        message: translateAxiosError(error),
        type: NotificationType.Error,
      });
    },
  });
};
