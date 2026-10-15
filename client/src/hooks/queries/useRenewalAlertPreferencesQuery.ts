import { useQuery } from "@tanstack/react-query";
import { renewalAlertPreferencesQueryKey } from "~/helpers/requests";
import { IRenewalAlertPreferenceList } from "~/models/renewalAlert";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

export const useRenewalAlertPreferencesQuery = () => {
  const { request } = useAuth();

  return useQuery({
    queryKey: [renewalAlertPreferencesQueryKey],
    queryFn: async (): Promise<IRenewalAlertPreferenceList> => {
      const response = await request({
        url: "/api/renewalAlert/preferences",
        method: "GET",
      });

      return response.data as IRenewalAlertPreferenceList;
    },
  });
};
