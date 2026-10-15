import { useQuery } from "@tanstack/react-query";
import { renewalAlertSettingsQueryKey } from "~/helpers/requests";
import { IRenewalAlertSettingsResponse } from "~/models/renewalAlert";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useRenewalAlertSettingsQuery = () => {
  const { request } = useAuth();

  return useQuery({
    queryKey: [renewalAlertSettingsQueryKey],
    queryFn: async (): Promise<IRenewalAlertSettingsResponse> => {
      const response = await request({
        url: "/api/renewalAlert/settings",
        method: "GET",
      });

      return response.data as IRenewalAlertSettingsResponse;
    },
  });
};
