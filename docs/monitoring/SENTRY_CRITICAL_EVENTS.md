# ECafe Sentry Critical Events

This document defines the production monitoring contract for Sentry.

## Event Tags

Critical ECafe events are reported with these tags:

- `category`: high-level area, for example `auth`, `notification`, `storage`
- `event`: concrete event name, for example `refresh_token_reuse`
- `severity`: `warning`, `error`, or `critical`
- `traceId`: API trace identifier for backend log correlation
- `path`: request path when the event was request-driven
- `method`: HTTP method when the event was request-driven

Do not send raw access tokens, refresh tokens, passwords, API keys, full email addresses, phone numbers, or IP addresses to Sentry.

## Dashboard Widgets

Recommended dashboard widgets:

- Auth critical events by `event` over the last 24 hours
- `refresh_token_reuse` count over the last 24 hours
- `login_lockout` count over the last 24 hours
- `SessionInvalid` and `UserDeactivated` counts by path
- Notification failures by `event`
- Outbox retry limit reached count by `eventType`
- Storage provider failures

## Alert Rules

Recommended production alerts:

- `event:refresh_token_reuse`: alert immediately when count is greater than or equal to 1 in 5 minutes
- `event:login_lockout`: alert when count is greater than or equal to 10 in 10 minutes
- `event:outbox_retry_limit_reached`: alert when count is greater than or equal to 1 in 10 minutes
- `event:NotificationProviderUnavailable`: alert when count is greater than or equal to 5 in 10 minutes
- `event:FileStorageUnavailable`: alert when count is greater than or equal to 1 in 5 minutes
- `event:SessionInvalid`: alert when count is unusually high after deployment

## Triage Notes

- `refresh_token_reuse` may mean a frontend refresh race, replayed stale cookie, or stolen refresh token.
- `login_lockout` may mean a user forgot the password or a brute-force attempt is happening.
- `outbox_retry_limit_reached` means the message has exhausted retries and requires operator attention.
- Provider unavailable events should be checked against provider status, credentials, balance, and network access.
- A sudden increase in `SessionInvalid` after deployment usually points to token/session compatibility or cookie configuration.
