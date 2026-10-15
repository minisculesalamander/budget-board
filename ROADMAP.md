# Roadmap

Candidate features for this personal fork. Nothing here is scheduled, and none of it should be built unless a task asks for it. Where upstream already covers part of the idea, extend that code instead of adding a second copy.

## Subscription and recurring-bill tracker

Inspired by [Wallos](https://github.com/ellite/Wallos): a list of subscriptions and bills, with renewal dates and a reminder when one is coming due.

Upstream recurring rules (`RecurringRule`, `RecurringRuleService`, Transactions → Settings → Recurring) match transactions that already happened. They are not a subscription catalog and they do not notify anyone about a renewal. Prefer a new model unless a small addition to those rules is clearly enough.

## Multi-year plan-ahead budgets and charts

Inspired by [Ocular](https://github.com/simonwep/ocular): a forward view across months and years, and charts that are easier to read over that horizon.

Upstream budgets are one spending limit per category per month (`Budget.Month`), with optional rollover from `RolloverStartMonth`. Trends already include spending, categories, net cash flow, flows, assets, liabilities, net worth, and values (`PageContent/Trends`, `components/Charts`). A plan-ahead view should sit on the monthly budget rows rather than replace them.

## Later candidates

- **Envelope budgeting.** Not in the product today. Envelopes would give money a job, which is different from a monthly category limit. Decide whether that replaces `Budget` or lives beside it before changing the schema.
- **Rules engine.** Automatic rules already update transactions from conditions such as description and amount (`AutomaticRuleService`, `AutomaticRuleConditionHandler`, `AutomaticRuleActionValidator`). Richer transaction rules should extend that pipeline. A separate engine is only worth it for rules that are not about transactions.
- **Receipt attachments.** `ILargeObjectStore` can read and write PostgreSQL large objects, and no screen attaches a file to a transaction yet. A later attachment feature can reuse that store.
