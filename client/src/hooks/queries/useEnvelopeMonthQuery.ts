import { useQuery } from "@tanstack/react-query";
import { AxiosResponse } from "axios";
import { envelopesQueryKey } from "~/helpers/requests";
import { IEnvelopeMonth } from "~/models/envelope";
import { useAuth } from "~/providers/AuthProvider/AuthProvider";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";

export const useEnvelopeMonthQuery = (month: Date) => {
  const { request } = useAuth();
  const { dayjs } = useLocale();
  const monthKey = dayjs(month).format("YYYY-MM");

  return useQuery({
    queryKey: [envelopesQueryKey, monthKey],
    queryFn: async (): Promise<IEnvelopeMonth> => {
      const res: AxiosResponse = await request({
        url: "/api/envelopes",
        method: "GET",
        params: { month: dayjs(month).startOf("month").format("YYYY-MM-DD") },
      });

      return res.data as IEnvelopeMonth;
    },
  });
};
