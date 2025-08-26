
# Issue 1 — [Gateway] Implement **Echo Mode** on **Local** runtime (LINE Webhook Gateway)

**Labels:** `area:gateway` `runtime:local` `mode:echo` `priority:P1`\
**Milestone:** v0.1-local\
**Depends on:** none
**Branch:** `feat/gateway-line-echo-local`

## Summary

Implement the first runnable slice of the LINE Webhook Gateway in **Local** runtime with **Echo Mode**. On inbound LINE text messages, validate `x-line-signature`, accept only `message` events, honor redelivery handling, schedule work via local queue/user‑locks, and reply with the **same text** via the LINE **Reply API**, respecting character counting and the **≤5 messages per reply** batching rule. (Derived from `docs/specs/gateway-line.spec.md` sections: *Endpoint*, *Event Filtering*, *Reply Token Timing*, *Echo*.)

## Platform & Language

- **Service:** `gateway-line` — C# / .NET 8 (ASP.NET Core) — see Microsoft Official Document via MCP tools called `microsoft_docs_search` and `microsoft_docs_fetch`
- **Runtime platform:** **Local** (developer machine)
- **Chat mode:** **Echo** implemented inside the gateway (no external HTTP call)

## Specs & README

- **Gateway spec:** `docs/specs/gateway-line.spec.md` — see *Endpoint*, *Event Filtering*, *Reply Token Timing*, and *Echo* rules.
- **Repository README(Current version):** `README.md` — see current
*Repository layout* and *Quickstart (local dev)* sections, you can update it.

> Note: When the spec mentions Azure components, ignore them for this issue (Local runtime only).

## Scope of Work

- **Endpoint**: Implement `POST /line/webhook` (JSON only). Validate `x-line-signature` (HMAC‑SHA256 with `Line:ChannelSecret`).
- **Event filtering**: Handle **only** `message` events; ignore events where `mode == "standby"`.
- **Local flow control**: Add in‑memory **user locks** and a bounded **local queue** (no Azure yet).
- **Worker**: Dequeue; apply `Reply:Qps` (local impl); invoke **Echo** chat service (split by `Chat:MaxCharsPerMessage`), and reply via LINE **Reply API**.
- **Reply constraints**: Batch up to **5** message objects per Reply API call. If more, log and fail the send.
- **Reply token policy**: Try **Reply** once using the webhook `replyToken`. If invalid/expired/already‑used → log; **do not** switch to push. Handle **redelivery** (`deliveryContext.isRedelivery`) via idempotency on `webhookEventId`.
- **Logging**: Follow LINE logging guidance (`x-line-request-id`, request/response meta). Do **not** log secrets or full bodies at normal levels.

## Non‑Goals (for this issue)

- No Azure Service Bus / Cosmos DB (that’s for cloud runtime).
- No HTTP chat mode / external provider (next issue).

## Configuration (Local + LINE)

Provide `appsettings.Development.json` + env overrides:

```jsonc
{
  "Runtime": { "Platform": "Local" },
  "Line": {
    "ChannelSecret": "<from LINE Console>",
    "ChannelAccessToken": "<from LINE Console>",
    "GetUserProfile": false,
    "MaxIncomingTextLength": 0
  },
  "Chat": {
    "Mode": "Echo",
    "ApiVersion": "0.1",
    "MaxCharsPerMessage": 1000,
    "RequestTimeoutSeconds": 20,
    "Fallbacks": { "Default": "The service is temporarily unavailable." }
  },
  "Local": {
    "Queue": { "MaxSize": 15 },
    "Locks": { "Enabled": true }
  },
  "Reply": {
    "Qps": 5.0,
    "QueueMaxSize": 15,
    "QueueFullMessages": [
      "Sorry, I’m a bit busy. Please try again in a moment.",
      "System is crowded now. Try again shortly."
    ]
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

### Environment variable names

- `RUNTIME_PLATFORM=Local`
- `LINE_CHANNEL_SECRET`, `LINE_CHANNEL_ACCESS_TOKEN`, `GET_LINE_USER_PROFILE=false`, `LINE_MAX_INCOMING_TEXT_LENGTH=0`
- `CHAT_MODE=Echo`, `CHAT_API_VERSION=0.1`, `CHAT_MAX_CHARS_PER_MESSAGE=1000`, `CHAT_REQUEST_TIMEOUT_SECONDS=20`, `CHAT_FALLBACK_DEFAULT="…"`
- `LOCAL_QUEUE_MAXSIZE=15`, `LOCAL_LOCKS_ENABLED=true`
- `REPLY_QPS=5.0`, `REPLY_QUEUE_MAXSIZE=15`, `REPLY_QUEUE_FULL_MESSAGES=["…"]`
- `LOG_LEVEL=Information`

## Implementation Notes

- **Signature validation**: Compute `base64(HMAC_SHA256(channelSecret, rawBody))` and compare to `x-line-signature`.
- **Character counting**: Follow LINE’s UTF‑16 code unit rule when splitting long text.
- **LINE Reply API**: `POST https://api.line.me/v2/bot/message/reply` with headers `Authorization: Bearer <ChannelAccessToken>`, `Content-Type: application/json` and body `{ replyToken, messages: [{ type: "text", text: "…" }] }` (1–5 messages).
- **Idempotency**: Keep a short‑lived in‑memory cache (Local) keyed by `webhookEventId` to ignore duplicated deliveries during development.

## Test Plan

- **Unit**
  - Signature verifier: valid + tampered + wrong secret.
  - Event filter: non‑`message` / `standby` are ignored (200 OK minimal processing).
  - Echo splitter: > `Chat:MaxCharsPerMessage` produces contiguous chunks.
  - Batch cap: >5 messages → send blocked & error logged.
- **Integration (Local)**
  - Happy path: minimal webhook with `message.text="hello"` replies `"hello"`.
  - Redelivery flag honored: duplicates are not re‑replied.
  - Busy path: simulate queue size ≥ `Reply:QueueMaxSize` → a random “busy” message is replied.
  - Locked path: two concurrent messages from the same user → one gets a “locked/in‑flight” message.

## Definition of Done

- Endpoint, worker, Echo chat, and LINE reply are implemented and covered by tests above.
- Readme section for **Local setup** (how to run + how to set LINE webhook URL via tunnel like ngrok/devtunnel).
- All configuration keys documented.
- Sample cURL for health check + webhook simulation included in repo `README.md`.

## References (Official docs)

- LINE — Messaging API Overview: [https://developers.line.biz/en/docs/messaging-api/overview/](https://developers.line.biz/en/docs/messaging-api/overview/)
- LINE — Webhooks: [https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook)
- LINE — Verify signature: [https://developers.line.biz/en/docs/messaging-api/receiving-messages/#signature-validation](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#signature-validation)
- LINE — Webhook event objects: [https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook-event-objects](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook-event-objects)
- LINE — Redelivered webhooks: [https://developers.line.biz/en/docs/messaging-api/receiving-messages/#redelivered-webhooks](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#redelivered-webhooks)
- LINE — Send reply message: [https://developers.line.biz/en/reference/messaging-api/#send-reply-message](https://developers.line.biz/en/reference/messaging-api/#send-reply-message)
- LINE — Character counting: [https://developers.line.biz/en/docs/messaging-api/character-counting/](https://developers.line.biz/en/docs/messaging-api/character-counting/)
- LINE — Common specs → response headers (`x-line-request-id`): [https://developers.line.biz/en/docs/messaging-api/common-specifications/#response-headers](https://developers.line.biz/en/docs/messaging-api/common-specifications/#response-headers)
- .NET — Support policy & lifecycle: [https://dotnet.microsoft.com/platform/support/policy/dotnet-core](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) , [https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core](https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core)

> **Notes for later (Cloud runtime):** Azure Service Bus (queues, quotas, sessions) and Cosmos DB (locks) will be implemented in a subsequent milestone.