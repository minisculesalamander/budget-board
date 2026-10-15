import { NumberInput } from "@teelur/budget-board-ui";
import React from "react";
import { useTranslation } from "react-i18next";
import { getCurrencySymbol } from "~/helpers/currency";
import { useLocale } from "~/providers/LocaleProvider/LocaleProvider";
import { useUserSettings } from "~/providers/UserSettingsProvider/UserSettingsProvider";

interface AssignedInputProps {
  category: string;
  value: number;
  disabled: boolean;
  onSave: (assigned: number) => void;
}

const AssignedInput = (props: AssignedInputProps): React.ReactNode => {
  const { t } = useTranslation();
  const { thousandsSeparator, decimalSeparator } = useLocale();
  const { preferredCurrency, decimalPlaces } = useUserSettings();
  const [draft, setDraft] = React.useState<number | string>(props.value);
  const focused = React.useRef(false);

  React.useEffect(() => {
    if (!focused.current) {
      setDraft(props.value);
    }
  }, [props.value]);

  const save = () => {
    const next = typeof draft === "number" ? draft : Number(draft);
    if (!Number.isFinite(next) || next === props.value) {
      setDraft(props.value);
      return;
    }

    props.onSave(next);
  };

  return (
    <NumberInput
      aria-label={t("envelope_assigned_for", { category: props.category })}
      value={draft}
      onChange={setDraft}
      onFocus={() => {
        focused.current = true;
      }}
      onBlur={() => {
        focused.current = false;
        save();
      }}
      disabled={props.disabled}
      thousandSeparator={thousandsSeparator}
      decimalSeparator={decimalSeparator}
      decimalScale={decimalPlaces}
      prefix={getCurrencySymbol(preferredCurrency)}
      size="xs"
      w="100%"
    />
  );
};

export default AssignedInput;
