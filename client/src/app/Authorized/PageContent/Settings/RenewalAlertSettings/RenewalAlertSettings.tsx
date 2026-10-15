import { Skeleton, Stack } from "@mantine/core";
import {
  BodyText,
  Button,
  Checkbox,
  HeadingText,
  NumberInput,
  PasswordInput,
  Select,
  TextInput,
} from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { NotificationType, showNotification } from "~/helpers/notifications";
import { useSendRenewalAlertTestMutation } from "~/hooks/mutations/renewalAlerts/useSendRenewalAlertTestMutation";
import { useUpdateRenewalAlertSettingsMutation } from "~/hooks/mutations/renewalAlerts/useUpdateRenewalAlertSettingsMutation";
import { useRenewalAlertSettingsQuery } from "~/hooks/queries/useRenewalAlertSettingsQuery";
import {
  IRenewalAlertSettingsResponse,
  IRenewalAlertSettingsUpdateRequest,
  RenewalAlertWebhookFormat,
  RenewalAlertWebhookFormats,
} from "~/models/renewalAlert";

const RenewalAlertSettings = (): React.ReactNode => {
  const { t } = useTranslation();
  const settingsQuery = useRenewalAlertSettingsQuery();
  const updateMutation = useUpdateRenewalAlertSettingsMutation();
  const testMutation = useSendRenewalAlertTestMutation();
  const [hydrated, setHydrated] = React.useState(false);
  const [enabled, setEnabled] = React.useState(false);
  const [daysBefore, setDaysBefore] = React.useState(3);
  const [emailEnabled, setEmailEnabled] = React.useState(false);
  const [emailAddress, setEmailAddress] = React.useState("");
  const [discordEnabled, setDiscordEnabled] = React.useState(false);
  const [discordWebhookUrl, setDiscordWebhookUrl] = React.useState("");
  const [clearDiscord, setClearDiscord] = React.useState(false);
  const [discordConfigured, setDiscordConfigured] = React.useState(false);
  const [telegramEnabled, setTelegramEnabled] = React.useState(false);
  const [telegramBotToken, setTelegramBotToken] = React.useState("");
  const [clearTelegram, setClearTelegram] = React.useState(false);
  const [telegramConfigured, setTelegramConfigured] = React.useState(false);
  const [telegramChatId, setTelegramChatId] = React.useState("");
  const [webhookEnabled, setWebhookEnabled] = React.useState(false);
  const [webhookUrl, setWebhookUrl] = React.useState("");
  const [clearWebhook, setClearWebhook] = React.useState(false);
  const [webhookConfigured, setWebhookConfigured] = React.useState(false);
  const [webhookToken, setWebhookToken] = React.useState("");
  const [clearWebhookToken, setClearWebhookToken] = React.useState(false);
  const [webhookTokenConfigured, setWebhookTokenConfigured] =
    React.useState(false);
  const [webhookFormat, setWebhookFormat] =
    React.useState<RenewalAlertWebhookFormat>(RenewalAlertWebhookFormats.Json);
  const [smtpConfigured, setSmtpConfigured] = React.useState(false);

  const applySettings = (settings: IRenewalAlertSettingsResponse) => {
    setEnabled(settings.enabled);
    setDaysBefore(settings.daysBefore);
    setEmailEnabled(settings.emailEnabled);
    setEmailAddress(settings.emailAddress ?? "");
    setDiscordEnabled(settings.discordEnabled);
    setDiscordConfigured(settings.discordWebhookConfigured);
    setTelegramEnabled(settings.telegramEnabled);
    setTelegramConfigured(settings.telegramBotTokenConfigured);
    setTelegramChatId(settings.telegramChatId ?? "");
    setWebhookEnabled(settings.webhookEnabled);
    setWebhookConfigured(settings.webhookUrlConfigured);
    setWebhookTokenConfigured(settings.webhookTokenConfigured);
    setWebhookFormat(settings.webhookFormat ?? RenewalAlertWebhookFormats.Json);
    setSmtpConfigured(settings.smtpConfigured);
    setDiscordWebhookUrl("");
    setTelegramBotToken("");
    setWebhookUrl("");
    setWebhookToken("");
    setClearDiscord(false);
    setClearTelegram(false);
    setClearWebhook(false);
    setClearWebhookToken(false);
  };

  React.useEffect(() => {
    if (!settingsQuery.data || hydrated) {
      return;
    }

    applySettings(settingsQuery.data);
    setHydrated(true);
  }, [settingsQuery.data, hydrated]);

  const save = () => {
    if (!Number.isInteger(daysBefore)) {
      return;
    }

    const payload: IRenewalAlertSettingsUpdateRequest = {
      enabled,
      daysBefore,
      emailEnabled,
      emailAddress: emailAddress.trim() === "" ? null : emailAddress.trim(),
      discordEnabled,
      telegramEnabled,
      telegramChatId: telegramChatId.trim() === "" ? null : telegramChatId.trim(),
      webhookEnabled,
      webhookFormat,
    };
    if (clearDiscord) {
      payload.discordWebhookUrl = null;
    } else if (discordWebhookUrl.trim() !== "") {
      payload.discordWebhookUrl = discordWebhookUrl.trim();
    }
    if (clearTelegram) {
      payload.telegramBotToken = null;
    } else if (telegramBotToken.trim() !== "") {
      payload.telegramBotToken = telegramBotToken.trim();
    }
    if (clearWebhook) {
      payload.webhookUrl = null;
    } else if (webhookUrl.trim() !== "") {
      payload.webhookUrl = webhookUrl.trim();
    }
    if (clearWebhookToken) {
      payload.webhookToken = null;
    } else if (webhookToken.trim() !== "") {
      payload.webhookToken = webhookToken.trim();
    }

    updateMutation.mutate(payload, {
      onSuccess: (settings) => {
        applySettings(settings);
        showNotification({
          type: NotificationType.Success,
          message: t("renewal_alerts_saved"),
        });
      },
    });
  };

  if (settingsQuery.isPending || !hydrated) {
    return <Skeleton height={240} />;
  }

  if (settingsQuery.isError) {
    return (
      <BodyText tone="secondary" size="sm">
        {t("renewal_alerts_load_error")}
      </BodyText>
    );
  }

  return (
    <Stack gap="0.75rem" maw={640}>
      <HeadingText tone="heading" level={5}>
        {t("renewal_alerts_title")}
      </HeadingText>
      <BodyText tone="secondary" size="sm">
        {t("renewal_alerts_description")}
      </BodyText>
      <Checkbox
        label={t("renewal_alerts_enabled")}
        checked={enabled}
        onChange={(event) => setEnabled(event.currentTarget.checked)}
      />
      <NumberInput
        label={t("renewal_alerts_days_before")}
        description={t("renewal_alerts_days_before_description")}
        min={0}
        max={90}
        step={1}
        allowDecimal={false}
        allowNegative={false}
        value={daysBefore}
        onChange={(value) => {
          if (typeof value === "number" && Number.isInteger(value)) {
            setDaysBefore(value);
          }
        }}
      />
      <Checkbox
        label={t("renewal_alerts_email")}
        description={t("renewal_alerts_email_help")}
        checked={emailEnabled}
        onChange={(event) => setEmailEnabled(event.currentTarget.checked)}
      />
      {emailEnabled && (
        <Stack gap="0.25rem">
          <TextInput
            label={t("renewal_alerts_email_address")}
            value={emailAddress}
            onChange={(event) => setEmailAddress(event.currentTarget.value)}
          />
          {!smtpConfigured && (
            <BodyText tone="secondary" size="sm">
              {t("renewal_alerts_smtp_missing")}
            </BodyText>
          )}
        </Stack>
      )}
      <Checkbox
        label={t("renewal_alerts_discord")}
        description={t("renewal_alerts_discord_help")}
        checked={discordEnabled}
        onChange={(event) => setDiscordEnabled(event.currentTarget.checked)}
      />
      {discordEnabled && (
        <Stack gap="0.25rem">
          {discordConfigured && (
            <BodyText tone="secondary" size="sm">
              {t("renewal_alerts_configured")}
            </BodyText>
          )}
          <PasswordInput
            label={t("renewal_alerts_discord")}
            placeholder={t("renewal_alerts_secret_placeholder")}
            autoComplete="new-password"
            value={discordWebhookUrl}
            disabled={clearDiscord}
            onChange={(event) => {
              setDiscordWebhookUrl(event.currentTarget.value);
              setClearDiscord(false);
            }}
          />
          {discordConfigured && (
            <Checkbox
              label={t("renewal_alerts_clear")}
              checked={clearDiscord}
              onChange={(event) => setClearDiscord(event.currentTarget.checked)}
            />
          )}
        </Stack>
      )}
      <Checkbox
        label={t("renewal_alerts_telegram")}
        description={t("renewal_alerts_telegram_help")}
        checked={telegramEnabled}
        onChange={(event) => setTelegramEnabled(event.currentTarget.checked)}
      />
      {telegramEnabled && (
        <Stack gap="0.25rem">
          {telegramConfigured && (
            <BodyText tone="secondary" size="sm">
              {t("renewal_alerts_configured")}
            </BodyText>
          )}
          <PasswordInput
            label={t("renewal_alerts_telegram_token")}
            placeholder={t("renewal_alerts_secret_placeholder")}
            autoComplete="new-password"
            value={telegramBotToken}
            disabled={clearTelegram}
            onChange={(event) => {
              setTelegramBotToken(event.currentTarget.value);
              setClearTelegram(false);
            }}
          />
          {telegramConfigured && (
            <Checkbox
              label={t("renewal_alerts_clear")}
              checked={clearTelegram}
              onChange={(event) =>
                setClearTelegram(event.currentTarget.checked)
              }
            />
          )}
          <TextInput
            label={t("renewal_alerts_telegram_chat")}
            value={telegramChatId}
            onChange={(event) => setTelegramChatId(event.currentTarget.value)}
          />
        </Stack>
      )}
      <Checkbox
        label={t("renewal_alerts_webhook")}
        description={t("renewal_alerts_webhook_help")}
        checked={webhookEnabled}
        onChange={(event) => setWebhookEnabled(event.currentTarget.checked)}
      />
      {webhookEnabled && (
        <Stack gap="0.25rem">
          <Select
            label={t("renewal_alerts_webhook_format")}
            data={[
              {
                value: RenewalAlertWebhookFormats.Json,
                label: t("renewal_alerts_webhook_format_json"),
              },
              {
                value: RenewalAlertWebhookFormats.Ntfy,
                label: t("renewal_alerts_webhook_format_ntfy"),
              },
            ]}
            value={webhookFormat}
            onChange={(value) => {
              if (
                value === RenewalAlertWebhookFormats.Json ||
                value === RenewalAlertWebhookFormats.Ntfy
              ) {
                setWebhookFormat(value);
              }
            }}
          />
          {webhookConfigured && (
            <BodyText tone="secondary" size="sm">
              {t("renewal_alerts_configured")}
            </BodyText>
          )}
          <PasswordInput
            label={t("renewal_alerts_webhook_url")}
            placeholder={t("renewal_alerts_secret_placeholder")}
            autoComplete="new-password"
            value={webhookUrl}
            disabled={clearWebhook}
            onChange={(event) => {
              setWebhookUrl(event.currentTarget.value);
              setClearWebhook(false);
            }}
          />
          {webhookConfigured && (
            <Checkbox
              label={t("renewal_alerts_clear")}
              checked={clearWebhook}
              onChange={(event) => setClearWebhook(event.currentTarget.checked)}
            />
          )}
          <PasswordInput
            label={t("renewal_alerts_webhook_token")}
            placeholder={t("renewal_alerts_secret_placeholder")}
            autoComplete="new-password"
            value={webhookToken}
            disabled={clearWebhookToken}
            onChange={(event) => {
              setWebhookToken(event.currentTarget.value);
              setClearWebhookToken(false);
            }}
          />
          {webhookTokenConfigured && (
            <Checkbox
              label={t("renewal_alerts_clear")}
              checked={clearWebhookToken}
              onChange={(event) =>
                setClearWebhookToken(event.currentTarget.checked)
              }
            />
          )}
        </Stack>
      )}
      <BodyText tone="secondary" size="sm">
        {t("renewal_alerts_test_help")}
      </BodyText>
      <Stack gap="0.5rem">
        <Button
          variant="filled"
          color="primary"
          size="compact-sm"
          loading={updateMutation.isPending}
          onClick={save}
        >
          {t("renewal_alerts_save")}
        </Button>
        <Button
          variant="outline"
          color="primary"
          size="compact-sm"
          loading={testMutation.isPending}
          onClick={() =>
            testMutation.mutate(undefined, {
              onSuccess: () =>
                showNotification({
                  type: NotificationType.Success,
                  message: t("renewal_alerts_test_sent"),
                }),
            })
          }
        >
          {t("renewal_alerts_send_test")}
        </Button>
      </Stack>
    </Stack>
  );
};

export default RenewalAlertSettings;
