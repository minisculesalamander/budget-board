import { Group, Stack } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { BodyText, Button, Modal, NumberInput, Select } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { getCurrencySymbol } from "~/helpers/currency";
import { useMoveEnvelopeMutation } from "~/hooks/mutations/envelopes/useMoveEnvelopeMutation";
import { IEnvelopeCategory } from "~/models/envelope";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";

interface MoveMoneyModalProps {
  month: string;
  from: IEnvelopeCategory;
  envelopes: IEnvelopeCategory[];
  disabled: boolean;
}

const MoveMoneyModal = (props: MoveMoneyModalProps): React.ReactNode => {
  const { t } = useTranslation();
  const { thousandsSeparator, decimalSeparator } = useLocale();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const [opened, { open, close }] = useDisclosure(false);
  const [toCategory, setToCategory] = React.useState<string | null>(null);
  const [amount, setAmount] = React.useState<number | string>("");
  const moveMutation = useMoveEnvelopeMutation();

  const destinations = props.envelopes.filter(
    (envelope) => envelope.category !== props.from.category,
  );
  const numericAmount = typeof amount === "number" ? amount : Number(amount);
  const canMove =
    toCategory !== null &&
    Number.isFinite(numericAmount) &&
    numericAmount > 0 &&
    numericAmount <= props.from.available;

  const openModal = () => {
    setToCategory(null);
    setAmount("");
    open();
  };

  const save = () => {
    if (!canMove || toCategory === null) {
      return;
    }

    moveMutation.mutate(
      {
        month: props.month,
        fromCategory: props.from.category,
        toCategory,
        amount: numericAmount,
      },
      { onSuccess: close },
    );
  };

  return (
    <>
      <Button
        variant="outline"
        color="primary"
        size="compact-sm"
        onClick={openModal}
        disabled={props.disabled || props.from.available <= 0 || destinations.length === 0}
      >
        {t("envelope_move")}
      </Button>
      <Modal
        opened={opened}
        onClose={close}
        title={t("envelope_move_title", { category: props.from.category })}
      >
        <Stack gap="0.75rem">
          <BodyText size="sm" tone="secondary">
            {t("envelope_move_description")}
          </BodyText>
          <Select
            label={t("envelope_move_to")}
            data={destinations.map((envelope) => ({
              value: envelope.category,
              label:
                envelope.parent.length > 0
                  ? `${envelope.parent} / ${envelope.category}`
                  : envelope.category,
            }))}
            value={toCategory}
            onChange={setToCategory}
            placeholder={t("envelope_move_to")}
          />
          <NumberInput
            label={t("envelope_move_amount")}
            value={amount}
            onChange={setAmount}
            min={0}
            max={props.from.available}
            decimalScale={decimalPlaces}
            thousandSeparator={thousandsSeparator}
            decimalSeparator={decimalSeparator}
            prefix={getCurrencySymbol(preferredCurrency)}
          />
          <Group justify="flex-end" gap="0.5rem">
            <Button variant="outline" color="primary" size="compact-sm" onClick={close}>
              {t("cancel")}
            </Button>
            <Button
              variant="filled"
              color="primary"
              size="compact-sm"
              onClick={save}
              disabled={!canMove}
              loading={moveMutation.isPending}
            >
              {t("envelope_move")}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </>
  );
};

export default MoveMoneyModal;
