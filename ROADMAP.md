# Roadmap

This fork tracks features that are not in upstream Budget Board yet.

## Subscriptions

The Subscriptions tab is a view over recurring rules. It does not keep a second subscription ledger.

- Confirmed rules are listed by next forecast date, with merchant, amount, cadence, last charge, and predicted next charge.
- A charge due within the next 7 days is highlighted.
- Opening the tab scans transactions for a regular cadence (weekly, biweekly, monthly, quarterly, or yearly) and a stable amount. Matches are saved as suggested recurring rules.
- Accept confirms a suggestion, so it forecasts and auto-matches like any other recurring rule. Dismiss keeps the suggestion from coming back.
- Add and edit reuse the recurring rule form.

Before this change, recurring rules were only created by hand. The server could forecast a rule and attach a transaction that already matched one unambiguous rule. It did not look through transaction history to find a cadence.

Forecasting and automatic matching ignore rules that are still suggestions or were dismissed.
