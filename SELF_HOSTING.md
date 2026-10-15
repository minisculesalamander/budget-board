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
| `EMAIL_SENDER`, `EMAIL_SENDER_USERNAME`, `EMAIL_SENDER_PASSWORD`, `EMAIL_SMTP_HOST` | empty | SMTP for confirmation mail |
| `EMAIL_SMTP_PORT` | `587` | SMTP port |

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

Data is the Compose volume `db-data`. `docker compose --env-file compose.env down -v` deletes that volume.

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
