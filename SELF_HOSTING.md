# Self-hosting

Local checklist for running this fork with Docker Compose. Deployment detail and screenshots live in the upstream docs at [budgetboard.net](https://budgetboard.net/).

## Requirements

Docker with Compose v2. Keep the host private. The database holds financial data.

## Configuration

`compose.yml` loads variables from `compose.env`:

```bash
docker compose --env-file compose.env up --build -d
```

The tracked `compose.env` contains local placeholders so Compose can start. Before the app is reachable beyond your own machine, replace `POSTGRES_PASSWORD`. Do not commit a real password. Optional OIDC and email lines in that file are comments; leave them unset until you use those features.

### Required

| Variable | Purpose |
| --- | --- |
| `POSTGRES_DATABASE` | Database name |
| `POSTGRES_USER` | Database user |
| `POSTGRES_PASSWORD` | Database password |

Compose fails fast when any of these is empty.

### Often changed

| Variable | Default | Purpose |
| --- | --- | --- |
| `CUSTOM_PORT` | `6253` | Host port for the web UI |
| `POSTGRES_HOST` | `budget-board-db` | Database hostname on the Compose network |
| `POSTGRES_PORT` | `5432` | Database port inside the network |
| `TZ` | `UTC` | Time zone for the API and Postgres |
| `LOG_LEVEL` | `Information` | API log level |
| `OIDC_ENABLED` | `false` | Turn on OIDC. Also set `OIDC_ISSUER`, `OIDC_CLIENT_ID`, and `OIDC_CLIENT_SECRET` |
| `DISABLE_NEW_USERS` | `false` | Refuse registration |
| `DISABLE_LOCAL_AUTH` | `false` | Password login off. Requires `OIDC_ENABLED=true` |
| `AUTO_UPDATE_DB` | `true` | Apply EF Core migrations when the API starts |
| `DISABLE_UPDATE_CHECK` | `false` | Skip the upstream version check |
| `DISABLE_AUTO_SYNC` | `false` | Skip scheduled SimpleFIN and LunchFlow sync |
| `SYNC_INTERVAL_HOURS` | `8` | Hours between syncs. Minimum `1` |
| `EMAIL_SENDER`, `EMAIL_SENDER_USERNAME`, `EMAIL_SENDER_PASSWORD`, `EMAIL_SMTP_HOST` | empty | SMTP for confirmation mail and renewal alerts |
| `EMAIL_SMTP_PORT` | `587` | SMTP port |
| `DISABLE_RENEWAL_ALERTS` | `false` | Skip the subscription renewal alert job |
| `RENEWAL_ALERT_INTERVAL_HOURS` | `12` | Hours between renewal alert checks. Minimum `1` |
| `DATA_PROTECTION_KEYS_PATH` | `/var/budgetboard/data-protection-keys` | Directory for keys that encrypt notification secrets. Persist it. This path is not a secret |

The UI is published on `CUSTOM_PORT`. Postgres is published on host port `6252`. The API listens on port `8080` inside the Compose network only; nginx in the client container proxies `/api/` to it.

## Run

```bash
docker compose --env-file compose.env up --build -d
docker compose --env-file compose.env ps
```

Open `http://localhost:6253` (or whatever `CUSTOM_PORT` is) and create an account. With `AUTO_UPDATE_DB=true`, migrations run before the API reports healthy. Nginx serves the UI and proxies `/api/`. `GET /health` is on the API container, not on the published UI port.

Logs:

```bash
docker compose --env-file compose.env logs -f budget-board-server
```

Stop and keep the database:

```bash
docker compose --env-file compose.env down
```

Data is the Compose volume `db-data`. Notification encryption keys are the volume `data-protection-keys`. `docker compose --env-file compose.env down -v` deletes those volumes.

## Renewal alerts

Confirmed subscriptions can send a reminder before the next charge. Open Settings, then Notifications. Choose how many days before (the default is 3; 0 means the charge date) and turn on one or more channels. On the Subscriptions tab, each subscription can use that default, a different number of days, or no alert.

The background job runs every `RENEWAL_ALERT_INTERVAL_HOURS` (default 12, minimum 1). It sends once per charge date and channel. A later run does not send that same charge on a channel that already succeeded. A channel that failed is tried again on the next run. Set `DISABLE_RENEWAL_ALERTS=true` to turn the job off.

| Channel | What you configure |
| --- | --- |
| Email | An address in Notification settings. Delivery uses `EMAIL_SENDER`, `EMAIL_SENDER_USERNAME`, `EMAIL_SENDER_PASSWORD`, `EMAIL_SMTP_HOST`, and `EMAIL_SMTP_PORT`. The SMTP password stays in the host environment. Leave the address blank to use the account email. |
| Discord | An incoming webhook URL (`https://discord.com/api/webhooks/...`). |
| Telegram | A bot token from BotFather and the chat ID that should receive messages. |
| Webhook | Any `http` or `https` URL. JSON posts `title`, `message`, `merchant`, `amount`, `account`, `nextChargeDate`, and `daysUntil`. Choose ntfy to POST plain text to a topic such as `https://ntfy.example/budget`, with the title in a `Title` header. An optional token is sent as `Authorization: Bearer ...`. |

Discord webhook URLs, Telegram bot tokens, webhook URLs, and webhook tokens are encrypted in the database with ASP.NET data protection. The API does not return them. Leave a field blank to keep a saved secret, or use "Remove saved secret" to clear it. Use "Send test notification" after saving.

Keys live in `DATA_PROTECTION_KEYS_PATH`. Compose mounts that directory on the `data-protection-keys` volume. Back up that volume with the database. If the keys are lost, saved notification secrets cannot be read and have to be entered again. Do not commit key files. Do not put webhook URLs, bot tokens, or SMTP passwords in `compose.env`.

The API posts to the webhook URL saved in settings, including a host on your own network. Only confirmed, active subscriptions are included. Suggested charges are not.

## Windows

Git for Windows defaults to `core.autocrlf=true`, which checks text files out with CRLF. The client container starts `/bin/sh nginx-substitute.sh` (`client/.docker/nginx/conf/nginx-substitute.sh`). CRLF in that file makes it crash-loop: `line 5: not found` and `unexpected end of file (expecting "then")`.

`.gitattributes` forces LF for `*.sh` and for `client/.docker/nginx/conf/default.conf.template`. That template is the nginx config the entrypoint writes inside the container. The API image does not run a shell script from this repository.

A pull does not rewrite files already sitting in the working tree. After pulling, check them out again and rebuild the client image. The script is copied in at build time.

```bash
git checkout -- client/.docker/nginx/conf/nginx-substitute.sh client/.docker/nginx/conf/default.conf.template
docker compose --env-file compose.env up --build -d
```

`git ls-files --eol client/.docker/nginx/conf/nginx-substitute.sh` should show `w/lf`.

## Day-to-day development

Coding agents and local development use the .NET SDK and Vite against a normal PostgreSQL install. That loop is in [AGENTS.md](AGENTS.md). Compose is the self-host stack, not the inner edit loop.
