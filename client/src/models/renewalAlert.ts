export const RenewalAlertWebhookFormats = {
  Json: "json",
  Ntfy: "ntfy",
} as const;

export type RenewalAlertWebhookFormat =
  (typeof RenewalAlertWebhookFormats)[keyof typeof RenewalAlertWebhookFormats];

export interface IRenewalAlertSettingsResponse {
  enabled: boolean;
  daysBefore: number;
  emailEnabled: boolean;
  emailAddress: string | null;
  discordEnabled: boolean;
  discordWebhookConfigured: boolean;
  telegramEnabled: boolean;
  telegramBotTokenConfigured: boolean;
  telegramChatId: string | null;
  webhookEnabled: boolean;
  webhookUrlConfigured: boolean;
  webhookTokenConfigured: boolean;
  webhookFormat: RenewalAlertWebhookFormat;
  smtpConfigured: boolean;
}

export interface IRenewalAlertSettingsUpdateRequest {
  enabled: boolean;
  daysBefore: number;
  emailEnabled: boolean;
  emailAddress: string | null;
  discordEnabled: boolean;
  discordWebhookUrl?: string | null;
  telegramEnabled: boolean;
  telegramBotToken?: string | null;
  telegramChatId: string | null;
  webhookEnabled: boolean;
  webhookUrl?: string | null;
  webhookToken?: string | null;
  webhookFormat: RenewalAlertWebhookFormat;
}

export interface ISubscriptionRenewalPreference {
  recurringRuleID: string;
  alertsEnabled: boolean;
  daysBefore: number | null;
}

export interface IRenewalAlertPreferenceList {
  defaultDaysBefore: number;
  alertsEnabled: boolean;
  preferences: ISubscriptionRenewalPreference[];
}

export interface ISubscriptionRenewalPreferenceUpdateRequest {
  alertsEnabled: boolean;
  daysBefore: number | null;
}
