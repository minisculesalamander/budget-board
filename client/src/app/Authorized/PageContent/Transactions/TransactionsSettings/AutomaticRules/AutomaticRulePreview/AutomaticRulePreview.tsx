import { Group, Skeleton, Stack } from "@mantine/core";
import { BodyText, Button, Modal } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { convertNumberToCurrency, SignDisplay } from "~/helpers/currency";
import { getFormattedCategoryValue } from "~/helpers/category";
import { usePreviewAutomaticRuleMutation } from "~/hooks/mutations/automaticRules/usePreviewAutomaticRuleMutation";
import { useRunAutomaticRuleMutation } from "~/hooks/mutations/automaticRules/useRunAutomaticRuleMutation";
import { IAutomaticRuleCreateRequest } from "~/models/automaticRule";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { useTransactionCategories } from "~/providers/TransactionCategoryProvider/TransactionCategoryProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";

interface AutomaticRulePreviewProps {
  opened: boolean;
  rule: IAutomaticRuleCreateRequest;
  onClose: () => void;
  onApplied: () => void;
}

const AutomaticRulePreview = (
  props: AutomaticRulePreviewProps,
): React.ReactNode => {
  const { t } = useTranslation();
  const { dayjs, dateFormat, intlLocale } = useLocale();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const { allTransactionCategories } = useTransactionCategories();
  const previewMutation = usePreviewAutomaticRuleMutation();
  const runMutation = useRunAutomaticRuleMutation();
  const wasOpened = React.useRef(false);
  const { mutate } = previewMutation;

  React.useEffect(() => {
    if (props.opened && !wasOpened.current) {
      mutate(props.rule);
    }
    wasOpened.current = props.opened;
  }, [props.opened, props.rule, mutate]);

  const preview = previewMutation.data;
  const matchCount = preview?.matchCount ?? 0;

  return (
    <Modal
      opened={props.opened}
      onClose={props.onClose}
      title={t("automatic_rule_preview")}
    >
      <Stack gap="0.75rem">
        {previewMutation.isPending ? (
          <Skeleton height={80} radius="md" />
        ) : matchCount === 0 ? (
          <BodyText tone="secondary" size="sm">
            {t("no_rule_matches")}
          </BodyText>
        ) : (
          <>
            {preview && preview.matches.length < matchCount ? (
              <BodyText tone="secondary" size="sm">
                {t("preview_showing", {
                  shown: preview.matches.length,
                  count: matchCount,
                })}
              </BodyText>
            ) : null}
            <Stack gap="0.35rem">
              {(preview?.matches ?? []).map((match) => (
                <Group key={match.id} justify="space-between" wrap="nowrap">
                  <Stack gap={0}>
                    <BodyText size="sm">
                      {match.merchantName || match.description || match.accountName}
                    </BodyText>
                    <BodyText tone="secondary" size="xs">
                      {[
                        dayjs(match.date).format(dateFormat),
                        match.accountName,
                        getFormattedCategoryValue(
                          match.subcategory || match.category || "",
                          allTransactionCategories,
                        ),
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                    </BodyText>
                  </Stack>
                  <BodyText size="sm">
                    {convertNumberToCurrency(
                      match.amount,
                      decimalPlaces,
                      preferredCurrency,
                      SignDisplay.Auto,
                      intlLocale,
                    )}
                  </BodyText>
                </Group>
              ))}
            </Stack>
          </>
        )}
        <Group justify="flex-end">
          <Button variant="outline" color="neutral" onClick={props.onClose}>
            {t("cancel")}
          </Button>
          <Button
            variant="filled"
            color="primary"
            disabled={matchCount === 0 || previewMutation.isPending}
            loading={runMutation.isPending}
            onClick={() => {
              runMutation.mutate(props.rule, {
                onSuccess: () => {
                  props.onApplied();
                },
              });
            }}
          >
            {t("apply_matches", { count: matchCount })}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
};

export default AutomaticRulePreview;
