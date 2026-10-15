# Roadmap

Candidate features for this personal fork. Nothing here is scheduled, and none of it should be built unless a task asks for it. Where upstream already covers part of the idea, extend that code instead of adding a second copy.

## Subscription and recurring-bill tracker

Inspired by [Wallos](https://github.com/ellite/Wallos): a list of subscriptions and bills, with renewal dates and a reminder when one is coming due.

Upstream recurring rules (`RecurringRule`, `RecurringRuleService`, Transactions → Settings → Recurring) match transactions that already happened. They were not a subscription catalog, and they do not notify anyone about a renewal. The catalog was a small addition to those rules, not a second model.

The Subscriptions tab is that catalog. It is a view over recurring rules.

- Confirmed rules are listed by next forecast date, with merchant, amount, cadence, last charge, and predicted next charge.
- A charge due within the next 7 days is highlighted in the app.
- Opening the tab scans transactions for a regular cadence (weekly, biweekly, monthly, quarterly, or yearly) and a stable amount. Matches are saved as suggested recurring rules.
- Accept confirms a suggestion, so it forecasts and auto-matches like any other recurring rule. Dismiss keeps the suggestion from coming back.
- Add and edit reuse the recurring rule form.

Before that scan, recurring rules were only created by hand. The server could forecast a rule and attach a transaction that already matched one unambiguous rule. It did not look through transaction history to find a cadence. Forecasting and automatic matching still ignore rules that are suggestions or were dismissed.

An outbound reminder when a renewal is coming due is still not built.

## Multi-year plan-ahead budgets and charts

Inspired by [Ocular](https://github.com/simonwep/ocular): a forward view across months and years, and charts that are easier to read over that horizon.

Upstream budgets are one spending limit per category per month (`Budget.Month`), with optional rollover from `RolloverStartMonth`. Trends already include spending, categories, net cash flow, flows, assets, liabilities, net worth, and values (`PageContent/Trends`, `components/Charts`). A plan-ahead view should sit on the monthly budget rows rather than replace them.

## Later candidates

- **Envelope budgeting.** Opt-in page at `/envelopes` (`EnvelopeBudgetService`). On-budget account balances fund a To Be Assigned pool, assignments give that cash a job, and leftover or overspending rolls to the next month. Monthly category limits (`Budget`) stay as they are.
- **Rules engine.** Automatic rules already update transactions from conditions such as description and amount (`AutomaticRuleService`, `AutomaticRuleConditionHandler`, `AutomaticRuleActionValidator`). Richer transaction rules should extend that pipeline. A separate engine is only worth it for rules that are not about transactions.
- **Receipt attachments.** `ILargeObjectStore` can read and write PostgreSQL large objects, and no screen attaches a file to a transaction yet. A later attachment feature can reuse that store.
