import { useQuery } from "@tanstack/react-query";
import { AxiosResponse } from "axios";
import { yearPlanQueryKey } from "~/helpers/requests";
import { IYearPlan } from "~/models/yearPlan";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";

interface UseYearPlanQueryProps {
  startYear: number;
  yearCount: number;
  enabled?: boolean;
}

export const useYearPlanQuery = ({
  startYear,
  yearCount,
  enabled = true,
}: UseYearPlanQueryProps) => {
  const { request } = useAuth();

  return useQuery({
    queryKey: [yearPlanQueryKey, startYear, yearCount],
    queryFn: async (): Promise<IYearPlan> => {
      const res: AxiosResponse = await request({
        url: "/api/yearplan",
        method: "GET",
        params: { startYear, yearCount },
      });

      return res.data as IYearPlan;
    },
    enabled,
  });
};
