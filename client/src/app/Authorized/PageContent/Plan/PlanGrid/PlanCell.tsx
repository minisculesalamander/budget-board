import React from "react";
import { useTranslation } from "react-i18next";
import { SignDisplay } from "~/helpers/currency";
import { useSensitiveAmountFormatter } from "~/hooks/useSensitiveAmountFormatter";
import classes from "./PlanGrid.module.css";

interface PlanCellProps {
  category: string;
  monthLabel: string;
  kind: string;
  planned: number | null;
  actual: number;
  editable?: boolean;
  saving?: boolean;
  onCommit?: (amount: string | null) => void;
}

const PlanCell = (props: PlanCellProps): React.ReactNode => {
  const { t } = useTranslation();
  const formatAmount = useSensitiveAmountFormatter();
  const [editing, setEditing] = React.useState(false);
  const [draft, setDraft] = React.useState("");
  const inputRef = React.useRef<HTMLInputElement>(null);

  React.useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const beginEdit = () => {
    if (!props.editable || props.saving) {
      return;
    }

    setDraft(props.planned == null ? "" : String(props.planned));
    setEditing(true);
  };

  const commit = () => {
    const next = draft.trim();
    const previous = props.planned == null ? "" : String(props.planned);
    setEditing(false);
    if (next === previous) {
      return;
    }

    props.onCommit?.(next.length === 0 ? null : next);
  };

  const plannedLabel =
    props.planned == null
      ? "—"
      : formatAmount(
          props.planned,
          SignDisplay.Never,
          undefined,
          Number.isInteger(props.planned) ? 0 : 2,
        );

  const showActual = props.actual !== 0;
  const higherIsBetter = props.kind === "income" || props.kind === "savings";
  const actualTone =
    props.planned == null || !showActual
      ? classes.actualMuted
      : higherIsBetter
        ? props.actual < props.planned
          ? classes.actualShort
          : classes.actualMet
        : props.actual > props.planned
          ? classes.actualShort
          : classes.actualMet;

  return (
    <div className={classes.cellBody}>
      {editing ? (
        <input
          ref={inputRef}
          className={classes.editor}
          value={draft}
          aria-label={`${props.category} ${props.monthLabel}`}
          onChange={(event) => setDraft(event.target.value)}
          onBlur={commit}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.currentTarget.blur();
            }
            if (event.key === "Escape") {
              setDraft(props.planned == null ? "" : String(props.planned));
              setEditing(false);
            }
          }}
        />
      ) : (
        <button
          type="button"
          className={classes.amountButton}
          disabled={!props.editable || props.saving}
          onClick={beginEdit}
          aria-label={`${t("planned")} ${props.category} ${props.monthLabel}`}
        >
          {plannedLabel}
        </button>
      )}
      {showActual && (
        <span className={`${classes.actual} ${actualTone}`}>
          {formatAmount(props.actual, SignDisplay.Never, undefined, 0)}
        </span>
      )}
    </div>
  );
};

export default PlanCell;
